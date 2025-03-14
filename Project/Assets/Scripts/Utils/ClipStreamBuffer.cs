using System;

namespace XiaoZhi.Unity
{
    public class ClipStreamBuffer
    {
        private readonly Memory<short> _buffer;
        private readonly int _capacity;
        private int _writePosition;
        private int _readPosition;

        public int Capacity => _capacity;
        public int WritePosition => _writePosition;
        public int ReadPosition => _readPosition;

        public ClipStreamBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new short[capacity];
            _buffer.Span.Clear();
            _writePosition = 0;
            _readPosition = 0;
        }

        public void Buffer(ReadOnlySpan<short> data)
        {
            if (data.Length > _capacity)
                throw new ArgumentOutOfRangeException();
            var writeCount = Math.Min(data.Length, _capacity - _writePosition);
            data[..writeCount].CopyTo(_buffer.Span[_writePosition..]);
            if (writeCount < data.Length) data[writeCount..].CopyTo(_buffer.Span);
            _writePosition = (_writePosition + data.Length) % _capacity;
        }

        public int Read(Span<short> destination)
        {
            if (destination.Length > _capacity)
                throw new ArgumentOutOfRangeException();
            var readLeft = _writePosition - _readPosition;
            if (readLeft < 0) readLeft += _capacity;
            var readCount = Math.Min(readLeft, destination.Length);
            if (readCount > 0)
            {
                var firstRead = Math.Min(readCount, _capacity - _readPosition);
                _buffer.Span.Slice(_readPosition, firstRead).CopyTo(destination);
                if (firstRead < readCount)
                    _buffer.Span[..(readCount - firstRead)].CopyTo(destination[firstRead..]);
            }
            
            destination[readCount..].Clear();
            _readPosition = (_readPosition + destination.Length) % _capacity;
            if (readLeft < destination.Length) BufferZero(destination.Length - readLeft);
            return readCount;
        }

        public void ReadAt(int position, Span<short> destination)
        {
            if (destination.Length > _capacity)
                throw new ArgumentOutOfRangeException();
            var readCount = destination.Length;
            var firstRead = Math.Min(readCount, _capacity - position);
            _buffer.Span.Slice(position, firstRead).CopyTo(destination);
            if (firstRead < readCount)
                _buffer.Span.Slice(0, readCount - firstRead).CopyTo(destination.Slice(firstRead));
        }

        public void Clear()
        {
            _writePosition = 0;
            _readPosition = 0;
        }
        
        private void BufferZero(int length)
        {
            if (length > _capacity)
                throw new ArgumentOutOfRangeException();
            var writeCount = Math.Min(length, _capacity - _writePosition);
            _buffer.Span.Slice(_writePosition, writeCount).Clear();
            if (writeCount < length) _buffer.Span[..(length - writeCount)].Clear();
            _writePosition = (_writePosition + length) % _capacity;
        }
    }
}