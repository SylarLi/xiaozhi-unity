using System;
using System.Buffers;
using System.Threading;
using Cysharp.Threading.Tasks;
using SherpaOnnx;

namespace XiaoZhi.Unity
{
    public class SherpaOnnxWakeService : WakeService
    {
        private bool _isRunning;
        private KeywordSpotterConfig _kwsConfig;
        private KeywordSpotter _kws;
        private OnlineStream _stream;
        private CancellationTokenSource _loopCts;
        private VadModelConfig _vadConfig;
        private VoiceActivityDetector _vad;

        public override void Initialize(int sampleRate)
        {
            var resourceType = FileUtility.FileType.StreamingAssets;
#if UNITY_ANDROID && !UNITY_EDITOR
            resourceType = FileUtility.FileType.DataPath;
#endif
            _kwsConfig = new KeywordSpotterConfig();
            _kwsConfig.FeatConfig.SampleRate = sampleRate;
            _kwsConfig.FeatConfig.FeatureDim = 80;
            _kwsConfig.ModelConfig.Transducer.Encoder = FileUtility.GetFullPath(resourceType,
                Config.Instance.KeyWordSpotterModelConfigTransducerEncoder);
            _kwsConfig.ModelConfig.Transducer.Decoder = FileUtility.GetFullPath(resourceType,
                Config.Instance.KeyWordSpotterModelConfigTransducerDecoder);
            _kwsConfig.ModelConfig.Transducer.Joiner = FileUtility.GetFullPath(resourceType,
                Config.Instance.KeyWordSpotterModelConfigTransducerJoiner);
            _kwsConfig.ModelConfig.Tokens =
                FileUtility.GetFullPath(resourceType, Config.Instance.KeyWordSpotterModelConfigToken);
            _kwsConfig.ModelConfig.Provider = "cpu";
            _kwsConfig.ModelConfig.NumThreads = Config.Instance.KeyWordSpotterModelConfigNumThreads;
            _kwsConfig.ModelConfig.Debug = 0;
            _kwsConfig.KeywordsFile =
                FileUtility.GetFullPath(resourceType, Config.Instance.KeyWordSpotterKeyWordsFile);
            _vadConfig = new VadModelConfig();
            _vadConfig.SileroVad.Model = FileUtility.GetFullPath(resourceType, Config.Instance.VadModelConfig);
            _vadConfig.SileroVad.MaxSpeechDuration = 4;
            _vadConfig.Debug = 0;
        }

        public override void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _kws = new KeywordSpotter(_kwsConfig);
            _stream = _kws.CreateStream();
            _vad = new VoiceActivityDetector(_vadConfig, 4);
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
        }

        public override void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
            }

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_kws != null)
            {
                _kws.Dispose();
                _kws = null;
            }

            if (_vad != null)
            {
                _vad.Dispose();
                _vad = null;
            }
        }

        public override void Feed(ReadOnlySpan<short> data)
        {
            var floatPcm = ArrayPool<float>.Shared.Rent(data.Length);
            Tools.PCM16Short2Float(data, floatPcm);
            _vad.AcceptWaveform(floatPcm);
            _stream.AcceptWaveform(_kwsConfig.FeatConfig.SampleRate, floatPcm);
            ArrayPool<float>.Shared.Return(floatPcm);
        }

        public override bool IsRunning => _isRunning;

        private async UniTaskVoid LoopUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IsVoiceDetected = _vad.IsSpeechDetected();
                while (_kws.IsReady(_stream))
                {
                    _kws.Decode(_stream);
                    var result = _kws.GetResult(_stream);
                    if (result.Keyword != string.Empty)
                    {
                        _kws.Reset(_stream);
                        RaiseWakeWordDetected(lastDetectedWakeWord = result.Keyword);
                        break;
                    }
                }

                await UniTask.Delay(100, DelayType.Realtime, PlayerLoopTiming.Update, token);
            }
        }
    }
}