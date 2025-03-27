using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class UIManager : IUIService, IDisposable
    {
        private enum StackType
        {
            Module,
            Popup
        }

        private GameObject _canvas;
        private GameObject _canvasForward;
        private readonly Dictionary<string, BaseUI> _uiInstances = new();
        private readonly Stack<StackData> _moduleUIStack = new();
        private readonly Stack<StackData> _popupUIStack = new();

        public void Init()
        {
            _canvas = GameObject.Find("UICamera/Canvas");
            _canvasForward = GameObject.Find("UICamera/CanvasForward");
        }

        private async UniTask<T> ShowUI<T>(BaseUIData data, Stack<StackData> uiStack) where T : BaseUI, new()
        {
            var stackType = uiStack == _popupUIStack ? StackType.Popup : StackType.Module;
            var alias = typeof(T).Name;
            T ui;
            if (_uiInstances.TryGetValue(alias, out var existingUI))
            {
                ui = existingUI as T;
                if (ui == null)
                    throw new NullReferenceException(
                        $"UI instance of type {typeof(T).Name} already exists, but is not of type {typeof(T).Name}");
            }
            else
            {
                ui = new T();
                ui.RegisterUIService(this);
                var prefab = Resources.Load<GameObject>($"UI/{ui.GetResourcePath()}");
                if (prefab == null)
                    throw new IOException($"Failed to load UI prefab: {ui.GetResourcePath()}");
                var parentCanvas = stackType == StackType.Popup ? _canvasForward : _canvas;
                var go = Object.Instantiate(prefab, parentCanvas.transform);
                ui.Init(go);
                _uiInstances[alias] = ui;
            }

            if (uiStack.Count > 0)
            {
                var currentUIData = uiStack.Peek();
                if (currentUIData.Alias != alias)
                {
                    var currentUI = _uiInstances[currentUIData.Alias];
                    currentUI.Hide();
                }
            }

            await ui.Show(data);
            uiStack.Push(new StackData(alias, data));
            return ui;
        }

        public T FindUI<T>() where T : BaseUI
        {
            return FindUI<T>(typeof(T).Name);
        }

        public T FindUI<T>(string alias) where T : BaseUI
        {
            return _uiInstances.TryGetValue(alias, value: out var instance) ? instance as T : null;
        }

        public async UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowUI<T>(data, _moduleUIStack);
        }

        public async UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowUI<T>(data, _popupUIStack);
        }

        public async UniTask CloseUI<T>() where T : BaseUI
        {
            var ui = FindUI<T>();
            await CloseUI(ui);
        }

        public async UniTask CloseUI<T>(T ui) where T : BaseUI
        {
            if (ui == null) return;
            var alias = ui.GetType().Name;
            Stack<StackData> targetStack = null;
            if (_moduleUIStack.Count > 0 && _moduleUIStack.Peek().Alias == alias)
                targetStack = _moduleUIStack;
            else if (_popupUIStack.Count > 0 && _popupUIStack.Peek().Alias == alias)
                targetStack = _popupUIStack;
            if (targetStack == null) return;
            targetStack.Pop();
            ui.Hide();
            if (targetStack.Count <= 0) return;
            var previousUIData = targetStack.Peek();
            var previousUI = _uiInstances[previousUIData.Alias];
            await previousUI.Show(previousUIData.Data);
        }

        public void CloseAllUI()
        {
            _moduleUIStack.Clear();
            _popupUIStack.Clear();
            foreach (var ui in _uiInstances.Values)
            {
                ui.Hide();
                Object.Destroy(ui.Go);
            }

            _uiInstances.Clear();
        }

        public void Dispose()
        {
            CloseAllUI();
        }

        private class StackData
        {
            public string Alias { get; set; }
            public BaseUIData Data { get; set; }

            public StackData(string alias, BaseUIData data)
            {
                Alias = alias;
                Data = data?.Clone();
            }
        }
    }
}