using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class MainUI : BaseUI, IDisplay
    {
        private static readonly Dictionary<string, string> Emojis = new()
        {
            { "happy", "😄" },
            { "sad", "🙁" },
            { "neutral", "🙂" },
            { "thinking", "🤔" }
        };

        private const int SpectrumUpdateInterval = 50;

        private TMP_Text _textStatus;
        private TMP_Text _textChat;
        private TMP_Text _textEmotion;
        private Button _btnSet;
        private Button _btnChat;
        private Button _btnTest;
        private XInputWave _xInputWave;

        private CancellationTokenSource _cts;

        public override string GetResourcePath()
        {
            return "MainUI/MainUI";
        }

        protected override void OnInit()
        {
            _textStatus = Tr.Find("Status").GetComponent<TMP_Text>();
            _textChat = Tr.Find("Chat").GetComponent<TMP_Text>();
            _textEmotion = Tr.Find("Emotion").GetComponent<TMP_Text>();
            _btnSet = Tr.Find("BtnSet").GetComponent<Button>();
            _btnSet.onClick.AddListener(() => { ShowModuleUI<SettingsUI>().Forget(); });
            _xInputWave = Tr.Find("Spectrum").GetComponent<XInputWave>();
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            SetEmotion("neutral");
            _textStatus.text = "";
            _textChat.text = "";
            _cts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _cts.Token);
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnHide()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            await UniTask.CompletedTask;
        }

        public void SetStatus(string status)
        {
            _textStatus.text = status;
        }

        public void SetEmotion(string emotion)
        {
            _textEmotion.text = Emojis[emotion];
        }

        public void SetChatMessage(ChatRole role, string content)
        {
            _textChat.text = content;
        }

        private async UniTaskVoid LoopUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(SpectrumUpdateInterval, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UniTask.SwitchToThreadPool();
                var codec = Context.App.GetCodec();
                var dirty = codec != null && _xInputWave.UpdateSpectrumData(codec);
                await UniTask.SwitchToMainThread();
                if (dirty) _xInputWave.SetVerticesDirty();
            }
        }
    }
}