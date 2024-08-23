using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OneScript.Commons;
using ScriptEngine.Machine;
using ScriptEngine.Debugging.Grpc;
using ScriptEngine.Machine.Debugging;
using System.Threading;
using System.Threading.Channels;
using System;
using System.Runtime.InteropServices;
using OneScript.Contexts;
using ScriptEngine.Machine.Contexts;
using System.Diagnostics.Eventing.Reader;
using OneScript.Values;
using OneScript.Types;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;

namespace ScriptEngine.Debugging
{
    public class DebugServer : OscriptDebug.OscriptDebugBase
    {
        private readonly TaskCompletionSource<int> _processExitedTask = new();
        private CancellationTokenSource _ioCts;
        private readonly IBreakpointManager _breakpointManager;
        private readonly ScriptingEngine _scriptingEngine;

        private bool _debugEnabled = false;
        private readonly Channel<OsThreadStatus> _threadEventsChannel = Channel.CreateUnbounded<OsThreadStatus>();
        private readonly Channel<OsInputOutput> _ioEventsChannel = Channel.CreateUnbounded<OsInputOutput>();
        private readonly Channel<OsStoppedEvent> _stoppedEventsChannel = Channel.CreateUnbounded<OsStoppedEvent>();

        public DebugServer(ScriptingEngine scriptingEngine, IBreakpointManager breakpointManager)
        {
            _breakpointManager = breakpointManager;
            _scriptingEngine = scriptingEngine;

            _scriptingEngine.EngineStopped += EngineStopped;
            MachineInstancesManager.MachineInstanceCreated += MachineInstancesManager_MachineInstanceCreated;
            MachineInstancesManager.MachineInstanceRun += MachineInstance_CommandsFlowRun;
            MachineInstancesManager.MachineInstanceFinished += MachineInstance_CommandsFlowFinished;
        }

        private void EngineStopped(int stoppingCode)
            => _processExitedTask.SetResult(stoppingCode);

        public override async Task InputOutputEvents(IAsyncStreamReader<OsInputOutput> requestStream, IServerStreamWriter<OsInputOutput> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var ioEvent = await _ioEventsChannel.Reader.ReadAsync(context.CancellationToken);
                await responseStream.WriteAsync(ioEvent, context.CancellationToken);
            }
        }

        public override Task<Empty> StartDebug(Empty request, ServerCallContext context)
        {
            _debugEnabled = true;

            _ioCts = new();
            StartIOStreaming();

            MachineInstancesManager.GetInstances().ForEach(c =>
            {
                c.EnableDebugMode(_breakpointManager);
                c.Continue();
            });

            return Task.FromResult(new Empty());
        }

        private void StartIOStreaming()
        {
            var currentProcess = Process.GetCurrentProcess();

            // output
            _ = Task.Run(async () =>
            {
                var sb = new StringBuilder();
                using var sw = new StringWriter(sb);
                Console.SetOut(sw);

                while (!_ioCts.IsCancellationRequested)
                {
                    if (sb.Length > 0)
                    {
                        await _ioEventsChannel.Writer.WriteAsync(new OsInputOutput()
                        {
                            Message = sb.ToString(),
                            Category = OsInputOutputCategory.StdOut
                        });

                        sb.Clear();
                    }
                }
            }, _ioCts.Token);
        }

        public override Task<Empty> StopDebug(OsStopDebugRequest request, ServerCallContext context)
        {
            _ioCts?.Cancel();

            _debugEnabled = false;
            _breakpointManager.Clear();

            MachineInstancesManager.GetInstances().ForEach(c => c.DisableDebugMode());

            return Task.FromResult(new Empty());
        }

        public override async Task<OsSetBreakpointsResponse> SetBreakpoints(OsSetBreakpointsRequest request, ServerCallContext context)
        {
            _breakpointManager.SetBreakpoints(request.Breakpoints.ToArray());

            var response = new OsSetBreakpointsResponse();
            int i = 1;
            request.Breakpoints.ForEach(c =>
            {
                response.Breakpoints.Add(new OsBreakpoint()
                {
                    Id = i++,
                    Line = c.Line,
                    Source = c.Source,
                    Verified = true
                });
            });

            return await Task.FromResult(response);
        }

