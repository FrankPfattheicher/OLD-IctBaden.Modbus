﻿using System;
using System.Threading;
using System.Diagnostics;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace IctBaden.Modbus
{
    // ReSharper disable once UnusedMember.Global
    public class ModbusDevicePollService
    {
        public TimeSpan PollInterval { get; set; }
        public TimeSpan PollRetryInterval { get; set; }
        public TimeSpan FailureRetryInterval { get; set; }
        public int PollRetries { get; set; }

        public ConnectedSlave Slave => _pollDevice;
        public bool IsConnected => _pollDevice?.IsConnected ?? false;

        public bool IsConnectionStable =>
            _state == ConnectionStable || _state == PollRegisters || _state == PollRegistersOk;

        public ushort[] ProcessImage { get; private set; }


        public enum Register
        {
            Input,

            // ReSharper disable once UnusedMember.Global
            Holding
        };

        public event Action<ProcessImageChangeEventParams> ProcessImageChanged;
        public event Action<string, int, bool> InputChanged;
        public event Action<string> PollFailed;
        public event Action<string, bool> ConnectionChanged;

        private readonly ModbusMaster _master;
        private readonly string _address;
        private readonly ushort _port;
        private readonly byte _id;
        private readonly Register _registerToPoll;
        private readonly int _pollOffset;
        private readonly int _pollCount;
        private readonly string _traceId;

        private Thread _pollingThread;
        private bool _forceInitialEvents;
        private ConnectedSlave _pollDevice;
        private Action _state;
        private int _retryCount;
        private int _failureCount;
        private bool _oldConnected;
        private ushort[] _newProcessImage;
        private ushort[] _oldProcessImage;
        private readonly string _connectionContext = GetPollContext();

        /// <summary>
        /// Start polling already connected slave.
        /// </summary>
        /// <param name="slave"></param>
        /// <param name="register"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public ModbusDevicePollService(ConnectedSlave slave, Register register, int offset, int count)
            : this(slave.Master, slave.Address.ToString(), slave.Port, slave.Id, register, offset, count)
        {
            _pollDevice = slave;
            _oldConnected = !_pollDevice.IsConnected;
        }

        /// <summary>
        /// Start polling the specified slave.
        /// Establish connection.
        /// </summary>
        /// <param name="master"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="id"></param>
        /// <param name="register"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public ModbusDevicePollService(ModbusMaster master, string address, ushort port, byte id, Register register,
            int offset, int count)
        {
            _master = master;
            _address = address;
            _port = port;
            _id = id;
            _pollDevice = null;
            _oldConnected = true;

            _registerToPoll = register;
            _pollOffset = offset;
            _pollCount = count;
            _traceId = $"Poll({address}:{port}#{id})";

            PollRetryInterval = TimeSpan.FromSeconds(0.1);
            FailureRetryInterval = TimeSpan.FromSeconds(1);
            PollRetries = 1;
        }

        public bool Start(TimeSpan interval, bool forceInitialEvents)
        {
            Trace.TraceInformation($"PollSource Start({interval.TotalSeconds:F1})");
            PollInterval = interval;
            _forceInitialEvents = forceInitialEvents;

            _retryCount = 0;
            _failureCount = 0;
            GoState(EstablishConnection);

            _pollingThread = new Thread(PollSource);
            _pollingThread.Start();
            return true;
        }

        public void Stop()
        {
            Trace.TraceInformation("PollSource Stop()");
            if (_pollingThread == null) return;

            var p = _pollingThread;
            _pollingThread = null;
            p?.Join(TimeSpan.FromSeconds(10));

            if (_pollDevice != null)
            {
                _pollDevice.Disconnect();
                _pollDevice.Dispose();
                _pollDevice = null;
            }
        }

        private ushort[] ReadSource()
        {
            var sourceData = _registerToPoll == Register.Input
                ? _pollDevice.ReadInputRegisters(_pollOffset, _pollCount)
                : _pollDevice.ReadHoldingRegisters(_pollOffset, _pollCount);
            return sourceData;
        }

        private void PollSource()
        {
            Trace.TraceInformation("PollSource started.");
            ThreadName.Set($"Poll:{_address}");
            while (_pollingThread != null)
            {
                try
                {
                    _state();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("PollSource: " + ex.Message);
                    Trace.TraceError(ex.StackTrace);

                    Thread.Sleep(FailureRetryInterval);

                    GoState(EstablishConnection);
                }
            }

            Trace.TraceInformation("PollSource terminated.");
        }

        private void GoState(Action nextState)
        {
            var message = $"{_traceId}: GoState {nextState.Method.Name}";
            Trace.TraceInformation(message);
            _state = nextState;
        }

        private void EstablishConnection()
        {
            if (_oldConnected)
            {
                OnConnectionChanged(_connectionContext, false);
                _oldConnected = false;
            }

            if (_pollDevice == null)
            {
                Thread.Sleep(FailureRetryInterval);
                _pollDevice = _master.ConnectDevice(_address, _port, _id);
                return;
            }

            if (_pollDevice.IsConnected)
            {
                _retryCount = 0;
                _failureCount = 0;
                GoState(Connected);
                return;
            }

            Thread.Sleep(FailureRetryInterval);
            _failureCount++;
            if (_failureCount > 2)
            {
                GoState(Disconnected);
            }
        }

        private void Connected()
        {
            _newProcessImage = ReadSource();

            if (!_pollDevice.IsConnected)
            {
                GoState(Disconnected);
                return;
            }

            if (_newProcessImage == null)
            {
                Thread.Sleep(FailureRetryInterval);
                _failureCount++;
                if (_failureCount > 2)
                {
                    GoState(Disconnected);
                }

                return;
            }

            if (_oldProcessImage == null)
            {
                _oldProcessImage = _newProcessImage;
                return;
            }

            if (_newProcessImage.SequenceEqual(_oldProcessImage))
            {
                _retryCount++;
                if (_retryCount >= 2)
                {
                    ProcessImage ??= _newProcessImage;
                    GoState(ConnectionStable);
                }

                return;
            }

            _retryCount = 0;
            _oldProcessImage = _newProcessImage;
        }

        private void ConnectionStable()
        {
            if (!_oldConnected)
            {
                OnConnectionChanged(_connectionContext, true);
                _oldConnected = true;
            }

            if (_forceInitialEvents)
            {
                Trace.TraceWarning($"{_traceId}: Force initial events");
                _forceInitialEvents = false;
                for (var ix = 0; ix < ProcessImage.Length; ix++)
                {
                    ProcessImage[ix] = (ushort)~ProcessImage[ix];
                }
            }

            _retryCount = 0;
            GoState(PollRegisters);
        }

        private void PollRegisters()
        {
            _newProcessImage = ReadSource();

            var next = (_pollDevice.IsConnected && _newProcessImage != null)
                ? (Action)PollRegistersOk
                : PollRegistersFailed;

            GoState(next);
        }

        public static string GetPollContext() => Guid.NewGuid()
            .ToString("N")
            .Substring(4, 6)
            .ToUpper();

        private void PollRegistersOk()
        {
            if (_newProcessImage.Length != _pollCount)
            {
                Trace.TraceError(
                    $"PollRegisters: Read returns {_newProcessImage.Length} registers, {_pollCount} expected");
            }
            else
            {
                var message = "ProcessImage:";
                var ctx = GetPollContext();
                for (var offset = 0; offset < _pollCount; offset++)
                {
                    message += $" {_newProcessImage[offset]:X4}";
                    if (_newProcessImage[offset] == ProcessImage[offset]) continue;

                    OnProcessImageChanged(new ProcessImageChangeEventParams(ctx, offset, ProcessImage[offset],
                        _newProcessImage[offset]));
                    for (var bit = 0; bit < 16; bit++)
                    {
                        var bitMask = 1 << bit;
                        if ((_newProcessImage[offset] & bitMask) != (ProcessImage[offset] & bitMask))
                        {
                            OnInputChanged(ctx, offset * 16 + bit, (_newProcessImage[offset] & bitMask) != 0);
                        }
                    }
                }

                Trace.TraceInformation(message);
                ProcessImage = _newProcessImage;
            }

            Thread.Sleep(PollInterval);
            GoState(PollRegisters);
        }

        private void PollRegistersFailed()
        {
            OnPollFailed(_connectionContext);
            _retryCount++;
            Thread.Sleep(PollRetryInterval);

            var next = (_pollDevice.IsConnected && _retryCount <= PollRetries)
                ? (Action)PollRegisters
                : Disconnected;

            GoState(next);
        }

        private void Disconnected()
        {
            if (_oldConnected)
            {
                OnConnectionChanged(_connectionContext, false);
                _oldConnected = false;
            }

            GoState(Reconnect);
        }

        private void Reconnect()
        {
            if (_pollDevice.IsConnected)
            {
                GoState(Connected);
                return;
            }

            _pollDevice.ReConnect();
            Thread.Sleep(FailureRetryInterval);
        }


        private void OnProcessImageChanged(ProcessImageChangeEventParams obj)
        {
            try
            {
                ProcessImageChanged?.Invoke(obj);
            }
            catch (Exception ex)
            {
                Trace.TraceError("PollRegisters.ProcessImageChanged: " + ex.Message);
                Trace.TraceError(ex.StackTrace);
            }
        }

        private void OnPollFailed(string obj)
        {
            try
            {
                PollFailed?.Invoke(obj);
            }
            catch (Exception ex)
            {
                Trace.TraceError("PollRegisters.PollFailed: " + ex.Message);
                Trace.TraceError(ex.StackTrace);
            }
        }

        private void OnInputChanged(string arg1, int arg2, bool arg3)
        {
            try
            {
                InputChanged?.Invoke(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                Trace.TraceError("PollRegisters.InputChanged: " + ex.Message);
                Trace.TraceError(ex.StackTrace);
            }
        }

        private void OnConnectionChanged(string arg1, bool arg2)
        {
            try
            {
                ConnectionChanged?.Invoke(arg1, arg2);
            }
            catch (Exception ex)
            {
                Trace.TraceError("PollRegisters.ConnectionChanged: " + ex.Message);
                Trace.TraceError(ex.StackTrace);
            }
        }
        
    }
}