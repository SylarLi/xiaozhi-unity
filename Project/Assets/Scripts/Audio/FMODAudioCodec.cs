using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEngine;
using XiaoZhi.Audio;
using Channel = FMOD.Channel;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity
{
    public class FMODAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 8;
        private const int PlaybackBufferSec = 2;
        private const int PlaybackBufferDelayMs = 10;

        private CancellationTokenSource _updateCts;

        private Sound _recordingSound;
        private readonly int _recordingBufferSize;
        private int _readPosition;
        private Sound _playbackSound;
        private Channel _playbackChannel;
        private int _writePosition;
        private readonly int _playbackBufferSize;
        private readonly int _playbackBufferDelaySize;
        private Memory<short> _shortBuffer;
        private int _deviceIndex;

        private readonly IntPtr _aecmInst;
        private readonly int _aecmFrameSize;
        private int _aecmFramePos;
        private int _aecmRenderDelay;
        private int _aecmCaptureDelay;

        public FMODAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            _playbackBufferSize = outputChannels * outputSampleRate * PlaybackBufferSec;
            _recordingBufferSize = inputChannels * inputSampleRate * RecordingBufferSec;
            _playbackBufferDelaySize = outputSampleRate * outputChannels / 1000 * PlaybackBufferDelayMs;

            _aecmFrameSize = Math.Min(inputSampleRate / 100, 160);
            _aecmInst = AECMWrapper.AECM_Create();
            AECMWrapper.AECM_Init(_aecmInst, _aecmFrameSize * 100);
            AECMWrapper.AECM_SetConfig(_aecmInst);

            _updateCts = new CancellationTokenSource();
            UniTask.Void(Update, _updateCts.Token);
        }

        public override void Dispose()
        {
            if (_updateCts != null)
            {
                _updateCts.Cancel();
                _updateCts.Dispose();
                _updateCts = null;
            }

            if (_playbackChannel.hasHandle())
            {
                _playbackChannel.stop();
                _playbackChannel.clearHandle();
            }

            if (_playbackSound.hasHandle())
            {
                _playbackSound.release();
                _playbackSound.clearHandle();
            }

            if (_recordingSound.hasHandle())
            {
                _recordingSound.release();
                _recordingSound.clearHandle();
            }
        }

        private async UniTaskVoid Update(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (!outputEnabled || !_playbackChannel.hasHandle()) continue;
                _playbackChannel.getPosition(out var pos, TIMEUNIT.PCM);
                var playbackPos = (int)pos;
                var playbackEnd = _writePosition;
                if (playbackPos - playbackEnd > _playbackBufferSize / 2) playbackEnd += _playbackBufferSize;
                if (playbackPos >= playbackEnd)
                {
                    _writePosition = playbackPos;
                    SetPlaybackPause(true);
                }
                else
                {
                    if (playbackEnd - playbackPos >= _playbackBufferDelaySize) SetPlaybackPause(false);
                    var framePos = playbackPos / _aecmFrameSize;
                    if (playbackPos % _aecmFrameSize != 0) framePos += 1;
                    var stepFrames =
                        Mathf.CeilToInt(outputSampleRate * outputChannels * Time.deltaTime / _aecmFrameSize) + 1;
                    var endFramePos = Math.Min(framePos + stepFrames, playbackEnd / _aecmFrameSize);
                    if (framePos < _aecmFramePos) framePos = _aecmFramePos;
                    if (framePos < endFramePos)
                    {
                        var bufferPos = framePos * _aecmFrameSize;
                        var bufferLen = (endFramePos - framePos) * _aecmFrameSize;
                        Tools.EnsureMemory(ref _shortBuffer, bufferLen);
                        FMODHelper.ReadPCM16(_playbackSound, bufferPos, _shortBuffer.Span[..bufferLen]);
                        unsafe
                        {
                            fixed (short* farInput = _shortBuffer.Span)
                                AECMWrapper.AECM_BufferFarend(_aecmInst, farInput, bufferLen, outputSampleRate);
                        }

                        var frameLen = _playbackBufferSize / _aecmFrameSize;
                        if (endFramePos >= frameLen) endFramePos -= frameLen;
                        _aecmFramePos = endFramePos;
                        _aecmRenderDelay = (int)Mathf.Lerp(_aecmRenderDelay, bufferPos - playbackPos, 0.5f);
                    }
                }
            }
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            var writeLen = FMODHelper.WritePCM16(_playbackSound, _writePosition, data);
            _writePosition = (_writePosition + writeLen) % _playbackBufferSize;
            return writeLen;
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !_recordingSound.hasHandle()) return 0;
            RuntimeManager.CoreSystem.getRecordPosition(_deviceIndex, out var pos);
            var position = (int)pos;
            if (position == _readPosition) return 0;
            if (position < _readPosition) position += _recordingBufferSize;
            var readLen = dest.Length;
            if (position - _readPosition < readLen) return 0;
            readLen = FMODHelper.ReadPCM16(_recordingSound, _readPosition, dest);

            _aecmCaptureDelay = (int)Mathf.Lerp(_aecmCaptureDelay, position - _readPosition, 0.5f);
            var playbackPaused = true;
            if (_playbackChannel.hasHandle()) _playbackChannel.getPaused(out playbackPaused);
            if (!playbackPaused)
            {
                var renderDelayMs = _aecmRenderDelay * 1000 / (inputSampleRate * inputChannels);
                var captureDelayMs = _aecmCaptureDelay * 1000 / (outputSampleRate * outputChannels);
                unsafe
                {
                    fixed (short* nearInput = dest)
                        AECMWrapper.AECM_Process(_aecmInst, nearInput, null, readLen, inputSampleRate,
                            renderDelayMs + captureDelayMs);
                }
            }

            _readPosition = (_readPosition + readLen) % _recordingBufferSize;
            return readLen;
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            if (enable)
            {
                if (!_playbackChannel.hasHandle())
                {
                    var exInfo = new CREATESOUNDEXINFO
                    {
                        cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                        numchannels = outputChannels,
                        format = SOUND_FORMAT.PCM16,
                        defaultfrequency = outputSampleRate,
                        length = (uint)(_playbackBufferSize << 1)
                    };

                    RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                        out _playbackSound);
                    RuntimeManager.CoreSystem.playSound(_playbackSound, default, true, out _playbackChannel);
                    _playbackChannel.setVolume(outputVolume / 100f);
                }
                else
                {
                    SetPlaybackPause(true);
                }

                _playbackChannel.setPosition(0, TIMEUNIT.PCMBYTES);
                _writePosition = 0;
                _aecmFramePos = 0;
            }
            else
            {
                SetPlaybackPause(true);
            }

            base.EnableOutput(enable);
        }

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            if (enable) StartRecording(_deviceIndex);
            else StopRecording(_deviceIndex);
            base.EnableInput(enable);
        }

        private void SetPlaybackPause(bool pause)
        {
            if (!_playbackChannel.hasHandle()) return;
            _playbackChannel.getPaused(out var current);
            if (current != pause) _playbackChannel.setPaused(pause);
        }

        private void StartRecording(int id)
        {
            if (!_recordingSound.hasHandle())
            {
                var exInfo = new CREATESOUNDEXINFO
                {
                    cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                    numchannels = inputChannels,
                    format = SOUND_FORMAT.PCM16,
                    defaultfrequency = inputSampleRate,
                    length = (uint)(_recordingBufferSize << 1)
                };
                RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                    out _recordingSound);
            }

            RuntimeManager.CoreSystem.isRecording(id, out var isRecording);
            if (isRecording) RuntimeManager.CoreSystem.recordStop(id);
            RuntimeManager.CoreSystem.recordStart(id, _recordingSound, true);
            _readPosition = 0;
        }

        private void StopRecording(int id)
        {
            if (_recordingSound.hasHandle())
            {
                RuntimeManager.CoreSystem.isRecording(id, out var isRecording);
                if (isRecording) RuntimeManager.CoreSystem.recordStop(id);
            }
        }

        public override void SwitchInputDevice()
        {
            RuntimeManager.CoreSystem.getRecordNumDrivers(out _, out var numConnected);
            var nextDeviceIndex = (_deviceIndex + 1) % numConnected;
            if (_deviceIndex != nextDeviceIndex)
            {
                StopRecording(_deviceIndex);
                _deviceIndex = nextDeviceIndex;
                if (inputEnabled) StartRecording(_deviceIndex);
                var deviceName = GetDeviceName();
                if (!string.IsNullOrEmpty(deviceName))
                    Debug.Log($"切换录音设备：{deviceName}");
            }
        }

        private string GetDeviceName()
        {
            RuntimeManager.CoreSystem.getRecordNumDrivers(out _, out var numConnected);
            if (numConnected == 0)
            {
                Debug.LogError("没有找到录音设备");
                return string.Empty;
            }

            RuntimeManager.CoreSystem.getDriverInfo(_deviceIndex, out var deviceName, 64, out _, out _, out _, out _);
            return deviceName;
        }
    }
}