/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System.IO;
using System.Net;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace VSCode.DebugAdapter
{
    partial class Program
    {
        static async Task Main(string[] args)
        {
            var app = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, sc) =>
                {
                    var debugMode = context.Configuration.GetValue("debug", false);

                    if (debugMode)
                        sc.AddHostedService<TcpDebugAdapterService>();
                    else
                        sc.AddHostedService<ConsoleDebugAdapterService>();
                })
                .Build();

            await app.RunAsync();
        }
    }
}