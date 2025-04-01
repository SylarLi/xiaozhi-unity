using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class UIDisplay : BaseUI, IDisplay
    {
        private static readonly Dictionary<string, string> Emojis = new()
        {
            { "happy", "😄" },
            { "sad", "🙁" },
            { "neutral", "🙂" },
            { "thinking", "🤔" }
        };
        
        private const int SpectrumUpdateInterval = 50;
        
        private App _app;
        private TMP_Text _textStatus;
        private TMP_Text _textChat;
        private TMP_Text _textEmotion;
        private Button _btnSet;
        private Button _btnChat;
        private Button _btnTest;
        private XInputWave _xInputWave;
        private CancellationTokenSource _loopCts;

        public void RegisterApp(App app)
        {
            _app = app;
        }
        
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
            _btnSet.onClick.AddListener(() =>
            {
                // Todo...
            });
            _xInputWave = Tr.Find("Spectrum").GetComponent<XInputWave>();
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            SetStatus("");
            SetChatMessage("system", "");
            SetEmotion("neutral");
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
            await UniTask.CompletedTask;
        }

        protected override void OnHide()
        {
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
            }
        }

        public void SetStatus(string status)
        {
            _textStatus.text = status;
        }

        public void SetEmotion(string emotion)
        {
            _textEmotion.text = Emojis[emotion];
        }

        public void SetChatMessage(string role, string content)
        {
            _textChat.text = content;
        }
        
        private async UniTaskVoid LoopUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(SpectrumUpdateInterval, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UniTask.SwitchToThreadPool();
                var dirty = _xInputWave.UpdateSpectrumData(_app.GetCodec());
                await UniTask.SwitchToMainThread();
                if (dirty) _xInputWave.SetVerticesDirty();
            }
        }
    }
}