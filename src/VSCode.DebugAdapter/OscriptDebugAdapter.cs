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
using VSCode.DebugAdapter.Debugee;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Grpc.Net.Client;
using OneScript.DebugProtocol.Grpc;
using Google.Protobuf.WellKnownTypes;
using System.Net;

namespace VSCode.DebugAdapter
{
    class OscriptDebugAdapter : DebugAdapterBase
    {
        private CancellationToken _cancellationToken;

        private bool _noDebug;
        private OscriptDebuggee _debuggee;
        private GrpcChannel _channel = null;
        private OscriptDebug.OscriptDebugClient _debuggeeClient = null;

        public string AdapterID { get; private set; }
        public string ClientID { get; private set; }

        public OscriptDebugAdapter(Stream inStream, Stream outStream)
        {
            InitializeProtocolClient(inStream, outStream);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _cancellationToken.Register(() => Protocol.Stop());

            await Task.Run(() =>
            {
                Protocol.Run();
                Protocol.WaitForReader();
            });
        }

        protected override void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder)
        {
            AdapterID = responder.Arguments.AdapterID;
            ClientID = responder.Arguments.ClientID;

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

            try
            {
                _debuggee.Run();
            }
            catch (Exception ex)
            {
                _debuggee?.Dispose();
                _debuggee = null;
                responder.SetError(new ProtocolException($"Ошибка запуска приложения: {ex}"));
            }

            if (!_noDebug)
            {
                _channel = GrpcChannel.ForAddress($"http://{options.DebugHost}:{options.DebugPort}");
                _debuggeeClient = new OscriptDebug.OscriptDebugClient(_channel);
            }
        }

        protected override async void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder)
        {
            await _debuggeeClient?.StartDebugAsync(new Empty(), cancellationToken: _cancellationToken);
            responder.SetResponse(new ConfigurationDoneResponse());
        }

        protected override void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder)
        {
            
        }
    }
}
