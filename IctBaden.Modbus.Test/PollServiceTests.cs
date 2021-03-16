using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IctBaden.Framework.AppUtils;
using IctBaden.Framework.Tron;
using Xunit;

namespace IctBaden.Modbus.Test
{
    public class PollServiceTests : IDisposable
    {
        private static ushort _port = (SystemInfo.Platform == Platform.Windows) ? 502 : 1502; 
        private readonly TestData _source;
        private ModbusMaster _master;
        private ModbusSlave _slave;
        private ConnectedSlave _client;
        private ModbusDevicePollService _poll;
        private int _connectionChanges;
        private int _processImageChanges;
        private int _inputChanges;
        private int _pollFailed;

        public PollServiceTests()
        {
            Trace.Listeners.Add(new TronTraceListener(true));

            _port++;
            
            _source = new TestData();

            _slave = new ModbusSlave("Test", _source, _port, 1);
            _slave.Start();

            _master = new ModbusMaster();

            _client = _master.ConnectDevice("localhost", _port, 1);
        }

        public void Dispose()
        {
            if (_poll != null)
            {
                _poll.Stop();
                _poll = null;
            }

            if (_client != null)
            {
                _client.Disconnect();
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

            Task.Delay(1000).Wait();
        }

        [Fact]
        public void StartPollServiceShouldSucceed()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Input, 0, 50);
            var started = poll.Start(TimeSpan.FromSeconds(1), false);
            Assert.True(started);
            WaitForStableConnection(poll);
        }

        [Fact]
        public void StartPollServiceShouldBeConnectedWithinOneSecond()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Input, 0, 50);
            poll.Start(TimeSpan.FromSeconds(1), false);

            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            Assert.True(poll.IsConnected);
        }

        [Fact]
        public void PollServiceShouldNotSendEventsIfNothingChanges()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            poll.ProcessImageChanged += (_) => { _processImageChanges++; };
            var started = poll.Start(TimeSpan.FromSeconds(1), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            Task.Delay(TimeSpan.FromSeconds(0.5)).Wait();
            _processImageChanges = 0;
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();

            Assert.True(_processImageChanges == 0);
        }

        private void WaitForStableConnection(ModbusDevicePollService poll)
        {
            for (var wait = 0; wait < 30; wait++)
            {
                if (poll.IsConnectionStable) return;
                Task.Delay(100).Wait();
            }
            Assert.True(false, "Poll service not reach stable connection");
        }
        
        [Fact]
        public void PollServiceShouldSendFirstEventWithinOneSecond()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            poll.ProcessImageChanged += (_) => { _processImageChanges++; };
            var started = poll.Start(TimeSpan.FromSeconds(1), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after a second - change data
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            _processImageChanges = 0;
            _source.WriteRegisters(0, new ushort[] { 0x55AA });

            // after one second - change should be reported
            AssertWait.Max(2000, () => _processImageChanges > 0);
        }

        [Fact]
        public void PollServiceShouldSetDisconnectedIfDeviceVanishes()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after one second - terminate slave
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            _slave.Terminate();

            // after two more seconds - service should not more be connected
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            AssertWait.Max(2000, () => !poll.IsConnected);
        }

        [Fact]
        public void PollServiceShouldSendFailedEventIfDeviceVanishesAndNoOthers()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            poll.ProcessImageChanged += _ => { _processImageChanges++; };
            poll.InputChanged += (_, _, _) => { _inputChanges++; };
            poll.PollFailed += (_) => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after one second - terminate slave
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;
            _slave.Terminate();
            AssertWait.Max(3000, () => _pollFailed > 0);

            Assert.Equal(0, _processImageChanges);
            Assert.Equal(0, _inputChanges);
        }

        [Fact]
        public void PollServiceShouldReconnectIfDeviceReturns()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50)
            {
                PollRetries = 0
            };
            poll.ConnectionChanged += (_, _) => { _connectionChanges++; };
            poll.ProcessImageChanged += _ => { _processImageChanges++; };
            poll.InputChanged += (_, _, _) => { _inputChanges++; };
            poll.PollFailed += (_) => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // wait for poll connected
            AssertWait.Max(1000, () => _connectionChanges > 0);
            _connectionChanges = 0;

            // after one second - terminate slave
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            _slave.Terminate();
            AssertWait.Max(2000, () => _connectionChanges > 0);

            // after two more seconds - restart slave
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            _slave.Start();

            // after two more seconds - the poll service should have reconnected
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            AssertWait.Max(2000, () => poll.IsConnected);
            Assert.Equal(2, _connectionChanges);

            // after one more second - there should be no events
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();

            Assert.Equal(0, _processImageChanges);
            Assert.Equal(0, _inputChanges);
            Assert.Equal(0, _pollFailed);

            // reconnected - now change image
            _source.WriteRegisters(0, new ushort[] { 0x55AA });
            // after one more second - there should be an event
            AssertWait.Max(2000, () => _processImageChanges > 0);

            // there should be no more connection changes
            AssertWait.Max(1000, () => _connectionChanges > 1);
        }

        [Fact]
        public void PollServiceShouldSignalChangeDuringReconnect()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50)
            {
                PollRetries = 0
            };
            poll.ConnectionChanged += (_, _) => { _connectionChanges++; };
            poll.ProcessImageChanged += _ => { _processImageChanges++; };
            poll.InputChanged += (_, _, _) => { _inputChanges++; };
            poll.PollFailed += (_) => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // wait for poll connected
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            _connectionChanges = 0;
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;

            // after two seconds - terminate slave
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            _slave.Terminate();

            // and change data
            _source.WriteRegisters(0, new ushort[] { 0x55AA });

            // after four more seconds - expect connection change
            Task.Delay(TimeSpan.FromSeconds(4)).Wait();
            AssertWait.Max(4000, () => _connectionChanges > 0);
            
            // restart slave
            _slave.Start();

            // after four more seconds - the poll service should have reconnected
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            AssertWait.Max(2000, () => poll.IsConnected);
            Assert.Equal(2, _connectionChanges);      // disconnected, connected
            Assert.Equal(1, _processImageChanges);    // one image
            Assert.Equal(8, _inputChanges);           // count one bits of 0x55AA
            Assert.True(_pollFailed > 0);                   // minimum one
        }

    }
}
