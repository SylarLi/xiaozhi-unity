using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public abstract class BaseUI : IUIService
    {
        public const float AnimationDuration = 0.25f;
        
        public GameObject Go { get; private set; }

        public RectTransform Tr { get; private set; }

        public bool IsVisible { get; private set; }
        
        public UILayer Layer { get; set; }

        private IUIService _uiService;

        private readonly Dictionary<long, UnityAction<bool>> _toggleListeners = new();

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

        public void Destroy()
        {
            OnDestroy();
            if (Go) Object.Destroy(Go);
        }

        public async UniTask Show(BaseUIData data = null)
        {
            Go.SetActive(true);
            IsVisible = true;
            await OnShow(data);
        }

        public async UniTask Hide()
        {
            await OnHide();
            Go.SetActive(false);
            IsVisible = false;
        }

        public async UniTask Close()
        {
            await _uiService.CloseUI(this);
        }

        public abstract string GetResourcePath();

        public virtual MaskUIData GetMaskData()
        {
            return null;
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnDestroy()
        {
            
        }

        protected virtual async UniTask OnShow(BaseUIData data = null)
        {
            await UniTask.CompletedTask;
        }

        protected virtual async UniTask OnHide()
        {
            await UniTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetLang(Transform tr, string key)
        {
            SetLang(tr, null, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetLang(Transform tr, string path, string key)
        {
            if (!tr) return;
            var text = (!string.IsNullOrEmpty(path) ? tr.Find(path) : tr).GetComponent<TextMeshProUGUI>();
            if (!text) return;
            text.text = Lang.Strings.Get(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetLang(Component comp, string key)
        {
            SetLang(comp, null, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetLang(Component comp, string path, string key)
        {
            if (!comp) return;
            SetLang(comp.transform, path, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T GetComponent<T>(Transform tr, string path) where T : Component
        {
            return !tr ? null : GetComponent<T>(tr.Find(path));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T GetComponent<T>(Transform tr) where T : Component
        {
            return !tr ? null : tr.GetComponent<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AddUniqueListener(Toggle toggle, int index, Action<Toggle, int, bool> action, int id = 0)
        {
            var uniqueId = BuildUniqueId(toggle, id);
            if (_toggleListeners.ContainsKey(uniqueId)) return;
            UnityAction<bool> unityAction = value => action(toggle, index, value);
            _toggleListeners.Add(uniqueId, unityAction);
            toggle.onValueChanged.AddListener(unityAction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RemoveUniqueListener(Toggle toggle, int id = 0)
        {
            var uniqueId = BuildUniqueId(toggle, id);
            if (!_toggleListeners.TryGetValue(uniqueId, out var listener)) return;
            toggle.onValueChanged.RemoveListener(listener);
            _toggleListeners.Remove(uniqueId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long BuildUniqueId(Component comp, int id)
        {
            return comp.GetInstanceID() * 100L + id;
        }

        public Context Context => _uiService.Context;

        public T FindUI<T>() where T : BaseUI
        {
            return _uiService.FindUI<T>();
        }

        public T FindUI<T>(string alias) where T : BaseUI
        {
            return _uiService.FindUI<T>(alias);
        }

        public async UniTask<T> ShowSceneUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await _uiService.ShowSceneUI<T>(data);
        }

        public async UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await _uiService.ShowModuleUI<T>(data);
        }

        public async UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await _uiService.ShowPopupUI<T>(data);
        }

        public async UniTask ShowNotificationUI<T>(NotificationUIData notification) where T : NotificationUI, new()
        {
            await _uiService.ShowNotificationUI<T>(notification);
        }

        public async UniTask ShowNotificationUI(string message, float duration = 3)
        {
            await _uiService.ShowNotificationUI(message, duration);
        }

        public async UniTask CloseUI<T>() where T : BaseUI
        {
            await _uiService.CloseUI<T>();
        }

        public async UniTask CloseUI(BaseUI ui)
        {
            await _uiService.CloseUI(ui);
        }

        public async UniTask DestroyUI<T>() where T : BaseUI
        {
            await _uiService.DestroyUI<T>();
        }

        public async UniTask DestroyUI(BaseUI ui)
        {
            await _uiService.DestroyUI(ui);
        }

        public void Dispose()
        {
            _uiService.Dispose();
        }
    }

    public abstract class BaseUIData
    {
        
    }
}