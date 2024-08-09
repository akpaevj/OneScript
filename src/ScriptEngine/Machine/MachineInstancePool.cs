/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using OneScript.DependencyInjection;
using System;
using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using OneScript.DebugProtocol.Grpc;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ScriptEngine.Machine
{
    public class MachineInstancePool : IMachineInstancePool
    {
        private readonly IServiceContainer _services;
        private readonly ConcurrentDictionary<int, MachineInstance> _instances = new ConcurrentDictionary<int, MachineInstance>();

        public MachineInstancePool(IServiceContainer services)
        {
            _services = services;
        }

        public MachineInstance GetCurrentThreadInstance()
        {
            var threadId = Environment.CurrentManagedThreadId;
            return GetInstance(threadId);
        }

        public MachineInstance GetInstance(int threadId)
        {
            _instances.TryAdd(threadId, new MachineInstance(_services));

            if (_instances.TryGetValue(threadId, out var instance))
                return instance;
            else
                throw new Exception($"Failed to get machine instance for the thread id {threadId}");
        }
    }
}
