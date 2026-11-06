using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PaperReadUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text requiredModulesText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text resultPartText;
    [SerializeField] private Button closeButton;

    [Header("Linked Systems")]
    [SerializeField] private PartCraftingUI craftingUI;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;

    public static PaperData LastReadPaper { get; private set; }

    private GameObject currentInteractor;
    private bool isOpen;
    private PlayerModalControlContext.Lease modalLease;

    private void Awake()
    {
        EnsureReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Hide();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null
            ? keyboard.escapeKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Escape);

        if (escapePressed)
            Close();
    }

    public void Open(PaperInteractable paper, PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || interactor == null) return;
        if (isOpen && currentInteractor != interactor.gameObject) return;
        EnsureReferences();
        if (canvas == null) return;
        var context = PlayerModalControlContext.For(interactor.gameObject, true);
        if (context == null) return;
        var options = PlayerModalControlContext.Options.None;
        if (disablePlayerControlWhenOpened) options |= PlayerModalControlContext.Options.Gameplay | PlayerModalControlContext.Options.PlayerInput;
        if (unlockCursorWhenOpened) options |= PlayerModalControlContext.Options.Cursor;
        modalLease = context.Acquire(this, options);
        if (modalLease == null || modalLease.IsReleased) return;
        currentInteractor = interactor.gameObject;
        try
        {
        PaperData data = paper != null ? paper.PaperData : null;
        LastReadPaper = data;

        if (titleText != null)
            titleText.text = paper != null ? paper.GetTitle() : "Manual";

        if (bodyText != null)
            bodyText.text = paper != null ? paper.GetBody() : "No manual data.";

        if (hintText != null)
            hintText.text = paper != null ? paper.GetHint() : string.Empty;

        if (requiredModulesText != null)
            requiredModulesText.text = BuildRequiredModuleText(paper);

        if (resultPartText != null)
            resultPartText.text = BuildResultPartText(paper);
        else if (requiredModulesText != null)
            requiredModulesText.text += "\n" + BuildResultPartText(paper);

        if (craftingUI == null)
            craftingUI = FindObjectOfType<PartCraftingUI>(true);

        if (craftingUI != null && data != null)
            craftingUI.SetActivePaper(data);

        Show();
        }
        catch { Close(); throw; }
    }

    public void Close()
    {
        isOpen = false;
        try { Hide(); }
        finally
        {
            modalLease?.Release();
            modalLease = null;
            currentInteractor = null;
        }
    }

    internal void CloseForPlayer(GameObject player)
    {
        if (isOpen && PlayerModalControlContext.For(player) == PlayerModalControlContext.For(currentInteractor)) Close();
    }

    private void OnDisable() { Close(); }
    private void OnDestroy() { Close(); }
    private void LateUpdate()
    {
        if (isOpen && (canvas == null || !canvas.gameObject.activeInHierarchy
            || modalLease == null || modalLease.IsReleased)) Close();
    }

    private void Show()
    {
        if (canvas != null)
            canvas.enabled = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        isOpen = true;
    }

    private void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (canvas != null)
            canvas.enabled = false;
    }

    private void EnsureReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>(true);
    }

    private static string BuildRequiredModuleText(PaperInteractable paper)
    {
        string[] modules = paper != null ? paper.GetRequiredModules() : null;
        if (modules == null || modules.Length == 0)
            return "Required modules: none";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Required modules");
        for (int i = 0; i < modules.Length; i++)
            builder.AppendLine("- " + modules[i]);

        return builder.ToString();
    }

    private static string BuildResultPartText(PaperInteractable paper)
    {
        string resultName = paper != null ? paper.GetResultPartName() : string.Empty;
        if (string.IsNullOrWhiteSpace(resultName))
            resultName = "Unknown Part";

        return "Result part: " + resultName;
    }


}
