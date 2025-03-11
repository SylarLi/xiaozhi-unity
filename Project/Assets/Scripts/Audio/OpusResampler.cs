using System;
using System.Buffers;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class OpusResampler : IDisposable
    {
        protected int inputSampleRate;
        public int InputSampleRate => inputSampleRate;

        protected int outputSampleRate;
        public int OutputSampleRate => outputSampleRate;

        protected IntPtr resamplerState;

        public void Configure(int inputSampleRate, int outputSampleRate)
        {
            var encode = inputSampleRate > outputSampleRate ? 1 : 0;
            if (resamplerState != IntPtr.Zero) Marshal.FreeHGlobal(resamplerState);
            resamplerState = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OpusWrapper.silk_resampler_state_struct)));
            var ret = OpusWrapper.silk_resampler_init(resamplerState, inputSampleRate, outputSampleRate, encode);
            if (ret != 0)
            {
                Debug.LogError($"Failed to initialize resampler: {ret}");
                return;
            }

            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            Debug.Log(
                $"Resampler configured with input sample rate {inputSampleRate} and output sample rate {outputSampleRate}");
        }

        public void Process(ReadOnlySpan<short> input, Span<short> output)
        {
            int ret;
            unsafe
            {
                fixed (short* pin = input)
                fixed (short* pout = output)
                    ret = OpusWrapper.silk_resampler(resamplerState, pout, pin, input.Length);
            }

            if (ret != 0)
                Debug.Log($"Failed to process resampler: {ret}");
        }

        public int GetOutputSamples(int inputSamples)
        {
            return inputSamples * outputSampleRate / inputSampleRate;
        }

        public void Dispose()
        {
            if (resamplerState != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(resamplerState);
                resamplerState = IntPtr.Zero;
            }
        }

        ~OpusResampler()
        {
            Dispose();
        }
    }
}