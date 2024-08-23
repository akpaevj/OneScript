/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.IO;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Grpc.Net.Client;
using ScriptEngine.Debugging.Grpc;
using Google.Protobuf.WellKnownTypes;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace VSCode.DebugAdapter
{
    class OscriptDebugAdapter : DebugAdapterBase, IDisposable
    {
        private CancellationToken _cancellationToken;

        private int _clientsFirstLine = 0;
        private bool _noDebug;
        private bool _attachMode = false;
        private OscriptDebuggee _debuggee;
        private GrpcChannel _channel = null;
        private OscriptDebug.OscriptDebugClient _debuggeeClient = null;

        private readonly References<(int ThreadId, int FrameIndex)> _frameReferences = new();
        private readonly References<(int ThreadId, int FrameIndex, bool IsLocal, List<int> Path)> _variableReferences = new();

        public string AdapterID { get; private set; }
        public string ClientID { get; private set; }

        public OscriptDebugAdapter(Stream inStream, Stream outStream)
        {
            InitializeProtocolClient(inStream, outStream);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _cancellationToken.Register(Protocol.Stop);

            await Task.Run(() =>
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                Protocol.Run();
                Protocol.WaitForReader();
            }, cancellationToken);
        }

        protected override void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder)
        {
            AdapterID = responder.Arguments.AdapterID;
            ClientID = responder.Arguments.ClientID;

            if (responder.Arguments.LinesStartAt1 == true)
                _clientsFirstLine = 1;

            Protocol.SendEvent(new InitializedEvent());

            var response = new InitializeResponse()
            {
                SupportsConditionalBreakpoints = true,
                SupportsConfigurationDoneRequest = true,
                SupportsExceptionFilterOptions = true,
                SupportsEvaluateForHovers = true,
                SupportTerminateDebuggee = true,
                SupportsSingleThreadExecutionRequests = true
            };
            response.ExceptionBreakpointFilters.Add(new ExceptionBreakpointsFilter()
            {
                Filter = "uncaught",
                Label = "Необработанные исключения",
                Description = "Остановка при возникновении необработанного исключения",
                SupportsCondition = true,
                ConditionDescription = "Искомая подстрока текста исключения"
            });
            response.ExceptionBreakpointFilters.Add(new ExceptionBreakpointsFilter()
            {
                Filter = "all",
                Label = "Все исключения",
                Description = "Остановка при возникновении любого исключения",
                SupportsCondition = true,
                ConditionDescription = "Искомая подстрока текста исключения"
            });

            responder.SetResponse(response);
        }

        protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            _noDebug = responder.Arguments.NoDebug == true;

            var options = DebuggeeOptions.FromConfigurationProperties(responder.Arguments.ConfigurationProperties);

            _debuggee = new OscriptDebuggee(options);
            _debuggee.ExitedEvent += exitCode =>
            {
                _debuggee?.Dispose();
                _debuggee = null;

                Protocol.SendEvent(new TerminatedEvent());
            };

            try
            {
                _debuggee.Run();

                if (!_noDebug)
                {
                    var address = $"http://localhost:{options.DebugPort}";

                    _channel = GrpcChannel.ForAddress(address);
                    _debuggeeClient = new OscriptDebug.OscriptDebugClient(_channel);
                }

                responder.SetResponse(new LaunchResponse());
            }
            catch (Exception ex)
            {
                _debuggee?.Dispose();
                _debuggee = null;
                responder.SetError(new ProtocolException($"Ошибка запуска приложения: {ex}"));
            }
        }

        protected override void HandleAttachRequestAsync(IRequestResponder<AttachArguments> responder)
        {
            _attachMode = true;

            var debugHost = responder.Arguments.ConfigurationProperties.GetValueAsString("debugHost") ?? "localhost";
            var debugPort = responder.Arguments.ConfigurationProperties.GetValueAsInt("debugPort") ?? 2801;

            var address = $"http://{debugHost}:{debugPort}";

            _channel = GrpcChannel.ForAddress(address);
            _debuggeeClient = new OscriptDebug.OscriptDebugClient(_channel);

            responder.SetResponse(new AttachResponse());
        }

        protected override async void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder)
        {
            await WrapWithOutputError(async () =>
            {
                await _debuggeeClient?.StartDebugAsync(new Empty(), cancellationToken: _cancellationToken);
                StartEventsStreaming();
                responder.SetResponse(new ConfigurationDoneResponse());
            });
        }

        private void StartEventsStreaming()
        {
            // io output
            _ = Task.Run(async () =>
            {
                var ioDuplexCall = _debuggeeClient.InputOutputEvents(cancellationToken: _cancellationToken);
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (await ioDuplexCall.ResponseStream.MoveNext(_cancellationToken))
                    {
                        var item = ioDuplexCall.ResponseStream.Current;

                        var category = item.Category switch
                        {
                            OsInputOutputCategory.StdOut => OutputEvent.CategoryValue.Stdout,
                            OsInputOutputCategory.StdErr => OutputEvent.CategoryValue.Stderr,
                            _ => throw new NotImplementedException()
                        };

                        Protocol.SendEvent(new OutputEvent(item.Message)
                        {
                            Category = category
                        });
                    }
                    else
                        await Task.Delay(1);
                }
            }, _cancellationToken);

            // thread events
            _ = Task.Run(async () =>
            {
                var threadEventsStream = _debuggeeClient.ThreadEvents(new Empty(), cancellationToken: _cancellationToken);
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (await threadEventsStream.ResponseStream.MoveNext(_cancellationToken))
                    {
                        var item = threadEventsStream.ResponseStream.Current;

                        Protocol.SendEvent(new ThreadEvent()
                        {
                            Reason = item.ThreadStopped ? ThreadEvent.ReasonValue.Exited : ThreadEvent.ReasonValue.Started,
                            ThreadId = item.ThreadId
                        });
                    }
                    else
                        await Task.Delay(1);
                }
            }, _cancellationToken);

            // stopped events
            _ = Task.Run(async () =>
            {
                var eventsStream = _debuggeeClient.StoppedEvents(new Empty(), cancellationToken: _cancellationToken);
                while (!_cancellationToken.IsCancellationRequested)
                {
                    if (await eventsStream.ResponseStream.MoveNext(_cancellationToken))
                    {
                        var item = eventsStream.ResponseStream.Current;

                        var reason = item.Reason switch
                        {
                            OsStoppedEventReason.Step => StoppedEvent.ReasonValue.Step,
                            OsStoppedEventReason.Breakpoint => StoppedEvent.ReasonValue.Breakpoint,
                            OsStoppedEventReason.Exception => StoppedEvent.ReasonValue.Exception,
                            OsStoppedEventReason.Entry => StoppedEvent.ReasonValue.Entry,
                            OsStoppedEventReason.Goto => StoppedEvent.ReasonValue.Goto,
                            OsStoppedEventReason.FunctionBreakpoint => StoppedEvent.ReasonValue.FunctionBreakpoint,
                            OsStoppedEventReason.DataBreakpoint => StoppedEvent.ReasonValue.DataBreakpoint,
                            OsStoppedEventReason.InstructionBreakpoint => StoppedEvent.ReasonValue.InstructionBreakpoint,
                            _ => throw new NotImplementedException(),
                        };

                        Protocol.SendEvent(new StoppedEvent()
                        {
                            ThreadId = item.ThreadId,
                            Reason = reason,
                            Text = item.Text
                        });
                    }
                    else
                        await Task.Delay(1);
                }
            }, _cancellationToken);
        }

        protected override async void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder)
        {
            await WrapWithOutputError(async() =>
            {
                var request = new OsSetBreakpointsRequest();

                responder.Arguments.Breakpoints.ForEach(c =>
                    request.Breakpoints.Add(new OsSourceBreakpoint()
                    {
                        Line = LineFromDebugger(c.Line),
                        Source = responder.Arguments.Source.Path,
                        Condition = c.Condition ?? ""
                    })
                );

                var debuggeeResponse = await _debuggeeClient.SetBreakpointsAsync(request, cancellationToken: _cancellationToken);

                var adapterResponse = new SetBreakpointsResponse();
                foreach (var item in debuggeeResponse.Breakpoints)
                    adapterResponse.Breakpoints.Add(new Breakpoint()
                    {
                        Id = item.Id,
                        Verified = item.Verified,
                        Line = LineToDebugger(item.Line)
                    });

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleSetExceptionBreakpointsRequestAsync(IRequestResponder<SetExceptionBreakpointsArguments, SetExceptionBreakpointsResponse> responder)
        {
            await WrapWithOutputError(async () => 
            {
                var request = new OsSetExceptionBreakpointRequest();

                for (int i = 0; i < responder.Arguments.FilterOptions.Count; i++)
                {
                    var filterOption = responder.Arguments.FilterOptions[i];
                    var filter = filterOption.FilterId;
                    var filterCondition = responder.Arguments.FilterOptions[i].Condition;

                    request.Breakpoints.Add(new OsExceptionBreakpoint()
                    {
                        Id = filter,
                        Condition = filterCondition ?? string.Empty
                    });
                }

                await _debuggeeClient.SetExceptionBreakpointsAsync(request, cancellationToken: _cancellationToken);

                var adapterResponse = new SetExceptionBreakpointsResponse();
                foreach (var _ in request.Breakpoints)
                    adapterResponse.Breakpoints.Add(new Breakpoint(true));

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleThreadsRequestAsync(IRequestResponder<ThreadsArguments, ThreadsResponse> responder)
        {
            await WrapWithOutputError(async () => 
            {
                var debuggeeResponse = await _debuggeeClient.GetThreadsAsync(new Empty(), cancellationToken: _cancellationToken);

                var adapterResponse = new ThreadsResponse();
                foreach (var item in debuggeeResponse.Threads)
                    adapterResponse.Threads.Add(new Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread(item, $"Поток {item}"));

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleStackTraceRequestAsync(IRequestResponder<StackTraceArguments, StackTraceResponse> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var debuggerRequest = new OsGetStackFramesRequest()
                {
                    ThreadId = responder.Arguments.ThreadId,
                    StartIndex = responder.Arguments.StartFrame ?? 0
                };
                var debuggeeResponse = await _debuggeeClient.GetStackFramesAsync(debuggerRequest, cancellationToken: _cancellationToken);

                var adapterResponse = new StackTraceResponse();
                foreach (var item in debuggeeResponse.StackFrames)
                {
                    var frameId = _frameReferences.Add((item.ThreadId, item.Index));

                    adapterResponse.StackFrames.Add(new StackFrame()
                    {
                        Name = item.MethodName,
                        Source = new Source() { Path = item.Source },
                        Line = item.LineNumber,
                        Id = frameId
                    });
                };

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleScopesRequestAsync(IRequestResponder<ScopesArguments, ScopesResponse> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var (ThreadId, FrameIndex) = _frameReferences.Get(responder.Arguments.FrameId);

                var debuggerRequest = new OsGetScopesRequest()
                {
                    ThreadId = ThreadId,
                    FrameIndex = FrameIndex
                };
                var debuggeeRespones = await _debuggeeClient.GetScopesAsync(debuggerRequest, cancellationToken: _cancellationToken);

                var debuggerResponse = new ScopesResponse();
                foreach (var osScope in debuggeeRespones.Scopes)
                {
                    var reference = _variableReferences.Add((ThreadId, FrameIndex, osScope.IsLocal, new()));

                    debuggerResponse.Scopes.Add(new Scope()
                    {
                        Name = osScope.IsLocal ? "Локальные переменные" : "Переменные модуля",
                        VariablesReference = reference
                    });
                }

                responder.SetResponse(debuggerResponse);
            });
        }

        protected override async void HandleVariablesRequestAsync(IRequestResponder<VariablesArguments, VariablesResponse> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var info = _variableReferences.Get(responder.Arguments.VariablesReference);

                var adapterRequest = new OsGetVariablesRequest()
                {
                    ThreadId = info.ThreadId,
                    FrameIndex = info.FrameIndex,
                    IsLocal = info.IsLocal
                };
                adapterRequest.Path.AddRange(info.Path);

                var appResponse = await _debuggeeClient.GetVariablesAsync(adapterRequest, cancellationToken: _cancellationToken);

                var adapterResponse = new VariablesResponse();

                foreach(var variable in appResponse.Variables)
                {
                    var path = new List<int>();
                    path.AddRange(info.Path);
                    path.Add(variable.Index);

                    // Если у переменной есть свойства или индексатор, то сформируем ссылку на получение дочерних членов
                    var reference = 0;
                    if (variable.IsStructured)
                        reference = _variableReferences.Add((info.ThreadId, info.FrameIndex, info.IsLocal, path));

                    adapterResponse.Variables.Add(new Variable()
                    {
                        Name = $"{variable.Name} ({variable.Type})",
                        Type = variable.Type,
                        Value = reference > 0 ? string.Empty : variable.Value,
                        VariablesReference = reference
                    });
                }

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleEvaluateRequestAsync(IRequestResponder<EvaluateArguments, EvaluateResponse> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var (ThreadId, FrameIndex) = _frameReferences.Get(responder.Arguments.FrameId.Value);

                var adapterRequest = new OsEvaluateRequest()
                {
                    ThreadId = ThreadId,
                    ContextFrame = FrameIndex,
                    Expression = responder.Arguments.Expression
                };
                var appResponse = await _debuggeeClient.EvaluateAsync(adapterRequest, cancellationToken: _cancellationToken);
                var reference = _variableReferences.Add((ThreadId, FrameIndex, true, new()));

                var adapterResponse = new EvaluateResponse()
                {
                    Result = appResponse.Variable.Value,
                    Type = appResponse.Variable.Type,
                    VariablesReference = reference
                };

                responder.SetResponse(adapterResponse);
            });
        }

        protected override async void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var debuggerRequest = new OsContinueRequest()
                {
                    ThreadId = responder.Arguments.ThreadId,
                    SingleThread = responder.Arguments.SingleThread == true
                };
                await _debuggeeClient.ContinueAsync(debuggerRequest, cancellationToken: _cancellationToken);

                var debuggerResponse = new ContinueResponse()
                {
                    AllThreadsContinued = responder.Arguments.SingleThread != true
                };
                responder.SetResponse(debuggerResponse);
            });
        }

        protected override async void HandleNextRequestAsync(IRequestResponder<NextArguments> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var debuggerRequest = new OsNextRequest()
                {
                    ThreadId = responder.Arguments.ThreadId,
                    SingleThread = responder.Arguments.SingleThread == true
                };
                await _debuggeeClient.NextAsync(debuggerRequest, cancellationToken: _cancellationToken);

                responder.SetResponse(new NextResponse());
            });
        }

        protected override async void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var debuggerRequest = new OsStepInRequest()
                {
                    ThreadId = responder.Arguments.ThreadId,
                    SingleThread = responder.Arguments.SingleThread == true
                };
                await _debuggeeClient.StepInAsync(debuggerRequest, cancellationToken: _cancellationToken);

                responder.SetResponse(new StepInResponse());
            });
        }
        
        protected override async void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder)
        {
            await WrapWithOutputError(async () =>
            {
                var debuggerRequest = new OsStepOutRequest()
                {
                    ThreadId = responder.Arguments.ThreadId,
                    SingleThread = responder.Arguments.SingleThread == true
                };
                await _debuggeeClient.StepOutAsync(debuggerRequest, cancellationToken: _cancellationToken);

                responder.SetResponse(new StepOutResponse());
            });
        }

        protected override async void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder)
        {
            // В удаленной отладке остановка отлаживаемого приложения не осуществляется
            var needTerminateDebuggee = responder.Arguments.TerminateDebuggee ?? true && !_attachMode;

            if (needTerminateDebuggee)
                _debuggee?.Dispose();
            else
                await WrapWithOutputError(async() => await _debuggeeClient.StopDebugAsync(new OsStopDebugRequest() { Terminate = false }));

            responder.SetResponse(new DisconnectResponse());
        }

        protected override void HandleProtocolError(Exception ex)
        {
            Protocol.SendEvent(new OutputEvent(ex.Message)
            {
                Category = OutputEvent.CategoryValue.Stderr
            });
        }

        private async Task WrapWithOutputError(Func<Task> action)
        {
            try
            {
                await action.Invoke();
            }
            catch (Exception ex)
            {
                Protocol.SendEvent(new OutputEvent(ex.Message)
                {
                    Category = OutputEvent.CategoryValue.Stderr
                });
            }
        }

        private int LineToDebugger(int line)
            => _clientsFirstLine > 0 ? line : line + _clientsFirstLine;

        private int LineFromDebugger(int line)
            => _clientsFirstLine > 0 ? line : line - _clientsFirstLine;

        public void Dispose()
        {
            _debuggee?.Dispose();
            _debuggee = null;

            _channel?.Dispose();
            _channel = null;
        }
    }
}
