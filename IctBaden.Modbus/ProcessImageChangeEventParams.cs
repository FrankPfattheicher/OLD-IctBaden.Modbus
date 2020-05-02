namespace IctBaden.Modbus
{
    public class ProcessImageChangeEventParams
    {
        public readonly string PollContext;
        public readonly int Offset;

        public readonly ushort OldValue;
        public readonly ushort NewValue;

        public ProcessImageChangeEventParams(string pollContext, int offset, ushort oldValue, ushort newValue)
        {
            PollContext = pollContext;
            Offset = offset;
            NewValue = newValue;
            OldValue = oldValue;
        }
    }
}