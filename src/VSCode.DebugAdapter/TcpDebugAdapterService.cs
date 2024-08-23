/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.IO;

namespace VSCode.DebugAdapter
{
    partial class Program
    {
        public class TcpDebugAdapterService : BackgroundService
        {
            private readonly ILogger<TcpDebugAdapterService> _logger;
            private readonly int _debugPort;
            private readonly TcpListener _listener;

            public TcpDebugAdapterService(IConfiguration configuration, ILogger<TcpDebugAdapterService> logger)
            {
                _logger = logger;
                _debugPort = configuration.GetValue("port", 4711);
                _listener = TcpListener.Create(_debugPort);
            }

            protected async override Task ExecuteAsync(CancellationToken stoppingToken)
            {
                var listener = TcpListener.Create(_debugPort);
                stoppingToken.Register(() =>
                {
                    listener?.Stop();
                });

                try
                {
                    _logger.LogInformation($"Starting listening to the client on the port {_debugPort}");
                    listener.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to start tcp listener");
                    return;
                }

                while (!stoppingToken.IsCancellationRequested && listener.Server.IsBound)
                {
                    TcpClient client = null;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync(stoppingToken);
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to accept client connection");
                    }

                    if (client is not null)
                    {
                        _logger.LogInformation($"Client is connected: {client.Client.RemoteEndPoint}");

                        using var stream = client.GetStream();
                        await new OscriptDebugAdapter(stream, stream).RunAsync(stoppingToken);

                        client.Dispose();
                        client = null;

                        _logger.LogInformation($"Client is disconnected");
                    }
                }

                _logger.LogInformation("Listening is stopped");
            }

            public override Task StopAsync(CancellationToken cancellationToken)
            {
                _listener.Stop();
                return Task.CompletedTask;
            }

            public override void Dispose()
            {
                base.Dispose();
                _listener.Stop();
            }
        }
    }
}