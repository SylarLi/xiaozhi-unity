using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public class XImage : Image
    {
        protected override void Awake()
        {
            base.Awake();
            ThemeManager.OnFillChanged.AddListener(OnFillChanged);
            UpdateSprite(ThemeManager.Fill);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ThemeManager.OnFillChanged.RemoveListener(OnFillChanged);
        }

        private void OnFillChanged(bool fill)
        {
            UpdateSprite(fill);
        }

        private void UpdateSprite(bool fill)
        {
            overrideSprite = sprite ? ThemeManager.FetchSprite(sprite.name, fill) : null;
        }
    }
}