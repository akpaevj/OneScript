using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OneScript.Commons;
using OneScript.DebugServices.Grpc;
using ScriptEngine.Machine;

namespace OneScript.DebugServices
{
    public class OscriptDebugServer : OscriptDebug.OscriptDebugBase
    {
        private readonly IBreakpointManager _breakpointManager;

        public OscriptDebugServer(IBreakpointManager breakpointManager)
        {
            _breakpointManager = breakpointManager;
        }

        public override Task<Empty> StartDebug(Empty request, ServerCallContext context)
        {
            MachineInstancesManager.GetAllInstances().ForEach(c => c.EnableDebugMode(_breakpointManager));
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> StopDebug(OsStopDebugRequest request, ServerCallContext context)
        {
            MachineInstancesManager.GetAllInstances().ForEach(c => c.DisableDebugMode());
            return Task.FromResult(new Empty());
        }


    }
}
