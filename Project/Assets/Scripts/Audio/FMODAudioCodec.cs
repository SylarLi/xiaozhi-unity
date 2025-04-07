using System;
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
        private const int RecorderBufferSec = 2;
        private const int InputBufferSec = 8;
        private const int PlayerBufferSec = 8;

        private CancellationTokenSource _updateCts;
        private FMOD.System _system;
        private Sound _recorder;
        private Channel _recorderChannel;
        private bool _isRecording;
        private int _recorderId = -1;
        private int _recorderLength;
        private int _readPosition;
        private Sound _player;
        private Channel _playerChannel;
        private int _playerLength;
        private int _writePosition;
        private float _playEndTime;
        private Memory<short> _shortBuffer1;
        private Memory<short> _shortBuffer2;
        private DSP _fftDsp;
        private Memory<float> _floatBuffer;
        private readonly RingBuffer<short> _inputBuffer;
        private readonly SpectrumAnalyzer _spectrumAnalyzer;
        private int _lastInputAnalysisPos;
        private int _lastOutputAnalysisPos;

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
            _spectrumAnalyzer = new SpectrumAnalyzer(SpectrumWindowSize);
            InitAudioProcessor();
            InitPlayer();
            InitRecorder();
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
            _system.getRecordPosition(_recorderId, out var pos);
            var recorderPos = (int)pos;
            var numFrames = Tools.Repeat(recorderPos - _apsCapturePos, _recorderLength) / inputFrameSize;
            if (numFrames <= 0) return;
            var outputFrameSize = outputSampleRate / 100 * outputChannels;
            _playerChannel.getPosition(out pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            var apsReversePos =
                Tools.Repeat((playerPos / outputFrameSize - numFrames) * outputFrameSize, _playerLength);
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
        
        public override bool GetOutputSpectrum(bool fft, out ReadOnlySpan<float> spectrum)
        {
            if (!outputEnabled || (fft && !_fftDsp.hasHandle()))
            {
                spectrum = default;
                return false;
            }

            if (fft)
            {
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
            }
            else
            {
                spectrum = default;
                const int readLen = SpectrumWindowSize;
                _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
                var playerPos = (int)pos;
                var position = (playerPos / readLen - 1) * readLen;
                position = Math.Max(position, 0);
                if (_lastOutputAnalysisPos == position) return false;
                _lastOutputAnalysisPos = position;
                var shortSpan = Tools.EnsureMemory(ref _shortBuffer1, readLen);
                FMODHelper.ReadPCM16(_player, _lastOutputAnalysisPos, shortSpan);
                var floatSpan = Tools.EnsureMemory(ref _floatBuffer, shortSpan.Length);
                Tools.PCM16Short2Float(shortSpan, floatSpan);
                spectrum = floatSpan;
            }
            
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
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOWSIZE, SpectrumWindowSize);
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

        public override bool GetInputSpectrum(bool fft, out ReadOnlySpan<float> spectrum)
        {
            spectrum = default;
            if (!_isRecording || !inputEnabled) return false;
            const int readLen = SpectrumWindowSize;
            var position = (_inputBuffer.WritePosition / readLen - 1) * readLen;
            position = Math.Max(position, 0);
            if (_lastInputAnalysisPos == position) return false;
            _lastInputAnalysisPos = position;
            var shortSpan = Tools.EnsureMemory(ref _shortBuffer1, readLen);
            var success = _inputBuffer.TryReadAt(_lastInputAnalysisPos, shortSpan);
            if (!success) return false;
            if (!fft)
            {
                var floatSpan = Tools.EnsureMemory(ref _floatBuffer, shortSpan.Length);
                Tools.PCM16Short2Float(shortSpan, floatSpan);
                spectrum = floatSpan;
                return true;
            }
            
            return _spectrumAnalyzer.Analyze(shortSpan, out spectrum);
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
            if (!_isRecording || !inputEnabled || !_recorder.hasHandle()) return 0;
            return _inputBuffer.TryRead(dest) ? dest.Length : 0;
        }

        public override bool GetInputDevice(out InputDevice device)
        {
            _system.getRecordNumDrivers(out var numDrivers, out _);
            for (var i = 0; i < numDrivers; i++)
            {
                _system.getRecordDriverInfo(i, out var deviceName, 64, out _, out var systemRate,
                    out var speakerMode, out var speakerModeChannels, out var state);
                if (state.HasFlag(DRIVER_STATE.CONNECTED) && state.HasFlag(DRIVER_STATE.DEFAULT))
                {
                    device = new InputDevice
                    {
                        Id = i, Name = deviceName, SystemRate = systemRate,
                        SpeakerMode = Enum.GetName(typeof(SPEAKERMODE), speakerMode),
                        SpeakerModeChannels = speakerModeChannels
                    };
                    return true;
                }
            }

            device = default;
            return false;
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
            var recorderId = -1;
            if (GetInputDevice(out var inputDevice)) recorderId = inputDevice.Id;
            if (_isRecording && _recorderId != recorderId)
            {
                _system.recordStop(_recorderId);
                _isRecording = false;
            }
            
            _recorderId = recorderId;
            if (!_isRecording && _recorderId >= 0)
            {
                _system.recordStart(_recorderId, _recorder, true);
                _isRecording = true;
            }
        }

        private void StopRecorder()
        {
            if (!_recorder.hasHandle()) return;
            if (_recorderId < 0) return;
            if (_isRecording)
            {
                _system.recordStop(_recorderId);
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