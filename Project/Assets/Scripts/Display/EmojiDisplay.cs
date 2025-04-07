using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public class EmojiDisplay : IDisplay
    {
        private readonly Context _context;

        private EmojiMainUI _emojiMainUI;
        
        public EmojiDisplay(Context context)
        {
            _context = context;
        }
        
        public void Dispose()
        {
            _emojiMainUI?.Dispose();
        }
        
        public async UniTask<bool> Load()
        {
            _emojiMainUI = await _context.UIManager.ShowSceneUI<EmojiMainUI>();
            return true;
        }

        public void Start()
        {
            
        }

        public void SetStatus(string status)
        {
            _emojiMainUI.SetStatus(status);
        }

        public void SetEmotion(string emotion)
        {
            _emojiMainUI.SetEmotion(emotion);
        }

        public void SetChatMessage(ChatRole role, string content)
        {
            _emojiMainUI.SetChatMessage(role, content);
        }
    }
}