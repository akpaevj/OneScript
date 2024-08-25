using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OneScript.Commons;
using OneScript.DependencyInjection;
using OneScript.Debug;

namespace ScriptEngine.Debugging
{
    public class DebugController : IDebugController, IDisposable
    {
        private readonly int _port;
        private readonly IServiceContainer _serviceContainer;

        private CancellationTokenSource _cts;
        private WebApplication _application;

        public DebugController(IServiceContainer serviceContainer, int port)
        {
            _serviceContainer = serviceContainer;
            _port = port;
        }

        public void StartDebug()
        {
            _cts = new();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureEndpointDefaults(options =>
                {
                    options.Protocols = HttpProtocols.Http2;
                });
                options.ListenAnyIP(_port, options =>
                {
                    options.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();
            builder.Services.AddSingleton(_serviceContainer.Resolve<ScriptingEngine>());
            builder.Services.AddSingleton<DebugServer>();
            builder.Services.AddSingleton<IBreakpointsManager, BreakpointsManager>();

            _application = builder.Build();
            _application.MapGrpcService<DebugServer>();
            _application.RunAsync(_cts.Token);
        }

        public void StopDebug()
        {
            _cts.Cancel();

            _cts = null;
            _application = null;
        }

        public void Dispose() 
            => GC.SuppressFinalize(this);
    }
}
