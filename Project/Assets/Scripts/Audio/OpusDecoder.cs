using System;

namespace XiaoZhi.Unity
{
    public class OpusDecoder : IDisposable
    {
        private int _sampleRate;
        private IntPtr _decoder;
        private readonly Memory<short> _frameData;

        public OpusDecoder(int sampleRate, int channels, int durationMs = 60)
        {
            _decoder = OpusWrapper.opus_decoder_create(sampleRate, channels, out var error);
            if (error != 0)
                throw new Exception("OpusWrapper.opus_decoder_create error: " + error);
            _frameData = new short[sampleRate / 1000 * channels * durationMs];
        }

        public void ResetState()
        {
            if (_decoder == IntPtr.Zero) return;
            OpusWrapper.opus_decoder_ctl(_decoder, OpusWrapper.OPUS_RESET_STATE);
        }

        public bool Decode(ReadOnlySpan<byte> opus, out ReadOnlySpan<short> pcm)
        {
            pcm = default;
            if (_decoder == IntPtr.Zero)
            {
                Console.WriteLine("Audio decoder is not configured");
                return false;
            }

            int decodeBytes;
            unsafe
            {
                fixed (byte* b = opus)
                fixed (short* p = _frameData.Span)
                    decodeBytes = OpusWrapper.opus_decode(_decoder, (char*)b, opus.Length, p, _frameData.Length, 0);
            }

            if (decodeBytes < 0) throw new Exception("OpusWrapper.opus_decode error: " + decodeBytes);
            pcm = _frameData.Span;
            return true;
        }

        public void Dispose()
        {
            if (_decoder == IntPtr.Zero) return;
            OpusWrapper.opus_decoder_destroy(_decoder);
            _decoder = IntPtr.Zero;
        }

        ~OpusDecoder()
        {
            Dispose();
        }
    }
}