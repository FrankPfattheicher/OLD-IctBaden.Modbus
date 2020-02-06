using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IctBaden.Framework.Tron;
using Xunit;

namespace IctBaden.Modbus.Test
{
    [CollectionDefinition(nameof(PollServiceTests), DisableParallelization = true)]
    public class PollServiceTests : IDisposable
    {
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

            _source = new TestData();

            _slave = new ModbusSlave("Test", _source, 502, 1);
            _slave.Start();

            _master = new ModbusMaster();

            _client = _master.ConnectDevice("localhost", 502, 1);
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

            Thread.Sleep(TimeSpan.FromSeconds(1));
            Assert.True(poll.IsConnected);
        }

        [Fact]
        public void PollServiceShouldNotSendEventsIfNothingChanges()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            poll.ProcessImageChanged += (e) => { _processImageChanges++; };
            var started = poll.Start(TimeSpan.FromSeconds(1), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            _processImageChanges = 0;
            Thread.Sleep(TimeSpan.FromSeconds(1));

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
            poll.ProcessImageChanged += (e) => { _processImageChanges++; };
            var started = poll.Start(TimeSpan.FromSeconds(1), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after half second - change data
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            _processImageChanges = 0;
            _source.WriteRegisters(0, new ushort[] { 0x55AA });

            // after one second - change should be reported
            Thread.Sleep(TimeSpan.FromSeconds(1));
            Assert.Equal(1, _processImageChanges);
        }

        [Fact]
        public void PollServiceShouldSetDisconnectedIfDeviceVanishes()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after one second - terminate slave
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _slave.Terminate();

            // after two more seconds - service should not more be connected
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.False(poll.IsConnected);
        }

        [Fact]
        public void PollServiceShouldSendFailedEventIfDeviceVanishesAndNoOthers()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50);
            poll.ProcessImageChanged += (e) => { _processImageChanges++; };
            poll.InputChanged += (i,v) => { _inputChanges++; };
            poll.PollFailed += () => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // after one second - terminate slave
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;
            _slave.Terminate();
            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.Equal(0, _processImageChanges);
            Assert.Equal(0, _inputChanges);
            Assert.True(_pollFailed > 0, "pollFailed is zero");
        }

        [Fact]
        public void PollServiceShouldReconnectIfDeviceReturns()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50)
            {
                PollRetries = 0
            };
            poll.ConnectionChanged += (e) => { _connectionChanges++; };
            poll.ProcessImageChanged += (e) => { _processImageChanges++; };
            poll.InputChanged += (i, v) => { _inputChanges++; };
            poll.PollFailed += () => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // wait for poll connected
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _connectionChanges = 0;

            // after one second - terminate slave
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _slave.Terminate();

            // after two more seconds - restart slave
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.Equal(1, _connectionChanges);
            _slave.Start();

            // after two more seconds - the poll service should have reconnected
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.True(poll.IsConnected);
            Assert.Equal(2, _connectionChanges);

            // after one more second - there should be no events
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.Equal(0, _processImageChanges);
            Assert.Equal(0, _inputChanges);
            Assert.Equal(0, _pollFailed);

            // reconnected - now change image
            _source.WriteRegisters(0, new ushort[] { 0x55AA });
            // after one more second - there should be an event
            Thread.Sleep(TimeSpan.FromSeconds(1));
            Assert.Equal(1, _processImageChanges);

            // there should be no more connection changes
            Assert.Equal(2, _connectionChanges);
        }

        [Fact]
        public void PollServiceShouldSignalChangeDuringReconnect()
        {
            var poll = new ModbusDevicePollService(_client, ModbusDevicePollService.Register.Holding, 0, 50)
            {
                PollRetries = 0
            };
            poll.ConnectionChanged += (e) => { _connectionChanges++; };
            poll.ProcessImageChanged += (e) => { _processImageChanges++; };
            poll.InputChanged += (i, v) => { _inputChanges++; };
            poll.PollFailed += () => { _pollFailed++; };
            var started = poll.Start(TimeSpan.FromSeconds(0.5), true);
            Assert.True(started);
            WaitForStableConnection(poll);

            // wait for poll connected
            Thread.Sleep(TimeSpan.FromSeconds(2));
            _connectionChanges = 0;
            _processImageChanges = 0;
            _inputChanges = 0;
            _pollFailed = 0;

            // after two seconds - terminate slave
            Thread.Sleep(TimeSpan.FromSeconds(2));
            _slave.Terminate();

            // and change data
            _source.WriteRegisters(0, new ushort[] { 0x55AA });

            // after four more seconds - restart slave
            Thread.Sleep(TimeSpan.FromSeconds(4));
            Assert.Equal(1, _connectionChanges);
            _slave.Start();

            // after four more seconds - the poll service should have reconnected
            Thread.Sleep(TimeSpan.FromSeconds(4));
            Assert.True(poll.IsConnected);
            Assert.Equal(2, _connectionChanges);      // disconnected, connected
            Assert.Equal(1, _processImageChanges);    // one image
            Assert.Equal(8, _inputChanges);           // count one bits of 0x55AA
            Assert.True(_pollFailed > 0);                   // minimum one
        }

    }
}
