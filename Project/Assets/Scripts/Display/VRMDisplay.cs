using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniVRM10;

namespace XiaoZhi.Unity
{
    public class VRMDisplay : IDisplay
    {
        private readonly Context _context;
        private VRMMainUI _mainUI;
        private Camera _mainCamera;
        private GameObject _character;
        private uLipSync.uLipSync _lipSync;
        private ULipSyncAudioProxy _lipSyncAudioProxy;
        private FaceAnimation _faceAnim;
        private CancellationTokenSource _loopCts;

        public VRMDisplay(Context context)
        {
            _context = context;
            ThemeManager.OnThemeChanged.AddListener(OnThemeChanged);
        }

        public void Dispose()
        {
            ThemeManager.OnThemeChanged.RemoveListener(OnThemeChanged);
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
                _loopCts = null;
            }

            _mainUI?.Dispose();
            _mainUI = null;
            if (_lipSyncAudioProxy != null)
            {
                _lipSyncAudioProxy.Dispose();
                _lipSyncAudioProxy = null;
            }
            
            if (_character)
            {
                Object.Destroy(_character);
                _character = null;
            }
        }

        public async UniTask<bool> Load()
        {
            _mainUI = await _context.UIManager.ShowSceneUI<VRMMainUI>();
            _mainCamera = Camera.main;
            UpdateCameraColor();
            var modelName = Config.Instance.CharacterName;
            var prefab = (GameObject)await Resources.LoadAsync<GameObject>($"Model/{modelName}/model");
            if (!prefab)
            {
                _context.UIManager.ShowNotificationUI($"Load character failed: {modelName}").Forget();
                return false;
            }

            _character = Object.Instantiate(prefab);
            _character.GetComponent<TransformFollower>().SetFollower(_mainCamera!.transform);
            _lipSync = _character.GetComponent<uLipSync.uLipSync>();
            _faceAnim = new FaceAnimation(_character.GetComponent<Vrm10Instance>(), "loading");
            return true;
        }

        public void Start()
        {
            _lipSyncAudioProxy = new ULipSyncAudioProxy(_lipSync, _context.App.GetCodec());
            _lipSyncAudioProxy.Start();
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
        }

        public void SetStatus(string status)
        {
        }

        public void SetEmotion(string emotion)
        {
            _mainUI.ShowLoading(emotion == "loading");
            _faceAnim.SetExpression(emotion);
        }

        public void SetChatMessage(ChatRole role, string content)
        {
        }

        private void OnThemeChanged(ThemeSettings.Theme theme)
        {
            UpdateCameraColor();
        }

        private void UpdateCameraColor()
        {
            if (!_mainCamera) return;
            var color = ThemeManager.FetchColor(ThemeManager.Theme);
            _mainCamera.backgroundColor = color;
        }

        private async UniTaskVoid LoopUpdate(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                _faceAnim.Update(Time.deltaTime);
            }
        }

        private class FaceAnimation
        {
            private static readonly Dictionary<string, ExpressionKey> ExpressionMap = new()
            {
                { "loading", new ExpressionKey(ExpressionPreset.custom, "sleeping") },
                { "sleep", new ExpressionKey(ExpressionPreset.custom, "sleeping") },
                { "yawn", new ExpressionKey(ExpressionPreset.custom, "sleeping") },
                { "activation", new ExpressionKey(ExpressionPreset.custom, "sleeping") },
                { "neutral", new ExpressionKey(ExpressionPreset.neutral) },
                { "happy", new ExpressionKey(ExpressionPreset.relaxed) },
                { "funny", new ExpressionKey(ExpressionPreset.relaxed) },
                { "sad", new ExpressionKey(ExpressionPreset.sad) },
                { "thinking", new ExpressionKey(ExpressionPreset.custom, "thinking") },
            };

            private const float CrossFadeDuration = 0.5f;

            private readonly Vrm10Instance _instance;

            private ExpressionKey _current;

            private List<CrossFader> _faders = new();

            public FaceAnimation(Vrm10Instance instance, string initialExpression)
            {
                _instance = instance;
                _current = ExpressionMap.GetValueOrDefault(initialExpression, ExpressionMap["neutral"]);
                _instance.Runtime.Expression.SetWeight(_current, 1.0f);
            }

            public void SetExpression(string expression)
            {
                Debug.Log($"SetExpression: {expression}");
                var newKey = ExpressionMap.GetValueOrDefault(expression, ExpressionMap["neutral"]);
                if (_current.Equals(newKey)) return;
                for (var i = _faders.Count - 1; i >= 0; i--)
                    if (_faders[i].From < _faders[i].To)
                        _faders[i] = _faders[i].Cancel();
                _faders.Add(new CrossFader(_current, 1, 0, CrossFadeDuration));
                _current = newKey;
                _faders.Add(new CrossFader(_current, 0, 1, CrossFadeDuration));
            }

            public void Update(float deltaTime)
            {
                foreach (var fade in _faders)
                {
                    fade.Update(deltaTime);
                    _instance.Runtime.Expression.SetWeight(fade.Key, fade.Weight);
                }

                for (var i = _faders.Count - 1; i >= 0; i--)
                    if (_faders[i].IsEnd())
                        _faders.RemoveAt(i);
            }

            private class CrossFader
            {
                private readonly ExpressionKey _key;
                public ExpressionKey Key => _key;
                private readonly float _from;
                public float From => _from;
                private readonly float _to;
                public float To => _to;
                private float _crossTime;
                private float _crossDuration;
                public bool IsEnd() => _crossDuration == 0;
                private float _weight;
                public float Weight => _weight;

                public CrossFader(ExpressionKey key, float from, float to, float crossDuration)
                {
                    _key = key;
                    _from = from;
                    _to = to;
                    _crossDuration = crossDuration;
                    _crossTime = 0;
                    _weight = _from;
                }

                public void Update(float deltaTime)
                {
                    if (IsEnd()) return;
                    _crossTime += deltaTime;
                    if (_crossTime >= _crossDuration)
                    {
                        _weight = _to;
                        _crossDuration = 0;
                    }
                    else
                    {
                        _weight = Mathf.Lerp(_from, _to, _crossTime / _crossDuration);
                    }
                }

                public CrossFader Cancel()
                {
                    return new CrossFader(_key, _weight, 0, (_weight - _from) / (_to - _from) * _crossDuration);
                }
            }
        }
    }
}