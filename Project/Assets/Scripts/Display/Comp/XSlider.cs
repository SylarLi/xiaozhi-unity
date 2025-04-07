using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class XSlider : Slider
    {
        [SerializeField] private TextMeshProUGUI _value;
        [SerializeField] private TextMeshProUGUI _from;
        [SerializeField] private TextMeshProUGUI _to;

        [SerializeField] private ColourModifier[] _reactModifiers;

        protected override void OnEnable()
        {
            base.OnEnable();
            transition = Transition.None;
            onValueChanged.AddListener(OnValueChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
            if (_value)
                _value.transform.parent.gameObject.SetActive(
                    currentSelectionState is SelectionState.Pressed or SelectionState.Selected);
        }

        private void OnValueChanged(float number)
        {
            if (_value) _value.text = number.ToString(CultureInfo.InvariantCulture);
            if (_from) _from.text = minValue.ToString(CultureInfo.InvariantCulture);
            if (_to) _to.text = maxValue.ToString(CultureInfo.InvariantCulture);
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