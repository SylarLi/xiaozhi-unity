using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class XRadio : Toggle
    {
        [Serializable]
        public struct ColorDef
        {
            public Color Background;
            public Color Circle;
            public Color CircleHover;
            public Color CircleSelected;
            public Color CirclePressed;
            public Color CircleDisabled;
        }

        [SerializeField] private ColorDef _off;
        [SerializeField] private ColorDef _on;
        [SerializeField] private Graphic _bg;
        [SerializeField] private Graphic _circle;
        [SerializeField] private Graphic _offGraphic;
        [SerializeField] private Graphic _onGraphic;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            transition = Transition.None;
            onValueChanged.AddListener(OnValueChanged);
            UpdateColor();
            UpdateToggle();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            onValueChanged.RemoveListener(OnValueChanged);
            _circle.transform.DOKill();
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

        private void OnValueChanged(bool _)
        {
            UpdateColor();
            UpdateToggle();
        }

        private void UpdateToggle()
        {
            if (_circle && _bg)
            {
                var radius = _circle.rectTransform.rect.height * 0.5f;
                var gap = _bg.rectTransform.rect.height * 0.5f - radius;
                var pos = -_bg.rectTransform.rect.width * 0.5f + gap + radius;
                _circle.rectTransform.DOKill();
                _circle.rectTransform.DOAnchorPosX(isOn ? -pos : pos, 0.2f);
            }
            
            if (_offGraphic) _offGraphic.enabled = !isOn;
            if (_onGraphic) _onGraphic.enabled = isOn;
        }
        
        private void UpdateColor()
        {
            var colorDef = isOn? _on : _off;
            if (_bg) _bg.color = colorDef.Background;
            if (_circle)
            {
                _circle.color = currentSelectionState switch
                {
                    SelectionState.Highlighted => colorDef.CircleHover,
                    SelectionState.Selected => colorDef.CircleSelected,
                    SelectionState.Pressed => colorDef.CirclePressed,
                    SelectionState.Disabled => colorDef.CircleDisabled,
                    _ => colorDef.Circle
                };
            }
        }
    }
}