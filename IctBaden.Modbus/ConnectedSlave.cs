using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Cet.IO;
using Cet.IO.Net;
using Cet.IO.Protocols;

namespace IctBaden.Modbus
{
    public class ConnectedSlave : IDataAccess, IDisposable
    {
        private Socket socketConnection;
        public ICommClient CommClient { get; private set; }

        public ModbusMaster Master { get; }
        public IPAddress Address { get; }
        public ushort Port { get; }
        public byte Id { get; }

        private readonly ModbusClient client;
        private bool reconnecting;

        public ConnectedSlave(ModbusMaster master, Socket connection, byte id)
        {
            Master = master;
            socketConnection = connection;
            CommClient = socketConnection.GetClient();
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
            client = new ModbusClient(codec) { Address = Id };
        }

        public override string ToString()
        {
            return $"{Address}:{Port}#{Id}";
        }

        public void Dispose()
        {
            Disconnect();
        }

        public bool IsConnected => (socketConnection != null) && socketConnection.Connected;

        public void ReConnect()
        {
            if (reconnecting) return;

            Disconnect();

            try
            {
                reconnecting = true;
                socketConnection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socketConnection.BeginConnect(Address, Port, RequestCallback, socketConnection);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ConnectedSlave:ReConnect: Reconnect failed: " + ex.Message);
            }
        }

        private void RequestCallback(IAsyncResult ar)
        {
            var socket = (Socket)ar.AsyncState;
            try
            {
                socket.EndConnect(ar);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ConnectedSlave:ReConnect: Reconnect failed: " + ex.Message);
            }

            if (!ar.IsCompleted) return;
            CommClient = socket.GetClient();
            reconnecting = false;
        }

        public void Disconnect()
        {
            if (socketConnection == null) return;

            try
            {
                if (socketConnection.IsBound)
                {
                    //socketConnection.Disconnect(true);
                }
                socketConnection.Close();
                socketConnection.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("ConnectedSlave.Disconnect: " + ex.Message);
            }
            socketConnection = null;
        }

        private ModbusCommand Execute(ModbusCommand command)
        {
            if (socketConnection == null)
            {
                Trace.TraceWarning("ConnectedSlave.Execute: No connection to slave.");
                return null;
            }
            if (!socketConnection.Connected)
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
                
                if (!socketConnection.Connected)
                    return null;
            }
            var response = client.ExecuteGeneric(CommClient, command);
            if (response.Status == CommResponse.Ack)
            {
                return response.Data.UserData as ModbusCommand;
            }

            var status = response.Status.ToString();
            switch (response.Status)
            {
                case CommResponse.Critical:
                    status = "Critical";
                    break;
                case CommResponse.Ignore:
                    status = "Ignore";
                    break;
                case CommResponse.Unknown:
                    status = "Unknown";
                    break;
            }
            Trace.TraceError("ConnectedSlave.Execute: Response status=" + status);
            return null;
        }

        public bool[] ReadInputDiscretes(int offset, int count)
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

        public bool[] ReadCoils(int offset, int count)
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

        public bool WriteCoils(int offset, bool[] values)
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

        public ushort[] ReadInputRegisters(int offset, int count)
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

        public ushort[] ReadHoldingRegisters(int offset, int count)
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

        public bool WriteRegisters(int offset, ushort[] values)
        {
            var cmd = new ModbusCommand(ModbusCommand.FuncWriteSingleRegister)
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
