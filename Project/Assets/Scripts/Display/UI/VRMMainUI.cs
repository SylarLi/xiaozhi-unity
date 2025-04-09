using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class VRMMainUI : BaseUI
    {
        private RectTransform _trSet;
        private Button _btnSet;
        private GameObject _goLoading;
        private GameObject _goChat;
        private TextMeshProUGUI _textChat;

        private CancellationTokenSource _autoHideCts;
        private DeviceState _lastDeviceState;

        public override string GetResourcePath()
        {
            return "MainUI/VRMMainUI";
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
            _goLoading = Tr.Find("Loading").gameObject;
            _trSet = GetComponent<RectTransform>(Tr, "BtnSet");
            _trSet.GetComponent<XButton>().onClick.AddListener(() => { ShowModuleUI<SettingsUI>().Forget(); });
            GetComponent<XButton>(Tr, "ClickRole").onClick.AddListener(() => Context.App.ToggleChatState().Forget());
            _goChat = Tr.Find("Chat").gameObject;
            _textChat = GetComponent<TextMeshProUGUI>(Tr, "Chat/Text");
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            Context.App.OnDeviceStateUpdate -= OnDeviceStateUpdate;
            Context.App.OnDeviceStateUpdate += OnDeviceStateUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate += OnAutoHideUIUpdate;
            DetectCompVisible(true);
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnHide()
        {
            ClearAutoHideCts();
            KillCompVisibleAnim();
            Context.App.OnDeviceStateUpdate -= OnDeviceStateUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            await UniTask.CompletedTask;
        }

        public void ShowLoading(bool show)
        {
            _goLoading.SetActive(show);
        }

        public void SetStatus(string status)
        {
            _goChat.SetActive(!string.IsNullOrEmpty(status));
            _textChat.text = status;
        }

        private void OnDeviceStateUpdate(DeviceState state)
        {
            ClearAutoHideCts();
            DetectCompVisible();
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
    }
}