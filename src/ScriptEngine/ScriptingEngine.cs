/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.Linq;
using System.Threading.Tasks;
using OneScript.Compilation;
using OneScript.Contexts;
using OneScript.DependencyInjection;
using OneScript.Execution;
using OneScript.Types;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Compiler;
using System.Reflection;

namespace ScriptEngine
{
    public class ScriptingEngine : IDisposable
    {
        private readonly ContextDiscoverer _contextDiscoverer;

        private AttachedScriptsFactory _attachedScriptsFactory;
        private IDebugController _debugController;
        private IRuntimeEnvironment _runtimeEnvironment;
        private readonly ILibraryManager _libraryManager;

        public ScriptingEngine(
            ITypeManager types,
            IRuntimeEnvironment env, 
            ILibraryManager libraryManager,
            ContextDiscoverer contextDiscoverer,
            OneScriptCoreOptions options,
            IServiceContainer services)
        {
            _contextDiscoverer = contextDiscoverer;
            TypeManager = types;

            _runtimeEnvironment = env;
            _libraryManager = libraryManager;
            
            Loader = new ScriptSourceFactory();
            Services = services;
            DebugController = services.TryResolve<IDebugController>();
            Loader.ReaderEncoding = options.FileReaderEncoding;
        }

        public IServiceContainer Services { get; }

        public IRuntimeEnvironment Environment => _runtimeEnvironment;

        public ILibraryManager LibraryManager => _libraryManager;

        public ITypeManager TypeManager { get; }
        
        private CodeGenerationFlags ProduceExtraCode { get; set; }
        
        public void AttachAssembly(Assembly asm, Predicate<Type> filter = null)
        {
            _contextDiscoverer.DiscoverClasses(asm, filter);
            _contextDiscoverer.DiscoverGlobalContexts(asm, filter);
        }

        public void AttachExternalAssembly(Assembly asm)
        {
            _contextDiscoverer.DiscoverClasses(asm);
            _contextDiscoverer.DiscoverGlobalContexts(asm);
        }

        public void Initialize()
        {
            EnableCodeStatistics();

            _attachedScriptsFactory = new AttachedScriptsFactory(this);
            AttachedScriptsFactory.SetInstance(_attachedScriptsFactory);
        }

        public ScriptSourceFactory Loader { get; }

        public ICompilerFrontend GetCompilerService()
        {
            using var scope = Services.CreateScope();
            var compiler = scope.Resolve<CompilerFrontend>();
            compiler.SharedSymbols = _runtimeEnvironment.GetSymbolTable();
            
            switch (System.Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    compiler.PreprocessorDefinitions.Add("Linux");
                    break;
                case PlatformID.MacOSX:
                    compiler.PreprocessorDefinitions.Add("MacOS");
                    break;
                case PlatformID.Win32NT:
                    compiler.PreprocessorDefinitions.Add("Windows");
                    break;
            }
            
            compiler.GenerateDebugCode = ProduceExtraCode.HasFlag(CodeGenerationFlags.DebugCode);
            compiler.GenerateCodeStat = ProduceExtraCode.HasFlag(CodeGenerationFlags.CodeStatistics);
            return compiler;
        }
        
        public static IRuntimeContextInstance NewObject(IExecutableModule module, ExternalContextData externalContext = null)
        {
            var scriptContext = CreateUninitializedSDO(module, externalContext);
            InitializeSDO(scriptContext);

            return scriptContext;
        }

        public static ScriptDrivenObject CreateUninitializedSDO(IExecutableModule module, ExternalContextData externalContext = null)
        {
            var scriptContext = new UserScriptContextInstance(module, true);
            if (externalContext != null)
            {
                foreach (var item in externalContext)
                {
                    scriptContext.AddProperty(item.Key, item.Value);
                }
            }

            scriptContext.InitOwnData();
            return scriptContext;
        }

        public static void InitializeSDO(ScriptDrivenObject sdo)
        {
            sdo.Initialize();
        }

        public static Task InitializeSDOAsync(ScriptDrivenObject sdo) => sdo.InitializeAsync();

        public AttachedScriptsFactory AttachedScriptsFactory => _attachedScriptsFactory;

        public IDebugController DebugController
        {
            get => _debugController;
            private set
            {
                _debugController = value;
                if (value != null)
                {
                    ProduceExtraCode |= CodeGenerationFlags.DebugCode;
                    MachineInstance.Current.SetDebugMode(_debugController.BreakpointManager);
                }
            }
        }

        private void EnableCodeStatistics()
        {
            var collector = Services.TryResolve<ICodeStatCollector>();
            if (collector == default)
                return;
            
            ProduceExtraCode |= CodeGenerationFlags.CodeStatistics;
        }

        #region IDisposable Members

        public void Dispose()
        {
            DebugController?.Dispose();
            AttachedScriptsFactory.SetInstance(null);
        }

        #endregion
    }
}
