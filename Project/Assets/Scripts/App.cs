using System;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.iOS;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity
{
    public enum DeviceState
    {
        Unknown,
        Starting,
        Idle,
        Connecting,
        Listening,
        Speaking,
        Activating,
        Error
    }

    public class App : IDisposable
    {
        private Context _context;
        private Protocol _protocol;
        private DeviceState _deviceState = DeviceState.Unknown;
        public DeviceState GetDeviceState() => _deviceState;
        private bool _voiceDetected;
        public bool VoiceDetected => _voiceDetected;
        private bool _keepListening;
        private bool _aborted;
        private int _opusDecodeSampleRate = -1;
        private WakeService _wakeService;
        private OpusEncoder _opusEncoder;
        private OpusDecoder _opusDecoder;
        private OpusResampler _inputResampler;
        private OpusResampler _outputResampler;
        private OTA _ota;
        private CancellationTokenSource _cts;
        private IDisplay _display;
        private AudioCodec _codec;
        public AudioCodec GetCodec() => _codec;
        private Settings _settings;

        public event Action<DeviceState> OnDeviceStateUpdate;

        public void Init(Context context)
        {
            _context = context;
            _cts = new CancellationTokenSource();
            _settings = new Settings("app");
            Application.runInBackground = true;
        }

        public async UniTaskVoid Start()
        {
            await InitUI();
            await Config.LoadConfig();
            await Lang.Strings.LoadStrings();
            SetDeviceState(DeviceState.Starting);
            if (!await CheckRequestPermission())
            {
                SetDeviceState(DeviceState.Error);
                _display.SetChatMessage(ChatRole.System, Lang.Strings.Get("Permission_Request_Failed"));
                return;
            }

            if (!await CheckNewVersion())
            {
                SetDeviceState(DeviceState.Error);
                _display.SetChatMessage(ChatRole.System, Lang.Strings.Get("ACTIVATION_FAILED_TIPS"));
                return;
            }

            SetDeviceState(DeviceState.Starting);
            _display.SetChatMessage(ChatRole.System, "");
            if (Config.Instance.UseWakeWordDetect)
            {
                _display.SetStatus(Lang.Strings.Get("LOADING_RESOURCES"));
                await PrepareResource(_cts.Token);
                _display.SetStatus(Lang.Strings.Get("LOADING_MODEL"));
                await InitializeWakeService();
            }

            InitializeAudio();
            InitializeProtocol();
            SetDeviceState(DeviceState.Idle);
            UniTask.Void(MainLoop, _cts.Token);
        }

        private async UniTaskVoid MainLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                InputAudio();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _protocol?.Dispose();
            _wakeService?.Dispose();
            _opusDecoder?.Dispose();
            _opusEncoder?.Dispose();
            _inputResampler?.Dispose();
            _outputResampler?.Dispose();
            _codec?.Dispose();
        }

        private async UniTask InitUI()
        {
            await _context.UIManager.Load();
            _display = await _context.UIManager.ShowSceneUI<MainUI>();
        }

        private async UniTask PrepareResource(CancellationToken cancellationToken)
        {
            if (!IsFirstEnter()) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            var streamingAssets = new[]
            {
                Config.Instance.KeyWordSpotterModelConfigTransducerEncoder,
                Config.Instance.KeyWordSpotterModelConfigTransducerDecoder,
                Config.Instance.KeyWordSpotterModelConfigTransducerJoiner,
                Config.Instance.KeyWordSpotterModelConfigToken,
                Config.Instance.KeyWordSpotterKeyWordsFile,
                Config.Instance.VadModelConfig
            };
            await UniTask.WhenAll(streamingAssets.Select(i => FileUtility.CopyStreamingAssetsToDataPath(i, cancellationToken)));
            await UniTask.SwitchToMainThread();
#endif
            MarkAsNotFirstEnter();
        }

        private void SetDeviceState(DeviceState state)
        {
            if (_deviceState == state) return;
            _deviceState = state;
            Debug.Log("设备状态改变: " + _deviceState);
            switch (state)
            {
                case DeviceState.Unknown:
                case DeviceState.Idle:
                    _display.SetStatus(Lang.Strings.Get("STATE_STANDBY"));
                    _display.SetEmotion("neutral");
                    break;

                case DeviceState.Connecting:
                    _display.SetStatus(Lang.Strings.Get("STATE_CONNECTING"));
                    _display.SetChatMessage(ChatRole.System, "");
                    break;

                case DeviceState.Listening:
                    _display.SetStatus(Lang.Strings.Get("STATE_LISTENING"));
                    _display.SetEmotion("neutral");
                    _opusDecoder.ResetState();
                    _opusEncoder.ResetState();
                    break;

                case DeviceState.Speaking:
                    _display.SetStatus(Lang.Strings.Get("STATE_SPEAKING"));
                    _opusDecoder.ResetState();
                    break;
                case DeviceState.Starting:
                    _display.SetStatus(Lang.Strings.Get("STATE_STARTING"));
                    _display.SetEmotion("loading");
                    break;
                case DeviceState.Activating:
                    _display.SetStatus(Lang.Strings.Get("ACTIVATION"));
                    _display.SetEmotion("activation");
                    break;
                case DeviceState.Error:
                    _display.SetStatus(Lang.Strings.Get("STATE_ERROR"));
                    _display.SetEmotion("error");
                    break;
            }

            OnDeviceStateUpdate?.Invoke(_deviceState);
        }

        private async UniTask AbortSpeaking(AbortReason reason)
        {
            Debug.Log("Abort speaking");
            _aborted = true;
            await _protocol.SendAbortSpeaking(reason);
        }

        public async UniTaskVoid ToggleChatState()
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
                    SetDeviceState(DeviceState.Idle);
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

        private void InputAudio()
        {
            var times = Mathf.CeilToInt(Time.deltaTime * 1000 / AudioCodec.InputFrameSizeMs);
            for (var i = 0; i < times; i++)
            {
                if (!_codec.InputData(out var data))
                    break;
                if (_codec.InputSampleRate != _inputResampler.OutputSampleRate)
                    _inputResampler.Process(data, out data);
                if (_deviceState is DeviceState.Listening)
                    _opusEncoder.Encode(data, opus => { _protocol.SendAudio(opus).Forget(); });
                if (_wakeService is { IsRunning: true })
                    _wakeService.Feed(data);
            }
        }

        private void OutputAudio(ReadOnlySpan<byte> opus)
        {
            if (_deviceState == DeviceState.Listening) return;
            if (_aborted) return;
            if (!_opusDecoder.Decode(opus, out var pcm)) return;
            if (_opusDecodeSampleRate != _codec.OutputSampleRate) _outputResampler.Process(pcm, out pcm);
            _codec.OutputData(pcm);
        }

        private void SetDecodeSampleRate(int sampleRate)
        {
            if (_opusDecodeSampleRate == sampleRate) return;
            _opusDecodeSampleRate = sampleRate;
            _opusDecoder.Dispose();
            _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, Config.Instance.OpusFrameDurationMs);
            if (_opusDecodeSampleRate == _codec.OutputSampleRate) return;
            Debug.Log($"Resampling audio from {_opusDecodeSampleRate} to {_codec.OutputSampleRate}");
            _outputResampler ??= new OpusResampler();
            _outputResampler.Configure(_opusDecodeSampleRate, _codec.OutputSampleRate);
        }

        private async UniTask InitializeWakeService()
        {
            _wakeService = new SherpaOnnxWakeService();
            _wakeService.Initialize(Config.Instance.ServerInputSampleRate);
            _wakeService.OnVadStateChanged += speaking => { _voiceDetected = speaking; };
            _wakeService.OnWakeWordDetected += wakeWord =>
            {
                Debug.Log($"Wake word detected: {wakeWord}");
                UniTask.Void(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    switch (_deviceState)
                    {
                        case DeviceState.Idle:
                        {
                            SetDeviceState(DeviceState.Connecting);
                            if (!await _protocol.OpenAudioChannel())
                            {
                                Debug.Log("Failed to open audio channel");
                                SetDeviceState(DeviceState.Idle);
                                return;
                            }

                            await _protocol.SendWakeWordDetected(wakeWord);
                            _keepListening = true;
                            SetDeviceState(DeviceState.Listening);
                            break;
                        }
                        case DeviceState.Speaking:
                            await AbortSpeaking(AbortReason.WakeWordDetected);
                            break;
                    }
                });
            };
            await UniTask.SwitchToThreadPool();
            _wakeService.Start();
            await UniTask.SwitchToMainThread();
        }

        private void InitializeAudio()
        {
            var inputSampleRate = Config.Instance.AudioInputSampleRate;
            var outputSampleRate = Config.Instance.AudioOutputSampleRate;
            _opusDecodeSampleRate = outputSampleRate;
            _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, Config.Instance.OpusFrameDurationMs);
            var resampleRate = Config.Instance.ServerInputSampleRate;
            _opusEncoder = new OpusEncoder(resampleRate, 1, Config.Instance.OpusFrameDurationMs);
            _inputResampler = new OpusResampler();
            _inputResampler.Configure(inputSampleRate, resampleRate);
            _codec = new FMODAudioCodec(inputSampleRate, 1, outputSampleRate, 1);
            _codec.Start();
        }

        private void InitializeProtocol()
        {
            _protocol = new WebSocketProtocol();
            _protocol.OnNetworkError += (error) => { _context.UIManager.ShowNotificationUI(error).Forget(); };
            _protocol.OnIncomingAudio += OutputAudio;
            _protocol.OnChannelOpened += () =>
            {
                if (_protocol.ServerSampleRate != _codec.OutputSampleRate)
                    Debug.Log(
                        $"Server sample rate {_protocol.ServerSampleRate} does not match device output sample rate {_codec.OutputSampleRate}, resampling may cause distortion");
                SetDecodeSampleRate(_protocol.ServerSampleRate);
            };
            _protocol.OnChannelClosed += () =>
            {
                _display.SetChatMessage(ChatRole.System, "");
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
                                {
                                    SetDeviceState(DeviceState.Speaking);
                                }

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
                                if (!string.IsNullOrEmpty(text)) _display.SetChatMessage(ChatRole.Assistant, text);
                                break;
                            }
                        }

                        break;
                    }
                    case "stt":
                    {
                        var text = message["text"].ToString();
                        if (!string.IsNullOrEmpty(text)) _display.SetChatMessage(ChatRole.User, text);
                        break;
                    }
                    case "llm":
                    {
                        var emotion = message["emotion"].ToString();
                        if (!string.IsNullOrEmpty(emotion)) _display.SetEmotion(emotion);
                        break;
                    }
                }
            };
            _protocol.Start();
        }

        private async UniTask<bool> CheckNewVersion()
        {
            var success = false;
            var macAddr = Config.GetMacAddress();
            var boardName = Config.GetBoardName();
            _ota = new OTA();
            _ota.SetCheckVersionUrl(Config.Instance.OtaVersionUrl);
            _ota.SetHeader("Device-Id", macAddr);
            _ota.SetHeader("Accept-Language", Config.Instance.LangCode);
            _ota.SetHeader("User-Agent", $"{boardName}/{Config.GetVersion()}");
            _ota.SetPostData(Config.BuildOtaPostData(macAddr, boardName));
            var showTips = true;
            const int maxRetry = 100;
            for (var i = 0; i < maxRetry; i++)
            {
                if (await _ota.CheckVersionAsync())
                {
                    if (string.IsNullOrEmpty(_ota.ActivationCode))
                    {
                        success = true;
                        break;
                    }

                    SetDeviceState(DeviceState.Activating);
                    _display.SetChatMessage(ChatRole.System, _ota.ActivationMessage);
                    try
                    {
                        GUIUtility.systemCopyBuffer = Regex.Match(_ota.ActivationMessage, @"\d+").Value;
                        if (showTips)
                        {
                            showTips = false;
                            _context.UIManager.ShowNotificationUI(Lang.Strings.Get("ACTIVATION_CODE_COPIED")).Forget();
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                await UniTask.Delay(3 * 1000);
            }
            
            return success;
        }

        private async UniTask<bool> CheckRequestPermission()
        {
            var success = true;
            var result = await PermissionManager.RequestPermissions(PermissionType.ReadStorage,
                PermissionType.WriteStorage, PermissionType.Microphone);
            foreach (var i in result)
            {
                if (i.Granted) continue;
                var permissionName =
                    Lang.Strings.Get($"Permission_{Enum.GetName(typeof(PermissionType), i.Type)}");
                _context.UIManager.ShowNotificationUI(
                        Lang.Strings.Get(string.Format(Lang.Strings.Get("Permission_Request_Failed"),
                            permissionName)))
                    .Forget();
                success = false;
            }

            return success;
        }

        private bool IsFirstEnter()
        {
            return !_settings.HasKey("i_have_played_with_xiaozhi");
        }

        private void MarkAsNotFirstEnter()
        {
            _settings.SetInt("i_have_played_with_xiaozhi", 1);
            _settings.Save();
        }
    }
}