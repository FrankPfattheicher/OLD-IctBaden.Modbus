using System;
using System.Linq;
using IctBaden.Modbus.Test;
using IctBaden.Stonehenge3.Core;
using IctBaden.Stonehenge3.ViewModel;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace IctBaden.Modbus.SampleDevice.ViewModels
{
    public class DeviceVm : ActiveViewModel, IDisposable
    {
        private readonly ModbusSlave _device;
        private readonly TestData _data;
        public InputVm[] Inputs { get; private set; }
        public string ConnectedServer { get; private set; }

        public DeviceVm(AppSession session, ModbusSlave device, TestData data)
            : base(session)
        {
            _device = device;
            _data = data;
        }

        public override void OnLoad()
        {
            Inputs = Enumerable.Range(1, 16)
                .Select(i => new InputVm {Number = i})
                .ToArray();

            _device.Connected += DeviceOnConnected;
            _device.Disconnected += DeviceOnDisconnected;

            if (_device.IsConnected)
            {
                DeviceOnConnected(_device.GetConnectedMasters().First());
            }
            else
            {
                DeviceOnDisconnected("");
            }
            
            UpdateData();
        }

        private void DeviceOnDisconnected(string server)
        {
            ConnectedServer = "NOT CONNECTED";
            NotifyAllPropertiesChanged();
        }

        private void DeviceOnConnected(string server)
        {
            ConnectedServer = server;
            NotifyAllPropertiesChanged();
        }

        [ActionMethod]
        public void ToggleInput(int number)
        {
            Inputs[number - 1].Value = !Inputs[number - 1].Value;
            UpdateData();
        }

        private void UpdateData()
        {
            var dataValue = (ushort) Inputs
                .Aggregate(0, (i, vm) => i | (vm.Value ? 1 : 0) << (vm.Number - 1));

            _data.WriteRegisters(0, new[] {dataValue});
            
            foreach (var input in Inputs)
            {
                _data.WriteCoils(input.Number - 1, new[] {input.Value});
            }
        }

        public void Dispose()
        {
            _device.Connected -= DeviceOnConnected;
            _device.Disconnected -= DeviceOnDisconnected;
        }
    }
}