        public override Task<Empty> SetExceptionBreakpoints(OsSetExceptionBreakpointRequest request, ServerCallContext context)
        {
            _breakpointManager.SetExceptionBreakpoints(request.Breakpoints.ToArray());

            return Task.FromResult(new Empty());
        }

        public override Task<OsGetThreadsResponse> GetThreads(Empty request, ServerCallContext context)
        {
            var threads = MachineInstancesManager.GetThreadIdentifiers();

            var response = new OsGetThreadsResponse();
            response.Threads.AddRange(threads);

            return Task.FromResult(response);
        }

        public override Task<OsGetStackFramesResponse> GetStackFrames(OsGetStackFramesRequest request, ServerCallContext context)
        {
            var response = new OsGetStackFramesResponse();

            var machineInstance = MachineInstancesManager.GetThreadInstance(request.ThreadId);

            var i = 0;
            machineInstance.GetExecutionFrames()?.ForEach(c =>
            {
                var stackFrame = new OsStackFrame
                {
                    Index = i++,
                    LineNumber = c.LineNumber,
                    MethodName = c.MethodName,
                    Source = c.Source,
                    ThreadId = request.ThreadId
                };

                response.StackFrames.Add(stackFrame);
            });

            return Task.FromResult(response);
        }

        public override Task<OsGetScopesResponse> GetScopes(OsGetScopesRequest request, ServerCallContext context)
        {
            var response = new OsGetScopesResponse();
           
            response.Scopes.Add(new OsScope() { IsLocal = false });
            response.Scopes.Add(new OsScope() { IsLocal = true });

            return Task.FromResult(response);
        }

        public override Task<OsGetVariablesResponse> GetVariables(OsGetVariablesRequest request, ServerCallContext context)
        {
            var visualizer = new DefaultVariableVisualizer(_scriptingEngine.TypeManager);
            var machineInstance = MachineInstancesManager.GetThreadInstance(request.ThreadId);
            var frames = machineInstance.GetExecutionFrames();

            var response = new OsGetVariablesResponse();

            if (frames != null)
            {
                var frame = frames[request.FrameIndex];

                var srcVariables = request.IsLocal switch
                {
                    true => frame.FrameObject.Locals,
                    _ => frame.FrameObject.ThisScope.Variables
                };

                if (request.Path.Count == 0)
                    for (int i = 0; i < srcVariables.Count; i++)
                    {
                        var variable = srcVariables[i];
                        var osVariable = visualizer.GetVariable(variable, i);
                        response.Variables.Add(osVariable);
                    }
                else
                {
                    var variable = srcVariables[request.Path.First()];

                    var path = request.Path.ToArray()[1..];
                    foreach (var i in path)
                        variable = visualizer.GetChildVariables(variable.Value)[i];

                    var variables = visualizer.GetChildVariables(variable.Value);
                    for (int i = 0; i < variables.Count; i++)
                    {
                        var osVariable = visualizer.GetVariable(variables[i], i);
                        response.Variables.Add(osVariable);
                    }
                }
            }

            return Task.FromResult(response);
        }

        public override Task<OsEvaluateResponse> Evaluate(OsEvaluateRequest request, ServerCallContext context)
        {
            var visualizer = new DefaultVariableVisualizer(_scriptingEngine.TypeManager);

            var value = MachineInstancesManager.GetThreadInstance(request.ThreadId).EvaluateInFrame(request.Expression, request.ContextFrame);
            var variable = visualizer.GetVariable(Variable.Create(value, ""), 0);

            return Task.FromResult(new OsEvaluateResponse()
            {
                Variable = variable
            });
        }

