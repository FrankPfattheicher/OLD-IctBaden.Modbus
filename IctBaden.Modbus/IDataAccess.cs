namespace IctBaden.Modbus
{
    public interface IDataAccess
    {
        /// <summary>
        /// Signals accessibility of source.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Reconnect to the data source
        /// </summary>
        void ReConnect();

        /// <summary>
        /// Using Modbus function 1
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        bool[] ReadCoils(int offset, int count);

        /// <summary>
        /// Using Modbus function 2
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        bool[] ReadInputDiscretes(int offset, int count);

        /// <summary>
        /// Using Modbus function 3
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ushort[] ReadHoldingRegisters(int offset, int count);

        /// <summary>
        /// Using Modbus function 4 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ushort[] ReadInputRegisters(int offset, int count);

        /// <summary>
        /// Using Modbus function 5
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool WriteCoils(int offset, bool[] values);

        /// <summary>
        /// Using Modbus function 6
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool WriteRegisters(int offset, ushort[] values);
    }
}
