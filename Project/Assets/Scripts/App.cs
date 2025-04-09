using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
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

    public enum BreakMode
    {
        None,
        Keyword,
        VAD,
        Free
    }

    public enum DisplayMode
    {
        Emoji,
        VRM
    }

    public class App : IDisposable
    {
        private Context _context;
        private Protocol _protocol;
        private DeviceState _deviceState = DeviceState.Unknown;
        public DeviceState GetDeviceState() => _deviceState;

        public bool IsDeviceReady() => _deviceState is DeviceState.Idle or DeviceState.Connecting
            or DeviceState.Speaking or DeviceState.Listening;

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
        private DateTime _vadAbortedSilenceTime;
        private DynamicBuffer<short> _freeBuffer;

        public event Action<DeviceState> OnDeviceStateUpdate;

        public void Init(Context context)
        {
            _context = context;
            _cts = new CancellationTokenSource();
        }

        public async UniTaskVoid Start()
        {
            AppSettings.Load();
            await Config.Load();
            await InitDisplay();
            await Lang.Strings.LoadStrings(AppSettings.Instance.GetLangCode());
            SetDeviceState(DeviceState.Starting);
            if (!await CheckRequestPermission())
            {
                SetDeviceState(DeviceState.Error);
                _display.SetChatMessage(ChatRole.System, Lang.Strings.Get("Permission_Request_Failed"));
                return;
            }

            await CheckInternetReachability();
            if (!await CheckNewVersion(_cts.Token))
            {
                SetDeviceState(DeviceState.Error);
                _display.SetChatMessage(ChatRole.System, Lang.Strings.Get("ACTIVATION_FAILED_TIPS"));
                return;
            }

            SetDeviceState(DeviceState.Starting);
            _display.SetChatMessage(ChatRole.System, "");
            if (Config.Instance.EnableWakeService)
            {
                _display.SetStatus(Lang.Strings.Get("LOADING_RESOURCES"));
                await PrepareResource(_cts.Token);
                _display.SetStatus(Lang.Strings.Get("LOADING_MODEL"));
                await InitializeWakeService();
            }

            InitializeAudio();
            if (!_codec.GetInputDevice(out _))
            {
                SetDeviceState(DeviceState.Error);
                _display.SetStatus(Lang.Strings.Get("STATE_MIC_NOT_FOUND"));
                return;
            }

            InitializeProtocol();
            StartDisplay();
            SetDeviceState(DeviceState.Idle);
            UniTask.Void(MainLoop, _cts.Token);
        }

        private async UniTaskVoid MainLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                InputAudio();
                CheckProtocol();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _display?.Dispose();
            _codec?.Dispose();
            _wakeService?.Dispose();
            _protocol?.Dispose();
            _opusDecoder?.Dispose();
            _opusEncoder?.Dispose();
            _inputResampler?.Dispose();
            _outputResampler?.Dispose();
        }

        private async UniTask InitDisplay()
        {
            await _context.UIManager.Load();
            _display = AppSettings.Instance.GetDisplayMode() switch
            {
                DisplayMode.Emoji => new EmojiDisplay(_context),
                DisplayMode.VRM => new VRMDisplay(_context),
                _ => throw new ArgumentOutOfRangeException()
            };

            await _display.Load();
        }

        private void StartDisplay()
        {
            _display.Start();
        }

        private async UniTask PrepareResource(CancellationToken cancellationToken)
        {
            if (!AppSettings.Instance.IsFirstEnter()) return;
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
#else
            var streamingAssets = new[]
            {
                Config.Instance.KeyWordSpotterKeyWordsFile
            };
#endif
            await UniTask.WhenAll(streamingAssets.Select(i =>
                FileUtility.CopyStreamingAssetsToDataPath(i, cancellationToken)));
            await UniTask.SwitchToMainThread(cancellationToken);
            AppSettings.Instance.MarkAsNotFirstEnter();
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
                    _display.SetEmotion("sleep");
                    break;

                case DeviceState.Connecting:
                    _display.SetStatus(Lang.Strings.Get("STATE_CONNECTING"));
                    _display.SetChatMessage(ChatRole.System, "");
                    _display.SetEmotion("yawn");
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
                    if (_wakeService is { IsRunning: true })
                        _wakeService.ClearVadBuffer();
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
            if (_aborted) return;
            Debug.Log("Abort speaking");
            _aborted = true;
            await _protocol.SendAbortSpeaking(reason);
        }

        private async UniTask<bool> OpenAudioChannel()
        {
            if (_protocol.IsAudioChannelOpened()) return true;
            SetDeviceState(DeviceState.Connecting);
            if (!await _protocol.OpenAudioChannel())
            {
                SetDeviceState(DeviceState.Idle);
                _context.UIManager.ShowNotificationUI(Lang.Strings.Get("Connect_Failed_Tips")).Forget();
                return false;
            }

            return true;
        }

        public async UniTaskVoid ToggleChatState()
        {
            switch (_deviceState)
            {
                case DeviceState.Idle:
                    if (!await OpenAudioChannel()) return;
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
            switch (_deviceState)
            {
                case DeviceState.Idle:
                {
                    if (!await OpenAudioChannel()) return;
                    await _protocol.SendStartListening(ListenMode.ManualStop);
                    SetDeviceState(DeviceState.Listening);
                    break;
                }
                case DeviceState.Speaking:
                    await AbortSpeaking(AbortReason.None);
                    await _protocol.SendStartListening(ListenMode.ManualStop);
                    SetDeviceState(DeviceState.Listening);
                    break;
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
                if (!_codec.InputData(out var data)) break;
                if (_aborted && DateTime.Now < _vadAbortedSilenceTime) continue;
                if (_codec.InputSampleRate != _inputResampler.OutputSampleRate)
                    _inputResampler.Process(data, out data);
                if (_aborted && _freeBuffer.Count > 0)
                {
                    _freeBuffer.Write(data);
                    continue;
                }

                if (_deviceState is DeviceState.Listening)
                    _opusEncoder.Encode(data, opus => { _protocol.SendAudio(opus).Forget(); });
                if (_wakeService is { IsRunning: true }) _wakeService.Feed(data);
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

        private void SendAudio(ReadOnlySpan<short> data)
        {
            var frameSize = Config.Instance.ServerInputSampleRate / 1000 * Config.Instance.OpusFrameDurationMs *
                            _codec.InputChannels;
            var dataLen = data.Length;
            for (var i = 0; i < dataLen; i += frameSize)
            {
                var end = Math.Min(i + frameSize, dataLen);
                _opusEncoder.Encode(data[i..end], opus => { _protocol.SendAudio(opus).Forget(); });
            }
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
            _freeBuffer = new DynamicBuffer<short>();
            _wakeService = new SherpaOnnxWakeService();
            _wakeService.Initialize(Config.Instance.ServerInputSampleRate);
            _wakeService.OnVadStateChanged += speaking =>
            {
                if (_voiceDetected == speaking) return;
                _voiceDetected = speaking;
                if (_voiceDetected && _deviceState == DeviceState.Speaking)
                {
                    switch (AppSettings.Instance.GetBreakMode())
                    {
                        case BreakMode.VAD:
                            var count1 = _wakeService.ReadVadBuffer(ref _freeBuffer.Memory);
                            if (count1 == 0) break;
                            Debug.Log("Break by vad.");
                            _vadAbortedSilenceTime = DateTime.Now.AddMilliseconds(1000);
                            AbortSpeaking(AbortReason.WakeWordDetected).Forget();
                            break;
                        case BreakMode.Free:
                            var count2 = _wakeService.ReadVadBuffer(ref _freeBuffer.Memory);
                            if (count2 == 0) break;
                            _freeBuffer.SetCount(count2);
                            Debug.Log($"Break by free {_freeBuffer.Count}");
                            AbortSpeaking(AbortReason.WakeWordDetected).Forget();
                            break;
                    }
                }
            };
            _wakeService.OnWakeWordDetected += wakeWord =>
            {
                UniTask.Void(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    switch (_deviceState)
                    {
                        case DeviceState.Idle:
                        {
                            if (!await OpenAudioChannel()) return;
                            await _protocol.SendWakeWordDetected(wakeWord);
                            _keepListening = true;
                            SetDeviceState(DeviceState.Listening);
                            break;
                        }
                        case DeviceState.Speaking:
                            if (AppSettings.Instance.GetBreakMode() == BreakMode.Keyword)
                            {
                                Debug.Log("Break by keyword.");
                                await AbortSpeaking(AbortReason.WakeWordDetected);
                            }

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
            _codec.SetOutputVolume(AppSettings.Instance.GetOutputVolume());
            _codec.Start();
            AppSettings.Instance.OnOutputVolumeUpdate += volume => { _codec.SetOutputVolume(volume); };
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
                                        if (_aborted && _freeBuffer.Count > 0)
                                        {
                                            SendAudio(_freeBuffer.Read());
                                            _freeBuffer.Clear();
                                        }
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

        private async UniTask<bool> CheckNewVersion(CancellationToken cancellationToken = default)
        {
            var success = false;
            var macAddr = AppSettings.Instance.GetMacAddress();
            var boardName = Config.GetBoardName();
            _ota = new OTA();
            _ota.SetCheckVersionUrl(Config.Instance.OtaVersionUrl);
            _ota.SetHeader("Device-Id", macAddr);
            _ota.SetHeader("Accept-Language", "zh-CN");
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

                await UniTask.Delay(3 * 1000, cancellationToken: cancellationToken);
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

        private async UniTask CheckInternetReachability()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                _display.SetStatus(Lang.Strings.Get("State_Internet_Break"));
            while (Application.internetReachability == NetworkReachability.NotReachable)
                await UniTask.Delay(1000);
        }

        private void CheckProtocol()
        {
            if (_deviceState is DeviceState.Listening or DeviceState.Speaking &&
                _protocol?.IsAudioChannelOpened() != true)
            {
                SetDeviceState(DeviceState.Idle);
                _context.UIManager.ShowNotificationUI(Lang.Strings.Get("CONNECTION_CLOSED_TIPS")).Forget();
            }
        }
    }
}