using System;
using System.Collections.Generic;
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
        private const int InputBufferSec = 8;
        private const int RecorderBufferSec = 2;
        private const int PlayerBufferSec = 8;
        private const int FFTWindowSize = 512;
        
        private CancellationTokenSource _updateCts;
        private FMOD.System _system;
        private Sound _recorder;
        private Channel _recorderChannel;
        private int _readPosition;
        private Sound _player;
        private Channel _playerChannel;
        private int _writePosition;
        private float _playEndTime;
        private int _deviceIndex;
        private Memory<short> _shortBuffer;
        private DSP _fftDsp;
        private Memory<float> _floatBuffer;
        private readonly RingBuffer<short> _inputBuffer;
        private readonly SpectrumAnalyzer _spectrumAnalyzer;
        private int _lastAnalysisPos;

        private readonly IntPtr _aecmInst;
        private readonly OpusResampler _aecmResampler;
        private readonly int _aecmLatency;
        private int _apsCapturePos;
        private int _apsReversePos;

        public FMODAudioCodec(int inputSampleRate, int outputSampleRate)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            _inputBuffer = new RingBuffer<short>(inputSampleRate * InputBufferSec);
            _spectrumAnalyzer = new SpectrumAnalyzer(FFTWindowSize << 1);
            _system = RuntimeManager.CoreSystem; 
            _system.getDSPBufferSize(out var bufferSize, out var numBuffers);
            _aecmLatency = (int)((numBuffers - 1.5) * bufferSize * 1000 / outputSampleRate);
            _aecmInst = AECMWrapper.AECM_Create();
            AECMWrapper.AECM_Init(_aecmInst, Math.Min(inputSampleRate / 100, 160) * 100);
            AECMWrapper.AECM_SetConfig(_aecmInst);
            _aecmResampler = new OpusResampler();
            _aecmResampler.Configure(outputSampleRate, inputSampleRate);
            InitPlayer();
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

            ClearPlayer();
            ClearRecorder();
        }

        private async UniTaskVoid Update(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                DetectIfPlayToEnd();
                await UniTask.SwitchToThreadPool();
                ProcessAudio();
            }
        }

        private void DetectIfPlayToEnd()
        {
            if (!outputEnabled) return;
            _playerChannel.getMute(out var mute);
            if (mute) return;
            var nowTime = Time.time;
            if (nowTime >= _playEndTime)
            {
                _player.getLength(out var length, TIMEUNIT.PCM);
                FMODHelper.ClearPCM16(_player, 0, (int)length);
                _playerChannel.setMute(true);
            }
            else if (_playEndTime - nowTime < Time.deltaTime)
            {
                FMODHelper.ClearPCM16(_player, _writePosition, (int)(Time.deltaTime * 2 * outputSampleRate));
            }
        }

        private void ProcessAudio()
        {
            var inputFrameSize = Math.Min(inputSampleRate / 100, 160);
            var outputFrameSize = inputFrameSize * outputSampleRate / inputSampleRate;
            _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            _player.getLength(out var length, TIMEUNIT.PCM);
            var playerLength = (int)length;
            var numReverseFrames = Tools.Repeat(playerPos - _apsReversePos, playerLength) / outputFrameSize;
            _system.getRecordPosition(_deviceIndex, out pos);
            var recorderPos = (int)pos;
            _recorder.getLength(out length, TIMEUNIT.PCM);
            var recorderLength = (int)length;
            var numCaptureFrames = Tools.Repeat(recorderPos - _apsCapturePos, recorderLength) / inputFrameSize;
            var numFrames = Math.Min(numReverseFrames, numCaptureFrames);
            if (numFrames <= 0) return;
            var reverseSamples = numFrames * outputFrameSize;
            Tools.EnsureMemory(ref _shortBuffer, reverseSamples);
            var reverseSpan = _shortBuffer.Span[..reverseSamples];
            FMODHelper.ReadPCM16(_player, _apsReversePos, reverseSpan);
            _aecmResampler.Process(reverseSpan, out var resampledReverseSpan);
            var captureSamples = numFrames * inputFrameSize;
            Tools.EnsureMemory(ref _shortBuffer, captureSamples);
            var captureSpan = _shortBuffer.Span[..captureSamples];
            FMODHelper.ReadPCM16(_recorder, _apsCapturePos, captureSpan);
            unsafe
            {
                fixed (short* reversePtr = resampledReverseSpan)
                fixed (short* capturePtr = captureSpan)
                {
                    var iReversePtr = reversePtr;
                    var iCapturePtr = capturePtr;
                    for (var i = 0; i < numFrames; i++)
                    {
                        AECMWrapper.AECM_BufferFarend(_aecmInst, iReversePtr, inputFrameSize);
                        AECMWrapper.AECM_Process(_aecmInst, iCapturePtr, null, iCapturePtr, inputFrameSize,
                            _aecmLatency);
                        iReversePtr += inputFrameSize;
                        iCapturePtr += inputFrameSize;
                    }
                }
            }

            _inputBuffer.TryWrite(captureSpan);
            _apsReversePos = Tools.Repeat(_apsReversePos + numFrames * outputFrameSize, playerLength);
            _apsCapturePos = Tools.Repeat(_apsCapturePos + numFrames * inputFrameSize, recorderLength);
        }

        // -------------------------------- output ------------------------------- //

        public override bool GetOutputSpectrum(out ReadOnlySpan<float> spectrum)
        {
            if (!outputEnabled || !_fftDsp.hasHandle())
            {
                spectrum = default;
                return false;
            }

            _fftDsp.getParameterData((int)DSP_FFT.SPECTRUMDATA, out var unmanagedData, out _);
            var fftData = Marshal.PtrToStructure<DSP_PARAMETER_FFT>(unmanagedData);
            if (fftData.numchannels <= 0)
            {
                spectrum = default;
                return false;
            }

            Tools.EnsureMemory(ref _floatBuffer, fftData.length);
            var floatSpan = _floatBuffer.Span[..fftData.length];
            fftData.getSpectrum(0, floatSpan);
            spectrum = floatSpan;
            return true;
        }

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

        public override void ResetOutput()
        {
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            _playerChannel.getMute(out var mute);
            if (mute)
            {
                _playerChannel.setMute(false);
                _writePosition = playerPos;
            }

            var writeLen = FMODHelper.WritePCM16(_player, _writePosition, data);
            _player.getLength(out var length, TIMEUNIT.PCM);
            var playerLen = (int)length;
            _writePosition = Tools.Repeat(_writePosition + writeLen, playerLen);
            var sampleDist = Tools.Repeat(_writePosition - playerPos, playerLen);
            _playEndTime = Time.time + (float)sampleDist / outputSampleRate;
            return writeLen;
        }

        private void InitPlayer()
        {
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = outputSampleRate,
                length = (uint)(outputSampleRate * PlayerBufferSec << 1)
            };

            var system = _system;
            system.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _player);
            system.playSound(_player, default, true, out _playerChannel);
            _playerChannel.setVolume(outputVolume / 100f);
            _playerChannel.setMute(true);
            system.createDSPByType(DSP_TYPE.FFT, out _fftDsp);
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOW, (int)DSP_FFT_WINDOW_TYPE.HANNING);
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOWSIZE, FFTWindowSize << 1);
            _playerChannel.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, _fftDsp);
        }

        private void ClearPlayer()
        {
            if (_fftDsp.hasHandle())
            {
                _fftDsp.release();
                _fftDsp.clearHandle();
            }

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

        public override bool GetInputSpectrum(out ReadOnlySpan<float> spectrum)
        {
            spectrum = default;
            if (!inputEnabled) return false;
            const int readLen = FFTWindowSize << 1;
            var position = (_inputBuffer.WritePosition / readLen - 1) * readLen;
            if (_lastAnalysisPos == position) return false;
            _lastAnalysisPos = position;
            Tools.EnsureMemory(ref _shortBuffer, readLen);
            var shortSpan = _shortBuffer.Span[..readLen];
            return _inputBuffer.TryReadAt(position, shortSpan) &&
                   _spectrumAnalyzer.Analyze(shortSpan, out spectrum);
        }

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
            _inputBuffer.Clear();
        }

        protected override int Read(Span<short> dest)
        {
            if (!inputEnabled || !_recorder.hasHandle()) return 0;
            return _inputBuffer.TryRead(dest) ? dest.Length : 0;
        }

        public override InputDevice[] GetInputDevices()
        {
            var inputDevices = new List<InputDevice>();
            _system.getRecordNumDrivers(out var numDrivers, out _);
            for (var i = 0; i < numDrivers; i++)
            {
                _system.getRecordDriverInfo(i, out var deviceName, 64, out _, out var systemRate,
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

            index = Tools.Repeat(index, inputDevices.Length);
            _system.getRecordDriverInfo(index, out var deviceName, 64, out _, out _, out _,
                out _, out var state);
            if (!state.HasFlag(DRIVER_STATE.CONNECTED))
            {
                Debug.LogError($"录音设备不可用: {deviceName}");
                return;
            }

            StopRecorder();
            ClearRecorder();
            base.SetInputDeviceIndex(index);
            InitRecorder();
            if (inputEnabled) StartRecorder();
        }

        private void InitRecorder()
        {
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = inputSampleRate,
                length = (uint)(inputSampleRate * RecorderBufferSec << 1)
            };
            _system.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _recorder);
        }

        private void ClearRecorder()
        {
            if (_recorderChannel.hasHandle())
            {
                _recorderChannel.stop();
                _recorderChannel.clearHandle();
            }

            if (_recorder.hasHandle())
            {
                _recorder.release();
                _recorder.clearHandle();
            }
        }

        private void StartRecorder()
        {
            if (!_recorder.hasHandle()) return;
            _system.isRecording(_deviceIndex, out var isRecording);
            if (!isRecording)
            {
                _system.recordStart(_deviceIndex, _recorder, true);
                ResetInput();
            }
        }

        private void StopRecorder()
        {
            if (!_recorder.hasHandle()) return;
            _system.isRecording(_deviceIndex, out var isRecording);
            if (isRecording) _system.recordStop(_deviceIndex);
        }
    }

    public static class fmod_dsp_extension
    {
        public static void getSpectrum(this DSP_PARAMETER_FFT fft, int channel, Span<float> buffer)
        {
            var bufferLength = Math.Min(fft.length, buffer.Length) * sizeof(float);
            unsafe
            {
                fixed (float* bufferPtr = buffer)
                    Buffer.MemoryCopy(fft.spectrum_internal[channel].ToPointer(), bufferPtr, bufferLength,
                        bufferLength);
            }
        }
    }
}