using System;

namespace XiaoZhi.Unity
{
    public class RingBuffer<T>
    {
        private readonly Memory<T> _buffer;
        private readonly int _capacity;
        private int _writePosition;
        private int _readPosition;
        private int _count;
        private readonly object _locker = new();

        public int Count => _count;
        public int Capacity => _capacity;
        public bool IsEmpty => _count == 0;
        public bool IsFull => _count == _capacity;
        public int WritePosition => _writePosition;
        public int ReadPosition => _readPosition;

        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
            _writePosition = 0;
            _readPosition = 0;
            _count = 0;
        }

        public bool TryWrite(ReadOnlySpan<T> data)
        {
            lock (_locker)
            {
                if (data.Length > _capacity - _count)
                    return false;
                var writeCount = Math.Min(data.Length, _capacity - _writePosition);
                data[..writeCount].CopyTo(_buffer.Span[_writePosition..]);
                if (writeCount < data.Length) data[writeCount..].CopyTo(_buffer.Span);
                _writePosition = (_writePosition + data.Length) % _capacity;
                _count += data.Length;
                return true;   
            }
        }

        public bool TryRead(Span<T> destination)
        {
            lock (_locker)
            {
                if (_count == 0 || destination.Length == 0 || _count < destination.Length)
                    return false;
                var readCount = destination.Length;
                var firstRead = Math.Min(readCount, _capacity - _readPosition);
                _buffer.Span.Slice(_readPosition, firstRead).CopyTo(destination);
                if (firstRead < readCount)
                    _buffer.Span[..(readCount - firstRead)].CopyTo(destination[firstRead..]);
                _readPosition = (_readPosition + readCount) % _capacity;
                _count -= readCount;
                return true;
            }
        }

        public bool TryReadAt(int position, Span<T> destination)
        {
            lock (_locker)
            {
                if (destination.Length == 0 || destination.Length > _capacity)
                    return false;
                position = Tools.Repeat(position, _capacity);
                var readCount = destination.Length;
                var firstRead = Math.Min(readCount, _capacity - position);
                _buffer.Span.Slice(position, firstRead).CopyTo(destination);
                if (firstRead < readCount)
                    _buffer.Span[..(readCount - firstRead)].CopyTo(destination[firstRead..]);
                return true;
            }
        }
        
        public void Clear()
        {
            lock (_locker)
            {
                _writePosition = 0;
                _readPosition = 0;
                _count = 0;
            }
        }
    }
}