using System;
using System.Buffers;

namespace XiaoZhi.Unity
{
    public abstract class AudioCodec
    {
        protected bool duplex = false;
        public bool Duplex => duplex;

        protected bool inputReference = false;
        public bool InputReference => inputReference;

        protected bool inputEnabled = false;

        protected bool outputEnabled = false;

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

        public AudioCodec()
        {
        }

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

        public void OutputData(ReadOnlySpan<short> data)
        {
            Write(data);
        }

        public bool InputData(out ReadOnlyMemory<short> data)
        {
            const int duration = 30;
            var inputFrameSize = inputSampleRate / 1000 * duration * inputChannels;
            var temp = new short[inputFrameSize];
            if (Read(temp.AsSpan()) > 0)
            {
                data = temp;
                return true;
            }

            data = default;
            return false;
        }

        protected abstract int Read(Span<short> dest);
        protected abstract int Write(ReadOnlySpan<short> data);
    }
}