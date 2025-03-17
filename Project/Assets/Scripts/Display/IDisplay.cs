namespace XiaoZhi.Unity
{
    public interface IDisplay
    {
        public void SetStatus(string status);
        
        public void SetEmotion(string emotion);

        public void SetChatMessage(string role, string content);
    }
}