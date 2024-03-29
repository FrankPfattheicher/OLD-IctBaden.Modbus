using System;
using System.Linq;
using IctBaden.Modbus.Test;
using IctBaden.Stonehenge3.Core;
using IctBaden.Stonehenge3.ViewModel;

// ReSharper disable UnusedMember.Global

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace IctBaden.Modbus.SampleDevice.ViewModels
{
    public class DeviceVm : ActiveViewModel, IDisposable
    {
        private readonly ModbusSlave _device;
        private readonly TestData _data;
        public InputVm[] Inputs { get; private set; }
        public RegisterVm[] Registers { get; private set; }
        public string Connections { get; private set; }

        public DeviceVm(AppSession session, ModbusSlave device, TestData data)
            : base(session)
        {
            _device = device;
            _data = data;
        }

        public override void OnLoad()
        {
            Inputs = Enumerable.Range(1, 16)
                .Select(i => new InputVm { Number = i })
                .ToArray();

            Registers = Enumerable.Range(1, 16)
                .Select(i => new RegisterVm { Number = i })
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
            var cnn = _device.GetConnectedMasters();
            Connections = cnn.Any()
                ? string.Join("; ", cnn)
                : "NOT CONNECTED";

            NotifyAllPropertiesChanged();
        }

        private void DeviceOnConnected(string server)
        {
            Connections = string.Join("; ", _device.GetConnectedMasters());
            NotifyAllPropertiesChanged();
        }

        [ActionMethod]
        public void ToggleInput(int number)
        {
            Inputs[number - 1].Value = !Inputs[number - 1].Value;

            var registerIndex = (number - 1) / 16;
            var registerMask  = (ushort)(1 << ((number - 1) % 16));

            if (Inputs[number - 1].Value)
            {
                Registers[registerIndex].Value |= registerMask;                
            }
            else
            {
                Registers[registerIndex].Value &= (ushort)~registerMask;                
            }
            
            UpdateData();
        }

        [ActionMethod]
        public void SetValue(int number, string value)
        {
            if (!ushort.TryParse(value, out var newValue)) return;

            Registers[number - 1].Value = newValue;

            if (number == 1)
            {
                var registerMask = 1;
                for (var ix = 0; ix < 16; ix++, registerMask <<= 1)
                {
                    Inputs[ix].Value = (newValue & registerMask) != 0;
                }
            }
            
            UpdateData();
        }

        private void UpdateData()
        {
            foreach (var input in Inputs)
            {
                _data.WriteCoils((ushort)(input.Number - 1), new[] { input.Value });
            }

            foreach (var register in Registers)
            {
                _data.WriteRegisters((ushort)(register.Number - 1), new[] { register.Value });
            }
        }

        public void Dispose()
        {
            _device.Connected -= DeviceOnConnected;
            _device.Disconnected -= DeviceOnDisconnected;
        }
    }
}