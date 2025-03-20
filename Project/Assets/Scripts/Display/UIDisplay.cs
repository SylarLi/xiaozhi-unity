using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class UIDisplay : IDisplay
    {
        private static readonly Dictionary<string, string> Emojis = new()
        {
            { "happy", "😄" },
            { "sad", "🙁" },
            { "neutral", "🙂" },
            { "thinking", "🤔" }
        };
        
        private TMP_Text _textStatus;

        private TMP_Text _textChat;

        private TMP_Text _textEmotion;

        private Button _btnSet;
        
        private Button _btnChat;
        
        private Button _btnTest;
        
        public UIDisplay()
        {
            var goMain = GameObject.Find("UICamera/Canvas/Main");
            var trMain = (RectTransform)goMain.transform;
            _textStatus = trMain.Find("Status").GetComponent<TMP_Text>();
            _textChat = trMain.Find("Chat").GetComponent<TMP_Text>();
            _textEmotion = trMain.Find("Emotion").GetComponent<TMP_Text>();
            _btnSet = trMain.Find("BtnSet").GetComponent<Button>();
            _btnSet.onClick.AddListener(() =>
            {
                // Todo...
            });
            _btnChat = trMain.Find("BtnChat").GetComponent<Button>();
            _btnChat.onClick.AddListener(() =>
            {
                Context.Instance.App.ToggleChatState().Forget();
            });
            
            SetStatus("");
            SetChatMessage("system", "");
            SetEmotion("neutral");
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
    }
}