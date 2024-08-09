using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace VSCode.DebugAdapter
{
    public class DebuggeeOptions
    {
        public bool RemoteDebugging { get; private set; } = false;
        public string DebugHost { get; private set; } = string.Empty;
        public int DebugPort { get; private set; } = 0;
        public string ExecutablePath { get; private set; } = string.Empty;
        public IReadOnlyList<string> ExecutableArguments { get; internal set; } = Array.Empty<string>();
        public string Workspace { get; private set; } = string.Empty;
        public string StartupScript { get; private set; } = string.Empty;
        public string[] ScriptArguments { get; private set; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, string> Env { get; private set; }

        public static DebuggeeOptions FromConfigurationProperties(Dictionary<string, JToken> properties)
        {
            var runtimeExecutable = properties.GetValueAsString("runtimeExecutable");
            properties.TryGetValue("runtimeArgs", out var runtimeArgs);
            properties.TryGetValue("args", out var args);
            properties.TryGetValue("env", out var environment);

            var debugHost = properties.GetValueAsString("debugHost");
            if (string.IsNullOrEmpty(debugHost))
                debugHost = "localhost";

            var debugPort = properties.GetValueAsInt("debugPort") ?? 2801;

            var remoteDebugging = false;

            // Если адрес из конфигурации  - это loopback адрес, то считаем, что это локлаьная отладка
            if (!string.IsNullOrEmpty(debugHost))
            {
                var addresses = Dns.GetHostAddresses(debugHost);

                if (addresses.Length == 0)
                    remoteDebugging = true;
                else
                    remoteDebugging = IPAddress.IsLoopback(addresses[0]);
            }

            return new DebuggeeOptions()
            {
                RemoteDebugging = remoteDebugging,
                DebugHost = debugHost,
                DebugPort = debugPort,
                ExecutablePath = runtimeExecutable,
                ExecutableArguments = runtimeArgs?.ToObject<string[]>() ?? Array.Empty<string>(),
                Workspace = properties.GetValueAsString("cwd"),
                StartupScript = properties.GetValueAsString("program"),
                ScriptArguments = args?.ToObject<string[]>() ?? Array.Empty<string>(),
                Env = environment?.ToObject<Dictionary<string, string>>() ?? new(),
            };
        }
    }
}
