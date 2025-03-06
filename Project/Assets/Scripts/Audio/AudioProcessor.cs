using System;
using System.Threading;

public abstract class AudioProcessor : IDisposable
{
    protected int channels;
    protected bool reference;
    protected bool isRunning;
    public bool IsRunning => isRunning;
    protected CancellationTokenSource processingCancellation;

    public event Action<Memory<short>> OnOutputData;

    protected AudioProcessor()
    {
        isRunning = false;
    }
    
    public abstract void Initialize(int channels, bool reference);
    
    public abstract void Input(ReadOnlyMemory<short> data);
    
    public virtual void Start()
    {
        if (isRunning) return;
        processingCancellation = new CancellationTokenSource();
        isRunning = true;
    }
    
    public virtual void Stop()
    {
        if (!isRunning) return;
        processingCancellation?.Cancel();
        isRunning = false;
    }

    protected virtual void RaiseOutputData(Memory<short> data)
    {
        OnOutputData?.Invoke(data);
    }

    public virtual void Dispose()
    {
        Stop();
        processingCancellation?.Dispose();
    }
}