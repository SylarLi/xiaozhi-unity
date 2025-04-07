using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public abstract class AudioCodec : IDisposable
    {
        public struct InputDevice
        {
            public string Name;
            public int Id;
            public int SystemRate;
            public string SpeakerMode;
            public int SpeakerModeChannels;
        }
        
        public const int InputFrameSizeMs = 30;
        
        public const int SpectrumWindowSize = 1024;

        protected bool inputEnabled;

        protected bool outputEnabled;
        
        protected int inputChannels = 1;
        public int InputChannels => inputChannels;

        protected int outputChannels = 1;
        public int OutputChannels => outputChannels;

        protected int inputSampleRate = 0;
        public int InputSampleRate => inputSampleRate;

        protected int outputSampleRate = 0;
        public int OutputSampleRate => outputSampleRate;

        protected int outputVolume = 50;

        public int OutputVolume => outputVolume;
        
        private Settings _settings;

        private Memory<short> _frameBuffer;
        
        public virtual void Start()
        {
            EnableInput(true);
            EnableOutput(true);
        }

        public abstract void Dispose();
        
        // --------------------------- output ---------------------------- //
        
        public abstract bool GetOutputSpectrum(bool fft, out ReadOnlySpan<float> spectrum);
        
        public virtual void SetOutputVolume(int volume)
        {
            outputVolume = volume;
        }

        public virtual void EnableOutput(bool enable)
        {
            outputEnabled = enable;
        }
        
        public void OutputData(ReadOnlySpan<short> data)
        {
            try
            {
                Write(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        
        protected abstract int Write(ReadOnlySpan<short> data);
        
        // --------------------------- input ---------------------------- //
        
        public abstract bool GetInputDevice(out InputDevice device);

        public abstract bool GetInputSpectrum(bool fft, out ReadOnlySpan<float> spectrum);

        public virtual void EnableInput(bool enable)
        {
            inputEnabled = enable;
        }
        
        public bool InputData(out ReadOnlySpan<short> data)
        {
            var frameSize = inputSampleRate / 1000 * InputFrameSizeMs * inputChannels;
            var span = Tools.EnsureMemory(ref _frameBuffer, frameSize);
            var len = 0;
            try
            {
                len = Read(span);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _frameBuffer[len..frameSize].Span.Clear();
            data = span;
            return len > 0;
        }

        protected abstract int Read(Span<short> dest);
    }
}