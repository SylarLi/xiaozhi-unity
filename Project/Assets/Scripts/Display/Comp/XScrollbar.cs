using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class XButton : Button
    {
        private ColourModifier[] _colourModifiers;

        private ColourModifier[] GetColourModifiers()
        {
            _colourModifiers ??= GetComponentsInChildren<ColourModifier>(true);
            return _colourModifiers;
        }
        
        public override void OnSelect(BaseEventData eventData)
        {
        }

        public override void OnDeselect(BaseEventData eventData)
        {
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ThemeManager.OnThemeChanged.AddListener(OnThemeChanged);
        }

        protected override void OnDisable()
        {
            base.OnDestroy();
            ThemeManager.OnThemeChanged.RemoveListener(OnThemeChanged);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            UpdateColor();
        }

        private void OnThemeChanged(ThemeSettings.Theme theme)
        {
            UpdateColor();
        }

        private void UpdateColor()
        {
            foreach (var modifier in GetColourModifiers())
                modifier.SetAction(GetCurrentAction());
        }

        private ThemeSettings.Action GetCurrentAction()
        {
            return currentSelectionState switch
            {
                SelectionState.Highlighted => ThemeSettings.Action.Hover,
                SelectionState.Selected => ThemeSettings.Action.Selected,
                SelectionState.Pressed => ThemeSettings.Action.Pressed,
                SelectionState.Disabled => ThemeSettings.Action.Disabled,
                _ => ThemeSettings.Action.Default
            };
        } 
    }
}