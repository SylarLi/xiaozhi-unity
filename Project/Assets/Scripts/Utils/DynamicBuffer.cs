using System;

namespace XiaoZhi.Unity
{
    public class DynamicBuffer<T>
    {
        private Memory<T> _buffer;
        public ref Memory<T> Memory => ref _buffer;
        public int Capacity => _buffer.Length;
        private int _count;
        public int Count => _count;

        public void SetCount(int value)
        {
            _count = value;
        }

        public DynamicBuffer(int initialCapacity = 1024)
        {
            _buffer = new Memory<T>(new T[initialCapacity]);
            _count = 0;
        }

        public void Write(ReadOnlySpan<T> data)
        {
            var requiredCapacity = _count + data.Length;
            Tools.EnsureMemory(ref _buffer, requiredCapacity);
            data.CopyTo(_buffer.Span[_count..]);
            _count += data.Length;
        }

        public void Write(T item)
        {
            var requiredCapacity = _count + 1;
            Tools.EnsureMemory(ref _buffer, requiredCapacity);
            _buffer.Span[_count] = item;
            _count++;
        }
        
        public ReadOnlySpan<T> Read()
        {
            return _buffer.Span[.._count];
        }

        public ReadOnlySpan<T> Read(int count)
        {
            if (count > _count)
                throw new ArgumentException("Requested count exceeds available data", nameof(count));
            return _buffer.Span[..count];
        }

        public void Clear()
        {
            _count = 0;
        }
    }
}