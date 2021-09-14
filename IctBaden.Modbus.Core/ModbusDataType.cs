// ReSharper disable InconsistentNaming
namespace IctBaden.Modbus.Core
{
    public enum ModbusDataType
    {
        // SMA
        S16,
        S32,
        STR32,
        U16,
        U32,
        U64,
        
        // general
        FLOAT,
        DOUBLE

        
        // public static ModbusDataType S16 = new("S16", 1, "0x8000");
        // public static ModbusDataType S32 = new("S32", 2, "0x8000_0000");
        // public static ModbusDataType STR32 = new("STR32", 2, null);
        // public static ModbusDataType U16 = new("U16", 1, "0xFFFF");
        // public static ModbusDataType U32 = new("U32", 2, "0xFFFF_FFFF");
        // public static ModbusDataType U64 = new("U64", 4, "0xFFFF_FFFF_FFFF_FFFF");
        
    }
}