using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;
using XiaoZhi.Audio;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UnityAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 8;
        private const int PlayingBufferSec = 4;

        private readonly int _kSampleRate;
        private AudioSource _audioSource;
        private AudioClip _recordingClip;
        private int _recordingPosition;
        private readonly ClipStreamBuffer _playbackBuffer;
        private Memory<short> _shortBuffer;
        private float[] _floatBuffer;
        private bool _isPlaying;
        private int _deviceIndex;

        private IntPtr _aecmInst;
        private int _aecmCompensation;
        private int _aecmRenderDelay;
        private int _aecmProcessDelay;

        public UnityAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            var playbackBufferSize = outputSampleRate * PlayingBufferSec;
            _playbackBuffer = new ClipStreamBuffer(playbackBufferSize);
            _kSampleRate = Mathf.Min(inputSampleRate / 100, 160);
            _aecmInst = AECMWrapper.AECM_Create();
            AECMWrapper.AECM_Init(_aecmInst, _kSampleRate * 100);
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
            var dataLen = data.Length;
            Tools.EnsureMemory(ref _shortBuffer, dataLen);
            var readBuffer = _shortBuffer.Span;
            var readPos = _playbackBuffer.ReadPosition;
            var readLen = _playbackBuffer.Read(readBuffer[..dataLen]);
            for (var i = 0; i < readLen; i++)
                data[i] = (float)readBuffer[i] / short.MaxValue;
            data.AsSpan(readLen).Clear();

            dataLen += _aecmCompensation;
            Tools.EnsureMemory(ref _shortBuffer, dataLen);
            readBuffer = _shortBuffer.Span;
            var aecmLen = dataLen / _kSampleRate * _kSampleRate;
            var aecmPos = readPos - _aecmCompensation;
            if (aecmPos < 0) aecmPos += _playbackBuffer.Capacity;
            _playbackBuffer.ReadAt(aecmPos, readBuffer[..aecmLen]);
            unsafe
            {
                fixed (short* farInput = readBuffer)
                    AECMWrapper.AECM_BufferFarend(_aecmInst, farInput, aecmLen, inputSampleRate);
            }

            _aecmCompensation = dataLen - aecmLen;
            _aecmRenderDelay = data.Length * 1000 / inputSampleRate;
            Debug.Log(_aecmRenderDelay);
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            _playbackBuffer.Buffer(data);
            if (!_isPlaying)
            {
                _isPlaying = true;
                if (!_audioSource.isPlaying)
                {
                    _audioSource.volume = outputVolume / 100f;
                    _audioSource.Play();
                }
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
                if (_audioSource.isPlaying)
                    _audioSource.Stop();
            }
            else if (!_audioSource)
            {
                _audioSource = new GameObject(GetType().Name).AddComponent<AudioSource>();
                Object.DontDestroyOnLoad(_audioSource.gameObject);
                var playbackClip = AudioClip.Create("StreamPlayback", _playbackBuffer.Capacity, outputChannels,
                    outputSampleRate,
                    true, OnAudioRead);
                _audioSource.clip = playbackClip;
                _audioSource.loop = true;
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
            if (position < 0 || position == _recordingPosition) return 0;
            if (position < _recordingPosition) position += _recordingClip.samples;
            var readMax = position - _recordingPosition;
            var readLen = Mathf.Min(dest.Length, readMax);
            _recordingPosition = ReadClip(_recordingClip, _recordingPosition, dest[..readLen]);
            dest[readLen..].Clear();
            if (_audioSource && readLen >= _kSampleRate)
            {
                var scale = readLen / _kSampleRate;
                if (readLen % _kSampleRate != 0) scale++;
                var aecmLen = Mathf.Min(scale * _kSampleRate, dest.Length);
                unsafe
                {
                    fixed (short* nearInput = dest)
                        AECMWrapper.AECM_Process(_aecmInst, nearInput, null, aecmLen, inputSampleRate);
                }
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
                    _recordingClip = Microphone.Start(deviceName, true, RecordingBufferSec, inputSampleRate);
                    _recordingPosition = 0;
                }
                else
                {
                    _recordingPosition = Microphone.GetPosition(deviceName);
                }
            }

            base.EnableInput(enable);
        }

        public void SwitchInputDevice()
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