using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UIManager : IUIService, IDisposable
    {
        private Dictionary<UILayer, GameObject> _canvasMap;
        private readonly Stack<SceneStackData> _stack = new();
        private readonly Queue<Tuple<Type, NotificationUIData>> _notificationQueue = new();
        private readonly Dictionary<string, BaseUI> _uiMap = new();
        private string _currentPopup;
        private Context _context;
        public Context Context => _context;

        public void Init(Context context)
        {
            _context = context;
            _canvasMap = Enum.GetNames(typeof(UILayer))
                .ToDictionary(Enum.Parse<UILayer>, i => GameObject.Find($"UICamera/Canvas{i}"));
        }

        public async UniTask Load()
        {
            await EnsureUI<MaskUI>(null, _canvasMap[UILayer.Module].transform);
        }

        private async UniTask<T> ShowStackUI<T>(BaseUIData data, UILayer layer) where T : BaseUI, new()
        {
            var alias = typeof(T).Name;
            var canvas = _canvasMap[layer].transform;
            await EnsureUI<T>(null, canvas);
            switch (layer)
            {
                case UILayer.Module:
                    if (_stack.Count == 0)
                        throw new InvalidOperationException("At least one scene ui should be loaded.");
                    var moduleStack = _stack.Peek().Stack;
                    if (moduleStack.Count > 0)
                    {
                        var currentUIData = moduleStack.Peek();
                        if (currentUIData.Alias != alias) await FindUI(currentUIData.Alias).Hide();
                    }

                    break;
                case UILayer.Scene:
                    if (_stack.Count > 0) await HideSceneUI(_stack.Peek());
                    break;
            }

            var ui = FindUI<T>();
            ui.Layer = layer;
            ui.Tr.SetAsLastSibling();
            var showTasks = ListPool<UniTask>.Get();
            if (layer == UILayer.Module)
            {
                var maskUI = FindUI<MaskUI>();
                maskUI.AsMaskOf(ui);
                showTasks.Add(maskUI.Show(ui.GetMaskData()));
            }

            showTasks.Add(ui.Show(data));
            await UniTask.WhenAll(showTasks);
            ListPool<UniTask>.Release(showTasks);
            switch (layer)
            {
                case UILayer.Module:
                    var moduleStack = _stack.Peek().Stack;
                    moduleStack.Push(new StackData(alias, data));
                    break;
                case UILayer.Scene:
                    _stack.Push(new SceneStackData(alias, data));
                    break;
            }

            return ui;
        }

        private async UniTask<T> EnsureUI<T>(Type type = null, Transform parent = null) where T : BaseUI, new()
        {
            var alias = (type ?? typeof(T)).Name;
            T ui;
            if (_uiMap.TryGetValue(alias, out var existingUI))
            {
                ui = existingUI as T;
                if (ui == null)
                    throw new NullReferenceException(
                        $"UI instance of type {alias} already exists, but is not of type {alias}");
                if (ui.Tr.parent != parent) ui.Tr.SetParent(parent, false);
            }
            else
            {
                ui = type != null ? Activator.CreateInstance(type) as T : new T();
                if (ui == null) throw new NullReferenceException($"Failed to create UI instance of type {alias}");
                ui.RegisterUIService(this);
                var prefab = await Resources.LoadAsync($"UI/{ui.GetResourcePath()}") as GameObject;
                if (!prefab) throw new IOException($"Failed to load UI prefab: {ui.GetResourcePath()}");
                var go = Object.Instantiate(prefab, parent);
                go.SetActive(false);
                ui.Init(go);
                _uiMap[alias] = ui;
            }

            return ui;
        }

        private async UniTask HideSceneUI(SceneStackData data)
        {
            var hideTasks = ListPool<UniTask>.Get();
            hideTasks.Add(FindUI(data.Alias).Hide());
            hideTasks.AddRange(from moduleData in data.Stack
                select FindUI(moduleData.Alias)
                into moduleUI
                where moduleUI?.IsVisible == true
                select moduleUI.Hide());
            var maskUI = FindUI<MaskUI>();
            if (maskUI.IsVisible) hideTasks.Add(maskUI.Hide());
            await UniTask.WhenAll(hideTasks);
            ListPool<UniTask>.Release(hideTasks);
        }

        public T FindUI<T>() where T : BaseUI
        {
            return FindUI<T>(typeof(T).Name);
        }

        public T FindUI<T>(string alias) where T : BaseUI
        {
            return _uiMap.TryGetValue(alias, value: out var instance) ? instance as T : null;
        }

        public BaseUI FindUI(string alias)
        {
            return _uiMap.GetValueOrDefault(alias);
        }

        public async UniTask<T> ShowSceneUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowStackUI<T>(data, UILayer.Scene);
        }

        public async UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowStackUI<T>(data, UILayer.Module);
        }

        public async UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            var canvas = _canvasMap[UILayer.Popup].transform;
            await EnsureUI<T>(null, canvas);
            if (!string.IsNullOrEmpty(_currentPopup))
            {
                var current = FindUI(_currentPopup);
                if (current != null) await current.Hide();
            }

            var ui = FindUI<T>();
            ui.Layer = UILayer.Popup;
            var maskUI = FindUI<MaskUI>();
            maskUI.AsMaskOf(ui);
            await UniTask.WhenAll(ui.Show(data), maskUI.Show(ui.GetMaskData()));
            _currentPopup = ui.GetType().Name;
            return ui;
        }
        
        public async UniTask ShowNotificationUI(string message, float duration = 3.0f)
        {
            await ShowNotificationUI<NotificationUI>(new NotificationUIData(message, duration));
        }

        public async UniTask ShowNotificationUI<T>(NotificationUIData notification) where T : NotificationUI, new()
        {
            _notificationQueue.Enqueue(new Tuple<Type, NotificationUIData>(typeof(T), notification));
            await ProcessNotificationUI();
        }

        private async UniTask ProcessNotificationUI()
        {
            NotificationUI ui;
            if (_notificationQueue.Count == 0)
            {
                ui = FindUI<NotificationUI>();
                if (ui.IsVisible) await ui.Hide();
                return;
            }

            var (type, notification) = _notificationQueue.Dequeue();
            var canvas = _canvasMap[UILayer.Notify].transform;
            ui = await EnsureUI<NotificationUI>(type, canvas);
            ui.Layer = UILayer.Notify;
            await ui.Show(notification);
        }

        public async UniTask CloseUI<T>() where T : BaseUI
        {
            var ui = FindUI<T>();
            await CloseUI(ui);
        }

        private async UniTask<bool> CloseMaskUI()
        {
            if (!string.IsNullOrEmpty(_currentPopup))
            {
                await CloseUI(FindUI(_currentPopup));
                return true;
            }

            if (_stack.Count > 0)
            {
                var moduleStack = _stack.Peek().Stack;
                if (moduleStack.Count > 0)
                {
                    await CloseUI(FindUI(moduleStack.Peek().Alias));
                    return true;
                }
            }

            return false;
        }

        private async UniTask<bool> ClosePopupUI(BaseUI ui)
        {
            var maskUI = FindUI<MaskUI>();
            await UniTask.WhenAll(ui.Hide(), maskUI.Hide());
            _currentPopup = null;
            if (_stack.Count > 0)
            {
                var moduleStack = _stack.Peek().Stack;
                if (moduleStack.Count > 0)
                {
                    var moduleUI = FindUI(moduleStack.Peek().Alias);
                    maskUI.AsMaskOf(moduleUI);
                    await maskUI.Show(moduleUI.GetMaskData());
                }
            }

            return true;
        }

        private async UniTask<bool> CloseModuleUI(BaseUI ui)
        {
            if (_stack.Count == 0) return false;
            var moduleStack = _stack.Peek().Stack;
            var alias = ui.GetType().Name;
            if (moduleStack.Count == 0 || moduleStack.Peek().Alias != alias) return false;
            moduleStack.Pop();
            var maskUI = FindUI<MaskUI>();
            if (moduleStack.Count == 0)
            {
                await UniTask.WhenAll(ui.Hide(), maskUI.Hide());
            }
            else
            {
                await ui.Hide();
                var previousUIData = moduleStack.Peek();
                var previousUI = FindUI(previousUIData.Alias);
                maskUI.AsMaskOf(previousUI);
                await previousUI.Show(previousUIData.Data);
            }

            return true;
        }

        private async UniTask<bool> CloseSceneUI(BaseUI ui)
        {
            if (_stack.Count == 0) return false;
            var alias = ui.GetType().Name;
            if (_stack.Peek().Alias != alias) return false;
            await HideSceneUI(_stack.Pop());
            if (_stack.Count > 0)
            {
                var previousUIData = _stack.Peek();
                var previousUI = FindUI(previousUIData.Alias);
                await previousUI.Show(previousUIData.Data);
                var moduleStack = previousUIData.Stack;
                if (moduleStack.Count > 0)
                {
                    var moduleUIData = moduleStack.Peek();
                    var moduleUI = FindUI(moduleUIData.Alias);
                    var maskUI = FindUI<MaskUI>();
                    await UniTask.WhenAll(moduleUI.Show(moduleUIData.Data), maskUI.Show(moduleUI.GetMaskData()));
                }
            }

            return true;
        }

        public async UniTask CloseUI(BaseUI ui)
        {
            switch (ui)
            {
                case null:
                    break;
                case MaskUI:
                    await CloseMaskUI();
                    break;
                case { Layer: UILayer.Notify }:
                    await ProcessNotificationUI();
                    break;
                case { Layer: UILayer.Popup }:
                    await ClosePopupUI(ui);
                    break;
                case { Layer: UILayer.Module }:
                    await CloseModuleUI(ui);
                    break;
                case { Layer: UILayer.Scene }:
                    await CloseSceneUI(ui);
                    break;
            }
        }

        public void CloseAllUI()
        {
            _stack.Clear();
            _notificationQueue.Clear();
            foreach (var ui in _uiMap.Values)
            {
                ui.Hide().Forget();
                Object.Destroy(ui.Go);
            }

            _uiMap.Clear();
        }

        public void Dispose()
        {
            CloseAllUI();
        }

        private class StackData
        {
            public string Alias { get; }
            public BaseUIData Data { get; }

            public StackData(string alias, BaseUIData data)
            {
                Alias = alias;
                Data = data;
            }
        }

        private class SceneStackData : StackData
        {
            public Stack<StackData> Stack { get; }

            public SceneStackData(string alias, BaseUIData data) : base(alias, data)
            {
                Stack = new Stack<StackData>();
            }
        }
    }
}