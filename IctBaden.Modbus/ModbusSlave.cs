using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cet.IO;
using Cet.IO.Net;
using Cet.IO.Protocols;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedMember.Global

/*
 * A Modbus Slave is a TCP server
 * waiting for a Master's connection and commands.
 * 
 */

namespace IctBaden.Modbus
{
    public class ModbusSlave
    {
        private BackgroundWorker runner;
        private Socket listener;
        private readonly ManualResetEvent clientAccepted;

        private readonly List<Socket> connectedMasters;
        public bool IsConnected => connectedMasters.Count > 0;

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

            connectedMasters = new List<Socket>();
            clientAccepted = new ManualResetEvent(false);
        }

        public void Start()
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            runner = new BackgroundWorker();
            runner.DoWork += RunnerDoWork;
            runner.WorkerSupportsCancellation = true;
            runner.RunWorkerAsync();

            Trace.TraceInformation("ModbusSlave: Started {0,5}:{1}  {2}", Port, Id, Name);
        }

        public void Terminate()
        {
            Trace.TraceInformation("ModbusSlave: Terminated");

            try
            {
                foreach (var client in connectedMasters)
                {
                    if (client.Connected)
                    {
                        client.Disconnect(true);
                    }
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client.Dispose();
                }
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
            try
            {
                runner.CancelAsync();
                listener.Close();
                listener.Dispose();
                listener = null;
            }
            catch (Exception ex)
            {
                // ignore
                Trace.TraceError(ex.Message);
            }
        }

        void RunnerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var ept = new IPEndPoint(IPAddress.Any, Port);
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Bind(ept);
                listener.Listen(10);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }

            clientAccepted.Set();
            while (!runner.CancellationPending && listener.IsBound)
            {
                try
                {
                    if (clientAccepted.WaitOne(1000, false))
                    {
                        clientAccepted.Reset();
                        listener?.BeginAccept(AcceptClient, null);
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
            clientAccepted.Set();
            if (listener == null)
                return;

            try
            {
                var client = listener.EndAccept(ar);
                var address = client.RemoteEndPoint.ToString();

                Trace.TraceInformation("ModbusSlave: Client connected " + address);
                connectedMasters.Add(client);
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
            connectedMasters.Remove(client);
            Disconnected?.Invoke(address);
        }

        void ListenerServeCommand(object sender, ServeCommandEventArgs e)
        {
            LastAccess = DateTime.Now;

            var command = (ModbusCommand)e.Data.UserData;

            var traceLines = new StringBuilder();

            traceLines.AppendLine(string.Format("[{0}:{1}] Command fn={2}, addr={3}, cnt={4}",
              Port, Id, command.FunctionCode, command.Address, command.Count));

            //take the proper function command handler
            switch (command.FunctionCode)
            {
                case ModbusCommand.FuncReadCoils:
                    var boolArray = DataAccess.ReadCoils(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputDiscretes:
                    boolArray = DataAccess.ReadInputDiscretes(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        command.Data[i] = (ushort)(boolArray[i] ? 1 : 0);
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncWriteCoil:
                    DataAccess.WriteCoils(command.Offset, new[] { command.Data[0] != 0 });
                    traceLines.AppendLine(string.Format("[{0}]={1} ", command.Offset, command.Data[0]));
                    break;


                case ModbusCommand.FuncForceMultipleCoils:
                    var boolList = new List<bool>();
                    for (var i = 0; i < command.Count; i++)
                    {
                        var index = command.Offset + (i / 16);
                        var mask = 1 << (i % 16);
                        var value = (command.Data[index] & mask) != 0;
                        boolList.Add(value);
                        traceLines.Append(string.Format("[{0}]={1} ", index, value));
                    }
                    DataAccess.WriteCoils(command.Offset, boolList.ToArray());
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadInputRegisters:
                    command.Data = DataAccess.ReadInputRegisters(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
                    }
                    traceLines.AppendLine(string.Empty);
                    break;


                case ModbusCommand.FuncReadMultipleRegisters:
                    command.Data = DataAccess.ReadHoldingRegisters(command.Offset, command.Count);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteMultipleRegisters:
                    DataAccess.WriteRegisters(command.Offset, command.Data);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
                    }
                    traceLines.AppendLine(string.Empty);
                    break;

                case ModbusCommand.FuncWriteSingleRegister:
                    DataAccess.WriteRegisters(command.Offset, command.Data);
                    for (var i = 0; i < command.Count; i++)
                    {
                        traceLines.Append(string.Format("[{0}]={1} ", command.Offset + i, command.Data[i]));
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
