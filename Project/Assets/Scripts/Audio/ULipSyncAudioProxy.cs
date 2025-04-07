using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class ULipSyncAudioProxy : IDisposable
    {
        private readonly uLipSync.uLipSync _uLipSync;

        private readonly AudioCodec _codec;
        
        private CancellationTokenSource _updateCts;

        private readonly float[] _buffer;

        public ULipSyncAudioProxy(uLipSync.uLipSync uLipSync, AudioCodec codec)
        {
            _codec = codec;
            _uLipSync = uLipSync;
            _uLipSync.profile.sampleCount = AudioCodec.SpectrumWindowSize;
            _uLipSync.profile.targetSampleRate = codec.OutputSampleRate;
            var configuration = AudioSettings.GetConfiguration();
            configuration.sampleRate = codec.OutputSampleRate;
            AudioSettings.Reset(configuration);
            _buffer = new float[_uLipSync.profile.sampleCount];
        }

        public void Start()
        {
            _updateCts = new CancellationTokenSource();
            UniTask.Void(Update, _updateCts.Token);
        }

        public void Dispose()
        {
            if (_updateCts != null)
            {
                _updateCts.Cancel();
                _updateCts.Dispose();
                _updateCts = null;
            }
        }

        private async UniTaskVoid Update(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                UpdateULipSync();
            }
        }

        private void UpdateULipSync()
        {
            if (!_codec.GetOutputSpectrum(false, out var data)) return;
            data.CopyTo(_buffer);
            _uLipSync.OnDataReceived(_buffer, _codec.OutputChannels);
        }
    }
}