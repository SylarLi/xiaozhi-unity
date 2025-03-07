using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UnityAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 3;
        private const int PlayingBufferSec = 3;

        private readonly AudioSource _audioSource;
        private AudioClip _recordingClip;
        private readonly float[] _recordingBuffer;
        private int _recordingPosition;
        private readonly float[] _playbackBuffer;
        private int _playbackEndPosition;
        private int _playbackReadPosition;
        private readonly int _playbackBufferSize;
        private bool _isPlaying;
        private readonly string _deviceName;

        public UnityAudioCodec(int inputSampleRate, int outputSampleRate, bool inputReference)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            this.inputReference = inputReference;
            inputChannels = inputReference ? 2 : 1;
            duplex = true;
            _audioSource = new GameObject(GetType().Name).AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(_audioSource.gameObject);
            _recordingBuffer = new float[inputSampleRate * RecordingBufferSec * inputChannels];

            // 初始化播放缓冲区
            _playbackBufferSize = outputSampleRate * PlayingBufferSec;
            _playbackBuffer = new float[_playbackBufferSize];
            _playbackEndPosition = 0;
            _playbackReadPosition = 0;
            _isPlaying = false;

            // 创建用于流式播放的AudioClip
            var playbackClip = AudioClip.Create("StreamPlayback", _playbackBufferSize, outputChannels, outputSampleRate,
                true, OnAudioRead);
            _audioSource.clip = playbackClip;
            _audioSource.loop = true;

            if (Microphone.devices.Length == 0)
                throw new NotSupportedException("没有可用的录音设备");
            _deviceName = Microphone.devices[^1];
        }

        private void OnAudioRead(float[] data)
        {
            var readLen = Mathf.Min(data.Length,
                (_playbackEndPosition < _playbackReadPosition
                    ? _playbackEndPosition + _playbackBufferSize
                    : _playbackEndPosition) - _playbackReadPosition);
            if (readLen > 0)
            {
                var dataOffset = 0;
                var leftLen = _playbackBufferSize - _playbackReadPosition;
                if (readLen > leftLen)
                {
                    Buffer.BlockCopy(_playbackBuffer, _playbackReadPosition, data, dataOffset, leftLen);
                    _playbackReadPosition = 0;
                    readLen -= leftLen;
                    dataOffset += leftLen;
                }

                Buffer.BlockCopy(_playbackBuffer, _playbackReadPosition, data, dataOffset, readLen);
                _playbackReadPosition += readLen;
            }

            data.AsSpan(readLen).Clear();
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled)
                return 0;
            var samples = data.Length;
            var position = _playbackEndPosition;
            for (var i = 0; i < samples; i++)
                _playbackBuffer[(position + i) % _playbackBufferSize] = data[i] / (float)short.MaxValue;
            _playbackEndPosition = (position + samples) % _playbackBufferSize;
            if (!_isPlaying)
            {
                _audioSource.volume = outputVolume / 100f;
                _audioSource.Play();
                _isPlaying = true;
            }

            return samples;
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            if (!enable)
            {
                _audioSource.Stop();
                _isPlaying = false;
                _playbackReadPosition = 0;
                _playbackEndPosition = 0;
            }

            base.EnableOutput(enable);
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !Microphone.IsRecording(_deviceName))
                return 0;

            // 获取录音数据
            var position = Microphone.GetPosition(_deviceName);
            if (position < 0) return 0;
            // 处理position归零的情况
            if (position < _recordingPosition)
            {
                // 先读取从当前位置到缓冲区末尾的数据
                var remainingSamples = _recordingBuffer.Length - _recordingPosition;
                var firstPartSamples = Math.Min(dest.Length, remainingSamples);
                _recordingClip.GetData(_recordingBuffer, _recordingPosition);

                for (var i = 0; i < firstPartSamples; i++)
                {
                    dest[i] = (short)(_recordingBuffer[i] * short.MaxValue);
                }

                // 如果dest还有空间，从缓冲区开始位置继续读取
                if (firstPartSamples < dest.Length && position > 0)
                {
                    var secondPartSamples = Math.Min(dest.Length - firstPartSamples, position);
                    _recordingClip.GetData(_recordingBuffer, 0);

                    for (var i = 0; i < secondPartSamples; i++)
                    {
                        dest[firstPartSamples + i] = (short)(_recordingBuffer[i] * short.MaxValue);
                    }

                    _recordingPosition = secondPartSamples;
                    return firstPartSamples + secondPartSamples;
                }

                _recordingPosition = 0;
                return firstPartSamples;
            }

            // 正常情况下读取数据
            var samplesToRead = Math.Min(dest.Length, position - _recordingPosition);
            if (samplesToRead <= 0) return 0;

            _recordingClip.GetData(_recordingBuffer, _recordingPosition);
            for (var i = 0; i < samplesToRead; i++)
            {
                dest[i] = (short)(_recordingBuffer[i] * short.MaxValue);
            }

            _recordingPosition = (_recordingPosition + samplesToRead) % _recordingBuffer.Length;
            return samplesToRead;
        }

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            if (enable)
            {
                if (!Microphone.IsRecording(_deviceName))
                {
                    Debug.Log($"开始录音：{_deviceName}");
                    _recordingClip = Microphone.Start(_deviceName, true, RecordingBufferSec, inputSampleRate);
                    _recordingPosition = 0;
                }
            }
            else
            {
                if (Microphone.IsRecording(_deviceName))
                {
                    Debug.Log($"停止录音：{_deviceName}");
                    Microphone.End(_deviceName);
                    _recordingPosition = 0;
                }
            }

            base.EnableInput(enable);
        }
    }
}