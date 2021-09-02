using System;
using System.Text;
using IctBaden.Framework.AppUtils;

// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo

namespace IctBaden.Modbus
{
    public class ThreadName
    {
        // /include/uapi/linux/prctl.h
        // #define PR_SET_NAME    15		/* Set process name */
        // #define PR_GET_NAME    16		/* Get process name */
        private const int PR_SET_NAME = 15;
        [System.Runtime.InteropServices.DllImport("libc")]
        // Linux only  
        private static extern int prctl(int option, byte[] arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);

        public static void Set(string name)
        {
            try
            {
                if (SystemInfo.Platform == Platform.Linux)
                {
                    // s = prctl(PR_SET_NAME,"myProcess\0",NULL,NULL,NULL); // name: myProcess
                    var bytes = Encoding.ASCII.GetBytes(name + "\0");
                    var result = prctl(PR_SET_NAME, bytes, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch
            {
                // ignore
            }
        }
        
    }
}