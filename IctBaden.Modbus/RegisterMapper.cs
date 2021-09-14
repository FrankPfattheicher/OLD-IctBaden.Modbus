using System;
using System.Reflection;
using IctBaden.Framework.Types;
using IctBaden.Modbus.Core;

namespace IctBaden.Modbus
{
    public static class RegisterMapper
    {
        public static void FromType(IDataAccess registers, ushort offset, object data)
        {
            var fields = data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (var info in fields)
            {
                var modbusAttribute = info.GetCustomAttribute<ModbusDataAttribute>();
                if (modbusAttribute == null) continue;

                var value = info.FieldType.IsEnum
                    ? Convert.ToInt32(info.GetValue(data)).ToString()
                    : info.GetValue(data)?.ToString() ?? "";

                var registerValues = ModbusValue.GetData(value, modbusAttribute.Type, modbusAttribute.Format);
                registers.WriteRegisters(offset, registerValues);
                offset += (ushort)registerValues.Length;
            }
        }
        
        public static TData ToType<TData>(IDataAccess registers, ushort offset)
        {
            var data = Activator.CreateInstance<TData>();
            var fields = data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (var info in fields)
            {
                var modbusAttribute = info.GetCustomAttribute<ModbusDataAttribute>();
                if (modbusAttribute == null) continue;

                var size = ModbusValue.GetSize(modbusAttribute.Type);
                var registerValues = registers.ReadHoldingRegisters(offset, size);

                var value = UniversalConverter.ConvertToType(ModbusValue.GetValue(registerValues, modbusAttribute.Type), info.FieldType);
                info.SetValue(data, value);
                
                offset += (ushort)registerValues.Length;
            }

            return data;
        }

        
    }
}