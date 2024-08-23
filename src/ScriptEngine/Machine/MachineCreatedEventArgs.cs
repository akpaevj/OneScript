namespace ScriptEngine.Machine
{
    public class MachineCreatedEventArgs
    {
        public int ThreadId { get; private set; }

        public MachineCreatedEventArgs(int threadId)
        {
            ThreadId = threadId;
        }
    }
}
