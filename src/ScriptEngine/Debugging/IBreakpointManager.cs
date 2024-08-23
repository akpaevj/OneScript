/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using ScriptEngine.Debugging.Grpc;
using System;

namespace ScriptEngine.Machine.Debugging
{
    public interface IBreakpointManager
    {
        void SetBreakpoints(OsSourceBreakpoint[] breakpoints);

        void SetExceptionBreakpoints(OsExceptionBreakpoint[] breakpoints);

        bool NeedStopOnBreakpoint(string source, int line);
        
        bool NeedStopOnException(Exception ex, bool userHandled);

        void Clear();
    }
}
