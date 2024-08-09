/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using OneScript.Contexts;
using OneScript.DebugProtocol;
using OneScript.DebugProtocol.TcpServer;
using OneScript.Language;
using ScriptEngine.Machine;
using StackFrame = OneScript.DebugProtocol.StackFrame;
using Variable = OneScript.DebugProtocol.Variable;
using MachineVariable = OneScript.Contexts.Variable;
using OneScript.DebugProtocol.Breakpoints;

namespace OneScript.DebugServices
{
    public class DefaultDebugService : IDebuggerService
    {
        private readonly IBreakpointManager _breakpointManager;
        private readonly IVariableVisualizer _visualizer;
        private readonly ThreadManager _threadManager;

        public DefaultDebugService(IBreakpointManager breakpointManager, ThreadManager threads, IVariableVisualizer visualizer)
        {
            _breakpointManager = breakpointManager;
            _visualizer = visualizer;
            _threadManager = threads;
        }
        
        public void Execute(int threadId)
        {
            if (threadId > 0)
            {
                var token = _threadManager.GetTokenForThread(threadId);
                token.Set();        
            }
            else
            {
                var tokens = _threadManager.GetAllTokens();

                foreach (var token in tokens)
                    token.Set();
            }
        }

        public void SetMachineExceptionBreakpoints((string Id, string Condition)[] filters)
        {
            _breakpointManager.SetExceptionBreakpoints(filters);

            // Уведомить все потоки о новых фильтрах
            foreach (var machine in _threadManager.GetAllTokens().Select(x => x.Machine))
                machine.SetDebugMode(_breakpointManager);
        }

        public Breakpoint[] SetMachineBreakpoints(Breakpoint[] breaksToSet)
        {
            var confirmedBreakpoints = new List<Breakpoint>();

            var grouped = breaksToSet.GroupBy(g => g.Source);

            foreach (var item in grouped)
            {
                var points = item
                    .Where(x => x.Line != 0)
                    .Select(x => (x.Line, x.Condition))
                    .ToArray();

                _breakpointManager.SetBreakpoints(item.Key, points);
                foreach (var point in points)
                {
                    confirmedBreakpoints.Add(new Breakpoint()
                    {
                        Line = point.Line,
                        Source = item.Key,
                        Condition = point.Condition
                    });
                }
            }

            // Уведомить все потоки о новых точках остановки
            foreach (var machine in _threadManager.GetAllTokens().Select(x=>x.Machine))
            {
                machine.SetDebugMode(_breakpointManager);
            }
            
            return confirmedBreakpoints.ToArray();
        }

        public StackFrame[] GetStackFrames(int threadId)
        {
            var machine = _threadManager.GetTokenForThread(threadId).Machine;
            var frames = machine.GetExecutionFrames();
            var result = new StackFrame[frames.Count];
            int index = 0;
            foreach (var frameInfo in frames)
            {
                var frame = new StackFrame
                {
                    LineNumber = frameInfo.LineNumber,
                    Index = index++,
                    MethodName = frameInfo.MethodName,
                    Source = frameInfo.Source
                };
                result[frame.Index] = frame;
            }
            return result;
        }

        private MachineInstance GetMachine(int threadId)
        {
            // Из-за того, что прикладные классы пока завязаны на статические сервисы машины тек. потока
            // Приходится в поток отладчика копировать сервисы отлаживаемой машины
            
            var savedMachine = _threadManager.GetTokenForThread(threadId).Machine;
            MachineInstance.Current.SetMemory(savedMachine.Memory);
            return savedMachine;
        }

        public Variable[] GetVariables(int threadId, int frameIndex, int[] path)
        {
            var machine = GetMachine(threadId);

            var locals = machine.GetFrameLocals(frameIndex);

            foreach (var step in path)
            {
                var variable = locals[step];
                locals = GetChildVariables(variable);
            }

            return GetDebugVariables(locals);
        }

        public Variable[] GetEvaluatedVariables(string expression, int threadId, int frameIndex, int[] path)
        {
            IValue value;

            try
            {
                value = GetMachine(threadId).EvaluateInFrame(expression, frameIndex);
            }
            catch (Exception e)
            {
                value = ValueFactory.Create(e.Message);
            }

            var locals = GetChildVariables(MachineVariable.Create(value, "$eval"));

            foreach (var step in path)
            {
                var variable = locals[step];
                locals = GetChildVariables(variable);
            }

            return GetDebugVariables(locals);
        }

        public Variable Evaluate(int threadId, int contextFrame, string expression)
        {
            try
            {
                var value = GetMachine(threadId)
                    .EvaluateInFrame(expression, contextFrame)
                    .GetRawValue();
                
                var variable = _visualizer.GetVariable(MachineVariable.Create(value, "$evalResult"));
                return variable;
            }
            catch (ScriptException e)
            {
                return CreateDebuggerVariable("$evalFault", e.ErrorDescription,
                    "Ошибка");
            }
        }

        public void Next(int threadId)
        {
            var t = _threadManager.GetTokenForThread(threadId);
            t.Machine.StepOver();
            t.Set();
        }

        public void StepIn(int threadId)
        {
            var t = _threadManager.GetTokenForThread(threadId);
            t.Machine.StepIn();
            t.Set();
        }

        public void StepOut(int threadId)
        {
            var t = _threadManager.GetTokenForThread(threadId);
            t.Machine.StepOut();
            t.Set();
        }

        public void Disconnect(bool terminate)
        {
            _breakpointManager.Clear();
            _threadManager.ReleaseAllThreads();

            throw new StopServiceException();
        }

        public int[] GetThreads()
        {
            return _threadManager.GetAllThreadIds();
        }
        
        public int GetProcessId()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }
        
        private Variable[] GetDebugVariables(IList<IVariable> machineVariables)
        {
            return machineVariables.Select(x => _visualizer.GetVariable(x))
                .ToArray();
        }
        
        public Variable CreateDebuggerVariable(string name, string presentation, string typeName)
        {
            if (presentation.Length > DebuggerSettings.MAX_PRESENTATION_LENGTH)
                presentation = presentation.Substring(0, DebuggerSettings.MAX_PRESENTATION_LENGTH) + "...";

            return new Variable()
            {
                Name = name,
                Presentation = presentation,
                TypeName = typeName
            };
        }

        private IList<IVariable> GetChildVariables(IVariable src)
        {
            return _visualizer.GetChildVariables(src).ToList();
        }
    }
}