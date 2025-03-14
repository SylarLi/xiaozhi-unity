using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using FMOD;
using FMODUnity;
using Channel = FMOD.Channel;

namespace XiaoZhi.Unity
{
    public class FMODAudioCodec : AudioCodec
    {
        private const int RecordingBufferSec = 8;
        private const int PlaybackBufferSec = 2;

        private Sound _recordingSound;
        private readonly uint _recordingBufferSize;
        private uint _readPosition;
        private Sound _playbackSound;
        private Channel _playbackChannel;
        private uint _writePosition;
        private readonly uint _playbackBufferSize;
        private Memory<short> _shortBuffer;
        private int _deviceIndex;

        public FMODAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            _playbackBufferSize = (uint)(outputChannels * outputSampleRate * PlaybackBufferSec * sizeof(short));
            _recordingBufferSize = (uint)(inputChannels * inputSampleRate * RecordingBufferSec * sizeof(short));
            Update().Forget();
        }

        public override void Dispose()
        {
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

        private async UniTaskVoid Update()
        {
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (!outputEnabled || !_playbackChannel.hasHandle()) continue;
                _playbackChannel.getPosition(out var playbackPos, TIMEUNIT.PCMBYTES);
                var playbackEnd = _writePosition;
                if (playbackPos - playbackEnd > _playbackBufferSize / 2) playbackEnd += _playbackBufferSize;
                if (playbackPos >= playbackEnd)
                {
                    _writePosition = playbackPos;
                    SetPlaybackPause(true);
                }
                else if (playbackEnd - playbackPos >= outputSampleRate * outputChannels * sizeof(short) / 1000 * 10)
                {
                    SetPlaybackPause(false);   
                }
            }
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            _playbackSound.@lock(_writePosition, (uint)data.Length * 2, out var ptr1, out var ptr2, out var len1,
                out var len2);
            unsafe
            {
                fixed (short* ptr = data)
                {
                    Buffer.MemoryCopy(ptr, ptr1.ToPointer(), len1, len1);
                    Buffer.MemoryCopy(ptr + len1 / 2, ptr2.ToPointer(), len2, len2);
                }
            }

            _playbackSound.unlock(ptr1, ptr2, len1, len2);
            var writeLen = len1 + len2;
            _writePosition = (_writePosition + writeLen) % _playbackBufferSize;
            return (int)(writeLen / 2);
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !_recordingSound.hasHandle()) return 0;
            RuntimeManager.CoreSystem.getRecordPosition(_deviceIndex, out var position);
            if (position == _readPosition) return 0;
            if (position < _readPosition) position += _recordingBufferSize;
            var readMax = position - _readPosition;
            var readLen = Math.Min((uint)dest.Length * 2, readMax);
            _recordingSound.@lock(_readPosition, readLen,
                out var ptr1, out var ptr2, out var len1, out var len2);
            unsafe
            {
                fixed (short* ptr = dest)
                {
                    Buffer.MemoryCopy(ptr1.ToPointer(), ptr, len1, len1);
                    Buffer.MemoryCopy(ptr2.ToPointer(), ptr + len1 / 2, len2, len2);
                }
            }

            _recordingSound.unlock(ptr1, ptr2, len1, len2);
            readLen = len1 + len2;
            _readPosition = (_readPosition + readLen) % _recordingBufferSize;
            var ret = (int)(readLen / 2);
            dest[ret..].Clear();
            return ret;
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
                        length = _playbackBufferSize
                    };

                    RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                        out _playbackSound);
                    RuntimeManager.CoreSystem.playSound(_playbackSound, default, true, out _playbackChannel);
                    _playbackChannel.setVolume(outputVolume / 100f);
                }

                SetPlaybackPause(true);
                _playbackChannel.setPosition(0, TIMEUNIT.PCM);
                _writePosition = 0;
            }
            else if (_playbackChannel.hasHandle())
            {
                SetPlaybackPause(true);
            }

            base.EnableOutput(enable);
        }

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            if (enable)
            {
                if (!_recordingSound.hasHandle())
                {
                    var exInfo = new CREATESOUNDEXINFO
                    {
                        cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                        numchannels = inputChannels,
                        format = SOUND_FORMAT.PCM16,
                        defaultfrequency = inputSampleRate,
                        length = _recordingBufferSize
                    };

                    RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                        out _recordingSound);
                }

                RuntimeManager.CoreSystem.isRecording(_deviceIndex, out var isRecording);
                if (isRecording) RuntimeManager.CoreSystem.recordStop(_deviceIndex);
                RuntimeManager.CoreSystem.recordStart(_deviceIndex, _recordingSound, true);
                _readPosition = 0;
            }
            else
            {
                if (_recordingSound.hasHandle())
                {
                    RuntimeManager.CoreSystem.isRecording(_deviceIndex, out var isRecording);
                    if (isRecording) RuntimeManager.CoreSystem.recordStop(_deviceIndex);
                }
            }

            base.EnableInput(enable);
        }

        private void SetPlaybackPause(bool pause)
        {
            if (!_playbackChannel.hasHandle()) return;
            _playbackChannel.getPaused(out var current);
            if (current != pause) _playbackChannel.setPaused(pause);
        }

        public void SwitchInputDevice()
        {
            RuntimeManager.CoreSystem.getRecordNumDrivers(out _, out var numConnected);
            _deviceIndex = (_deviceIndex + 1) % numConnected;
            var deviceName = GetDeviceName();
            if (!string.IsNullOrEmpty(deviceName))
                UnityEngine.Debug.Log($"切换录音设备：{deviceName}");
        }

        private string GetDeviceName()
        {
            RuntimeManager.CoreSystem.getRecordNumDrivers(out var numDrivers, out var numConnected);
            if (numConnected == 0)
            {
                UnityEngine.Debug.LogError("没有找到录音设备");
                return string.Empty;
            }

            RuntimeManager.CoreSystem.getDriverInfo(_deviceIndex, out var deviceName, 128, out _, out _, out _, out _);
            return deviceName;
        }
    }
}