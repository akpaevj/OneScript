/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using System.Net.Sockets;
using OneScript.DebugProtocol.TcpServer;
using ScriptEngine.Machine;
using System.Net;
using OneScript.DebugProtocol;

namespace OneScript.DebugServices
{
    public class TcpDebugController : IDebugController
    {
        private readonly TcpListener _listener;
        private bool disposedValue;

        public IBreakpointManager BreakpointManager { get; private set; }

        public TcpDebugController(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void AttachToThread()
        {
            throw new NotImplementedException();
        }

        public void DetachFromThread()
        {
            throw new NotImplementedException();
        }

        public void Init()
        {
            throw new NotImplementedException();
        }

        public void NotifyProcessExit(int exitCode)
        {
            throw new NotImplementedException();
        }

        public void Wait()
        {
            throw new NotImplementedException();
        }

        private static ThreadStopReason ConvertStopReason(MachineStopReason reason) => reason switch
        {
            MachineStopReason.Breakpoint => ThreadStopReason.Breakpoint,
            MachineStopReason.BreakpointConditionError => ThreadStopReason.Breakpoint,
            MachineStopReason.Step => ThreadStopReason.Step,
            MachineStopReason.Exception => ThreadStopReason.Exception,
            _ => throw new NotImplementedException(),
        };

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                }

                _listener?.Stop();

                disposedValue = true;
            }
        }

        ~TcpDebugController()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class BinaryTcpDebugServer
    {
        private readonly int _port;

        public BinaryTcpDebugServer(int port)
        {
            _port = port;
        }

        public IDebugController CreateDebugController()
        {
            var listener = TcpListener.Create(_port);
            var channel = new DelayedConnectionChannel(listener);
            var ipcServer = new DefaultMessageServer<RpcCall>(channel)
            {
                ServerThreadName = "debug-server"
            };
            var callback = new TcpEventCallbackChannel(channel);
            var threadManager = new ThreadManager();
            var breakpoints = new DefaultBreakpointManager();
            var debuggerService = new DefaultDebugService(breakpoints, threadManager, new DefaultVariableVisualizer());
            var controller = new DefaultDebugController(ipcServer, debuggerService, callback, threadManager, breakpoints);

            return controller;
        }
    }
}