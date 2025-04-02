using UnityEngine.EventSystems;
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
            UpdateBackground();
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            ThemeManager.OnThemeChanged.RemoveListener(OnThemeChanged);
            onValueChanged.RemoveListener(OnValueChanged);
        }
        
        public override void OnSelect(BaseEventData eventData)
        {
            if (!Config.IsMobile())
                base.OnSelect(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (!Config.IsMobile())
                base.OnDeselect(eventData);
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
            UpdateBackground();
        }

        private void UpdateBackground()
        {
            var background = isOn ? ThemeSettings.Background.SpotThin : ThemeSettings.Background.Stateful;
            foreach (var modifier in GetColourModifiers())
                modifier.SetBackground(background);
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