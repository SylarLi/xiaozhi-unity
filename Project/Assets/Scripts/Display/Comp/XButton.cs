using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class XButton : Button
    {
        [SerializeField] private ColourModifier[] _reactModifiers;

        protected override void OnEnable()
        {
            base.OnEnable();
            transition = Transition.None;
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

        private void UpdateColor()
        {
            if (_reactModifiers == null) return;
            foreach (var modifier in _reactModifiers)
                if (modifier) modifier.SetAction(GetCurrentAction());
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