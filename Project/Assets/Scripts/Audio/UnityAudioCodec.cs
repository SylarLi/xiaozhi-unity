using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UnityAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 4;
        private const int PlayingBufferSec = 4;

        private readonly AudioSource _audioSource;
        private AudioClip _recordingClip;
        private float[] _recordingBuffer;
        private readonly int _recordingBufferSize;
        private int _recordingPosition;
        private readonly float[] _playbackBuffer;
        private int _playbackEndPosition;
        private int _playbackReadPosition;
        private readonly int _playbackBufferSize;
        private bool _isPlaying;
        private readonly string _deviceName;

        public UnityAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            inputChannels = 1;
            _audioSource = new GameObject(GetType().Name).AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(_audioSource.gameObject);
            _recordingBufferSize = inputSampleRate * RecordingBufferSec * inputChannels;
            _playbackBufferSize = outputSampleRate * PlayingBufferSec;
            _playbackBuffer = new float[_playbackBufferSize];
            _playbackEndPosition = 0;
            _playbackReadPosition = 0;
            _isPlaying = false;
            var playbackClip = AudioClip.Create("StreamPlayback", _playbackBufferSize, outputChannels, outputSampleRate,
                true, OnAudioRead);
            _audioSource.clip = playbackClip;
            _audioSource.loop = true;
            if (Microphone.devices.Length == 0)
                throw new NotSupportedException("没有可用的录音设备");
            _deviceName = Microphone.devices[0];
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
                    Array.Copy(_playbackBuffer, _playbackReadPosition, data, dataOffset, leftLen);
                    _playbackReadPosition = 0;
                    readLen -= leftLen;
                    dataOffset += leftLen;
                }

                Array.Copy(_playbackBuffer, _playbackReadPosition, data, dataOffset, readLen);
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
                _isPlaying = true;
                UniTask.Post(() =>
                {
                    if (!_audioSource.isPlaying)
                    {
                        _audioSource.volume = outputVolume / 100f;
                        _audioSource.Play();
                    }
                });
            }

            return samples;
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            if (!enable)
            {
                _playbackReadPosition = 0;
                _playbackEndPosition = 0;
                _isPlaying = false;
                UniTask.Post(() =>
                {
                    if (_audioSource.isPlaying)
                        _audioSource.Stop();
                });
            }

            base.EnableOutput(enable);
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !Microphone.IsRecording(_deviceName))
                return 0;
            var position = Microphone.GetPosition(_deviceName);
            if (position < 0) return 0;
            _recordingBuffer ??= new float[dest.Length];
            if (_recordingBuffer.Length < dest.Length) Array.Resize(ref _recordingBuffer, Mathf.NextPowerOfTwo(dest.Length));
            var firstRead = 0;
            if (position < _recordingPosition)
            {
                firstRead = Math.Min(dest.Length, _recordingBufferSize - _recordingPosition);
                _recordingClip.GetData(_recordingBuffer, _recordingPosition);
                for (var i = 0; i < firstRead; i++) dest[i] = (short)(_recordingBuffer[i] * short.MaxValue);
                _recordingPosition = 0;
            }

            var samplesToRead = Math.Min(dest.Length - firstRead, position - _recordingPosition);
            if (samplesToRead <= 0) return firstRead;
            _recordingClip.GetData(_recordingBuffer, _recordingPosition);
            for (var i = 0; i < samplesToRead; i++) dest[i + firstRead] = (short)(_recordingBuffer[i] * short.MaxValue);
            _recordingPosition += samplesToRead;
            return firstRead + samplesToRead;
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