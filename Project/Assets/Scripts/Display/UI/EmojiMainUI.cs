using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class EmojiMainUI : BaseUI
    {
        private static readonly Dictionary<string, string> Emojis = new()
        {
            { "yawn", "🥱" },
            { "sleep", "😴" },
            { "happy", "😄" },
            { "funny", "" },
            { "sad", "🙁" },
            { "neutral", "🙂" },
            { "thinking", "🤔" },
            { "activation", "🤖" }
        };

        private const int SpectrumUpdateInterval = 50;
        private const float OutputScaleFactor = 0.5f;

        private TMP_Text _textStatus;
        private TMP_Text _textChat;
        private Transform _trEmotion;
        private TMP_Text _textEmotion;
        private RectTransform _trSet;
        private Button _btnSet;
        private Button _btnChat;
        private XInputWave _xInputWave;
        private GameObject _goLoading;

        private CancellationTokenSource _loopCts;
        private CancellationTokenSource _autoHideCts;
        private Sequence _breatheSequence;
        private Sequence _wakeUpSequence;
        private DeviceState _lastDeviceState;
        private float _normalizedOutputDb;

        public override string GetResourcePath()
        {
            return "MainUI/EmojiMainUI";
        }

        protected override void OnInit()
        {
            Tr.GetComponent<XButton>().onClick.AddListener(() =>
            {
                if (Context.App.IsDeviceReady() && AppSettings.Instance.IsAutoHideUI())
                {
                    ClearAutoHideCts();
                    UpdateCompVisible(true);
                    AutoHideComp();
                }
            });
            _textStatus = Tr.Find("Status").GetComponent<TMP_Text>();
            _textChat = Tr.Find("Chat").GetComponent<TMP_Text>();
            _trEmotion = Tr.Find("Emotion");
            _textEmotion = _trEmotion.GetComponent<TMP_Text>();
            GetComponent<XButton>(_textEmotion).onClick.AddListener(() => Context.App.ToggleChatState().Forget());
            _goLoading = Tr.Find("Loading").gameObject;
            _trSet = GetComponent<RectTransform>(Tr, "BtnSet");
            _trSet.GetComponent<XButton>().onClick.AddListener(() => { ShowModuleUI<SettingsUI>().Forget(); });
            _xInputWave = Tr.Find("Spectrum").GetComponent<XInputWave>();
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            _textEmotion.text = "";
            _textStatus.text = "";
            _textChat.text = "";
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
            Context.App.OnDeviceStateUpdate -= OnDeviceStateUpdate;
            Context.App.OnDeviceStateUpdate += OnDeviceStateUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate += OnAutoHideUIUpdate;
            DetectCompVisible(true);
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnHide()
        {
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
                _loopCts = null;
            }

            ClearAutoHideCts();
            KillCompVisibleAnim();
            StopBreathingAnimation();
            StopWakeUpAnimation();
            Context.App.OnDeviceStateUpdate -= OnDeviceStateUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            await UniTask.CompletedTask;
        }

        public void SetStatus(string status)
        {
            _textStatus.text = status;
        }

        public void SetEmotion(string emotion)
        {
            switch (emotion)
            {
                case "loading":
                    _goLoading.SetActive(emotion == "loading");
                    _textEmotion.text = "";
                    break;
                default:
                    _goLoading.SetActive(false);
                    _textEmotion.text = Emojis.GetValueOrDefault(emotion, Emojis["neutral"]);
                    break;
            }
        }

        public void SetChatMessage(ChatRole role, string content)
        {
            if (_textEmotion.text == "🤖")
            {
                _textChat.text = $"<u><link=\"{Config.Instance.ActivationURL}\">{content}</link></u>";
                return;
            }

            _textChat.text = content;
        }

        private async UniTaskVoid LoopUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(SpectrumUpdateInterval / 2, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UpdateInputWave(token);
                await UniTask.Delay(SpectrumUpdateInterval / 2, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UpdateOutputWave(token);
            }
        }

        private async UniTask UpdateInputWave(CancellationToken token)
        {
            if (!_xInputWave.gameObject.activeInHierarchy) return;
            await UniTask.SwitchToThreadPool();
            var codec = Context.App.GetCodec();
            var inputDirty = codec != null && _xInputWave.UpdateSpectrumData(codec);
            await UniTask.SwitchToMainThread(token);
            if (inputDirty) _xInputWave.SetVerticesDirty();
        }

        private async UniTask UpdateOutputWave(CancellationToken token)
        {
            if (!_trEmotion.gameObject.activeInHierarchy) return;
            await UniTask.SwitchToThreadPool();
            QuantizeOutputWave();
            await UniTask.SwitchToMainThread(token);
            var scale = 1 + _normalizedOutputDb * OutputScaleFactor;
            _trEmotion.localScale = Vector3.Lerp(_trEmotion.localScale,
                new Vector3(scale, scale, 1), 0.7f);
        }

        private void QuantizeOutputWave()
        {
            var codec = Context.App.GetCodec();
            if (codec == null) return;
            if (!codec.GetOutputSpectrum(true, out var outputSpectrum)) return;
            var sums = 0.0f;
            foreach (var sample in outputSpectrum) sums += sample;
            _normalizedOutputDb = Tools.Linear2dB(Math.Max(sums, 0) / outputSpectrum.Length);
        }
        
        private void OnDeviceStateUpdate(DeviceState state)
        {
            ClearAutoHideCts();
            DetectCompVisible();
            if (state is DeviceState.Idle or DeviceState.Connecting) StartBreathingAnimation();
            else StopBreathingAnimation();
            if (_lastDeviceState == DeviceState.Connecting && state == DeviceState.Listening)
                PlayWakeUpAnimation();
            _lastDeviceState = state;
        }

        private void OnAutoHideUIUpdate(bool autoHide)
        {
            ClearAutoHideCts();
            if (autoHide) AutoHideComp();
            else DetectCompVisible();
        }

        private void ClearAutoHideCts()
        {
            if (_autoHideCts != null)
            {
                _autoHideCts.Cancel();
                _autoHideCts.Dispose();
                _autoHideCts = null;
            }
        }

        private void AutoHideComp()
        {
            _autoHideCts = new CancellationTokenSource();
            UniTask.Void(async token =>
            {
                await UniTask.Delay(3000, cancellationToken: token);
                DetectCompVisible();
            }, _autoHideCts.Token);
        }

        private void DetectCompVisible(bool instant = false)
        {
            UpdateCompVisible(Context.App.IsDeviceReady() && !AppSettings.Instance.IsAutoHideUI(), instant);
        }

        private void UpdateCompVisible(bool visible, bool instant = false)
        {
            var trSetPosY = visible ? -100 : 100;
            if (instant)
            {
                _trSet.SetAnchorPosY(trSetPosY);
            }
            else
            {
                _trSet.DOAnchorPosY(trSetPosY, AnimationDuration).SetEase(Ease.InOutSine);
            }
        }

        private void KillCompVisibleAnim()
        {
            _trSet.DOKill();
        }

        private void StartBreathingAnimation()
        {
            if (_breatheSequence != null) return;
            _breatheSequence = DOTween.Sequence();
            _breatheSequence.Append(_trEmotion.DOScale(1.1f, 2f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOScale(1f, 2f).SetEase(Ease.InOutSine))
                .SetLoops(-1);
        }

        private void StopBreathingAnimation()
        {
            if (_breatheSequence == null) return;
            _breatheSequence.Kill();
            _breatheSequence = null;
            _trEmotion.localScale = Vector3.one;
        }

        private void PlayWakeUpAnimation()
        {
            StopWakeUpAnimation();
            _wakeUpSequence = DOTween.Sequence();
            _wakeUpSequence.Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, 5), 0.1f).SetEase(Ease.OutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, -5), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, 3), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, -2), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.OutSine));
        }

        private void StopWakeUpAnimation()
        {
            if (_wakeUpSequence == null) return;
            _wakeUpSequence.Kill();
            _wakeUpSequence = null;
            _trEmotion.localRotation = Quaternion.identity;
        }
    }
}