        public override Task<Empty> Continue(OsContinueRequest request, ServerCallContext context)
        {
            if (request.SingleThread && request.ThreadId >= 0)
                MachineInstancesManager.GetThreadInstance(request.ThreadId).Continue();
            else
                MachineInstancesManager.GetInstances().ForEach(c => c.Continue());

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Pause(OsPauseRequest request, ServerCallContext context)
        {
            if (request.SingleThread && request.ThreadId >= 0)
                MachineInstancesManager.GetThreadInstance(request.ThreadId).Pause();
            else
                MachineInstancesManager.GetInstances().ForEach(c => c.Pause());

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Next(OsNextRequest request, ServerCallContext context)
        {
            if (request.SingleThread && request.ThreadId >= 0)
                MachineInstancesManager.GetThreadInstance(request.ThreadId).Next();
            else
                MachineInstancesManager.GetInstances().ForEach(c => c.Next());

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> StepOut(OsStepOutRequest request, ServerCallContext context)
        {
            if (request.SingleThread && request.ThreadId >= 0)
                MachineInstancesManager.GetThreadInstance(request.ThreadId).StepOut();
            else
                MachineInstancesManager.GetInstances().ForEach(c => c.StepOut());

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> StepIn(OsStepInRequest request, ServerCallContext context)
        {
            if (request.SingleThread && request.ThreadId >= 0)
                MachineInstancesManager.GetThreadInstance(request.ThreadId).StepIn();
            else
                MachineInstancesManager.GetInstances().ForEach(c => c.StepIn());

            return Task.FromResult(new Empty());
        }

        public override Task<OsGetProcessIdResponse> GetProcessId(Empty request, ServerCallContext context)
            => Task.FromResult(new OsGetProcessIdResponse() { ProcessId = System.Environment.ProcessId });

        public override async Task ProcessExited(Empty request, IServerStreamWriter<OsProcessExitedMessage> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var exitCode = await _processExitedTask.Task;
                _ioCts?.Cancel();

                await responseStream.WriteAsync(new OsProcessExitedMessage() { ExitCode = exitCode }); 
            }
        }

        public override async Task StoppedEvents(Empty request, IServerStreamWriter<OsStoppedEvent> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var stoppedEvent = await _stoppedEventsChannel.Reader.ReadAsync(context.CancellationToken);
                await responseStream.WriteAsync(stoppedEvent, context.CancellationToken);
            }
        }

        public override async Task ThreadEvents(Empty request, IServerStreamWriter<OsThreadStatus> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var threadEvent = await _threadEventsChannel.Reader.ReadAsync(context.CancellationToken);
                await responseStream.WriteAsync(threadEvent, context.CancellationToken);
            }
        }

        private void MachineInstancesManager_MachineInstanceCreated(object sender, MachineContextEventArgs e)
        {
            var machineInstance = sender as MachineInstance;

            if (_debugEnabled)
                machineInstance.EnableDebugMode(_breakpointManager);
        }

        private async void MachineInstance_CommandsFlowRun(object sender, MachineContextEventArgs e)
        {
            var machineInstance = sender as MachineInstance;
            machineInstance.MachineStopped += MachineInstanceStopped;

            await _threadEventsChannel.Writer.WriteAsync(new OsThreadStatus()
            {
                ThreadId = e.ThreadId,
                ThreadStopped = false
            });
        }

        private async void MachineInstance_CommandsFlowFinished(object sender, MachineContextEventArgs e)
        {
            var machineInstance = sender as MachineInstance;
            machineInstance.MachineStopped -= MachineInstanceStopped;

            await _threadEventsChannel.Writer.WriteAsync(new OsThreadStatus()
            {
                ThreadId = e.ThreadId,
                ThreadStopped = true
            });
        }

        private async void MachineInstanceStopped(object sender, MachineStoppedEventArgs args)
        {
            var machineInstance = sender as MachineInstance;

            var reason = args.Reason switch
            {
                MachineStoppingReason.Step => OsStoppedEventReason.Step,
                MachineStoppingReason.Exception => OsStoppedEventReason.Exception,
                MachineStoppingReason.Breakpoint => OsStoppedEventReason.Breakpoint,
                _ => throw new NotImplementedException(),
            };
            await _stoppedEventsChannel.Writer.WriteAsync(new()
            {
                ThreadId = args.ThreadId,
                Reason = reason,
                Text = args.Details
            });

            machineInstance.WaitContinue();
        }
    }
}
