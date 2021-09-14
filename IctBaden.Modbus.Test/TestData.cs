using System;
using System.Diagnostics;

namespace IctBaden.Modbus.Test
{
    public class TestData : IDataAccess
    {
        private readonly bool[] _coils = new bool[1024];
        private readonly ushort[] _registers = new ushort[1024];

        public bool IsConnected => true;
        public void ReConnect() { }


        public bool[] ReadInputDiscretes(ushort offset, ushort count)
        {
            Trace.TraceInformation($"ReadInputDiscretes({offset}, {count})");
            offset %= 10000;
            var data = new bool[count];
            try
            {
                Array.Copy(_coils, offset, data, 0, count);
                for (var ix = 0; ix < count; ix++)
                {
                    data[ix] = ((offset + ix) & 1) != 0;
                }
            }
            catch
            {
                // ignore
            }
            return data;
        }

        public bool[] ReadCoils(ushort offset, ushort count)
        {
            Trace.TraceInformation($"ReadCoils({offset}, {count})");
            offset %= 10000;
            var data = new bool[count];
            try
            {
                Array.Copy(_coils, offset, data, 0, count);
            }
            catch
            {
                // ignore
            }
            return data;
        }

        public bool WriteCoils(ushort offset, bool[] values)
        {
            Trace.TraceInformation($"WriteCoils({offset}, {values.Length})");
            offset %= 10000;
            try
            {
                Array.Copy(values, 0, _coils, offset, values.Length);
                return true;
            }
            catch
            {
                // ignore
            }
            return false;
        }

        public ushort[] ReadInputRegisters(ushort offset, ushort count)
        {
            Trace.TraceInformation($"ReadInputRegisters({offset}, {count})");
            offset %= 10000;
            var data = new ushort[count];
            try
            {
                Array.Copy(_registers, offset, data, 0, count);
            }
            catch
            {
                // ignore
            }
            return data;
        }

        public ushort[] ReadHoldingRegisters(ushort offset, ushort count)
        {
            Trace.TraceInformation($"ReadHoldingRegisters({offset}, {count})");
            offset %= 10000;
            var data = new ushort[count];
            try
            {
                Array.Copy(_registers, offset, data, 0, count);
            }
            catch
            {
                // ignore
            }
            return data;
        }

        public bool WriteRegisters(ushort offset, ushort[] values)
        {
            Trace.TraceInformation($"WriteRegisters({offset}, {values.Length})");
            offset %= 10000;
            try
            {
                Array.Copy(values, 0, _registers, offset, values.Length);
                return true;
            }
            catch
            {
                // ignore
            }
            return false;
        }
    }
}
