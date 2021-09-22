using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Cet.IO;
using Cet.IO.Net;
using Cet.IO.Protocols;
using IctBaden.Framework.AppUtils;
using IctBaden.Modbus.Core;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable EventNeverSubscribedTo.Global

namespace IctBaden.Modbus
{
    /// <summary>
    /// A Modbus Slave is a TCP server
    /// waiting for a Master's connection and commands. 
    /// </summary>
    public class ModbusSlave : IDisposable
    {
        private Socket _listener;
        private bool _enableCommandTrace;
        private readonly List<Socket> _connectedMasters;
        public bool IsConnected => _connectedMasters.Count > 0;
        public string[] GetConnectedMasters() => _connectedMasters
            .Where(m => m.Connected)
            .Select(m => m.RemoteEndPoint.ToString())
            .ToArray();

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
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        public void EnableCommandTrace(bool enable = true)
        {
            _enableCommandTrace = enable;
        }
        
        public void Start()
        {
            try
            {
                var ipEndPoint = new IPEndPoint(IPAddress.Any, Port);
                _listener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.LingerState = new LingerOption(false, 0);

                _listener.Bind(ipEndPoint);
                _listener.Listen(10);

                _listener.BeginAccept(AcceptClient, _listener);
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception native)
                {
                    if (native.NativeErrorCode == 13 && SystemInfo.Platform == Platform.Linux && Port < 1024)
                    {
                        Trace.TraceError("Ports below 1024 are considered 'privileged' and can only be bound to with an equally privileged user (read: root).");
                    }
                }
                Trace.TraceError("ModbusSlave: " + ex.Message);
                _listener?.Close();
                _listener?.Dispose();
            }
                
            Trace.TraceInformation("ModbusSlave: Started {0,5}:{1}  {2}", Port, Id, Name);
        }

        public void Terminate()
        {
            Trace.TraceInformation("ModbusSlave: Terminated");

            try
            {
                _listener.Close();
                _listener.Dispose();
                
                var connected = _connectedMasters
                    .Where(cm => cm.Connected)
                    .ToArray();
                
                foreach (var client in connected)
                {
                    try
                    {
                        client.Shutdown(SocketShutdown.Both);
                        // Do NOT reuse socket as client
                        client.Disconnect(false);                    
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    finally
                    {
                        client?.Close();
                        client?.Dispose();
                    }
                }
                
                _connectedMasters.Clear();
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
            try
            {
                for(var w = 0; w < 10; w++)
                {
                    if (!_connectedMasters.ToArray().Any()) break;
                    Task.Delay(100).Wait();
                }
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
        }

        private void AcceptClient(IAsyncResult ar)
        {
            if (!(ar.AsyncState is Socket listener)) return;

            try
            {
                var client = listener.EndAccept(ar);
                var address = client?.RemoteEndPoint.ToString();

                if (address != null)
                {
                    Trace.TraceInformation("ModbusSlave: Client connected " + address);
                    _connectedMasters.Add(client);
                    Connected?.Invoke(address);
                    LastAccess = DateTime.Now;

                    var codec = new ModbusTcpCodec();
                    var server = new ModbusServer(codec) {Address = Id};
                    var host = new TcpServer(client, server) {IdleTimeout = 60};
                    host.ServeCommand += ListenerServeCommand;
                    host.Start();
                    host.Disconnected += () => OnClientDisconnected(client, address);
                }

                listener.BeginAccept(AcceptClient, listener);
            }
            catch (ObjectDisposedException)
            {
                // listener terminated
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

            traceLines.AppendLine($"[{Port}:{Id}] Command fn={command.FunctionCode}, address={command.Address}, cnt={command.Count}");

            //take the proper function command handler
            switch (command.FunctionCode)
            {
                case ModbusCommand.FuncReadCoils:
                    var boolArray = DataAccess.ReadCoils((ushort)command.Offset, (ushort)command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputDiscretes:
                    boolArray = DataAccess.ReadInputDiscretes((ushort)command.Offset, (ushort)command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncWriteCoil:
                    DataAccess.WriteCoils((ushort)command.Offset, new[] { command.Data[0] != 0 });
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
                    DataAccess.WriteCoils((ushort)command.Offset, boolList.ToArray());
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputRegisters:
                    command.Data = DataAccess.ReadInputRegisters((ushort)command.Offset, (ushort)command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadMultipleRegisters:
                    command.Data = DataAccess.ReadHoldingRegisters((ushort)command.Offset, (ushort)command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteMultipleRegisters:
                    DataAccess.WriteRegisters((ushort)command.Offset, command.Data);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append($"[{command.Offset + i}]={command.Data[i]} ");
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteSingleRegister:
                    DataAccess.WriteRegisters((ushort)command.Offset, command.Data);
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

            if (_enableCommandTrace)
            {
                Trace.TraceInformation(traceLines.ToString());
            }
        }

    }
}
