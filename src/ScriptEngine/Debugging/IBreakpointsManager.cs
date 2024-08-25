/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.Debug.Grpc;
using System;

namespace ScriptEngine.Debugging
{
    public interface IBreakpointsManager
    {
        void SetBreakpoints(OsSourceBreakpoint[] breakpoints);

        void SetExceptionBreakpoints(OsExceptionBreakpoint[] breakpoints);

        bool TryGetBreakpoint(string source, int line, out OsSourceBreakpoint breakpoint);
        
        bool NeedStopOnException(Exception ex, bool userHandled);

        void Clear();
    }
}
