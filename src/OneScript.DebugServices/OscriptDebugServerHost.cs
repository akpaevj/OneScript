using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScriptEngine.Machine;

namespace OneScript.DebugServices
{
    public class OscriptDebugServerHost : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public IBreakpointManager BreakpointManager = new DefaultBreakpointManager();

        public OscriptDebugServerHost(int port)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => 
            {
                options.ListenAnyIP(port);
            });
            
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<IBreakpointManager, DefaultBreakpointManager>();

            var app = builder.Build();
            app.MapGrpcService<OscriptDebugServer>();
            app.RunAsync(_cts.Token);
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }
    }
}
