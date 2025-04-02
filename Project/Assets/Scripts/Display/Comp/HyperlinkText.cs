using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HyperlinkText : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text _tmpText;

    public void OnPointerClick(PointerEventData eventData)
    {
        _tmpText ??= GetComponent<TMP_Text>();
        var linkIndex = TMP_TextUtilities.FindIntersectingLink(_tmpText, eventData.position, _tmpText.canvas.worldCamera);
        if (linkIndex == -1) return;
        var linkInfo = _tmpText.textInfo.linkInfo[linkIndex];
        Application.OpenURL(linkInfo.GetLinkID());
    }
}