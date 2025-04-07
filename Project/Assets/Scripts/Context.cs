using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class Context
    {
        public App App { get; private set; }

        public UIManager UIManager { get; private set; }
        
        public bool Restarting { get; private set; }
        
        public void Init()
        {
            UIManager = new UIManager();
            UIManager.Init(this);
            App = new App();
            App.Init(this);
            Application.runInBackground = true;
        }

        public void Dispose()
        {
            UIManager.Dispose();
            App.Dispose();
            DOTween.KillAll();
            Resources.UnloadUnusedAssets();
        }

        public void Start()
        {
            App.Start().Forget();
        }

        public async UniTask Restart(int delayMs = 0)
        {
            if (Restarting) return;
            Restarting = true;
            await UniTask.Delay(delayMs);
            Dispose();
            await UniTask.Delay(1000);
            Init();
            Start();
            Restarting = false;
        }
    }
}