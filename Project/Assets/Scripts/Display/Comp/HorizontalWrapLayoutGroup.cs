using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

[AddComponentMenu("Layout/Horizontal Wrap Layout Group", 151)]
public class HorizontalWrapLayoutGroup : HorizontalOrVerticalLayoutGroup
{
    [SerializeField] private int _fixedRowHeight = 100;
    
    private int _rowCount;
    
    /// <summary>
    /// The fixed height for each row in the layout.
    /// </summary>
    public int FixedRowHeight
    {
        get => _fixedRowHeight;
        set => SetProperty(ref _fixedRowHeight, value);
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        var totalWidth = rectTransform.rect.width;
        SetLayoutInputForAxis(totalWidth, totalWidth, totalWidth, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        var totalHeight = _rowCount * _fixedRowHeight + padding.vertical;
        SetLayoutInputForAxis(totalHeight, totalHeight, totalHeight, 1);
    }

    public override void SetLayoutHorizontal()
    {
        var lineSpaces = ListPool<float>.Get();
        float currentLineWidth = 0;
        var availableWidth = rectTransform.rect.width - padding.horizontal;
        var startIndex = m_ReverseArrangement ? rectChildren.Count - 1 : 0;
        var endIndex = m_ReverseArrangement ? 0 : rectChildren.Count;
        var increment = m_ReverseArrangement ? -1 : 1;
        for (var i = startIndex; m_ReverseArrangement ? i >= endIndex : i < endIndex; i += increment)
        {
            var child = rectChildren[i];
            GetChildSizes(child, 0, m_ChildControlWidth, m_ChildForceExpandWidth, out var min, out var preferred, out _);
            var scaleFactor = m_ChildScaleWidth ? child.localScale[0] : 1f;
            var requiredSpace = Mathf.Max(min, preferred);
            if (currentLineWidth > 0 && currentLineWidth + requiredSpace * scaleFactor > availableWidth)
            {
                lineSpaces.Add(currentLineWidth - spacing);
                currentLineWidth = 0;
            }

            currentLineWidth += requiredSpace * scaleFactor + spacing;
        }

        if (currentLineWidth > 0) lineSpaces.Add(currentLineWidth - spacing);
        _rowCount = lineSpaces.Count;
        
        var alignmentOnAxisX = GetAlignmentOnAxis(0);
        var alignmentOnAxisY = GetAlignmentOnAxis(1);
        currentLineWidth = 0;
        availableWidth = rectTransform.rect.width - padding.horizontal;
        var rowIndex = 0;
        for (var i = startIndex; m_ReverseArrangement ? i >= endIndex : i < endIndex; i += increment)
        {
            var child = rectChildren[i];
            GetChildSizes(child, 0, m_ChildControlWidth, m_ChildForceExpandWidth, out var min, out var preferred,
                out _);
            var scaleFactorX = m_ChildScaleWidth ? child.localScale[0] : 1f;
            var requiredSpaceX = Mathf.Max(min, preferred);
            if (currentLineWidth > 0 && currentLineWidth + requiredSpaceX * scaleFactorX > availableWidth)
            {
                rowIndex++;
                currentLineWidth = 0;
            }

            var innerSizeX = lineSpaces[rowIndex];
            var startOffsetX = GetStartOffset(0, Mathf.Max(innerSizeX, min));
            if (m_ChildControlWidth)
            {
                SetChildAlongAxisWithScale(child, 0, startOffsetX + currentLineWidth, requiredSpaceX, scaleFactorX);
            }
            else
            {
                var offsetInCell = (requiredSpaceX - child.sizeDelta[0]) * alignmentOnAxisX;
                SetChildAlongAxisWithScale(child, 0, startOffsetX + currentLineWidth + offsetInCell, scaleFactorX);
            }

            currentLineWidth += requiredSpaceX * scaleFactorX + spacing;
            GetChildSizes(child, 1, false, false, out min, out preferred, out _);
            var scaleFactorY = m_ChildScaleHeight ? child.localScale[1] : 1f;
            var requiredSpaceY = Mathf.Max(min, preferred) * scaleFactorY;
            SetChildAlongAxisWithScale(child, 1, padding.top + _fixedRowHeight * rowIndex + (_fixedRowHeight - requiredSpaceY) * alignmentOnAxisY, scaleFactorY);
        }
    }

    public override void SetLayoutVertical()
    {
    }

    private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand, out float min,
        out float preferred, out float flexible)
    {
        if (!controlSize)
        {
            min = child.sizeDelta[axis];
            preferred = min;
            flexible = 0;
        }
        else
        {
            min = LayoutUtility.GetMinSize(child, axis);
            preferred = LayoutUtility.GetPreferredSize(child, axis);
            flexible = LayoutUtility.GetFlexibleSize(child, axis);
        }

        if (childForceExpand)
            flexible = Mathf.Max(flexible, 1);
    }
}