namespace IctBaden.Modbus.SampleDevice.ViewModels
{
    public class RegisterVm
    {
        public int Number { get; set; }
        public ushort Value { get; set; }

        public override string ToString() => $"{Number}: {Value}";
    }
}