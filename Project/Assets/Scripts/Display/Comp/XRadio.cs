using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class XToggle : Toggle
    {
        private ColourModifier[] _colourModifiers;

        private ColourModifier[] GetColourModifiers()
        {
            _colourModifiers ??= GetComponentsInChildren<ColourModifier>(true);
            return _colourModifiers;
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            transition = Transition.None;
            ThemeManager.OnThemeChanged.AddListener(OnThemeChanged);
            onValueChanged.AddListener(OnValueChanged);
            UpdateSpot();
        }

        protected override void OnDisable()
        {
            base.OnDestroy();
            ThemeManager.OnThemeChanged.RemoveListener(OnThemeChanged);
            onValueChanged.RemoveListener(OnValueChanged);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            UpdateColor();
        }

        private void OnThemeChanged(ThemeSettings.Theme theme)
        {
            UpdateColor();
        }

        private void OnValueChanged(bool _)
        {
            UpdateSpot();
        }

        private void UpdateSpot()
        {
            foreach (var modifier in GetColourModifiers())
                modifier.SetBackground(isOn ? ThemeSettings.Background.SpotThin : ThemeSettings.Background.Stateful);
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