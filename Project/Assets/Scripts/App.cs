using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

    public class App : IDisposable
    {
        public static App Instance { get; } = new();

        private Protocol _protocol;
        private DeviceState _deviceState = DeviceState.Unknown;
        public DeviceState GetDeviceState() => _deviceState;
        private bool _keepListening;
        private bool _aborted;
        private bool _voiceDetected;
        public bool IsVoiceDetected() => _voiceDetected;
        private DateTime _lastOutputTime;
        private int _opusDecodeSampleRate = -1;
        private WakeWordDetect _wakeWordDetect;
        private AudioProcessor _audioProcessor;
        private OpusEncoder _opusEncoder;
        private OpusDecoder _opusDecoder;
        private OpusResampler _inputResampler;
        private OpusResampler _outputResampler;
        private OTA _ota;

        public async void Start()
        {
            InitializeTask();
            InitializePlatform();
            var display = Context.Instance.Display;
            SetDeviceState(DeviceState.Starting);
            InitializeAudio();
            display.SetStatus("正在加载协议");
            InitializeProtocol();
            await CheckNewVersion();
            if (Config.Instance.UseAudioProcessing) ConfigureAudioProcessing();
            SetDeviceState(DeviceState.Idle);
            MainLoop().Forget();
        }

        private async UniTaskVoid MainLoop()
        {
            var codec = Context.Instance.AudioCodec;
            while (true)
            {
                await UniTask.Delay(30);
                switch (_deviceState)
                {
                    case DeviceState.Listening:
                        InputAudio();
                        break;
                    case DeviceState.Idle:
                        var duration = (DateTime.Now - _lastOutputTime).TotalSeconds;
                        const int maxSilenceSeconds = 10;
                        if (duration > maxSilenceSeconds) codec.EnableOutput(false);
                        break;
                }
            }
        }

        public void Dispose()
        {
            _protocol?.Dispose();
            _opusDecoder?.Dispose();
            _opusEncoder?.Dispose();
            _inputResampler?.Dispose();
            _outputResampler?.Dispose();
            _audioProcessor?.Dispose();
        }

        private void InitializePlatform()
        {
        }

        private void InitializeTask()
        {
        }

        private void SetDeviceState(DeviceState state)
        {
            if (_deviceState == state) return;
            _deviceState = state;
            Debug.Log("设备状态改变: " + _deviceState);
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

        private void Alert(string status, string message, string emotion = null)
        {
            var display = Context.Instance.Display;
            display.SetStatus(status);
            display.SetChatMessage("system", message);
            display.SetEmotion(emotion);
            Debug.Log("Alert: " + status + " " + message + " " + emotion);
        }

        private async UniTask AbortSpeaking(AbortReason reason)
        {
            Debug.Log("Abort speaking");
            _aborted = true;
            await _protocol.SendAbortSpeaking(reason);
        }

        public async UniTask ToggleChatState()
        {
            if (_deviceState == DeviceState.Activating)
            {
                SetDeviceState(DeviceState.Idle);
                return;
            }

            if (_protocol == null)
            {
                Debug.Log("Protocol not initialized");
                return;
            }

            switch (_deviceState)
            {
                case DeviceState.Idle:
                    SetDeviceState(DeviceState.Connecting);
                    if (!await _protocol.OpenAudioChannel())
                        return;
                    _keepListening = true;
                    await _protocol.SendStartListening(ListenMode.AutoStop);
                    SetDeviceState(DeviceState.Listening);
                    break;
                case DeviceState.Speaking:
                    await AbortSpeaking(AbortReason.None);
                    break;
                case DeviceState.Listening:
                    await _protocol.CloseAudioChannel();
                    break;
            }
        }

        public async UniTask StartListening()
        {
            if (_deviceState == DeviceState.Activating)
            {
                SetDeviceState(DeviceState.Idle);
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
                    SetDeviceState(DeviceState.Connecting);
                    if (!await _protocol.OpenAudioChannel())
                        return;
                }

                await _protocol.SendStartListening(ListenMode.ManualStop);
                SetDeviceState(DeviceState.Listening);
            }
            else if (_deviceState == DeviceState.Speaking)
            {
                await AbortSpeaking(AbortReason.None);
                await _protocol.SendStartListening(ListenMode.ManualStop);
                SetDeviceState(DeviceState.Listening);
            }
        }

        public async UniTask StopListening()
        {
            if (_deviceState == DeviceState.Listening)
            {
                await _protocol.SendStopListening();
                SetDeviceState(DeviceState.Idle);
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
                _inputResampler.Process(data, out data);
            if (Config.Instance.UseWakeWordDetect && _deviceState != DeviceState.Listening &&
                _wakeWordDetect.IsDetectionRunning) _wakeWordDetect.Feed(data);
            if (Config.Instance.UseAudioProcessing)
            {
                if (_audioProcessor.IsRunning) _audioProcessor.Input(data);
            }
            else
            {
                if (_deviceState == DeviceState.Listening)
                    _opusEncoder.Encode(data, opus => { _protocol.SendAudio(opus).Forget(); });
            }
        }

        private void OutputAudio(ReadOnlySpan<byte> opus)
        {
            if (_deviceState == DeviceState.Listening)
                return;
            _lastOutputTime = DateTime.Now;
            if (_aborted) return;
            if (!_opusDecoder.Decode(opus, out var pcm)) return;
            var codec = Context.Instance.AudioCodec;
            if (_opusDecodeSampleRate != codec.OutputSampleRate) _outputResampler.Process(pcm, out pcm);
            codec.OutputData(pcm);
        }

        private void ResetDecoder()
        {
            _opusDecoder.ResetState();
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

        private async UniTask ShowActivationCode()
        {
            Alert(Lang.Strings.ACTIVATION, _ota.ActivationMessage, "happy");
            await UniTask.Delay(1000);
        }

        private void ConfigureAudioProcessing()
        {
            var codec = Context.Instance.AudioCodec;
            _audioProcessor.Initialize(codec.InputChannels);
            _audioProcessor.OnOutputData += data =>
            {
                _opusEncoder.Encode(data.Span, opus => { _protocol.SendAudio(opus).Forget(); });
            };

            _wakeWordDetect.Initialize(codec.InputChannels);
            _wakeWordDetect.OnVadStateChanged += speaking =>
            {
                if (_deviceState != DeviceState.Listening) return;
                _voiceDetected = speaking;
            };
            _wakeWordDetect.OnWakeWordDetected += wakeWord =>
            {
                UniTask.Void(async () =>
                {
                    if (_deviceState == DeviceState.Idle)
                    {
                        SetDeviceState(DeviceState.Connecting);
                        _wakeWordDetect.EncodeWakeWordData();
                        if (!await _protocol.OpenAudioChannel())
                        {
                            Debug.Log("Failed to open audio channel");
                            SetDeviceState(DeviceState.Idle);
                            _wakeWordDetect.StartDetection();
                            return;
                        }

                        while (_wakeWordDetect.GetWakeWordOpus(out var opus))
                            await _protocol.SendAudio(opus);
                        await _protocol.SendWakeWordDetected(wakeWord);
                        Debug.Log($"Wake word detected: {wakeWord}");
                        _keepListening = true;
                        SetDeviceState(DeviceState.Listening);
                    }
                    else if (_deviceState == DeviceState.Speaking)
                    {
                        await AbortSpeaking(AbortReason.WakeWordDetected);
                    }
                    else if (_deviceState == DeviceState.Activating)
                    {
                        SetDeviceState(DeviceState.Idle);
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
            _inputResampler.Configure(codec.InputSampleRate, resampleRate);
            codec.Start();
        }

        private void InitializeProtocol()
        {
            var display = Context.Instance.Display;
            var codec = Context.Instance.AudioCodec;
            _protocol = new WebSocketProtocol();
            _protocol.OnNetworkError += (error) => { Alert(Lang.Strings.ERROR, error, "sad"); };
            _protocol.OnIncomingAudio += OutputAudio;
            _protocol.OnChannelOpened += () =>
            {
                if (_protocol.ServerSampleRate != codec.OutputSampleRate)
                    Debug.Log(
                        $"Server sample rate {_protocol.ServerSampleRate} does not match device output sample rate {codec.OutputSampleRate}, resampling may cause distortion");
                SetDecodeSampleRate(_protocol.ServerSampleRate);
            };
            _protocol.OnChannelClosed += () =>
            {
                display.SetChatMessage("system", "");
                SetDeviceState(DeviceState.Idle);
            };
            _protocol.OnIncomingJson += message =>
            {
                var type = message["type"].ToString();
                switch (type)
                {
                    case "hello":
                    {
                        break;
                    }
                    case "tts":
                    {
                        var state = message["state"].ToString();
                        switch (state)
                        {
                            case "start":
                            {
                                _aborted = false;
                                if (_deviceState is DeviceState.Idle or DeviceState.Listening)
                                    SetDeviceState(DeviceState.Speaking);
                                break;
                            }
                            case "stop":
                            {
                                if (_deviceState != DeviceState.Speaking) return;
                                UniTask.Void(async () =>
                                {
                                    if (_keepListening)
                                    {
                                        await _protocol.SendStartListening(ListenMode.AutoStop);
                                        SetDeviceState(DeviceState.Listening);
                                    }
                                    else
                                    {
                                        SetDeviceState(DeviceState.Idle);
                                    }
                                });
                                break;
                            }
                            case "sentence_start":
                            {
                                var text = message["text"].ToString();
                                if (!string.IsNullOrEmpty(text)) display.SetChatMessage("assistant", text);
                                break;
                            }
                        }

                        break;
                    }
                    case "stt":
                    {
                        var text = message["text"].ToString();
                        if (!string.IsNullOrEmpty(text)) display.SetChatMessage("user", text);
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

        private async UniTask CheckNewVersion()
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
                        SetDeviceState(DeviceState.Activating);
                        await ShowActivationCode();
                        for (var t = 0; t < 60; t++)
                        {
                            if (_deviceState == DeviceState.Idle) break;
                            await UniTask.Delay(1000);
                        }

                        if (_deviceState == DeviceState.Idle) break;
                    }
                    else
                    {
                        break;
                    }
                }

                await UniTask.Delay(1000);
            }
        }
    }
}