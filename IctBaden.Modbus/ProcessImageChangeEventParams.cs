namespace IctBaden.Modbus
{
    public class ProcessImageChangeEventParams
    {
        public readonly int Offset;

        public readonly ushort OldValue;
        public readonly ushort NewValue;

        public ProcessImageChangeEventParams(int offset, ushort oldValue, ushort newValue)
        {
            Offset = offset;
            NewValue = newValue;
            OldValue = oldValue;
        }
    }
}