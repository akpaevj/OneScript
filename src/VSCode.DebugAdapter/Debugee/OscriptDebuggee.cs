using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using OneScript.DebugProtocol.Grpc;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;

namespace VSCode.DebugAdapter.Debugee
{
    public class OscriptDebuggee : IDisposable
    {
        private bool disposedValue;

        private readonly DebuggeeOptions _options;
        private Process _process = null;

        public delegate void ExitedHandler(int code);
        public ExitedHandler Exited;

        public OscriptDebuggee(DebuggeeOptions options)
        {
            _options = options;
        }

        public void Run()
        {
            var args = $"{Utilities.ConcatArguments(_options.ExecutableArguments)} -debug -port={_options.DebugPort} \"{_options.StartupScript}\" {_options.ScriptArguments}";

            _process = new Process
            {
                EnableRaisingEvents = true
            };
            _process.Exited += (sender, eventArgs) => Exited?.Invoke(_process.ExitCode);

            var psi = _process.StartInfo;
            psi.FileName = _options.ExecutablePath;
            psi.UseShellExecute = false;
            psi.Arguments = args;
            psi.WorkingDirectory = _options.Workspace;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;

            foreach (var item in _options.Env)
                psi.EnvironmentVariables[item.Key] = item.Value;

            _process.Start();
        }

        private void Stop()
        {
            _process?.Dispose();
            _process = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Stop();

                disposedValue = true;
            }
        }

        ~OscriptDebuggee()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
