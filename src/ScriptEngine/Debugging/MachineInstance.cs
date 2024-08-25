using OneScript.Contexts;
using OneScript.Language;
using OneScript.Values;
using ScriptEngine.Debugging;
using ScriptEngine.Machine.Contexts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScriptEngine.Machine
{
    // Debug members
    public partial class MachineInstance
    {
        private readonly ManualResetEvent _resetEvent = new(true);

        private readonly object _locker = new();
        private (MachineStoppingReason StopReason, string ReasonDetails, bool IsLogMessage) _stopInfo = new(MachineStoppingReason.Breakpoint, string.Empty, false);
        private DebugState _currentState = DebugState.Run;
        private IBreakpointsManager _breakpointManager = null;
        private bool DebugMode => _breakpointManager != null;

        public event EventHandler<MachineStoppedEventArgs> Stopped;

        public void EnableDebugMode(IBreakpointsManager breakpointManager)
        {
            lock (_locker)
                _breakpointManager = breakpointManager;
        }

        public void DisableDebugMode()
        {
            lock (_locker)
                _breakpointManager = null;
        }

        public void Continue()
        {
            SetDebugState(DebugState.Run);
            _resetEvent.Set();
        }

        public void Pause(bool raiseEvent = true)
        {
            _resetEvent.Reset();

            if (raiseEvent)
            {
                _stopInfo = new(MachineStoppingReason.Pause, string.Empty, false);
                InvokeMachineStopped();
            }
        }

        public void WaitContinue()
            => _resetEvent.WaitOne();

        public void Next()
        {
            SetDebugState(DebugState.Next);
            _resetEvent.Set();
        }

        public void StepIn() 
        {
            SetDebugState(DebugState.StepIn);
            _resetEvent.Set();
        }

        public void StepOut()
        {
            SetDebugState(DebugState.StepOut);
            _resetEvent.Set();
        }

        internal IValue EvaluateInFrame(string expression, ExecutionFrame selectedFrame)
        {
            MachineInstance runner = new()
            {
                _mem = _mem,
                _globalContexts = _globalContexts,
                _debugInfo = CurrentScript
            };

            runner.SetFrame(selectedFrame);

            ExecutionFrame frame;
            try
            {
                var code = runner.CompileExpressionModule(expression);

                var localScope = new AttachedContext(new UserScriptContextInstance(code), selectedFrame.Locals);

                frame = new ExecutionFrame
                {
                    MethodName = code.Source.Name,
                    Module = code,
                    ThisScope = localScope,
                    Locals = Array.Empty<IVariable>(),
                    Scopes = CreateFrameScopes(selectedFrame.Scopes, localScope),
                    InstructionPointer = 0,
                    LineNumber = 1
                };
            }
            catch
            {
                throw;
            }

            try
            {
                runner.PushFrame(frame);
                runner.MainCommandLoop();
            }
            catch { }

            return runner._operationStack.Pop().GetRawValue();
        }

        public IValue EvaluateInFrame(string expression, int frameId)
        {
            if (frameId < 0 || frameId >= _fullCallstackCache.Count)
                throw new ScriptException("Stack frame index out of range");

            ExecutionFrame selectedFrame = _fullCallstackCache[frameId].FrameObject;

            return EvaluateInFrame(expression, selectedFrame);
        }

        private void SetDebugState(DebugState debugState)
        {
            lock (_locker)
            {
                if (!DebugMode)
                    throw new InvalidOperationException("Machine is not in debug mode");

                _currentState = debugState;
            }
        }

        private void EmitStopOnExceptionIfNeed(Exception ex, bool needRethrow)
        {
            if (!DebugMode)
                return;

            if (_breakpointManager.NeedStopOnException(ex, !needRethrow))
            {
                _stopInfo = new(MachineStoppingReason.Exception, ex.Message, false);
                
                CreateFullCallstack();
                InvokeMachineStopped();
            }
        }

        private void EmitStopOnLineIfNeed()
        {
            if (!DebugMode)
                return;

            var needStop = false;
            var error = string.Empty;

            // Для начала проверим состояние машины и необходимость остановки при пошаговой отладке
            needStop = _currentState switch
            {
                DebugState.Next => _fullCallstackCache.Select(c => c.FrameObject).Contains(_currentFrame),
                DebugState.StepIn => true,
                DebugState.StepOut => _fullCallstackCache.Select(c => c.FrameObject).Skip(1).Contains(_currentFrame),
                _ => false
            };

            if (needStop)
                _stopInfo = new(MachineStoppingReason.Step, error, false);
            else
            {
                if (_breakpointManager.TryGetBreakpoint(_currentFrame.Module.Source.Location, _currentFrame.LineNumber, out var breakpoint))
                {
                    needStop = true;

                    // Если условие брейкпоинта заполнено, то его проверяем как для log point для и для остановки
                    if (!string.IsNullOrEmpty(breakpoint.Condition))
                    {
                        try
                        {
                            needStop = EvaluateInFrame(breakpoint.Condition, _currentFrame).AsBoolean();
                        }
                        catch (Exception ex)
                        {
                            needStop = true;
                            error = $"Не удалось выполнить условие точки останова: {ex.Message}";
                        }
                    }

                    if (needStop)
                    {
                        // Если это точка останова с log point, то событие Stopped отработаем, но реально поток останавливать не будем
                        if (!string.IsNullOrEmpty(breakpoint.LogMessage))
                            _stopInfo = new(MachineStoppingReason.Breakpoint, breakpoint.LogMessage, true);
                        else
                            _stopInfo = new(MachineStoppingReason.Breakpoint, error, false);
                    }
                }
            }

            if (needStop)
            {
                CreateFullCallstack();
                InvokeMachineStopped();
            }
        }

        private void InvokeMachineStopped()
        {
            if (DebugMode)
            {
                if (!_stopInfo.IsLogMessage)
                    _resetEvent.Reset();

                var args = new MachineStoppedEventArgs(Environment.CurrentManagedThreadId, _stopInfo.StopReason, _stopInfo.ReasonDetails, _stopInfo.IsLogMessage);
                Stopped?.Invoke(this, args);
            }
        }
    }
}
