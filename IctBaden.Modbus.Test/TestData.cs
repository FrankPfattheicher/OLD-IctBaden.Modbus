using System;
using System.Diagnostics;
using System.Text;

namespace IctBaden.Modbus.Test
{
    public class TestData : IDataAccess
    {
        private readonly bool[] _coils = new bool[100];
        private readonly ushort[] _registers = new ushort[100];

        public bool IsConnected => true;
        public void ReConnect() { }


        private void TraceCoilData(string context)
        {
            var text = new StringBuilder();
            text.Append($"{context} - Coils: ");
            foreach (var coil in _coils)
            {
                text.Append($"{(coil ? 1 : 0)}, ");
            }
            Trace.TraceInformation(text.ToString());
        }
        
        
        public bool[] ReadInputDiscretes(int offset, int count)
        {
            var data = new bool[count];
            Array.Copy(_coils, offset, data, 0, count);
            for (var ix = 0; ix < count; ix++)
            {
                data[ix] = ((offset + ix) & 1) != 0;
            }
            return data;
        }

        public bool[] ReadCoils(int offset, int count)
        {
            var data = new bool[count];
            Array.Copy(_coils, offset, data, 0, count);
            TraceCoilData("Read");
            return data;
        }

        public bool WriteCoils(int offset, bool[] values)
        {
            Array.Copy(values, 0, _coils, offset, values.Length);
            TraceCoilData("Write");
            return true;
        }

        public ushort[] ReadInputRegisters(int offset, int count)
        {
            var data = new ushort[count];
            Array.Copy(_registers, offset, data, 0, count);
            for (var ix = 0; ix < count; ix++)
            {
                data[ix] = (ushort)((offset + ix) >> 1);
            }
            return data;
        }

        public ushort[] ReadHoldingRegisters(int offset, int count)
        {
            var data = new ushort[count];
            Array.Copy(_registers, offset, data, 0, count);
            return data;
        }

        public bool WriteRegisters(int offset, ushort[] values)
        {
            Array.Copy(values, 0, _registers, offset, values.Length);
            return true;
        }
    }
}