namespace IctBaden.Modbus
{
    public class DataModel
    {
    }

    public class UnifiedDataModel : DataModel
    {
        public DataBlock Registers { get; set; }
    }
    
    public class SeparateDataModel : DataModel
    {
        public DataBlock InputDiscretes { get; set; }
        public DataBlock Coils { get; set; }
        public DataBlock InputRegisters { get; set; }
        public DataBlock HoldingRegisters { get; set; }
    }
}
