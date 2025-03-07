using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace XiaoZhi.Unity
{
    public class OpusResampler : IDisposable
    {
        protected int inputSampleRate;
        public int InputSampleRate => inputSampleRate;

        protected int outputSampleRate;
        public int OutputSampleRate => outputSampleRate;

        protected IntPtr resamplerState;

        public OpusResampler()
        {
            resamplerState = IntPtr.Zero;
        }

        public void Configure(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
        }

        public void Process(ReadOnlySpan<short> input, Span<short> output)
        {
        }

        public int GetOutputSamples(int inputSamples)
        {
            return inputSamples * outputSampleRate / inputSampleRate;
        }

        public void Dispose()
        {
        }
    }
}