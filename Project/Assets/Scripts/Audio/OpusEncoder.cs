using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class OpusEncoder : IDisposable
    {
        private const int MaxOpusPacketSize = 1500;

        private IntPtr _encoder;
        private Memory<short> _inBuffer;
        private readonly int _frameSize;

        public OpusEncoder(int sampleRate, int channels, int durationMs)
        {
            _encoder = OpusWrapper.opus_encoder_create(sampleRate, channels, OpusWrapper.OPUS_APPLICATION_VOIP,
                out var error);
            if (error != 0)
                throw new Exception($"Failed to create audio encoder, error code: {error}");
            SetDtx(true);
            SetComplexity(5);
            _frameSize = sampleRate / 1000 * channels * durationMs;
            _inBuffer = new Memory<short>();
        }

        public void Dispose()
        {
            if (_encoder == IntPtr.Zero) return;
            OpusWrapper.opus_encoder_destroy(_encoder);
            _encoder = IntPtr.Zero;
        }

        public bool Encode(ReadOnlySpan<short> pcm, Action<ReadOnlyMemory<byte>> handler)
        {
            if (_encoder == IntPtr.Zero)
            {
                Debug.LogError("Audio encoder is not configured");
                return false;
            }

            // Append new PCM data to buffer
            Memory<short> newBuffer = new short[_inBuffer.Length + pcm.Length];
            _inBuffer.CopyTo(newBuffer);
            pcm.CopyTo(newBuffer[_inBuffer.Length..].Span);
            _inBuffer = newBuffer;
            while (_inBuffer.Length >= _frameSize)
            {
                var opus = new byte[MaxOpusPacketSize];
                int encodedBytes;
                unsafe
                {
                    fixed (short* inPtr = _inBuffer.Span)
                    fixed (byte* outPtr = opus)
                        encodedBytes = OpusWrapper.opus_encode(_encoder, inPtr, _frameSize, (char*)outPtr, opus.Length);
                }

                if (encodedBytes < 0)
                    throw new Exception("OpusWrapper.opus_encode error: " + encodedBytes);
                handler?.Invoke(new ReadOnlyMemory<byte>(opus, 0, encodedBytes));
                _inBuffer = _inBuffer[_frameSize..];
            }

            return true;
        }

        public void ResetState()
        {
            if (_encoder == IntPtr.Zero) return;
            OpusWrapper.opus_encoder_ctl(_encoder, OpusWrapper.OPUS_RESET_STATE);
        }

        public void SetDtx(bool enable)
        {
            if (_encoder == IntPtr.Zero) return;
            OpusWrapper.opus_encoder_ctl(_encoder, OpusWrapper.OPUS_SET_DTX_REQUEST, enable ? 1 : 0);
        }

        public void SetComplexity(int complexity)
        {
            if (_encoder == IntPtr.Zero) return;
            OpusWrapper.opus_encoder_ctl(_encoder, OpusWrapper.OPUS_SET_COMPLEXITY_REQUEST, complexity);
        }
    }
}