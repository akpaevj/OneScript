namespace ScriptEngine.Debugging
{
    public class MachineContextEventArgs
    {
        public int ThreadId { get; private set; }

        public MachineContextEventArgs(int threadId)
        {
            ThreadId = threadId;
        }
    }
}
