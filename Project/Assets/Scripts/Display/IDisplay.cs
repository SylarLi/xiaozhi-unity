using System;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public interface IDisplay : IDisposable
    {
        UniTask<bool> Load();

        void Start();
        
        void SetStatus(string status);
        
        void SetEmotion(string emotion);

        void SetChatMessage(ChatRole role, string content);
    }
}