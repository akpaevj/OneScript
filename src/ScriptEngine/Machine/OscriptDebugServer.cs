/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OneScript.DebugProtocol.Grpc;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ScriptEngine.Machine
{
    public class OscriptDebugServer : OscriptDebug.OscriptDebugBase
    {
        private readonly CancellationToken _cancellationToken;
        private readonly Channel<ThreadStatus> _threadStatuses = Channel.CreateUnbounded<ThreadStatus>();

        public delegate void StartDebugHandler();
        public event StartDebugHandler DebugStarted;

        public delegate void StopDebugHandler();
        public event StopDebugHandler DebugStopped;

        public override async Task ThreadsMonitor(Empty request, IServerStreamWriter<ThreadStatus> responseStream, ServerCallContext context)
        {
            while (!_cancellationToken.IsCancellationRequested) 
            {
                try
                {
                    if (await _threadStatuses.Reader.WaitToReadAsync(_cancellationToken))
                    {
                        var status = await _threadStatuses.Reader.ReadAsync(_cancellationToken);
                        await responseStream.WriteAsync(status);
                    }
                }
                catch (TaskCanceledException) { }
            }
        }

        public override Task<Empty> StartDebug(Empty request, ServerCallContext context)
        {
            DebugStarted?.Invoke();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> StopDebug(StopDebugRequest request, ServerCallContext context)
        {
            DebugStopped?.Invoke();
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> Continue(ContinueRequest request, ServerCallContext context)
        {
            return base.Continue(request, context);
        }

        public async Task SendThreadStatus(int threadId, bool stopped = false)
        {
            await _threadStatuses.Writer.WriteAsync(new ThreadStatus()
            {
                ThreadId = threadId,
                ThreadStopped = stopped
            });
        }
    }
}
