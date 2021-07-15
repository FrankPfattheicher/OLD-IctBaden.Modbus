using System;
using System.Diagnostics;
using IctBaden.Framework.Tron;
using IctBaden.Modbus.Test;
using IctBaden.Stonehenge3.Hosting;
using IctBaden.Stonehenge3.Kestrel;
using IctBaden.Stonehenge3.Resources;
using IctBaden.Stonehenge3.Vue;

namespace IctBaden.Modbus.SampleDevice
{
    internal static class Program
    {
        // ReSharper disable once UnusedParameter.Local
        private static void Main(string[] args)
        {
            Console.WriteLine("IctBaden.Modbus.SampleDevice");

            Trace.Listeners.Add(new TronTraceListener(true));

            var logger = Framework.Logging.Logger.DefaultFactory.CreateLogger("Modbus");

            var source = new TestData();
            var device = new ModbusSlave("Sample", source, 1502, 1);
            device.Start();

            var options = new StonehengeHostOptions
            {
                Title = "Modbus",
                StartPage = "device",
                ServerPushMode = ServerPushModes.LongPolling,
                PollIntervalSec = 5
            };
            var vue = new VueResourceProvider(logger);
            var provider = StonehengeResourceLoader.CreateDefaultLoader(logger, vue);
            
            provider.Services.AddService(typeof(TestData), source);
            provider.Services.AddService(typeof(ModbusSlave), device);

            var host = new KestrelHost(provider, options);
            if (!host.Start("*", 0))
            {
                Console.WriteLine("Failed to start stonehenge server");
                return;
            }

            var wnd = new HostWindow(host.BaseUrl, "Modbus", new Point(600, 800));
            if (!wnd.Open())
            {
                Console.WriteLine("Failed to open window");
            }
            
            Console.WriteLine("Done.");
        }
    }
}