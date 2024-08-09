/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.Contexts;
using OneScript.DependencyInjection;
using ScriptEngine.Machine.Contexts;
using System.Collections.Generic;
using System;

namespace ScriptEngine.Hosting
{
    public interface IEngineBuilder
    {
        List<Action<ContextDiscoverer>> AssembliesSetupActions { get; }
        List<Action<IRuntimeEnvironment>> EnvironmentSetupActions { get; }

        ConfigurationProviders ConfigurationProviders { get; }
        
        IServiceDefinitions Services { get; }
        
        ScriptingEngine Build();
    }
}