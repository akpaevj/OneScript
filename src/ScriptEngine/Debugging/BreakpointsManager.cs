/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.Commons;
using OneScript.Debug.Grpc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ScriptEngine.Debugging
{
    internal class BreakpointsManager : IBreakpointsManager
    {
        private readonly ConcurrentBag<OsSourceBreakpoint> _breakpoints = new();
        private readonly ConcurrentDictionary<string, string> _exceptionBreakpoints = new();

        public void Clear()
        {
            _breakpoints.Clear();
            _exceptionBreakpoints.Clear();
        }

        public bool NeedStopOnException(Exception ex, bool userHandled)
        {
            var needStop = false;

            if (_exceptionBreakpoints.TryGetValue("all", out var condition))
                if (string.IsNullOrEmpty(condition))
                    needStop = true;
                else
                    needStop = ex.Message.ToLower().Contains(condition);

            if (!userHandled && _exceptionBreakpoints.TryGetValue("uncaught", out condition))
                if (string.IsNullOrEmpty(condition))
                    needStop = true;
                else
                    needStop = ex.Message.ToLower().Contains(condition);

            return needStop;
        }

        public bool TryGetBreakpoint(string source, int line, out OsSourceBreakpoint breakpoint)
        {
            breakpoint = _breakpoints.FirstOrDefault(c => c.Source.ToUpper() == source.ToUpper() && c.Line == line);

            return breakpoint != null;
        }

        public void SetBreakpoints(OsSourceBreakpoint[] breakpoints)
        {
            _breakpoints.Clear();
            breakpoints.ForEach(c => _breakpoints.Add(c));
        }

        public void SetExceptionBreakpoints(OsExceptionBreakpoint[] breakpoints)
        {
            _exceptionBreakpoints.Clear();
            breakpoints.ForEach(c => _exceptionBreakpoints.TryAdd(c.Id, c.Condition.ToLower()));
        }
    }
}
