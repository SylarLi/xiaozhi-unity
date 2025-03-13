using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using XiaoZhi.Audio;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UnityAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 4;
        private const int PlayingBufferSec = 4;

        private readonly AudioSource _audioSource;
        private AudioClip _recordingClip;
        private int _recordingPosition;
        private readonly RingBuffer<short> _playbackBuffer;
        private Memory<short> _shortBuffer;
        private float[] _floatBuffer;
        private bool _isPlaying;
        private int _deviceIndex;

        private IntPtr _aecmInst;
        private int _aecmSync;

        public UnityAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            _audioSource = new GameObject(GetType().Name).AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(_audioSource.gameObject);
            var playbackBufferSize = outputSampleRate * PlayingBufferSec;
            _playbackBuffer = new RingBuffer<short>(playbackBufferSize);
            _isPlaying = false;
            var playbackClip = AudioClip.Create("StreamPlayback", playbackBufferSize, outputChannels, outputSampleRate,
                true, OnAudioRead);
            _audioSource.clip = playbackClip;
            _audioSource.loop = true;
            _deviceIndex = 0;

            _aecmInst = AECMWrapper.AECM_Create();
            AECMWrapper.AECM_Init(_aecmInst, 16000);
            AECMWrapper.AECM_SetConfig(_aecmInst);
        }

        public override void Dispose()
        {
            if (_aecmInst != IntPtr.Zero)
            {
                AECMWrapper.AECM_Free(_aecmInst);
                _aecmInst = IntPtr.Zero;
            }
        }

        ~UnityAudioCodec()
        {
            Dispose();
        }

        private void OnAudioRead(float[] data)
        {
            var readPos = _playbackBuffer.ReadPosition;
            var readLen = Mathf.Min(data.Length, _playbackBuffer.Count);
            Tools.EnsureMemory(ref _shortBuffer, readLen);
            var readBuffer = _shortBuffer.Span;
            if (!_playbackBuffer.TryRead(readBuffer[..readLen])) readLen = 0;
            for (var i = 0; i < readLen; i++)
                data[i] = (float)readBuffer[i] / short.MaxValue;
            data.AsSpan(readLen).Clear();
            var readTime = DateTime.Now;
            UniTask.Post(() =>
            {
                readPos += (int)((DateTime.Now - readTime).TotalSeconds * inputSampleRate);
                _aecmSync = readPos - _audioSource.timeSamples;
            });
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled)
                return 0;
            if (!_playbackBuffer.TryWrite(data))
                return 0;
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

            return data.Length;
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            if (!enable)
            {
                _playbackBuffer.Clear();
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
            var deviceName = Microphone.devices[_deviceIndex];
            if (string.IsNullOrEmpty(deviceName))
                return 0;
            if (!inputEnabled || !Microphone.IsRecording(deviceName))
                return 0;
            var position = Microphone.GetPosition(deviceName);
            if (position < 0) return 0;
            if (position < _recordingPosition) position += _recordingClip.samples;
            var readLen = Mathf.Min(dest.Length, position - _recordingPosition);
            _recordingPosition = ReadClip(_recordingClip, _recordingPosition, dest[..readLen]);
            dest[readLen..].Clear();
            Tools.EnsureMemory(ref _shortBuffer, readLen);
            var syncPos = _aecmSync + _audioSource.timeSamples;
            if (syncPos < 0) syncPos += _playbackBuffer.Count;
            if (syncPos >= _playbackBuffer.Count) syncPos -= _playbackBuffer.Count;
            _playbackBuffer.TryReadAt(syncPos, _shortBuffer.Span);
            unsafe
            {
                fixed (short* nearInput = dest)
                fixed (short* farInput = _shortBuffer.Span)
                    AECMWrapper.AECM_Process(_aecmInst, nearInput, farInput, dest.Length, inputSampleRate);
            }

            return readLen;
        }

        private int ReadClip(AudioClip clip, int position, Span<short> dest)
        {
            _floatBuffer ??= new float[dest.Length];
            Tools.EnsureArray(ref _floatBuffer, dest.Length);
            var clipSamples = clip.samples;
            var endPosition = (position + dest.Length) % clipSamples;
            var firstRead = 0;
            if (endPosition < position)
            {
                firstRead = Math.Min(dest.Length, clipSamples - position);
                clip.GetData(_floatBuffer, position);
                for (var i = 0; i < firstRead; i++) dest[i] = (short)(_floatBuffer[i] * short.MaxValue);
                position = 0;
            }

            var nextRead = endPosition - position;
            if (nextRead > 0)
            {
                clip.GetData(_floatBuffer, position);
                for (var i = 0; i < nextRead; i++) dest[i + firstRead] = (short)(_floatBuffer[i] * short.MaxValue);
            }

            return endPosition;
        }

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            var deviceName = GetDeviceName();
            if (string.IsNullOrEmpty(deviceName))
                return;
            if (enable)
            {
                if (!Microphone.IsRecording(deviceName))
                {
                    Debug.Log($"开始录音：{deviceName}");
                    _recordingClip = Microphone.Start(deviceName, true, RecordingBufferSec, inputSampleRate);
                    _recordingPosition = 0;
                }
            }
            else
            {
                if (Microphone.IsRecording(deviceName))
                {
                    Debug.Log($"停止录音：{deviceName}");
                    Microphone.End(deviceName);
                    _recordingPosition = 0;
                    Object.Destroy(_recordingClip);
                    _recordingClip = null;
                }
            }

            base.EnableInput(enable);
        }

        public override void SwitchInput()
        {
            _deviceIndex = (_deviceIndex + 1) % Microphone.devices.Length;
            var deviceName = GetDeviceName();
            if (!string.IsNullOrEmpty(deviceName))
                Debug.Log($"切换录音设备：{deviceName}");
        }

        private string GetDeviceName()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("没有找到录音设备");
                return string.Empty;
            }

            return Microphone.devices[_deviceIndex];
        }
    }
}