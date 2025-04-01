using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class ColourModifier : UIBehaviour
    {
        [SerializeField] private ThemeSettings.Background _background;
        
        [SerializeField] private bool _react;
        
        [SerializeField][Range(0, 1)] private float _alpha = 1.0f;

        private Graphic _graphic;

        private ThemeSettings.Action _action = ThemeSettings.Action.Default;
        
        private Graphic GetGraphic()
        {
            _graphic ??= GetComponent<Graphic>();
            return _graphic;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ThemeManager.OnThemeChanged.AddListener(OnThemeChanged);
            UpdateColor();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ThemeManager.OnThemeChanged.RemoveListener(OnThemeChanged);
            UpdateColor();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            UpdateColor();
            base.OnDidApplyAnimationProperties();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateColor();
        }
#endif
        
        private void OnThemeChanged(ThemeSettings.Theme theme)
        {
            UpdateColor();
        }

        private void UpdateColor()
        {
            var color = ThemeManager.FetchColor(ThemeManager.Theme, _background, _action);
            color.a *= _alpha;
            GetGraphic().color = color;
        }
        
        public void SetBackground(ThemeSettings.Background background)
        {
            if (!_react) return;
            _background = background;
            UpdateColor();
        }

        public void SetAction(ThemeSettings.Action action)
        {
            if (!_react) return;
            _action = action;
            UpdateColor();
        }
    }
}