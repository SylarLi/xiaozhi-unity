using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMOD;
using FMODUnity;
using Channel = FMOD.Channel;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity
{
    public class FMODAudioCodec : AudioCodec
    {
        private const int RecorderBufferSec = 8;
        private const int PlayerBufferSec = 8;

        private CancellationTokenSource _updateCts;

        private Sound _recorder;
        private int _readPosition;
        private Memory<short> _readBuffer;
        private Sound _player;
        private Channel _playerChannel;
        private int _writePosition;
        private bool _playerStartFlag;
        private bool _playerFinishFlag;
        private DateTime _playerFinishTime;
        private Memory<short> _shortBuffer;
        private int _deviceIndex;

        // private readonly IntPtr _aecmInst;
        // private readonly OpusResampler _aecmResampler;

        public FMODAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;

            // var aecmFrameSize = Math.Min(inputSampleRate / 100, 160);
            // _aecmInst = AECMWrapper.AECM_Create();
            // AECMWrapper.AECM_Init(_aecmInst, aecmFrameSize * 100);
            // AECMWrapper.AECM_SetConfig(_aecmInst);
            // _aecmResampler = new OpusResampler();
            // _aecmResampler.Configure(outputSampleRate, inputSampleRate);

            _updateCts = new CancellationTokenSource();
            UniTask.Void(Update, _updateCts.Token);

            InitPlayer();
        }

        public override void Dispose()
        {
            if (_updateCts != null)
            {
                _updateCts.Cancel();
                _updateCts.Dispose();
                _updateCts = null;
            }

            ClearPlayer();
            ClearRecorder();
        }

        private async UniTaskVoid Update(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (outputEnabled && _playerFinishFlag && DateTime.Now >= _playerFinishTime)
                {
                    _playerFinishFlag = false;
                    _playerChannel.setMute(true);
                }
            }
        }

        // -------------------------------- output ------------------------------- //

        public override void SetOutputVolume(int volume)
        {
            base.SetOutputVolume(volume);
            _playerChannel.setVolume(volume / 100f);
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            base.EnableOutput(enable);
            _playerChannel.getPaused(out var current);
            if (current != !outputEnabled) _playerChannel.setPaused(!outputEnabled);
        }

        public override void StartOutput()
        {
            if (!outputEnabled) return;
            _playerStartFlag = true;
        }

        public override void FinishOutput()
        {
            if (!outputEnabled) return;
            _playerFinishFlag = true;
            _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            _player.getLength(out var length, TIMEUNIT.PCM);
            var playerLen = (int)length;
            var sampleDist = Tools.Repeat(_writePosition - playerPos, playerLen);
            var durationMs = sampleDist * 1000 / (outputSampleRate * outputChannels);
            _playerFinishTime = DateTime.Now + TimeSpan.FromMilliseconds(durationMs + 10);
            FMODHelper.ClearPCM16(_player, _writePosition, outputSampleRate * outputChannels / 1000 * 200);
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            if (_playerStartFlag)
            {
                _playerStartFlag = false;
                _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
                _playerChannel.setMute(false);
                _writePosition = (int)pos;
            }

            var writeLen = FMODHelper.WritePCM16(_player, _writePosition, data);
            _player.getLength(out var length, TIMEUNIT.PCM);
            _writePosition = Tools.Repeat(_writePosition + writeLen, (int)length);
            return writeLen;
        }

        private void InitPlayer()
        {
            outputChannels = 1;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = outputChannels,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = outputSampleRate,
                length = (uint)(outputChannels * outputSampleRate * PlayerBufferSec << 1)
            };

            RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _player);
            RuntimeManager.CoreSystem.playSound(_player, default, true, out _playerChannel);
            _playerChannel.setVolume(outputVolume / 100f);
            _playerChannel.setMute(true);
        }

        private void ClearPlayer()
        {
            if (_playerChannel.hasHandle())
            {
                _playerChannel.stop();
                _playerChannel.clearHandle();
            }

            if (_player.hasHandle())
            {
                _player.release();
                _player.clearHandle();
            }
        }

        // -------------------------------- input ------------------------------- //

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            if (enable) StartRecorder();
            else StopRecorder();
            base.EnableInput(enable);
        }

        public override void ResetInput()
        {
            if (!inputEnabled) return;
            RuntimeManager.CoreSystem.getRecordPosition(_deviceIndex, out var recorderPos);
            _readPosition = (int)recorderPos;
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !_recorder.hasHandle()) return 0;
            RuntimeManager.CoreSystem.getRecordPosition(_deviceIndex, out var pos);
            var position = (int)pos;
            if (position == _readPosition) return 0;
            _recorder.getLength(out var length, TIMEUNIT.PCM);
            var recorderLen = (int)length;
            if (position < _readPosition) position += recorderLen;
            var readLen = dest.Length;
            if (position - _readPosition < readLen) return 0;
            readLen = FMODHelper.ReadPCM16(_recorder, _readPosition, dest);
            _readPosition = (_readPosition + readLen) % recorderLen;
            return readLen;
        }

        public override InputDevice[] GetInputDevices()
        {
            var inputDevices = new List<InputDevice>();
            RuntimeManager.CoreSystem.getRecordNumDrivers(out var numDrivers, out _);
            for (var i = 0; i < numDrivers; i++)
            {
                RuntimeManager.CoreSystem.getRecordDriverInfo(i, out var deviceName, 64, out _, out var systemRate,
                    out var speakerMode, out var speakerModeChannels, out var state);
                if (state.HasFlag(DRIVER_STATE.CONNECTED))
                    inputDevices.Add(new InputDevice
                    {
                        Id = i, Name = deviceName, SystemRate = systemRate,
                        SpeakerMode = Enum.GetName(typeof(SPEAKERMODE), speakerMode),
                        SpeakerModeChannels = speakerModeChannels
                    });
            }

            return inputDevices.ToArray();
        }

        public override void SetInputDeviceIndex(int index)
        {
            var inputDevices = GetInputDevices();
            if (inputDevices.Length == 0)
            {
                Debug.LogError("没有可用的录音设备");
                return;
            }

            StopRecorder();
            ClearRecorder();
            index = Tools.Repeat(index, inputDevices.Length);
            base.SetInputDeviceIndex(index);
            RuntimeManager.CoreSystem.getRecordDriverInfo(_deviceIndex, out var deviceName, 64, out _, out _, out _,
                out _,
                out var state);
            if (!state.HasFlag(DRIVER_STATE.CONNECTED))
            {
                Debug.LogError($"录音设备不可用: {deviceName}");
                return;
            }

            InitRecorder();
            if (inputEnabled) StartRecorder();
        }

        private void InitRecorder()
        {
            inputChannels = 1;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = inputChannels,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = inputSampleRate,
                length = (uint)(inputChannels * inputSampleRate * RecorderBufferSec << 1)
            };
            RuntimeManager.CoreSystem.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _recorder);
        }

        private void ClearRecorder()
        {
            if (_recorder.hasHandle())
            {
                _recorder.release();
                _recorder.clearHandle();
            }
        }

        private void StartRecorder()
        {
            if (!_recorder.hasHandle()) return;
            RuntimeManager.CoreSystem.isRecording(_deviceIndex, out var isRecording);
            if (!isRecording)
            {
                RuntimeManager.CoreSystem.recordStart(_deviceIndex, _recorder, true);
                _readPosition = 0;
                ResetInput();
            }
        }

        private void StopRecorder()
        {
            if (!_recorder.hasHandle()) return;
            RuntimeManager.CoreSystem.isRecording(_deviceIndex, out var isRecording);
            if (isRecording) RuntimeManager.CoreSystem.recordStop(_deviceIndex);
        }
    }
}