/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System.Collections.Generic;
using OneScript.Commons;
using OneScript.Contexts;
using ScriptEngine.Machine.Contexts;

namespace ScriptEngine
{
    public class LibraryManager : ILibraryManager
    {
        private readonly IRuntimeEnvironment _runtimeEnvironment;

        private readonly List<ExternalLibraryDef> _externalLibs = new List<ExternalLibraryDef>();

        public LibraryManager(IRuntimeEnvironment runtimeEnvironment)
        {
            _runtimeEnvironment = runtimeEnvironment;
        }

        public IEnumerable<ExternalLibraryDef> GetLibraries()
        {
            return _externalLibs.ToArray();
        }

        public void InitExternalLibrary(ExternalLibraryDef library)
        {
            var loadedObjects = new ScriptDrivenObject[library.Modules.Count];
            int i = 0;
            foreach (var module in library.Modules)
            {
                var instance = ScriptingEngine.CreateUninitializedSDO(module.Module);

                _runtimeEnvironment.SetGlobalProperty(module.Symbol, instance);
                loadedObjects[i++] = instance;
            }

            _externalLibs.Add(library);
            loadedObjects.ForEach(ScriptingEngine.InitializeSDO);
        }
    }
}
