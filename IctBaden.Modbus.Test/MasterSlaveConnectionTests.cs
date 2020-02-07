using System;
using System.Diagnostics;
using IctBaden.Framework.AppUtils;
using IctBaden.Framework.Tron;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace IctBaden.Modbus.Test
{
    public class MasterSlaveConnectionTests : IDisposable
    {
        private readonly ushort _port = (ushort) ((SystemInfo.Platform == Platform.Windows) ? 502 : 1502); 
        private readonly TestData _source;
        private ModbusMaster _master;
        private ModbusSlave _slave;
        private ConnectedSlave _client;

        public MasterSlaveConnectionTests()
        {
            Trace.Listeners.Add(new TronTraceListener(true));
            _source = new TestData();
        }

        public void Dispose()
        {
            if (_master != null)
            {
                _master.DisconnectAllDevices();
                _master = null;
            }

            if (_slave != null)
            {
                _slave.Terminate();
                _slave = null;
            }
        }

        private void ConnectMasterAndSlave()
        {
            _slave = new ModbusSlave("Test", _source, _port, 1);
            _slave.Start();

            _master = new ModbusMaster();
            _client = _master.ConnectDevice("localhost", _port, 1);

            AssertWait.Max(2000,() => _client.IsConnected);
        }

        [Fact]
        public void CreateSlaveShouldSucceed()
        {
            _slave = new ModbusSlave("Test", _source, _port, 1);
            Assert.NotNull(_slave);
            _slave.Start();
        }

        [Fact]
        public void CreateMasterShouldSucceed()
        {
            _master = new ModbusMaster();
            Assert.NotNull(_master);
        }

        [Fact]
        public void MasterAndSlaveConnection()
        {
            _slave = new ModbusSlave("Test", _source, _port, 1);
            Assert.NotNull(_slave);
            _slave.Start();

            _master = new ModbusMaster();
            Assert.NotNull(_master);

            var data2 = _master.ConnectDevice("localhost", _port, 1);
            Assert.NotNull(data2);
        }

        [Fact]
        public void ReadDiscreteInput()
        {
            ConnectMasterAndSlave();

            var result = _client.ReadInputDiscretes(4, 1);
            Assert.False(result[0]);

            result = _client.ReadInputDiscretes(7, 1);
            Assert.True(result[0]);

            result = _client.ReadInputDiscretes(7, 4);
            Assert.Equal(new[] { true, false, true, false }, result);
        }

        [Fact]
        public void ReadDiscreteRegister()
        {
            ConnectMasterAndSlave();

            var result = _client.ReadInputRegisters(4, 1);
            Assert.Equal(2, result[0]);

            result = _client.ReadInputRegisters(7, 1);
            Assert.Equal(3, result[0]);

            result = _client.ReadInputRegisters(4, 4);
            Assert.Equal(new ushort[] { 2, 2, 3, 3 }, result);
        }

        [Fact]
        public void TransferCoils()
        {
            ConnectMasterAndSlave();

            var result = _client.ReadCoils(7, 1);
            Assert.False(result[0]);

            _source.WriteCoils(7, new[] { true });
            result = _client.ReadCoils(7, 1);
            Assert.True(result[0]);

            _source.WriteCoils(7, new[] { false });
            result = _client.ReadCoils(7, 1);
            Assert.False(result[0]);
        }

        [Fact]
        public void TransferRegisters()
        {
            ConnectMasterAndSlave();

            var result = _client.ReadHoldingRegisters(7, 1);
            Assert.Equal(0, result[0]);

            _source.WriteRegisters(7, new ushort[] { 0x55AA });
            result = _client.ReadHoldingRegisters(7, 1);
            Assert.Equal(0x55AA, result[0]);

            _source.WriteRegisters(7, new ushort[] { 0xAA55 });
            result = _client.ReadHoldingRegisters(7, 1);
            Assert.Equal(0xAA55, result[0]);

            _source.WriteRegisters(8, new ushort[] { 0x55AA, 0xF0F0, 0x0F0F });
            result = _client.ReadHoldingRegisters(7, 4);
            Assert.Equal(new ushort[] { 0xAA55, 0x55AA, 0xF0F0, 0x0F0F }, result);
        }

    }
}
