using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class OpusEncoder : IDisposable
    {
        private const int MaxOpusPacketSize = 1500;

        private IntPtr _encoder;
        private readonly RingBuffer<short> _inBuffer;
        private readonly Memory<short> _frameBuffer;
        private readonly Memory<byte> _outBuffer;
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
            _frameBuffer = new short[_frameSize];
            _inBuffer = new RingBuffer<short>(4096);
            _outBuffer = new byte[MaxOpusPacketSize];
        }

        public bool Encode(ReadOnlySpan<short> pcm, Action<ReadOnlyMemory<byte>> handler)
        {
            if (_encoder == IntPtr.Zero)
            {
                Debug.LogError("Audio encoder is not configured");
                return false;
            }

            if (!_inBuffer.TryWrite(pcm))
            {
                Debug.LogError("Audio encoder buffer is full!");
                return false;
            }
            
            while (_inBuffer.TryRead(_frameBuffer.Span))
            {
                int encodedBytes;
                unsafe
                {
                    fixed (short* inPtr = _frameBuffer.Span)
                    fixed (byte* outPtr = _outBuffer.Span)
                        encodedBytes = OpusWrapper.opus_encode(_encoder, inPtr, _frameSize, (char*)outPtr,
                            _outBuffer.Length);
                }

                if (encodedBytes < 0)
                    throw new Exception("OpusWrapper.opus_encode error: " + encodedBytes);
                handler.Invoke(_outBuffer.Slice(0, encodedBytes));
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
        
        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }
        
        private void InternalDispose()
        {
            if (_encoder == IntPtr.Zero) return;
            OpusWrapper.opus_encoder_destroy(_encoder);
            _encoder = IntPtr.Zero;
        }

        ~OpusEncoder()
        {
            InternalDispose();
        }
    }
}