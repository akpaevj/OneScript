namespace ScriptEngine.Debugging
{
    public class MachineStoppedEventArgs
    {
        public int ThreadId { get; private set; }
        public MachineStoppingReason Reason { get; private set; }
        public string Details { get; private set; }

        public MachineStoppedEventArgs(int threadId, MachineStoppingReason reason, string details = "")
        {
            ThreadId = threadId;
            Reason = reason;
            Details = details;
        }
    }
}
