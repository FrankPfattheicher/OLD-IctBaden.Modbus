using System;
using System.Threading.Tasks;
using Xunit;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable IntroduceOptionalParameters.Global

namespace IctBaden.Modbus.Test
{
    public static class AssertWait
    {
        public static void Max(int milliseconds, Func<bool> condition)
        {
            Max(milliseconds, condition, null);
        }
        public static void Max(int milliseconds, Func<bool> condition, string message)
        {
            while (milliseconds > 0)
            {
                if (condition()) return;
                Task.Delay(100).Wait();
                milliseconds -= 100;
            }

            Assert.True(false, message);
        }
    }
}