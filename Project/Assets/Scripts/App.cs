using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace XiaoZhi.Unity
{
    public enum DeviceState
    {
        Unknown,
        Starting,
        WifiConfiguring,
        Idle,
        Connecting,
        Listening,
        Speaking,
        Upgrading,
        Activating,
        FatalError
    }

    public enum AppTaskType
    {
        Main,
        Background
    }

    public class App : IDisposable
    {
        public static App Instance { get; } = new();

        // 音频处理相关字段
        private WakeWordDetect _wakeWordDetect;
        private AudioProcessor _audioProcessor;

        // 系统状态相关字段
        private Dictionary<AppTaskType, Task> _taskMap = new();
        private Protocol _protocol;
        private DeviceState _deviceState = DeviceState.Unknown;
        private bool _keepListening;
        private bool _aborted;
        private bool _voiceDetected;

        // 音频编解码相关字段
        private DateTime _lastOutputTime;
        private ConcurrentQueue<ReadOnlyMemory<byte>> _audioDecodeQueue = new();
        private OpusEncoder _opusEncoder;
        private OpusDecoder _opusDecoder;
        private int _opusDecodeSampleRate = -1;
        private OpusResampler _inputResampler;
        private OpusResampler _referenceResampler;
        private OpusResampler _outputResampler;

        private OTA _ota;

        public DeviceState GetDeviceState() => _deviceState;
        public bool IsVoiceDetected() => _voiceDetected;

        public App()
        {
            _taskMap = new Dictionary<AppTaskType, Task>();
            foreach (AppTaskType task in Enum.GetValues(typeof(AppTaskType)))
                _taskMap[task] = Task.CompletedTask;
        }

        public async void Start()
        {
            var display = Context.Instance.Display;
            await SetDeviceState(DeviceState.Starting);
            InitializeAudio();
            display.SetStatus("正在加载协议");
            InitializeProtocol();
            await CheckNewVersion();
            if (Config.Instance.UseAudioProcessing) ConfigureAudioProcessing();
            await SetDeviceState(DeviceState.Idle);
        }

        public void Update()
        {
            if (_deviceState == DeviceState.Listening)
                InputAudio();
            else if (_deviceState == DeviceState.Speaking)
                OutputAudio();
        }

        public void Dispose()
        {
        }

        public async Task SetDeviceState(DeviceState state)
        {
            if (_deviceState == state) return;
            _deviceState = state;
            Debug.Log("设备状态改变: " + _deviceState);
            await GetTask(AppTaskType.Background);
            var context = Context.Instance;
            var display = context.Display;
            var codec = context.AudioCodec;
            switch (state)
            {
                case DeviceState.Unknown:
                case DeviceState.Idle:
                    display.SetStatus("待机中");
                    display.SetEmotion("neutral");
                    if (Config.Instance.UseAudioProcessing) _audioProcessor.Stop();
                    break;

                case DeviceState.Connecting:
                    display.SetStatus("正在连接");
                    display.SetChatMessage("system", "");
                    break;

                case DeviceState.Listening:
                    display.SetStatus("正在聆听");
                    display.SetEmotion("neutral");
                    ResetDecoder();
                    _opusEncoder.ResetState();
                    if (Config.Instance.UseAudioProcessing) _audioProcessor.Start();
                    break;

                case DeviceState.Speaking:
                    display.SetStatus("正在说话");
                    ResetDecoder();
                    codec.EnableOutput(true);
                    if (Config.Instance.UseAudioProcessing) _audioProcessor.Stop();
                    break;
                case DeviceState.Starting:
                case DeviceState.WifiConfiguring:
                case DeviceState.Upgrading:
                case DeviceState.Activating:
                case DeviceState.FatalError:
                default:
                    break;
            }
        }

        public void Schedule(Func<Task> callback, AppTaskType taskType = AppTaskType.Main)
        {
            SetTask(taskType, Task.WhenAll(GetTask(taskType), Task.Run(callback)));
        }

        public void Schedule(Action callback, AppTaskType taskType = AppTaskType.Main)
        {
            SetTask(taskType, Task.WhenAll(GetTask(taskType), Task.Run(callback)));
        }

        public void Alert(string status, string message, string emotion = null)
        {
            var display = Context.Instance.Display;
            display.SetStatus(status);
            display.SetChatMessage("system", message);
            display.SetEmotion(emotion);
            Debug.Log("Alert: " + status + " " + message + " " + emotion);
        }

        public async Task AbortSpeaking(AbortReason reason)
        {
            Debug.Log("Abort speaking");
            _aborted = true;
            await _protocol.SendAbortSpeaking(reason);
        }

        // public void ToggleChatState();

        public async Task StartListening()
        {
            if (_deviceState == DeviceState.Activating)
            {
                await SetDeviceState(DeviceState.Idle);
                return;
            }

            if (_protocol == null)
            {
                Debug.LogError("Protocol not initialized");
                return;
            }

            _keepListening = false;
            if (_deviceState == DeviceState.Idle)
            {
                if (!_protocol.IsAudioChannelOpened())
                {
                    await SetDeviceState(DeviceState.Connecting);
                    if (!await _protocol.OpenAudioChannel())
                        return;
                }

                await _protocol.SendStartListening(ListenMode.ManualStop);
                await SetDeviceState(DeviceState.Listening);
            }
            else if (_deviceState == DeviceState.Speaking)
            {
                await AbortSpeaking(AbortReason.None);
                await _protocol.SendStartListening(ListenMode.ManualStop);
                await SetDeviceState(DeviceState.Listening);
            }
        }

        public async Task StopListening()
        {
            if (_deviceState == DeviceState.Listening)
            {
                await _protocol.SendStopListening();
                await SetDeviceState(DeviceState.Idle);
            }
        }

        // public void Reboot();
        // public void WakeWordInvoke(string wakeWord);

        private void InputAudio()
        {
            var codec = Context.Instance.AudioCodec;
            if (!codec.InputData(out var data))
                return;
            if (codec.InputSampleRate != _inputResampler.OutputSampleRate)
            {
                if (codec.InputChannels == 2)
                {
                    var micChannel = new short[data.Length / 2];
                    var referenceChannel = new short[data.Length / 2];
                    for (int i = 0, j = 0; i < micChannel.Length; ++i, j += 2)
                    {
                        micChannel[i] = data.Span[j];
                        referenceChannel[i] = data.Span[j + 1];
                    }

                    var resampledMic = new short[_inputResampler.GetOutputSamples(micChannel.Length)];
                    var resampledReference = new short[_referenceResampler.GetOutputSamples(referenceChannel.Length)];
                    _inputResampler.Process(micChannel, resampledMic);
                    _referenceResampler.Process(referenceChannel, resampledReference);
                    var resampledData = new short[resampledMic.Length + resampledReference.Length];
                    for (int i = 0, j = 0; i < resampledMic.Length; ++i, j += 2)
                    {
                        resampledData[j] = resampledMic[i];
                        resampledData[j + 1] = resampledReference[i];
                    }

                    data = resampledData;
                }
                else
                {
                    var resampled = new short[_inputResampler.GetOutputSamples(data.Length)];
                    _inputResampler.Process(data.Span, resampled);
                    data = resampled;
                }
            }

            if (Config.Instance.UseWakeWordDetect)
            {
                if (_deviceState != DeviceState.Listening && _wakeWordDetect.IsDetectionRunning)
                    _wakeWordDetect.Feed(data);
            }

            if (Config.Instance.UseAudioProcessing)
            {
                if (_audioProcessor.IsRunning) _audioProcessor.Input(data);
            }
            else
            {
                if (_deviceState == DeviceState.Listening)
                {
                    Schedule(() =>
                    {
                        _opusEncoder.Encode(data.Span,
                            opus => { Schedule(async () => { await _protocol.SendAudio(opus); }); });
                        return Task.CompletedTask;
                    }, AppTaskType.Background);
                }
            }
        }

        private void OutputAudio()
        {
            var now = DateTime.Now;
            var codec = Context.Instance.AudioCodec;
            const int maxSilenceSeconds = 10;
            if (!_audioDecodeQueue.TryPeek(out _))
            {
                if (_deviceState != DeviceState.Idle) return;
                var duration = (now - _lastOutputTime).TotalSeconds;
                if (duration > maxSilenceSeconds) codec.EnableOutput(false);
                return;
            }

            if (_deviceState == DeviceState.Listening)
            {
                _audioDecodeQueue.Clear();
                return;
            }

            _lastOutputTime = now;
            if (!_audioDecodeQueue.TryDequeue(out var opus))
                return;

            Schedule(() =>
            {
                if (_aborted)
                    return;
                if (!_opusDecoder.Decode(opus.Span, out var pcm))
                    return;
                if (_opusDecodeSampleRate != codec.OutputSampleRate)
                {
                    var resampled = new short[_outputResampler.GetOutputSamples(pcm.Length)];
                    _inputResampler.Process(pcm.Span, resampled.AsSpan());
                    pcm = resampled;
                }

                codec.OutputData(pcm.Span);
            }, AppTaskType.Background);
        }

        private void ResetDecoder()
        {
            _opusDecoder.ResetState();
            _audioDecodeQueue.Clear();
            _lastOutputTime = DateTime.Now;
        }

        private void SetDecodeSampleRate(int sampleRate)
        {
            if (_opusDecodeSampleRate == sampleRate) return;
            _opusDecodeSampleRate = sampleRate;
            _opusDecoder.Dispose();
            _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, Config.Instance.OpusFrameDurationMs);
            var codec = Context.Instance.AudioCodec;
            if (_opusDecodeSampleRate == codec.OutputSampleRate) return;
            Debug.Log($"Resampling audio from {_opusDecodeSampleRate} to {codec.OutputSampleRate}");
            _outputResampler ??= new OpusResampler();
            _outputResampler.Configure(_opusDecodeSampleRate, codec.OutputSampleRate);
        }

        private async Task ShowActivationCode()
        {
            Alert(Lang.Strings.ACTIVATION, _ota.ActivationMessage, "happy");
            await Task.Delay(1000);
        }
        
        // private void OnClockTimer();
        // private void PlayLocalFile(byte[] data);

        private void ConfigureAudioProcessing()
        {
            var codec = Context.Instance.AudioCodec;
            _audioProcessor.Initialize(codec.InputChannels, codec.InputReference);
            _audioProcessor.OnOutputData += data =>
            {
                Schedule(() =>
                {
                    _opusEncoder.Encode(data.Span,
                        opus => { Schedule(async () => { await _protocol.SendAudio(opus); }); });
                    return Task.CompletedTask;
                }, AppTaskType.Background);
            };

            _wakeWordDetect.Initialize(codec.InputChannels, codec.InputReference);
            _wakeWordDetect.OnVadStateChanged += speaking =>
            {
                if (_deviceState != DeviceState.Listening) return;
                _voiceDetected = speaking;
            };
            _wakeWordDetect.OnWakeWordDetected += wakeWord =>
            {
                Schedule(async () =>
                {
                    if (_deviceState == DeviceState.Idle)
                    {
                        await SetDeviceState(DeviceState.Connecting);
                        _wakeWordDetect.EncodeWakeWordData();
                        if (!await _protocol.OpenAudioChannel())
                        {
                            Debug.Log("Failed to open audio channel");
                            await SetDeviceState(DeviceState.Idle);
                            _wakeWordDetect.StartDetection();
                            return;
                        }

                        while (_wakeWordDetect.GetWakeWordOpus(out var opus))
                            await _protocol.SendAudio(opus);
                        await _protocol.SendWakeWordDetected(wakeWord);
                        Debug.Log($"Wake word detected: {wakeWord}");
                        _keepListening = true;
                        await SetDeviceState(DeviceState.Listening);
                    }
                    else if (_deviceState == DeviceState.Speaking)
                    {
                        await AbortSpeaking(AbortReason.WakeWordDetected);
                    }
                    else if (_deviceState == DeviceState.Activating)
                    {
                        await SetDeviceState(DeviceState.Idle);
                    }

                    _wakeWordDetect.StartDetection();
                });
            };
            _wakeWordDetect.StartDetection();
        }

        private void InitializeAudio()
        {
            var codec = Context.Instance.AudioCodec;
            _opusDecodeSampleRate = codec.OutputSampleRate;
            _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, Config.Instance.OpusFrameDurationMs);
            var resampleRate = Config.Instance.AudioInputResampleRate;
            _opusEncoder = new OpusEncoder(resampleRate, 1, Config.Instance.OpusFrameDurationMs);
            _inputResampler = new OpusResampler();
            _referenceResampler = new OpusResampler();
            _inputResampler.Configure(codec.InputSampleRate, resampleRate);
            _referenceResampler.Configure(codec.InputSampleRate, resampleRate);
            codec.Start();
        }

        private void InitializeProtocol()
        {
            var display = Context.Instance.Display;
            var codec = Context.Instance.AudioCodec;
            _protocol = new WebSocketProtocol();
            _protocol.OnNetworkError += (error) => { Alert(Lang.Strings.ERROR, error, "sad"); };
            _protocol.OnIncomingAudio += (data) => { _audioDecodeQueue.Enqueue(data); };
            _protocol.OnChannelOpened += () =>
            {
                if (_protocol.ServerSampleRate != codec.OutputSampleRate)
                    Debug.Log(
                        $"Server sample rate {_protocol.ServerSampleRate} does not match device output sample rate {codec.OutputSampleRate}, resampling may cause distortion");
                SetDecodeSampleRate(_protocol.ServerSampleRate);
            };
            _protocol.OnChannelClosed += () =>
            {
                Schedule(async () =>
                {
                    display.SetChatMessage("system", "");
                    await SetDeviceState(DeviceState.Idle);
                });
            };
            _protocol.OnIncomingJson += message =>
            {
                var type = message["type"].ToString();
                switch (type)
                {
                    case "tts":
                    {
                        var state = message["state"].ToString();
                        switch (state)
                        {
                            case "start":
                            {
                                Schedule(async () =>
                                {
                                    _aborted = false;
                                    if (_deviceState is DeviceState.Idle or DeviceState.Listening)
                                        await SetDeviceState(DeviceState.Speaking);
                                });
                                break;
                            }
                            case "stop":
                            {
                                if (_deviceState != DeviceState.Speaking) return;
                                Schedule(async () =>
                                {
                                    await GetTask(AppTaskType.Background);
                                    if (_keepListening)
                                    {
                                        await _protocol.SendStartListening(ListenMode.AutoStop);
                                        await SetDeviceState(DeviceState.Listening);
                                    }
                                    else
                                    {
                                        await SetDeviceState(DeviceState.Idle);
                                    }
                                });
                                break;
                            }
                            case "sentence_start":
                            {
                                var text = message["text"].ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    display.SetChatMessage("assistant", text);
                                }

                                break;
                            }
                        }

                        break;
                    }
                    case "stt":
                    {
                        var text = message["text"].ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            display.SetChatMessage("user", text);
                        }

                        break;
                    }
                    case "llm":
                    {
                        var emotion = message["emotion"].ToString();
                        if (!string.IsNullOrEmpty(emotion)) display.SetEmotion(emotion);
                        break;
                    }
                }
            };
            _protocol.Start();
        }

        private async Task CheckNewVersion()
        {
            var macAddr = Context.Instance.GetMacAddress();
            var boardName = Context.Instance.GetBoardName();
            _ota = new OTA();
            _ota.SetCheckVersionUrl(Config.Instance.OtaVersionUrl);
            _ota.SetHeader("Device-Id", macAddr);
            _ota.SetHeader("Accept-Language", Lang.Code.Value);
            _ota.SetHeader("User-Agent", $"{boardName}/{Context.Instance.GetVersion()}");
            _ota.SetPostData(Config.BuildOTAPostData(macAddr, boardName));
            const int MAX_RETRY = 10;
            for (var i = 0; i < MAX_RETRY; i++)
            {
                if (await _ota.CheckVersionAsync())
                {
                    if (!string.IsNullOrEmpty(_ota.ActivationCode))
                    {
                        await SetDeviceState(DeviceState.Activating);
                        await ShowActivationCode();
                        for (var t = 0; t < 60; t++)
                        {
                            if (_deviceState == DeviceState.Idle) break;
                            await Task.Delay(1000);
                        }

                        if (_deviceState == DeviceState.Idle) break;
                    }
                    else
                    {
                        break;
                    }
                }

                await Task.Delay(1000);
            }
        }

        private Task GetTask(AppTaskType taskType)
        {
            return _taskMap[taskType];
        }

        private void SetTask(AppTaskType taskType, Task task)
        {
            _taskMap[taskType] = task;
        }
    }
}