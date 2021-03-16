namespace IctBaden.Modbus.SampleDevice.ViewModels
{
    public class InputVm
    {
        public int Number { get; set; }
        public bool Value { get; set; }

        public override string ToString() => $"{Number}: {Value}";
    }
}