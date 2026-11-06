using UnityEngine;
using UnityEngine.InputSystem;

public class PartsWorkbenchInteractable : MonoBehaviour, IHoldInteractable, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "\uD30C\uCE20 \uC81C\uC791\uAE30 \uAC00\uB3D9";
    [SerializeField] private float holdDuration = 1.1f;

    [Header("Parts UI")]
    [SerializeField] private Canvas partsCanvas;
    [SerializeField] private CanvasGroup partsCanvasGroup;
    [SerializeField] private GameObject partsUIRoot;
    [SerializeField] private string fallbackCanvasNameContains = "parts make";
    [SerializeField] private bool pauseGameWhenOpened;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;
    [SerializeField] private bool closeWithEscape = true;
    [SerializeField] private int openedSortingOrder = 900;

    [Header("Proximity Open")]
    [SerializeField] private bool openWhenPlayerNearby;
    [SerializeField] private float autoOpenDistance = 2.2f;
    [SerializeField] private float autoCloseDistance = 3.0f;
    [SerializeField] private string playerTag = "Player";

    private GameObject currentInteractor;
    private bool isOpen;
    private PlayerModalControlContext.Lease modalLease;
    private float savedTimeScale;
    private Transform proximityPlayer;
    private Collider[] distanceColliders;

    private void Start()
    {
        CacheDistanceColliders();
    }

    public string GetInteractionName()
    {
        return interactionName;
    }

    public string GetDisplayName()
    {
        return interactionName;
    }

    public Sprite GetIcon()
    {
        return null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        // The parts workbench is opened by holding F through ProximityHoldPrompt.
        // Implementing IInteractable lets PlayerInteractor recognize the console
        // so nearby pickup/paper prompts do not steal the UI while looking at it.
    }

    public float GetHoldDuration()
    {
        return holdDuration;
    }

    public void BeginHold(GameObject interactor)
    {
    }

    public void UpdateHold(GameObject interactor, float holdProgress)
    {
    }

    public void CancelHold(GameObject interactor)
    {
    }

    public void CompleteHold(GameObject interactor)
    {
        OpenPartsUI(interactor);
    }

    private void Awake()
    {
        ResolvePartsUI();
        HidePartsUI();
    }

    private void Update()
    {
        if (openWhenPlayerNearby)
            TickProximityOpen();

        if (!isOpen || !closeWithEscape)
            return;

        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null
            ? keyboard.escapeKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Escape);

        if (escapePressed)
            ClosePartsUI();
    }

    private void ResolvePartsUI()
    {
        if (partsUIRoot != null)
        {
            if (partsCanvas == null)
                partsCanvas = partsUIRoot.GetComponent<Canvas>();

            if (partsCanvasGroup == null)
                partsCanvasGroup = partsUIRoot.GetComponent<CanvasGroup>();

            if (partsCanvasGroup == null)
                partsCanvasGroup = partsUIRoot.AddComponent<CanvasGroup>();

            return;
        }

        if (partsCanvas == null && !string.IsNullOrEmpty(fallbackCanvasNameContains))
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && ContainsIgnoreCase(canvas.name, fallbackCanvasNameContains))
                {
                    partsCanvas = canvas;
                    break;
                }
            }
        }

        if (partsCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && IsFallbackPartsCanvasName(canvas.name))
                {
                    partsCanvas = canvas;
                    break;
                }
            }
        }

        if (partsCanvas == null)
            return;

        partsUIRoot = partsCanvas.gameObject;

        if (partsCanvasGroup == null)
            partsCanvasGroup = partsCanvas.GetComponent<CanvasGroup>();

        if (partsCanvasGroup == null)
            partsCanvasGroup = partsCanvas.gameObject.AddComponent<CanvasGroup>();
    }

    private void OpenPartsUI(GameObject interactor)
    {
        if (isOpen || !isActiveAndEnabled) return;
        ResolvePartsUI();
        if (partsUIRoot == null) return;
        var context = PlayerModalControlContext.For(interactor, true);
        if (context == null) return;
        var options = PlayerModalControlContext.Options.None;
        if (disablePlayerControlWhenOpened) options |= PlayerModalControlContext.Options.Gameplay | PlayerModalControlContext.Options.PlayerInput;
        if (unlockCursorWhenOpened) options |= PlayerModalControlContext.Options.Cursor;
        modalLease = context.Acquire(this, options);
        if (modalLease == null || modalLease.IsReleased) return;
        currentInteractor = interactor;
        savedTimeScale = Time.timeScale;
        isOpen = true;
        try
        {

        if (partsUIRoot != null)
        {
            ActivateParents(partsUIRoot.transform);
            partsUIRoot.SetActive(true);
        }

        if (!isOpen || !isActiveAndEnabled || !partsUIRoot.activeInHierarchy)
        { ClosePartsUI(); return; }

        if (partsCanvas != null)
        {
            partsCanvas.overrideSorting = true;
            partsCanvas.sortingOrder = openedSortingOrder;
            partsCanvas.enabled = true;
        }

        if (partsCanvasGroup != null)
        {
            partsCanvasGroup.alpha = 1f;
            partsCanvasGroup.interactable = true;
            partsCanvasGroup.blocksRaycasts = true;
        }

        HideOtherBlockingCanvases();

        PartCraftingUI craftingUI = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<PartCraftingUI>(true) : null;
        if (craftingUI == null)
            craftingUI = FindObjectOfType<PartCraftingUI>(true);
        if (craftingUI != null)
        {
            craftingUI.SetActivePaper(PaperReadUI.LastReadPaper);
            craftingUI.RefreshInventoryList();
        }

        PartsCraftingInventoryBridge bridge = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<PartsCraftingInventoryBridge>(true) : null;
        if (bridge == null)
            bridge = FindObjectOfType<PartsCraftingInventoryBridge>(true);
        if (bridge != null)
            bridge.RefreshFromInventory();

        CraftingController controller = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<CraftingController>(true) : null;
        if (controller == null)
            controller = FindObjectOfType<CraftingController>(true);
        if (controller != null)
        {
            CraftingRecipeData activeRecipe = PaperReadUI.LastReadPaper != null && PaperReadUI.LastReadPaper.relatedRecipe != null
                ? PaperReadUI.LastReadPaper.relatedRecipe
                : PartResourceResolver.FindRecipe("superjump_paper");
            controller.SetActivePartRecipe(activeRecipe);
            controller.OnMaterialsChanged();
        }

        if (pauseGameWhenOpened)
            Time.timeScale = 0f;
        }
        catch { ClosePartsUI(); throw; }
    }

    public void OpenPartsUIFromExternal(GameObject interactor)
    {
        OpenPartsUI(interactor);
    }

    private void ClosePartsUI()
    {
        bool wasOpen = isOpen;
        isOpen = false;
        try { HidePartsUI(); }
        finally
        {
            if (wasOpen && pauseGameWhenOpened) Time.timeScale = savedTimeScale;
            modalLease?.Release();
            modalLease = null;
            currentInteractor = null;
        }
    }

    private void OnDisable() { ClosePartsUI(); }
    private void OnDestroy() { ClosePartsUI(); }
    private void LateUpdate()
    {
        if (isOpen && (partsUIRoot == null || !partsUIRoot.activeInHierarchy
            || modalLease == null || modalLease.IsReleased)) ClosePartsUI();
    }

    private void TickProximityOpen()
    {
        if (proximityPlayer == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (foundPlayer != null)
                proximityPlayer = foundPlayer.transform;
            else
            {
                Move_Hankyul hankyul = FindObjectOfType<Move_Hankyul>();
                if (hankyul != null)
                    proximityPlayer = hankyul.transform;
                else
                {
                    Move_Yunseo yunseo = FindObjectOfType<Move_Yunseo>();
                    if (yunseo != null)
                        proximityPlayer = yunseo.transform;
                }
            }
        }

        if (proximityPlayer == null)
            return;

        float distance = Vector3.Distance(transform.position, proximityPlayer.position);
        if (distanceColliders == null || distanceColliders.Length == 0)
            CacheDistanceColliders();

        if (distanceColliders != null)
        {
            for (int i = 0; i < distanceColliders.Length; i++)
            {
                Collider targetCollider = distanceColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                distance = Mathf.Min(
                    distance,
                    Vector3.Distance(targetCollider.ClosestPoint(proximityPlayer.position), proximityPlayer.position)
                );
            }
        }

        if (!isOpen && distance <= autoOpenDistance)
            OpenPartsUI(proximityPlayer.gameObject);
        else if (isOpen && distance >= autoCloseDistance)
            ClosePartsUI();
    }

    private void CacheDistanceColliders()
    {
        distanceColliders = GetComponentsInChildren<Collider>(true);
    }

    public void CloseOpenedPartsUI()
    {
        if (isOpen)
            ClosePartsUI();
        else
            HidePartsUI();
    }

    private void HidePartsUI()
    {
        if (partsCanvasGroup != null)
        {
            partsCanvasGroup.alpha = 0f;
            partsCanvasGroup.interactable = false;
            partsCanvasGroup.blocksRaycasts = false;
        }

        if (partsCanvas != null)
            partsCanvas.enabled = false;
    }

    private void HideOtherBlockingCanvases()
    {
        PaperReadUI paperUI = FindObjectOfType<PaperReadUI>(true);
        // Transfer from this Player's paper owner after acquiring the workbench lease.
        if (paperUI != null) paperUI.CloseForPlayer(currentInteractor);
    }

    private static void ActivateParents(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }



    private static bool IsFallbackPartsCanvasName(string canvasName)
    {
        return string.Equals(canvasName, "parts make", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(canvasName, "Canvas (1)", System.StringComparison.OrdinalIgnoreCase)
            || ContainsIgnoreCase(canvasName, "\uD30C\uCE20");
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return false;

        return source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
