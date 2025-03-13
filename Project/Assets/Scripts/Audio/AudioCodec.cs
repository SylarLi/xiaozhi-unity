using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public abstract class AudioCodec: IDisposable
    {
        protected bool inputEnabled;

        protected bool outputEnabled;

        protected int inputSampleRate = 0;
        public int InputSampleRate => inputSampleRate;

        protected int outputSampleRate = 0;
        public int OutputSampleRate => outputSampleRate;

        protected int inputChannels = 1;
        public int InputChannels => inputChannels;

        protected int outputChannels = 1;
        public int OutputChannels => outputChannels;

        protected int outputVolume = 70;

        public int OutputVolume => outputVolume;

        private Settings _settings;

        private Memory<short> _frameBuffer = new();

        public virtual void SetOutputVolume(int volume)
        {
            outputVolume = volume;
            _settings.SetInt("output_volume", outputVolume);
            _settings.Save();
        }

        public virtual void EnableInput(bool enable)
        {
            inputEnabled = enable;
        }

        public virtual void EnableOutput(bool enable)
        {
            outputEnabled = enable;
        }

        public void Start()
        {
            _settings = new Settings("audio");
            outputVolume = _settings.GetInt("output_volume", outputVolume);
            EnableInput(true);
            EnableOutput(true);
        }
        
        public abstract void Dispose();

        public abstract void SwitchInput();
        
        public void OutputData(ReadOnlySpan<short> data)
        {
            Write(data);
        }

        public bool InputData(out ReadOnlySpan<short> data)
        {
            const int duration = 30;
            var frameSize = inputSampleRate / 1000 * duration * inputChannels;
            if (_frameBuffer.Length < frameSize) _frameBuffer = new short[Mathf.NextPowerOfTwo(frameSize)];
            var span = _frameBuffer[..frameSize].Span;
            var len = Read(span);
            _frameBuffer[len..].Span.Clear();
            data = span;
            return len > 0;
        }

        protected abstract int Read(Span<short> dest);
        protected abstract int Write(ReadOnlySpan<short> data);
    }
}