using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable StringLiteralTypo

namespace IctBaden.Modbus.Core
{
    public static class ModbusValue
    {
        public const string NotANumber = "NaN";

        public static ushort GetSize(ModbusDataType dataType)
        {
            switch (dataType)
            {
                case ModbusDataType.U16:
                case ModbusDataType.S16:
                    return 1;
                case ModbusDataType.S32:
                case ModbusDataType.STR32:
                case ModbusDataType.U32:
                    return 2;
                case ModbusDataType.U64:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }

        public static bool IsNaN(ModbusDataType dataType, ushort[] data)
        {
            if (data == null) return true;
            switch (dataType)
            {
                case ModbusDataType.S16:
                    return data[0] == 0x8000;
                case ModbusDataType.S32:
                    return data[0] == 0x8000 && data[1] == 0x0000;
                case ModbusDataType.STR32:
                    return false;
                case ModbusDataType.U16:
                    return data[0] == 0xFFFF;
                case ModbusDataType.U32:
                    return data[0] == 0xFFFF && data[1] == 0xFFFF;
                case ModbusDataType.U64:
                    return data[0] == 0xFFFF && data[1] == 0xFFFF && data[2] == 0xFFFF && data[3] == 0xFFFF;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }

        public static string Format(ushort[] data, ModbusDataType dataType, ModbusDataFormat dataFormat, Dictionary<string, object> enumValues)
        {
            if (IsNaN(dataType, data)) return NotANumber;

            object value = GetValue(data, dataType);

            switch (dataFormat)
            {
                case ModbusDataFormat.ENUM:
                    var e = enumValues
                        .FirstOrDefault(v => v.Value?.ToString() == value.ToString());
                    return e.Key != null
                        ? $"{e.Value}: {e.Key}"
                        : value.ToString();
                case ModbusDataFormat.TAGLIST:
                    return "TAGLIST";
                case ModbusDataFormat.FIX0:
                    return value.ToString();
                case ModbusDataFormat.FIX1:
                    return $"{Convert.ToDouble(value) / 10.0}";
                case ModbusDataFormat.FIX2:
                    return $"{Convert.ToDouble(value) / 100.0}";
                case ModbusDataFormat.FIX3:
                    return $"{Convert.ToDouble(value) / 1000.0}";
                case ModbusDataFormat.FIX4:
                    return $"{Convert.ToDouble(value) / 10000.0}";
                case ModbusDataFormat.FUNKTION_SEC:
                    return "FUNKTION_SEC";
                case ModbusDataFormat.FW:
                    return $"Version {value}";
                case ModbusDataFormat.HW:
                    return $"Version {value}";
                case ModbusDataFormat.IP4:
                    var bytes = new byte[data.Length * 2];
                    Buffer.BlockCopy(data, 0, bytes, 0, data.Length * 2);
                    return $"{new IPAddress(bytes)}";
                case ModbusDataFormat.RAW:
                    return value.ToString();
                case ModbusDataFormat.REV:
                    return value.ToString();
                case ModbusDataFormat.TEMP:
                    return $"{Convert.ToInt64(value) / 10}";
                case ModbusDataFormat.TM:
                    return "TM";
                case ModbusDataFormat.UTF8:
                    return $"{Convert.ToChar(value)}";
                case ModbusDataFormat.DT:
                    var dt = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value));
                    return $"{dt:G}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataFormat), dataFormat, null);
            }
        }

        public static object GetValue(ushort[] data, ModbusDataType dataType)
        {
            if (IsNaN(dataType, data)) return null;

            object value;
            switch (dataType)
            {
                case ModbusDataType.S16:
                    value = (int)data[0];
                    break;
                case ModbusDataType.S32:
                    value = data[0] * 0x10000 + data[1];
                    break;
                case ModbusDataType.STR32:
                    value = (char)data[0];
                    break;
                case ModbusDataType.U16:
                    value = (uint)data[0];
                    break;
                case ModbusDataType.U32:
                    value = data[0] * 0x10000u + data[1];
                    break;
                case ModbusDataType.U64:
                    value = data[0] * 0x1000000000000u + data[1] * 0x100000000u + data[2] * 0x1000u + data[3];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }

            return value;
        }
        
        public static ushort[] GetData(string value, ModbusDataType dataType, ModbusDataFormat dataFormat)
        {
            var size = GetSize(dataType);
            var data = new ushort[size];

            if (double.TryParse(value, out var doubleValue))
            {
                if (dataFormat == ModbusDataFormat.FIX1)
                {
                    doubleValue *= 10.0;
                }
                else if (dataFormat == ModbusDataFormat.FIX2)
                {
                    doubleValue *= 100.0;
                }
                else if (dataFormat == ModbusDataFormat.FIX3)
                {
                    doubleValue *= 1000.0;
                }
                else if (dataFormat == ModbusDataFormat.FIX4)
                {
                    doubleValue *= 10000.0;
                }
            }

            if (dataType is ModbusDataType.S16 or ModbusDataType.S32)
            {
                var dataValue = (long)doubleValue;
                switch (size)
                {
                    case 1:
                        data[0] = (ushort)(dataValue & 0xFFFF);
                        break;
                    case 2:
                        data[0] = (ushort)(dataValue >> 16);
                        data[1] = (ushort)(dataValue & 0xFFFF);
                        break;
                }
            }
            else if (dataType is ModbusDataType.U16 or ModbusDataType.U32 or ModbusDataType.U64)
            {
                var dataValue = (ulong)doubleValue;
                switch (size)
                {
                    case 1:
                        data[0] = (ushort)dataValue;
                        break;
                    case 2:
                        data[0] = (ushort)(dataValue >> 16);
                        data[1] = (ushort)(dataValue & 0xFFFF);
                        break;
                    case 4:
                        data[0] = (ushort)(dataValue >> 48 & 0xFFFF);
                        data[1] = (ushort)(dataValue >> 32 & 0xFFFF);
                        data[2] = (ushort)(dataValue >> 16 & 0xFFFF);
                        data[3] = (ushort)(dataValue & 0xFFFF);
                        break;
                }
            }

            return data;
        }
    }
}
