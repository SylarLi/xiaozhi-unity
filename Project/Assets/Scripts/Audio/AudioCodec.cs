using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public abstract class AudioCodec : IDisposable
    {
        public const int InputFrameSizeMs = 30;
        
        public struct InputDevice
        {
            public string Name;
            public int Id;
            public int SystemRate;
            public string SpeakerMode;
            public int SpeakerModeChannels;
        }

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

        protected int outputVolume = 70;

        public int OutputVolume => outputVolume;

        protected int inputDeviceIndex;

        public int InputDeviceIndex => inputDeviceIndex;
        
        private Settings _settings;

        private Memory<short> _frameBuffer;
        
        public virtual void Start()
        {
            _settings = new Settings("audio");
            outputVolume = _settings.GetInt("output_volume", outputVolume);
            SetInputDeviceIndex(0);
            EnableInput(true);
            EnableOutput(true);
        }

        public abstract void Dispose();
        
        // --------------------------- output ---------------------------- //
        
        public abstract bool GetOutputSpectrum(out ReadOnlySpan<float> spectrum);
        
        public virtual void SetOutputVolume(int volume)
        {
            outputVolume = volume;
            _settings.SetInt("output_volume", outputVolume);
            _settings.Save();
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

        public abstract bool GetInputSpectrum(out ReadOnlySpan<float> spectrum);
        
        public abstract InputDevice[] GetInputDevices();
        
        public virtual void SetInputDeviceIndex(int index)
        {
            inputDeviceIndex = index;
        }

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