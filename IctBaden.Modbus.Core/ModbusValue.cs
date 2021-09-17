using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using IctBaden.Framework.Types;

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
                case ModbusDataType.FLOAT:
                    return 2;
                case ModbusDataType.U64:
                case ModbusDataType.DOUBLE:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }

        public static bool IsNaN(ushort[] data, ModbusDataType dataType, ModbusDataFormat dataFormat)
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
                    return dataFormat == ModbusDataFormat.ENUM 
                        ? data[0] == 0x00FF && data[1] == 0xFFFD
                        : data[0] == 0xFFFF && data[1] == 0xFFFF;
                case ModbusDataType.U64:
                    return data[0] == 0xFFFF && data[1] == 0xFFFF && data[2] == 0xFFFF && data[3] == 0xFFFF;
                case ModbusDataType.FLOAT:
                    return false;
                case ModbusDataType.DOUBLE:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }

        public static string Format(ushort[] data, ModbusDataType dataType, ModbusDataFormat dataFormat,
            Dictionary<string, object> enumValues)
        {
            if (IsNaN(data, dataType, dataFormat)) return NotANumber;

            object value = GetValue(data, dataType, dataFormat);

            switch (dataFormat)
            {
                case ModbusDataFormat.ENUM:
                    var e = enumValues
                        .FirstOrDefault(v => v.Value?.ToString() == value.ToString());
                    return e.Key ?? $"{value}  0x{value:X8}";
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
                case ModbusDataFormat.FLAGS:
                    return Enumerable.Range(0, data.Length * 16)
                        .Select(ix => (UniversalConverter.ConvertTo<ulong>(value) & ((ulong)1 << ix)) != 0)
                        .Select(bit => bit ? "1 " : "0 ")
                        .Aggregate((s, b) => s + b);
                case ModbusDataFormat.FLOAT:
                    return $"{Convert.ToSingle(value)}";
                case ModbusDataFormat.DOUBLE:
                    return $"{Convert.ToDouble(value)}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataFormat), dataFormat, null);
            }
        }

        public static object GetValue(ushort[] data, ModbusDataType dataType, ModbusDataFormat dataFormat)
        {
            if (IsNaN(data, dataType, dataFormat)) return null;

            object value;
            byte[] bytes;
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
                case ModbusDataType.FLOAT:
                    bytes = new[]
                    {
                        (byte)(data[0] >> 8),
                        (byte)(data[0] & 0xFF),
                        (byte)(data[1] >> 8),
                        (byte)(data[1] & 0xFF)
                    };
                    value = BitConverter.ToSingle(bytes);
                    break;
                case ModbusDataType.DOUBLE:
                    bytes = new[]
                    {
                        (byte)(data[0] >> 8),
                        (byte)(data[0] & 0xFF),
                        (byte)(data[1] >> 8),
                        (byte)(data[1] & 0xFF),
                        (byte)(data[2] >> 8),
                        (byte)(data[2] & 0xFF),
                        (byte)(data[3] >> 8),
                        (byte)(data[3] & 0xFF)
                    };
                    value = BitConverter.ToDouble(bytes);
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
            else if (dataType is ModbusDataType.FLOAT && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                var bytes = BitConverter.GetBytes(floatValue);
                data[0] = (ushort)((ushort)(bytes[0] << 8) + bytes[1]);
                data[1] = (ushort)((ushort)(bytes[2] << 8) + bytes[3]);
            }
            else if (dataType is ModbusDataType.DOUBLE && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblValue))
            {
                var bytes = BitConverter.GetBytes(dblValue);
                data[0] = (ushort)((ushort)(bytes[0] << 8) + bytes[1]);
                data[1] = (ushort)((ushort)(bytes[2] << 8) + bytes[3]);
                data[2] = (ushort)((ushort)(bytes[4] << 8) + bytes[5]);
                data[3] = (ushort)((ushort)(bytes[6] << 8) + bytes[7]);
            }

            return data;
        }
    }
}