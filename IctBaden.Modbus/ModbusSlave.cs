using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cet.IO;
using Cet.IO.Net;
using Cet.IO.Protocols;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable EventNeverSubscribedTo.Global

/*
 * A Modbus Slave is a TCP server
 * waiting for a Master's connection and commands.
 * 
 */

namespace IctBaden.Modbus
{
    public class ModbusSlave
    {
        private Task _runner;
        private CancellationTokenSource _cancel;
        private Socket _listener;
        private readonly ManualResetEvent _clientAccepted;

        private readonly List<Socket> _connectedMasters;
        public bool IsConnected => _connectedMasters.Count > 0;

        public string Name { get; private set; }
        public ushort Port { get; private set; }
        public byte Id { get; private set; }
        public IDataAccess DataAccess { get; private set; }

        public DateTime LastAccess { get; private set; }

        public string Endpoint => $"{Port,5}:{Id}";

        public event Action<string> Connected;
        public event Action<string> Disconnected;

        public ModbusSlave(string name, IDataAccess dataAccess, ushort port, byte id)
        {
            Name = name;
            DataAccess = dataAccess;
            Port = port;
            Id = id;

            _connectedMasters = new List<Socket>();
            _clientAccepted = new ManualResetEvent(false);
        }

        public void Start()
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _cancel = new CancellationTokenSource();
            _runner = new Task(RunnerDoWork, _cancel.Token);
            _runner.Start();

            Trace.TraceInformation("ModbusSlave: Started {0,5}:{1}  {2}", Port, Id, Name);
        }

        public void Terminate()
        {
            Trace.TraceInformation("ModbusSlave: Terminated");

            try
            {
                foreach (var client in _connectedMasters)
                {
                    if (client.Connected)
                    {
                        var disconnect = new SocketAsyncEventArgs()
                        {
                            DisconnectReuseSocket = true
                        };
                        client.DisconnectAsync(disconnect);
                    }
                }
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
            try
            {
                _cancel.Cancel();
                _runner.Wait();
                _runner.Dispose();

                _listener.Close();
                _listener.Dispose();
                _listener = null;
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
        }

        void RunnerDoWork()
        {
            try
            {
                var ept = new IPEndPoint(IPAddress.Any, Port);
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Bind(ept);
                _listener.Listen(10);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }

            _clientAccepted.Set();
            while(!_cancel.IsCancellationRequested && _listener.IsBound)
            {
                try
                {
                    if (_clientAccepted.WaitOne(1000, false))
                    {
                        _clientAccepted.Reset();
                        _listener?.BeginAccept(AcceptClient, null);
                    }
                }
                catch (SocketException ex)
                {
                    Trace.TraceError(ex.Message);
                    break;
                }
            }
        }

        private void AcceptClient(IAsyncResult ar)
        {
            _clientAccepted.Set();
            if (_listener == null)
                return;

            try
            {
                var client = _listener.EndAccept(ar);
                var address = client.RemoteEndPoint.ToString();

                Trace.TraceInformation("ModbusSlave: Client connected " + address);
                _connectedMasters.Add(client);
                Connected?.Invoke(address);
                LastAccess = DateTime.Now;

                var codec = new ModbusTcpCodec();
                var server = new ModbusServer(codec) { Address = Id };
                var host = new TcpServer(client, server) { IdleTimeout = 60 };
                host.ServeCommand += ListenerServeCommand;
                host.Start();
                host.Disconnected += () => OnClientDisconnected(client, address);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
        }

        private void OnClientDisconnected(Socket client, string address)
        {
            Trace.TraceInformation("ModbusSlave: Client disconnected " + address);
            _connectedMasters.Remove(client);
            Disconnected?.Invoke(address);
        }

        void ListenerServeCommand(object sender, ServeCommandEventArgs e)
        {
            LastAccess = DateTime.Now;

            var command = (ModbusCommand)e.Data.UserData;

            var traceLines = new StringBuilder();

            traceLines.AppendLine($"[{Port}:{Id}] Command fn={command.FunctionCode}, addr={command.Address}, cnt={command.Count}");

            //take the proper function command handler
            switch (command.FunctionCode)
            {
                case ModbusCommand.FuncReadCoils:
                    var boolArray = DataAccess.ReadCoils(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputDiscretes:
                    boolArray = DataAccess.ReadInputDiscretes(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncWriteCoil:
                    DataAccess.WriteCoils(command.Offset, new[] { command.Data[0] != 0 });
                    traceLines.AppendLine($"[{command.Offset}]={command.Data[0]} ");
                    break;


                case ModbusCommand.FuncForceMultipleCoils:
                    var boolList = new List<bool>();
                    for (var i = 0; i < command.Count; i++)
                    {
                        var index = command.Offset + (i / 16);
                        var mask = 1 << (i % 16);
                        var value = (command.Data[index] & mask) != 0;
                        boolList.Add(value);
                        traceLines.Append($"[{index}]={value} ");
                    }
                    DataAccess.WriteCoils(command.Offset, boolList.ToArray());
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputRegisters:
                    command.Data = DataAccess.ReadInputRegisters(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadMultipleRegisters:
                    command.Data = DataAccess.ReadHoldingRegisters(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteMultipleRegisters:
                    DataAccess.WriteRegisters(command.Offset, command.Data);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteSingleRegister:
                    DataAccess.WriteRegisters(command.Offset, command.Data);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncReadExceptionStatus:
                    traceLines.AppendLine("ModbusSlave: Unhandled command FuncReadExceptionStatus");
                    break;


                default:
                    //return an exception
                    Trace.TraceError("ModbusSlave: Illegal Modbus FunctionCode");
                    command.ExceptionCode = ModbusCommand.ErrorIllegalFunction;
                    break;
            }

            Trace.TraceInformation(traceLines.ToString());
        }

    }
}
