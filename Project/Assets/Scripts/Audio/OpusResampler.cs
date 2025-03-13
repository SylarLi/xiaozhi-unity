using System;
using System.Buffers;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class OpusResampler : IDisposable
    {
        private int _inputSampleRate;
        public int InputSampleRate => _inputSampleRate;

        private int _outputSampleRate;
        public int OutputSampleRate => _outputSampleRate;

        private IntPtr _resamplerState;

        private Memory<short> _outputBuffer;

        public void Configure(int inputSampleRate, int outputSampleRate)
        {
            var encode = inputSampleRate > outputSampleRate ? 1 : 0;
            if (_resamplerState != IntPtr.Zero) Marshal.FreeHGlobal(_resamplerState);
            _resamplerState = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OpusWrapper.silk_resampler_state_struct)));
            var ret = OpusWrapper.silk_resampler_init(_resamplerState, inputSampleRate, outputSampleRate, encode);
            if (ret != 0)
            {
                Debug.LogError($"Failed to initialize resampler: {ret}");
                return;
            }

            _inputSampleRate = inputSampleRate;
            _outputSampleRate = outputSampleRate;
            _outputBuffer = new Memory<short>();
            Debug.Log(
                $"Resampler configured with input sample rate {inputSampleRate} and output sample rate {outputSampleRate}");
        }

        public bool Process(ReadOnlySpan<short> input, out ReadOnlySpan<short> output)
        {
            var outputSamples = GetOutputSamples(input.Length);
            Tools.EnsureMemory(ref _outputBuffer, outputSamples);
            int ret;
            unsafe
            {
                fixed (short* pin = input)
                fixed (short* pout = _outputBuffer.Span)
                    ret = OpusWrapper.silk_resampler(_resamplerState, pout, pin, input.Length);
            }

            if (ret != 0) Debug.Log($"Failed to process resampler: {ret}");
            output = _outputBuffer.Slice(0, outputSamples).Span;
            return ret == 0;
        }

        public int GetOutputSamples(int inputSamples)
        {
            return inputSamples * _outputSampleRate / _inputSampleRate;
        }

        public void Dispose()
        {
            if (_resamplerState != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_resamplerState);
                _resamplerState = IntPtr.Zero;
            }
        }

        ~OpusResampler()
        {
            Dispose();
        }
    }
}