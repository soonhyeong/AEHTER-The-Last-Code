using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForceUIVisible : MonoBehaviour
{
    private void OnEnable()
    {
        CanvasGroup[] canvasGroups = GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup cg in canvasGroups)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            rect.localScale = Vector3.one;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in graphics)
        {
            Color c = g.color;
            c.a = 1f;
            g.color = c;
            g.gameObject.SetActive(true);
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text t in texts)
        {
            Color c = t.color;
            c.a = 1f;
            t.color = c;
            t.gameObject.SetActive(true);
        }

        Debug.Log("[ForceUIVisible] PuzzleRoot 강제 표시 적용");
    }
}