using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public interface IUIService
    {
        Context Context { get; }
        
        T FindUI<T>() where T : BaseUI;

        T FindUI<T>(string alias) where T : BaseUI;

        UniTask<T> ShowSceneUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask CloseUI<T>() where T : BaseUI;

        UniTask CloseUI(BaseUI ui);

        void CloseAllUI();
    }
}