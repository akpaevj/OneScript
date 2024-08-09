/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.StandardLibrary.Collections;
using ScriptEngine.Hosting;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;

namespace OneScript.Web.Server
{
    public static class ContextDiscovererExtensions
    {
        public static ContextDiscoverer AddWebServer(this ContextDiscoverer contextDiscoverer)
        {
            return contextDiscoverer.AddAssembly(typeof(WebServer).Assembly);
        }
    }
}