using Google.Protobuf.WellKnownTypes;
using OneScript.Commons;
using ScriptEngine.Debugging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ScriptEngine.Machine
{
    public static class MachineInstancesManager
    {
        private static readonly ConcurrentDictionary<int, MachineInstance> _machineInstances = new();

        public static event EventHandler<MachineContextEventArgs> MachineInstanceCreated;
        public static event EventHandler<MachineContextEventArgs> MachineInstanceRun;
        public static event EventHandler<MachineContextEventArgs> MachineInstanceFinished;

        public static MachineInstance MainInstance
        {
            get
            {
                if (_machineInstances.IsEmpty)
                    return GetCurrentThreadInstance();
                else
                    return _machineInstances.FirstOrDefault().Value;
            }
        }

        public static MachineInstance GetCurrentThreadInstance()
            => GetThreadInstance(Environment.CurrentManagedThreadId);

        public static MachineInstance GetThreadInstance(int threadId)
        {
            if (!_machineInstances.TryGetValue(threadId, out var value))
            {
                value = new MachineInstance();
                value.CommandsFlowRun += MachineInstanceCommandsFlowRun;
                value.CommandsFlowFinished += MachineInstanceCommandsFlowFinished;

                if (!_machineInstances.TryAdd(threadId, value))
                    throw new Exception("Failed to instantiate new MachineInstance");

                MachineInstanceCreated?.Invoke(value, new(threadId));
            }

            return value;
        }

        private static void MachineInstanceCommandsFlowRun(object sender, MachineContextEventArgs e)
        {
            MachineInstanceRun?.Invoke(sender, new(e.ThreadId));
        }

        private static void MachineInstanceCommandsFlowFinished(object sender, MachineContextEventArgs e)
        {
            ((MachineInstance)sender).CommandsFlowRun -= MachineInstanceCommandsFlowRun;
            ((MachineInstance)sender).CommandsFlowFinished -= MachineInstanceCommandsFlowFinished;

            _machineInstances.Remove(e.ThreadId, out var _);
            MachineInstanceFinished?.Invoke(sender, new(e.ThreadId));
        }

        public static IReadOnlyList<MachineInstance> GetInstances()
            => _machineInstances.Values.ToList();

        public static IReadOnlyList<int> GetThreadIdentifiers()
            => _machineInstances.Keys.ToList();
    }
}
