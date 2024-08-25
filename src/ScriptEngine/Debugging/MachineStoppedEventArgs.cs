namespace ScriptEngine.Debugging
{
    public class MachineStoppedEventArgs
    {
        public int ThreadId { get; private set; }
        public MachineStoppingReason Reason { get; private set; }
        public string Details { get; private set; }
        public bool IsLogMessage { get; private set; }

        public MachineStoppedEventArgs(int threadId, MachineStoppingReason reason, string details, bool isLogMessage)
        {
            ThreadId = threadId;
            Reason = reason;
            Details = details;
            IsLogMessage = isLogMessage;
        }
    }
}
