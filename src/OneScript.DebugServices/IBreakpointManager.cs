/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using OneScript.DebugServices.Grpc;

namespace OneScript.DebugServices
{
    public interface IBreakpointManager
    {
        void SetBreakpoints(OsExceptionBreakpoint[] breakpoints);

        void SetExceptionBreakpoints((string Id, string Condition)[] filters);

        bool StopOnAnyException(string message);

        bool StopOnUncaughtException(string message);
        
        bool FindBreakpoint(string module, int line);

        string GetCondition(string module, int line);

        void Clear();
    }
}
