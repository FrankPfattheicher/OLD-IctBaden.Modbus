using System;

namespace IctBaden.Modbus.Core
{
    public class ModbusDataAttribute : Attribute
    {
        public readonly ModbusDataType Type;
        public readonly ModbusDataFormat Format;

        public ModbusDataAttribute(ModbusDataType type, ModbusDataFormat format)
        {
            Type = type;
            Format = format;
        }
    }
}