using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public interface IUIService
    {
        T FindUI<T>() where T : BaseUI;

        T FindUI<T>(string alias) where T : BaseUI;

        UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask CloseUI<T>() where T : BaseUI;

        UniTask CloseUI<T>(T ui) where T : BaseUI;

        void CloseAllUI();
    }
}