﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Cet.IO;
using Cet.IO.Net;
using Cet.IO.Protocols;
using IctBaden.Modbus.Core;

// ReSharper disable MemberCanBePrivate.Global

namespace IctBaden.Modbus
{
    public class ConnectedSlave : IDataAccess, IDisposable
    {
        private Socket _socketConnection;
        public ICommClient CommClient { get; private set; }

        public ModbusMaster Master { get; }
        public IPAddress Address { get; }
        public ushort Port { get; }
        public byte Id { get; }

        private readonly ModbusClient _client;
        private int _failures;
        private bool _reconnecting;

        public ConnectedSlave(ModbusMaster master, Socket connection, byte id)
        {
            Master = master;
            _socketConnection = connection;
            CommClient = _socketConnection.GetClient();
            if (connection.RemoteEndPoint is IPEndPoint ipEndPoint)
            {
                Address = ipEndPoint.Address;
                Port = (ushort)ipEndPoint.Port;
            }
            else
            {
                throw new NotSupportedException("ConnectedSlave without IP endpoint");
            }
            Id = id;
            var codec = new ModbusTcpCodec();
            _client = new ModbusClient(codec) { Address = Id };
            _failures = 0;
        }

        public override string ToString()
        {
            return $"{Address}:{Port}#{Id}";
        }

        public void Dispose()
        {
            Disconnect();
        }

        public bool IsConnected => _socketConnection is {Connected: true};

        public event Action Disconnected;
        
        
        public void ReConnect()
        {
            if (_reconnecting)
            {
                Trace.TraceWarning("ConnectedSlave:ReConnect: Already reconnecting");
                return;
            }

            Disconnect();

            try
            {
                _reconnecting = true;
                _socketConnection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                // Do NOT reuse address as client
                _socketConnection.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                _socketConnection.BeginConnect(Address, Port, RequestCallback, _socketConnection);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ConnectedSlave:ReConnect: Reconnect failed #1: " + ex.Message);
                _reconnecting = false;
            }
        }

        private void RequestCallback(IAsyncResult ar)
        {
            var socket = (Socket)ar.AsyncState;
            try
            {
                socket?.EndConnect(ar);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ConnectedSlave:ReConnect: Reconnect failed #2: " + ex.Message);
            }

            if (!ar.IsCompleted) return;
            CommClient = socket.GetClient();
            _reconnecting = false;
        }

        public void Disconnect()
        {
            if (_socketConnection == null) return;

            try
            {
                if (_socketConnection.IsBound)
                {
                    _socketConnection.Shutdown(SocketShutdown.Both);
                    // Do NOT reuse socket as client
                    _socketConnection.Disconnect(false);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ConnectedSlave.Disconnect: " + ex.Message);
            }
            finally
            {
                _socketConnection?.Close();
                _socketConnection?.Dispose();

                Disconnected?.Invoke();
            }
            _socketConnection = null;
        }

        private ModbusCommand Execute(ModbusCommand command)
        {
            if (_socketConnection == null)
            {
                Trace.TraceWarning("ConnectedSlave.Execute: No connection to slave.");
                return null;
            }
            if (!_socketConnection.Connected)
            {
                Trace.TraceWarning("ConnectedSlave.Execute: Reconnect to slave.");
                try
                {
                    ReConnect();
                }
                catch (SocketException ex)
                {
                    Trace.TraceError("ConnectedSlave:Execute: Connect failed: " + ex.Message);
                }
                
                if (!_socketConnection.Connected)
                    return null;
            }
            var response = _client.ExecuteGeneric(CommClient, command);
            if (response.Status == CommResponse.Ack)
            {
                _failures = 0;
                return response.Data.UserData as ModbusCommand;
            }

            var status = response.Status.ToString();
            switch (response.Status)
            {
                case CommResponse.Critical:
                    status = "Critical";
                    _failures++;
                    if (_failures > 3)
                    {
                        // Do NOT reuse socket as client
                        _socketConnection.Disconnect(false);
                        Disconnected?.Invoke();
                    }
                    break;
                case CommResponse.Ignore:
                    status = "Ignore";
                    break;
                case CommResponse.Unknown:
                    status = "Unknown";
                    break;
                case CommResponse.ConnectionLost:
                    status = "Connection Lost";
                    // Do NOT reuse socket as client
                    _socketConnection.Disconnect(false);                    
                    Disconnected?.Invoke();
                    break;
            }
            Trace.TraceError("ConnectedSlave.Execute: Response status=" + status);
            return null;
        }

        public bool[] ReadInputDiscretes(ushort offset, ushort count)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncReadInputDiscretes)
            {
                Address = Id,
                Offset = offset,
                Count = count
            };
            var response = Execute(cmd);
            return response?.Data.Select(d => d != 0).ToArray();
        }

        public bool[] ReadCoils(ushort offset, ushort count)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncReadCoils)
            {
                Address = Id,
                Offset = offset,
                Count = count
            };
            var response = Execute(cmd);
            return response?.Data.Select(d => d != 0).ToArray();
        }

        public bool WriteCoils(ushort offset, bool[] values)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncWriteCoil)
            {
                Address = Id,
                Offset = offset,
                Count = values.Length,
                Data = new ushort[0]
            };
            var response = Execute(cmd);
            return (response != null);
        }

        public ushort[] ReadInputRegisters(ushort offset, ushort count)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncReadInputRegisters)
            {
                Address = Id,
                Offset = offset,
                Count = count
            };
            var response = Execute(cmd);
            return response?.Data;
        }

        public ushort[] ReadHoldingRegisters(ushort offset, ushort count)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncReadMultipleRegisters)
            {
                Address = Id,
                Offset = offset,
                Count = count
            };
            var response = Execute(cmd);
            return response?.Data;
        }

        public bool WriteRegister(ushort offset, ushort value)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncWriteSingleRegister)
            {
                Address = Id,
                Offset = offset,
                Count = 1,
                Data = new[] { value }
            };
            var response = Execute(cmd);
            return (response != null);
        }

        public bool WriteRegisters(ushort offset, ushort[] values)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncWriteMultipleRegisters)
            {
                Address = Id,
                Offset = offset,
                Count = values.Length,
                Data = values
            };
            var response = Execute(cmd);
            return (response != null);
        }
    }
}
