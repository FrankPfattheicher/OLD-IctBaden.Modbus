using System;
using System.Diagnostics;
using System.Linq;
using IctBaden.Framework.AppUtils;
using IctBaden.Framework.Tron;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace IctBaden.Modbus.Test
{
    public class MasterSlaveConnectionTests : IDisposable
    {
        private static ushort _port = (ushort)((SystemInfo.Platform == Platform.Windows) ? 502 : 1502); 
        private readonly TestData _source;
        private ModbusMaster _master;
        private ModbusSlave _slave;
        private ConnectedSlave _client;

        public MasterSlaveConnectionTests()
        {
            Trace.Listeners.Add(new TronTraceListener(true));
            _source = new TestData();
            _port++;
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Disconnect();
                _client.Dispose();
                _client = null;
            }

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
        public void MasterShouldBeAbleToConnectSlave()
        {
            _slave = new ModbusSlave("Test", _source, _port, 1);
            Assert.NotNull(_slave);
            _slave.Start();

            _master = new ModbusMaster();
            Assert.NotNull(_master);

            var device = _master.ConnectDevice("localhost", _port, 1);
            Assert.NotNull(device);
            Assert.True(device.IsConnected);
            Assert.True(_master.ConnectedSlaveDevices.First() == device);

            device.Dispose();
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
        public void AfterConnectingDeviceMasterShouldHaveItInList()
        {
            ConnectMasterAndSlave();
            
            Assert.True(_master.ConnectedSlaveDevices.First() == _client);
            AssertWait.Max(1000, () => _slave.IsConnected);
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
            
            _source.WriteRegisters(0, new ushort[] { 0x0000, 0x1111, 0x2222, 0x3333, 0x4444, 0x5555, 0x6666, 0x7777 });

            var result = _client.ReadInputRegisters(4, 1);
            Assert.Equal(0x4444, result[0]);

            result = _client.ReadInputRegisters(7, 1);
            Assert.Equal(0x7777, result[0]);

            result = _client.ReadInputRegisters(3, 4);
            Assert.Equal(new ushort[] { 0x3333, 0x4444, 0x5555, 0x6666 }, result);
        }

        [Fact]
        public void DisconnectClientShouldBeRemovedFromMaster()
        {
            ConnectMasterAndSlave();
            AssertWait.Max(1000, () => _slave.IsConnected);
            
            _client.Disconnect();
            
            AssertWait.Max(10000, () => _master.ConnectedSlaveDevices.Count == 0);
        }

        
        [Fact]
        public void TerminatingSlaveShouldBeRemovedFromMaster()
        {
            ConnectMasterAndSlave();
            AssertWait.Max(1000, () => _slave.IsConnected);
            
            _slave.Terminate();

            _client.ReadInputRegisters(0, 1);
            Assert.True(_master.ConnectedSlaveDevices.Count == 0);
        }

        
        [Fact]
        public void TransferCoils()
        {
            ConnectMasterAndSlave();

            var result1 = _client.ReadCoils(7, 1);
            Assert.False(result1[0]);

            _source.WriteCoils(7, new[] { true });
            var result2 = _client.ReadCoils(7, 1);
            Assert.True(result2[0]);

            _source.WriteCoils(7, new[] { false });
            var result3 = _client.ReadCoils(7, 1);
            Assert.False(result3[0]);
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
