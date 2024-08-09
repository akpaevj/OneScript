/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.Contexts;
using OneScript.DependencyInjection;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;
using System;
using System.Collections.Generic;

namespace ScriptEngine.Hosting
{
    public class DefaultEngineBuilder : IEngineBuilder
    {
        public List<Action<IRuntimeEnvironment>> EnvironmentSetupActions { get; } = new();
        public List<Action<ContextDiscoverer>> AssembliesSetupActions { get; } = new();

        protected DefaultEngineBuilder()
        {
            AssembliesSetupActions.Add(cd => cd.AddAssembly(GetType().Assembly));
        }
        
        public static IEngineBuilder Create()
        {
            var builder = new DefaultEngineBuilder();
            return builder;
        }
        
        public ConfigurationProviders ConfigurationProviders { get; } = new ConfigurationProviders();
        
        public IServiceDefinitions Services { get; private set; } = new TinyIocImplementation();
        
        public virtual ScriptingEngine Build()
        {
            var container = Services.CreateContainer();

            var contextDiscoverer = container.Resolve<ContextDiscoverer>();
            AssembliesSetupActions.ForEach(c => c.Invoke(contextDiscoverer));

            var environment = container.Resolve<IRuntimeEnvironment>();
            EnvironmentSetupActions.ForEach(c => c.Invoke(environment));

            var engine = container.Resolve<ScriptingEngine>();
            
            var dependencyResolver = container.TryResolve<IDependencyResolver>();
            dependencyResolver?.Initialize(engine);
            
            return engine;
        }
    }
}