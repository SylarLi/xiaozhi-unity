using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public abstract class AudioCodec : IDisposable
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

        private Memory<short> _frameBuffer;

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
        }

        public abstract void Dispose();

        public abstract void SwitchInputDevice();

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

        public bool InputData(out ReadOnlySpan<short> data)
        {
            const int duration = 30;
            var frameSize = inputSampleRate / 1000 * duration * inputChannels;
            Tools.EnsureMemory(ref _frameBuffer, frameSize);
            var span = _frameBuffer[..frameSize].Span;
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
        protected abstract int Write(ReadOnlySpan<short> data);
    }
}