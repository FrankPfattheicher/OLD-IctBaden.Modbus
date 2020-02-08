/*
 * A Modbus Master is a TCP client connecting
 * to the Slaves and sends commands.
 * 
 */

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedMember.Global

// ReSharper disable MemberCanBePrivate.Global
namespace IctBaden.Modbus
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Sockets;

    public class ModbusMaster
    {
        public const int DefaultPort = 502;

        public string Name { get; private set; }
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string LastError { get; private set; }
        public List<ConnectedSlave> ConnectedSlaveDevices { get; private set; }

        // ReSharper disable once UnusedMember.Global
        public ModbusMaster(string name)
            : this()
        {
            Name = name;
        }
        public ModbusMaster()
        {
            ConnectedSlaveDevices = new List<ConnectedSlave>();
        }

        public override string ToString()
        {
            return Name ?? "Master";
        }

        public ConnectedSlave ConnectDevice(string address, ushort port, byte id)
        {
            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            try
            {
                sock.Connect(address, port);
                if (!sock.Connected)
                {
                    LastError = "Connect failed";
                    sock.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Trace.TraceError("ModbusMaster: Could not connect to {0}:{1} id={2}" + Environment.NewLine + ex.Message, address, port, id);
                sock.Dispose();
                return null;
            }

            LastError = null;
            var dev = new ConnectedSlave(this, sock, id);
            dev.Disconnected += () =>
            {
                lock (ConnectedSlaveDevices)
                {
                    ConnectedSlaveDevices.Remove(dev);
                }
            };
            
            lock (ConnectedSlaveDevices)
            {
                ConnectedSlaveDevices.Add(dev);
            }

            Trace.TraceInformation("ModbusMaster: Connected to device {0}:{1} id={2}", address, port, id);
            return dev;
        }

        public void DisconnectDevice(IDataAccess slave)
        {
            ConnectedSlave dev;
            lock (ConnectedSlaveDevices)
            {
                dev = ConnectedSlaveDevices.FirstOrDefault(d => slave == d as IDataAccess);
            }
            if (dev == null)
                return;

            dev.Disconnect();
            dev.Dispose();
            lock (ConnectedSlaveDevices)
            {
                ConnectedSlaveDevices.Remove(dev);
            }
            Trace.TraceInformation("ModbusMaster: Disconnected from device {0}:{1} id={2}", dev.Address, dev.Port, dev.Id);
        }

        public void DisconnectAllDevices()
        {
            lock (ConnectedSlaveDevices)
            {
                foreach (var dev in ConnectedSlaveDevices)
                {
                    try
                    {
                        dev.Disconnect();
                        dev.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("ModbusMaster: Could not disconnect slave" + Environment.NewLine + ex.Message);
                    }
                }
                ConnectedSlaveDevices.Clear();
            }
        }

        
    }
}