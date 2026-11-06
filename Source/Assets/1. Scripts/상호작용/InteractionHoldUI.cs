using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionHoldUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image radialFillImage;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private string fallbackTitle = "상호작용";
    [SerializeField] private string fallbackActionText = "[F키를 길게 누르기]";

    private Object currentOwner;

    private void Awake()
    {
        ResolveTextReference();
        ForceHide();
    }

    public void Show()
    {
        ShowFor(null, string.Empty, string.Empty);
    }

    public void Show(string title, string actionText)
    {
        ShowFor(null, title, actionText);
    }

    public void ShowFor(Object owner, string title, string actionText)
    {
        ShowFor(owner, title, actionText, false);
    }

    public void ShowFor(Object owner, string title, string actionText, bool suppressText)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        currentOwner = owner;
        ResolveTextReference();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (radialFillImage != null)
            radialFillImage.fillAmount = 0f;

        if (suppressText)
            ClearText();
        else
            SetText(title, actionText);
    }

    public void UpdateProgress(float value)
    {
        UpdateProgressFor(null, value);
    }

    public void UpdateProgressFor(Object owner, float value)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        if (radialFillImage != null)
            radialFillImage.fillAmount = Mathf.Clamp01(value);
    }

    public void HideImmediate()
    {
        ForceHide();
    }

    public void HideFor(Object owner)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        ForceHide();
    }

    private void ForceHide()
    {
        currentOwner = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (radialFillImage != null)
            radialFillImage.fillAmount = 0f;
    }

    private void SetText(string title, string actionText)
    {
        ResolveTextReference();

        if (interactionText == null)
            return;

        interactionText.enabled = true;

        title = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim();
        actionText = string.IsNullOrWhiteSpace(actionText) ? fallbackActionText : actionText.Trim();

        if (string.IsNullOrWhiteSpace(title))
            interactionText.text = actionText;
        else if (string.IsNullOrWhiteSpace(actionText))
            interactionText.text = title;
        else
            interactionText.text = title + "\n" + actionText;
    }

    private void ClearText()
    {
        ResolveTextReference();

        if (interactionText == null)
            return;

        interactionText.text = string.Empty;
        interactionText.enabled = false;
    }

    private void ResolveTextReference()
    {
        if (interactionText != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null)
                continue;

            string objectName = candidate.name.ToLowerInvariant();
            if (objectName.Contains("key"))
                continue;

            if (objectName.Contains("prompt")
                || objectName.Contains("label")
                || objectName.Contains("interaction")
                || objectName.Contains("action"))
            {
                interactionText = candidate;
                return;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || candidate.name.ToLowerInvariant().Contains("key"))
                continue;

            interactionText = candidate;
            return;
        }

        CreateFallbackInteractionText();
    }

    private void CreateFallbackInteractionText()
    {
        GameObject textObject = new GameObject("Interaction Action Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 80f);
        rect.anchoredPosition = new Vector2(0f, -118f);

        interactionText = textObject.GetComponent<TMP_Text>();
        interactionText.alignment = TextAlignmentOptions.Center;
        interactionText.fontSize = 24f;
        interactionText.color = Color.white;
        interactionText.raycastTarget = false;
    }
}

