namespace XiaoZhi.Unity
{
    public abstract class Display
    {
        public abstract void SetStatus(string status);

        public abstract void ShowNotification(string notification, int durationMs = 3000);

        public abstract void SetEmotion(string emotion);

        public abstract void SetChatMessage(string role, string content);

        public abstract void SetIcon(string icon);
    }
}