/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using System.Diagnostics.CodeAnalysis;
using OneScript.Compilation;
using OneScript.Contexts;
using OneScript.DependencyInjection;
using OneScript.Exceptions;
using OneScript.Execution;
using OneScript.Language;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Types;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine.Interfaces;

namespace ScriptEngine.Hosting
{
    public static class EngineBuilderExtensions
    {
        public static IEngineBuilder SetupEnvironment(this IEngineBuilder b, Action<IRuntimeEnvironment> action)
        {
            b.EnvironmentSetupActions.Add(action);
            return b;
        }

        public static IEngineBuilder SetupAssemblies(this IEngineBuilder b, Action<ContextDiscoverer> action)
        {
            b.AssembliesSetupActions.Add(action);
            return b;
        }
        
        public static IEngineBuilder SetDefaultOptions(this IEngineBuilder builder)
        {
            var services = builder.Services;
            
            services.Register(sp => sp);
            services.RegisterSingleton<IExceptionInfoFactory, ExceptionInfoFactory>();
            services.RegisterSingleton<CompileTimeSymbolsProvider>();
            services.RegisterSingleton<IErrorSink>(svc => new ThrowingErrorSink(CompilerException.FromCodeError));
            services.RegisterSingleton<IRuntimeEnvironment, RuntimeEnvironment>();
            services.RegisterSingleton<ITypeManager, DefaultTypeManager>();
            services.RegisterSingleton<ILibraryManager, LibraryManager>();
            services.RegisterSingleton<ContextDiscoverer>();
            services.RegisterSingleton<IMachineInstancePool, MachineInstancePool>();
            
            services.Register<ExecutionDispatcher>();
            services.Register<IDependencyResolver, NullDependencyResolver>();
            
            services.RegisterEnumerable<IExecutorProvider, StackMachineExecutor>();
            services.RegisterEnumerable<IDirectiveHandler, ConditionalDirectiveHandler>();
            services.RegisterEnumerable<IDirectiveHandler, RegionDirectiveHandler>();
            
            services.EnablePredefinedIterables();
            services.Register(sp =>
            {
                var providers = sp.ResolveEnumerable<IDirectiveHandler>();
                return new PreprocessorHandlers(providers);
            });
            
            services.Register(sp =>
            {
                var providers = sp.Resolve<ConfigurationProviders>();
                return providers.CreateConfig();
            });
            
            services.Register<ScriptingEngine>();

            return builder;
        }

        public static IEngineBuilder UseImports(this IEngineBuilder b)
        {
            b.Services.UseImports();
            return b;
        }

        public static IEngineBuilder WithDebugger(this IEngineBuilder b, IDebugController debugger)
        {
            b.Services.RegisterSingleton(debugger);
            return b;
        }
    }
}