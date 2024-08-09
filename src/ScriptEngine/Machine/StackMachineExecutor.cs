/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using OneScript.Contexts;
using OneScript.Execution;
using OneScript.Values;

namespace ScriptEngine.Machine
{
    public class StackMachineExecutor : IExecutorProvider
    {
        private readonly IMachineInstancePool _machineInstancePool;

        public StackMachineExecutor(IMachineInstancePool machineInstancePool)
        {
            _machineInstancePool = machineInstancePool;
        }
        
        public Type SupportedModuleType => typeof(StackRuntimeModule);
        
        public Invoker GetInvokeDelegate()
        {
            return Executor;
        }

        private BslValue Executor(BslObjectValue target, IExecutableModule module, BslMethodInfo method, IValue[] arguments)
        {
            if (!(method is MachineMethodInfo scriptMethodInfo))
            {
                throw new InvalidOperationException();
            }
            
            if (!(target is IRunnable runnable))
            {
                throw new InvalidOperationException();
            }
            
            return (BslValue)_machineInstancePool.GetCurrentThreadInstance().ExecuteMethod(runnable, scriptMethodInfo, arguments);
        }
    }
}