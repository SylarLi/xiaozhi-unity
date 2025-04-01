using UnityEngine;

namespace XiaoZhi.Unity
{
    public static class UGUIExtensions
    {
        public static RectTransform SetAnchorPosX(this RectTransform tr, float x)
        {
            var pos = tr.anchoredPosition;
            pos.x = x;
            tr.anchoredPosition = pos;
            return tr;
        }
        
        public static RectTransform SetAnchorPosY(this RectTransform tr, float y)
        {
            var pos = tr.anchoredPosition;
            pos.y = y;
            tr.anchoredPosition = pos;
            return tr;
        }
    }
}