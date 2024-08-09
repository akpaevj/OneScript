﻿/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.StandardLibrary;
using ScriptEngine.HostedScript;
using ScriptEngine.Hosting;
using ScriptEngine.HostedScript.Extensions;
using OneScript.Web.Server;

namespace oscript
{
    internal static class ConsoleHostBuilder
    {
        public static IEngineBuilder Create(string codePath)
        {
            var builder = DefaultEngineBuilder.Create()
                .SetupConfiguration(p =>
                {
                    p.UseSystemConfigFile()
                        .UseEnvironmentVariableConfig("OSCRIPT_CONFIG")
                        .UseEntrypointConfigFile(codePath);
                })
                .SetDefaultOptions()
                .UseImports()
                .UseFileSystemLibraries()
                .UseNativeRuntime()
                .SetupEnvironment(env =>
                {
                    env.AddStandardLibrary()
                     .AddWebServer()
                     .UseTemplateFactory(new DefaultTemplatesFactory());
                });

            return builder;
        }

        public static HostedScriptEngine Build(IEngineBuilder builder)
        {
            var engine = builder.Build(); 
            var mainEngine = new HostedScriptEngine(engine);

            return mainEngine;
        }
    }
}