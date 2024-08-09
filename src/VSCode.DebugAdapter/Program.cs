/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Serilog;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace VSCode.DebugAdapter
{

    class Program
    {
        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            TcpListener listener = null;
            cts.Token.Register(() => listener?.Stop());

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Адаптер может быть запущен в серверном режиме (--debug) и опционально - с указанием порта прослушки (--port=4711)
            if (args.Contains("--debug"))
            {
                var strPort = args.FirstOrDefault(c => c.Contains("--port"))?.Split('=')[1] ?? "";
                var port = string.IsNullOrEmpty(strPort) ? 4711 : int.Parse(strPort);

                listener = TcpListener.Create(port);

                try
                {
                    listener.Start();
                    Console.WriteLine($"Listening on port {port}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start tcp listener: {ex.Message}");
                    return;
                }

                while (!cts.IsCancellationRequested && listener.Server.IsBound)
                {
                    TcpClient client = null;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                        Console.WriteLine($"Client is connected: {client.Client.RemoteEndPoint}");
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to accept client connection: {ex}");
                    }

                    if (client is not null)
                    {
                        using var stream = client.GetStream();
                        await RunDebugAdapter(stream, stream, cts.Token);

                        client.Dispose();
                        client = null;

                        Console.WriteLine($"Client is disconnected");
                    }
                }
            }
            else
                await RunDebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), cts.Token);
        }

        private static async Task RunDebugAdapter(Stream input, Stream output, CancellationToken cancellationToken)
        {
            var adapter = new OscriptDebugAdapter(input, output);
            await adapter.RunAsync(cancellationToken);
        }
    }
}
