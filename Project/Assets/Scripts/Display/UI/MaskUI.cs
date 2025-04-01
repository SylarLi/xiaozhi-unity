using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class MaskUIData : BaseUIData
    {
        public Color Color;
    }
    
    public class MaskUI : BaseUI
    {
        private static readonly Color DefaultColor = new Color(0, 0, 0, 0.5f);
        
        private Image _bg;
        
        public override string GetResourcePath()
        {
            return "MaskUI/MaskUI";
        }

        protected override void OnInit()
        {
            _bg = GetComponent<Image>(Tr);
            GetComponent<Button>(Tr).onClick.AddListener(() => Close().Forget());
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            var uiData = data as MaskUIData;
            var color = uiData?.Color ?? DefaultColor;
            _bg.DOKill();
            await _bg.DOColor(color, AnimationDuration);
        }

        protected override async UniTask OnHide()
        {
            _bg.DOKill();
            await _bg.DOColor(Color.clear, AnimationDuration);
        }

        public void AsMaskOf(BaseUI ui)
        {
            var parent = ui.Tr.parent.transform;
            if (Tr.parent != parent) Tr.SetParent(parent, false);
            Tr.SetSiblingIndex(ui.Tr.GetSiblingIndex() - 1);
        }
    }
}