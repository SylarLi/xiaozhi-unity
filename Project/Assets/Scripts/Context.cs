using DG.Tweening;

namespace XiaoZhi.Unity
{
    public class Context
    {
        public App App { get; private set; }

        public UIManager UIManager { get; private set; }
        
        public void Init()
        {
            UIManager = new UIManager();
            UIManager.Init(this);
            App = new App();
            App.Init(this);
        }

        public void Dispose()
        {
            DOTween.KillAll();
            UIManager.Dispose();
            App.Dispose();
        }
    }
}