using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using System.Text;

namespace VSCode.DebugAdapter
{
    public class OscriptDebuggee : IDisposable
    {
        private bool disposedValue;

        private readonly DebuggeeOptions _options;
        private Process _process = null;

        public delegate void ExitedHandler(int code);
        public ExitedHandler ExitedEvent;

        public bool Exited => _process.HasExited;

        public OscriptDebuggee(DebuggeeOptions options)
        {
            _options = options;
        }

        public void Run()
        {
            var args = $"{ConcatArguments(_options.ExecutableArguments)} -debug -port={_options.DebugPort} \"{_options.StartupScript}\" {_options.ScriptArguments}";

            _process = new Process
            {
                EnableRaisingEvents = true
            };
            _process.Exited += (sender, eventArgs) => ExitedEvent?.Invoke(_process.ExitCode);

            var psi = _process.StartInfo;
            psi.FileName = _options.ExecutablePath;
            psi.UseShellExecute = false;
            psi.Arguments = args;
            psi.WorkingDirectory = _options.Workspace;

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

        private static string ConcatArguments(IEnumerable<string> args)
        {
            if (args == null)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var stringArg in args)
            {
                sb.Append(' ');
                sb.Append('\"');
                sb.Append(stringArg);
                sb.Append('\"');
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
