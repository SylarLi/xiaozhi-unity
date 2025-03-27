using UnityEngine;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public abstract class BaseUI : IUIService
    {
        public GameObject Go { get; private set; }

        public RectTransform Tr { get; private set; }

        public bool IsVisible { get; private set; }

        private IUIService _uiService;

        public void RegisterUIService(IUIService uiService)
        {
            _uiService = uiService;
        }

        public void Init(GameObject go)
        {
            Go = go;
            Tr = (RectTransform)go.transform;
            OnInit();
        }

        public async UniTask Show(BaseUIData data = null)
        {
            Go.SetActive(true);
            IsVisible = true;
            await OnShow(data);
        }

        public void Hide()
        {
            Go.SetActive(false);
            IsVisible = false;
            OnHide();
        }

        public async UniTask Close()
        {
            await _uiService.CloseUI(this);
        }
        
        public abstract string GetResourcePath();

        protected virtual void OnInit()
        {
        }

        protected virtual async UniTask OnShow(BaseUIData data = null)
        {
            await UniTask.CompletedTask;
        }

        protected virtual void OnHide()
        {
        }

        public T FindUI<T>() where T : BaseUI
        {
            return _uiService.FindUI<T>();
        }

        public T FindUI<T>(string alias) where T : BaseUI
        {
            return _uiService.FindUI<T>(alias);
        }

        public async UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await _uiService.ShowModuleUI<T>(data);
        }

        public async UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await _uiService.ShowPopupUI<T>(data);
        }

        public async UniTask CloseUI<T>() where T : BaseUI
        {
            await _uiService.CloseUI<T>();
        }

        public async UniTask CloseUI<T>(T ui) where T : BaseUI
        {
            await _uiService.CloseUI(ui);
        }

        public void CloseAllUI()
        {
            _uiService.CloseAllUI();
        }
    }

    public abstract class BaseUIData
    {
        public virtual BaseUIData Clone()
        {
            return MemberwiseClone() as BaseUIData;
        }
    }
}