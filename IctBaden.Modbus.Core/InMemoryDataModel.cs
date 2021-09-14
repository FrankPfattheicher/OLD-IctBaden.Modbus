using System.Linq;
using IctBaden.Modbus.Core;
using Microsoft.Extensions.Logging;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace IctBaden.Modbus
{
    /// <summary>
    /// The Modbus Data model defines four tables as follows
    ///
    /// | Primary tables    | Object type | Type of    | Comments
    /// |-------------------|-------------|------------|-------------------------
    /// | Discretes Input   | Single bit  | Read-Only  | This type of data can be provided by an I/O system
    /// | Coils             | Single bit  | Read-Write | This type of data can be alterable by an application program
    /// | Input Registers   | 16-bit word | Read-Only  | This type of data can be provided by an I/O system
    /// | Holding Registers | 16-bit word | Read-Write | This type of data can be alterable by an application program
    /// 
    /// </summary>
    public class InMemoryDataModel : IDataAccess
    {
        private readonly ILogger _logger;
        private readonly InMemoryDataBlock<bool> _inputDiscretes;
        private readonly InMemoryDataBlock<bool> _coils;
        private readonly InMemoryDataBlock<ushort> _inputRegisters;
        private readonly InMemoryDataBlock<ushort> _holdingRegisters;
        private readonly InMemoryDataBlock<ushort> _unifiedRegisters;

        /// <summary>
        /// Creates a data model specifying
        /// how the data should be stored in the memory 
        /// </summary>
        /// <param name="logger">Optional provided logger for trace messages</param>
        /// <param name="model">Data model specification to be used</param>
        public InMemoryDataModel(ILogger logger, DataModel model)
        {
            _logger = logger;

            if (model is UnifiedDataModel unified)
            {
                _unifiedRegisters = new InMemoryDataBlock<ushort>(unified.Registers.Offset, unified.Registers.Size);
            }
            else if (model is SeparateDataModel separate)
            {
                _inputDiscretes =
                    new InMemoryDataBlock<bool>(separate.InputDiscretes.Offset, separate.InputDiscretes.Size);
                _coils = new InMemoryDataBlock<bool>(separate.Coils.Offset, separate.Coils.Size);
                _inputRegisters =
                    new InMemoryDataBlock<ushort>(separate.InputRegisters.Offset, separate.InputRegisters.Size);
                _holdingRegisters =
                    new InMemoryDataBlock<ushort>(separate.HoldingRegisters.Offset, separate.HoldingRegisters.Size);
            }
        }


        public bool IsConnected => true;

        public void ReConnect()
        {
        }

        public bool[] ReadCoils(ushort offset, ushort count)
        {
            _logger?.LogTrace($"ReadCoils({offset}, {count})");
            var coils = _unifiedRegisters != null
                ? Enumerable.Range(offset, count)
                    .Select(ix => (_unifiedRegisters[(ushort)(ix / 16)] & (ushort)(1 << (ix % 16))) != 0u)
                    .ToArray()
                : Enumerable.Range(offset, count)
                    .Select(ix => _coils[(ushort)ix])
                    .ToArray();
            return coils;
        }

        public bool[] ReadInputDiscretes(ushort offset, ushort count)
        {
            _logger?.LogTrace($"ReadInputDiscretes({offset}, {count})");
            var inputs = _unifiedRegisters != null
                ? Enumerable.Range(offset, count)
                    .Select(ix => (_unifiedRegisters[(ushort)(ix / 16)] & (ushort)(1 << (ix % 16))) != 0u)
                    .ToArray()
                : Enumerable.Range(offset, count)
                    .Select(ix => _inputDiscretes[(ushort)ix])
                    .ToArray();
            return inputs;
        }

        public ushort[] ReadHoldingRegisters(ushort offset, ushort count)
        {
            _logger?.LogTrace($"ReadHoldingRegisters({offset}, {count})");
            var registers = _unifiedRegisters != null
                ? Enumerable.Range(offset, count)
                    .Select(ix => _unifiedRegisters[(ushort)ix])
                    .ToArray()
                : Enumerable.Range(offset, count)
                    .Select(ix => _holdingRegisters[(ushort)ix])
                    .ToArray();
            return registers;
        }

        public ushort[] ReadInputRegisters(ushort offset, ushort count)
        {
            _logger?.LogTrace($"ReadInputRegisters({offset}, {count})");
            var registers = _unifiedRegisters != null
                ? Enumerable.Range(offset, count)
                    .Select(ix => _unifiedRegisters[(ushort)ix])
                    .ToArray()
                : Enumerable.Range(offset, count)
                    .Select(ix => _inputRegisters[(ushort)ix])
                    .ToArray();
            return registers;
        }

        public bool WriteCoils(ushort offset, bool[] values)
        {
            _logger?.LogTrace($"WriteCoils({offset})");
            foreach (var i in Enumerable.Range(0, values.Length))
            {
                var ix = (ushort)i;
                if (_unifiedRegisters != null)
                {
                    var mask = (ushort)(1 << ((offset + ix) % 16));
                    if (values[ix])
                    {
                        _unifiedRegisters[(ushort)((ushort)(offset + ix) / 16)] |= mask;
                    }
                    else
                    {
                        _unifiedRegisters[(ushort)((ushort)(offset + ix) / 16)] &= (ushort)~mask;
                    }
                }
                else
                {
                    _coils[(ushort)(offset + ix)] = values[ix];
                }
            }
            return true;
        }

        public bool WriteRegisters(ushort offset, ushort[] values)
        {
            _logger?.LogTrace($"WriteRegisters({offset})");
            foreach (var i in Enumerable.Range(0, values.Length))
            {
                var ix = (ushort)i;
                if (_unifiedRegisters != null)
                {
                    _unifiedRegisters[(ushort)(offset + ix)] = values[ix];
                }
                else
                {
                    _holdingRegisters[(ushort)(offset + ix)] = values[ix];
                }
            }
            return true;
        }
    }
}