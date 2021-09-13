namespace IctBaden.Modbus
{
    public record DataBlock
    {
        public ushort Offset { get; set; }
        public ushort Size { get; set; }
    }
}