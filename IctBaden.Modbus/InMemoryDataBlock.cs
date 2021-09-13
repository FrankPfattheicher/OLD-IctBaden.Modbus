namespace IctBaden.Modbus
{
    /// <summary>
    /// Represents an in memory data block
    /// representing device data
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class InMemoryDataBlock<TData> 
    {
        private readonly ushort _offset;
        private readonly TData[] _data;
        
        public InMemoryDataBlock(ushort offset, ushort size)
        {
            _offset = offset;
            _data = new TData[size];
        }

        public TData this[ushort offset]
        {
            get =>
                offset < _offset + _data.Length
                    ? _data[offset]
                    : default;
            set
            {
                if(offset >= _offset + _data.Length) return;
                _data[offset] = value;
            }
        }
    }
}