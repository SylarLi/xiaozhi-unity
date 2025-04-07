using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public abstract class WakeService : IDisposable
    {
        protected string lastDetectedWakeWord = string.Empty;
        
        private bool _isVoiceDetected;

        public event Action<bool> OnVadStateChanged;
        public event Action<string> OnWakeWordDetected;

        public abstract void Initialize(int sampleRate);

        public abstract void Start();
        
        public abstract void Feed(ReadOnlySpan<short> data);
        
        public abstract void Stop();

        public abstract bool IsRunning { get; }

        public string LastDetectedWakeWord => lastDetectedWakeWord;
        
        protected void RaiseWakeWordDetected(string wakeWord)
        {
            OnWakeWordDetected?.Invoke(wakeWord);
        }

        public bool IsVoiceDetected
        {
            get => _isVoiceDetected;
            set
            {
                if (_isVoiceDetected != value)
                {
                    _isVoiceDetected = value;
                    Debug.Log($"vad: {_isVoiceDetected}");
                    RaiseVadStateChanged(_isVoiceDetected);
                }
            }
        }

        protected void RaiseVadStateChanged(bool speaking)
        {
            OnVadStateChanged?.Invoke(speaking);
        }

        public abstract int ReadVadBuffer(ref Memory<short> buffer);
        
        public abstract void ClearVadBuffer();

        public virtual void Dispose()
        {
            Stop();
        }
    }
} 