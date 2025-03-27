using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEngine;
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
        private bool _isRecording;
        private int _recorderLength;
        private int _readPosition;
        private Sound _player;
        private Channel _playerChannel;
        private int _playerLength;
        private int _writePosition;
        private float _playEndTime;
        private int _deviceIndex;
        private Memory<short> _shortBuffer1;
        private Memory<short> _shortBuffer2;
        private DSP _fftDsp;
        private Memory<float> _floatBuffer;
        private RingBuffer<short> _inputBuffer;
        private SpectrumAnalyzer _spectrumAnalyzer;
        private int _lastAnalysisPos;

        private FMODAudioProcessor _aps;
        private int _apsCapturePos;

        public FMODAudioCodec(int inputSampleRate, int inputChannels, int outputSampleRate, int outputChannels)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            this.inputChannels = inputChannels;
            this.outputChannels = outputChannels;
            _system = RuntimeManager.CoreSystem;
            _inputBuffer = new RingBuffer<short>(inputSampleRate * inputChannels * InputBufferSec);
            _spectrumAnalyzer = new SpectrumAnalyzer(FFTWindowSize << 1);
            InitAudioProcessor();
            InitPlayer();
        }

        public override void Start()
        {
            base.Start();
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

            ClearAudioProcessor();
            ClearPlayer();
            ClearRecorder();
        }

        private void InitAudioProcessor()
        {
            _aps = new FMODAudioProcessor(inputSampleRate, inputChannels, outputSampleRate, outputChannels);
        }

        private void ClearAudioProcessor()
        {
            if (_aps != null)
            {
                _aps.Dispose();
                _aps = null;
            }
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
                FMODHelper.ClearPCM16(_player, 0, _playerLength);
                _playerChannel.setMute(true);
            }
            else if (_playEndTime - nowTime < Time.deltaTime)
            {
                FMODHelper.ClearPCM16(_player, _writePosition, (int)(Time.deltaTime * 2 * outputSampleRate));
            }
        }

        private void ProcessAudio()
        {
            if (!outputEnabled || !_isRecording) return;
            var inputFrameSize = inputSampleRate / 100 * inputChannels;
            _system.getRecordPosition(_deviceIndex, out var pos);
            var recorderPos = (int)pos;
            var numFrames = Tools.Repeat(recorderPos - _apsCapturePos, _recorderLength) / inputFrameSize;
            if (numFrames <= 0) return;
            var outputFrameSize = outputSampleRate / 100 * outputChannels;
            _playerChannel.getPosition(out pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            var apsReversePos = Tools.Repeat((playerPos / outputFrameSize - numFrames) * outputFrameSize, _playerLength);
            var reverseSamples = numFrames * outputFrameSize;
            var reverseSpan = Tools.EnsureMemory(ref _shortBuffer1, reverseSamples);
            FMODHelper.ReadPCM16(_player, apsReversePos, reverseSpan);
            var captureSamples = numFrames * inputFrameSize;
            var captureSpan = Tools.EnsureMemory(ref _shortBuffer2, captureSamples);
            FMODHelper.ReadPCM16(_recorder, _apsCapturePos, captureSpan);
            for (var i = 0; i < numFrames; i++)
            {
                var span1 = reverseSpan.Slice(i * outputFrameSize, outputFrameSize);
                var result1 = _aps.ProcessReverseStream(span1, span1);
                if (result1 != 0)
                {
                    Debug.LogError($"ProcessReverseStream error: {result1}");
                    return;
                }
                
                _aps.SetStreamDelayMs(0);
                var span2 = captureSpan.Slice(i * inputFrameSize, inputFrameSize);
                var result2 = _aps.ProcessStream(span2, span2);
                if (result2 != 0)
                {
                    Debug.LogError($"ProcessStream error: {result2}");
                    return;
                }
            }

            _inputBuffer.TryWrite(captureSpan);
            _apsCapturePos = Tools.Repeat(_apsCapturePos + numFrames * inputFrameSize, _recorderLength);
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

            var floatSpan = Tools.EnsureMemory(ref _floatBuffer, fftData.length);
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
            _playerLength = outputSampleRate * outputChannels * PlayerBufferSec;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = outputSampleRate,
                length = (uint)(_playerLength << 1)
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
                if (_playerChannel.hasHandle())
                    _playerChannel.removeDSP(_fftDsp);
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
            var shortSpan = Tools.EnsureMemory(ref _shortBuffer1, readLen);
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
            _recorderLength = inputSampleRate * inputChannels * RecorderBufferSec;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = inputSampleRate,
                length = (uint)(_recorderLength << 1)
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
                _isRecording = true;
            }
        }

        private void StopRecorder()
        {
            if (!_recorder.hasHandle()) return;
            _system.isRecording(_deviceIndex, out var isRecording);
            if (isRecording)
            {
                _system.recordStop(_deviceIndex);
                _isRecording = false;
            }
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