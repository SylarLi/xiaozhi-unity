using System;
using System.Collections.Generic;
using System.Threading;

namespace XiaoZhi.Unity
{
    public abstract class WakeWordDetect : IDisposable
    {
        protected int channels;
        protected bool reference;
        protected bool isDetectionRunning;
        protected string lastDetectedWakeWord;
        protected List<string> wakeWords;
        protected CancellationTokenSource detectionCancellation;

        public event Action<string> OnWakeWordDetected;
        public event Action<bool> OnVadStateChanged;

        protected WakeWordDetect()
        {
            wakeWords = new List<string>();
            isDetectionRunning = false;
            lastDetectedWakeWord = string.Empty;
        }

        public abstract void Initialize(int channels, bool reference);

        public abstract void Feed(ReadOnlyMemory<short> data);

        public virtual void StartDetection()
        {
            if (isDetectionRunning) return;
            detectionCancellation = new CancellationTokenSource();
            isDetectionRunning = true;
        }

        public virtual void StopDetection()
        {
            if (!isDetectionRunning) return;
            detectionCancellation?.Cancel();
            isDetectionRunning = false;
        }

        public bool IsDetectionRunning => isDetectionRunning;

        public string LastDetectedWakeWord => lastDetectedWakeWord;

        public abstract void EncodeWakeWordData();

        public abstract bool GetWakeWordOpus(out Memory<byte> opus);

        protected virtual void RaiseWakeWordDetected(string wakeWord)
        {
            OnWakeWordDetected?.Invoke(wakeWord);
        }

        protected virtual void RaiseVadStateChanged(bool speaking)
        {
            OnVadStateChanged?.Invoke(speaking);
        }

        public virtual void Dispose()
        {
            StopDetection();
            detectionCancellation?.Dispose();
        }
    }
}