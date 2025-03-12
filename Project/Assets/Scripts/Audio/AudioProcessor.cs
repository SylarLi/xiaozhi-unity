using System;
using System.Buffers;
using System.Threading;

namespace XiaoZhi.Unity
{
    public abstract class AudioProcessor : IDisposable
    {
        protected int channels;
        protected bool reference;
        protected bool isRunning;
        public bool IsRunning => isRunning;

        public event Action<ReadOnlyMemory<short>> OnOutputData;

        protected AudioProcessor()
        {
            isRunning = false;
        }

        public abstract void Initialize(int channels);

        public abstract void Input(ReadOnlySpan<short> data);

        public virtual void Start()
        {
            if (isRunning) return;
            isRunning = true;
        }

        public virtual void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
        }

        protected virtual void RaiseOutputData(ReadOnlyMemory<short> data)
        {
            OnOutputData?.Invoke(data);
        }

        public virtual void Dispose()
        {
            Stop();
        }
    }
}