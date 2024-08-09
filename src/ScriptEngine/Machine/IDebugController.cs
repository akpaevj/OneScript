/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading.Tasks;
using Grpc.Core;

namespace ScriptEngine.Machine
{
    public interface IDebugController : IDisposable
    {
        IBreakpointManager BreakpointManager { get; }

        void Init();
        void Wait();
        void NotifyProcessExit(int exitCode);
        void AttachToThread();
        void DetachFromThread();
    }

    public interface IBreakpointManager
    {
        void SetExceptionBreakpoints((string Id, string Condition)[] filters);

        void SetBreakpoints(string module, (int Line, string Condition)[] breakpoints);

        bool StopOnAnyException(string message);

        bool StopOnUncaughtException(string message);
        
        bool FindBreakpoint(string module, int line);

        string GetCondition(string module, int line);

        void Clear();
    }
}
