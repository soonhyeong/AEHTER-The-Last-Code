using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoldInteractionUI : MonoBehaviour
{
    [Header("UI 오브젝트")]
    [SerializeField] private GameObject interactionUIRoot;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text interactionText;

    [Header("기본 문구")]
    [SerializeField] private string defaultMessage = "F 키를 누르고 해킹 시작";

    private void Awake()
    {
        HideUI();
        SetProgress(0f);
        SetText(defaultMessage);
    }

    public void ShowUI(string message = "")
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(true);

        if (!string.IsNullOrEmpty(message))
            SetText(message);
        else
            SetText(defaultMessage);
    }

    public void HideUI()
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(false);

        SetProgress(0f);
    }

    public void SetProgress(float normalizedValue)
    {
        if (progressFill == null)
            return;

        progressFill.fillAmount = Mathf.Clamp01(normalizedValue);
    }

    public void SetText(string message)
    {
        if (interactionText != null)
            interactionText.text = message;
    }
}