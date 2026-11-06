using UnityEngine;
using UnityEngine.InputSystem;

public class PartsSlot2ToggleController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;

    private PlayerModalControlContext playerContext;
    private PlayerModalControlContext.Lease modalLease;
    private bool isOpen;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();

    private void Awake()
    {
        PartsSlot2GlobalBootstrap.EnsureEventSystem();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        RefreshPlayerReferences();
        // Closing an owner that never acquired a lease cannot enable Player controls.
        SetOpen(false);
    }

    private void Update()
    {
        if (!CanReadLocalInput()) return;
        Keyboard keyboard = Keyboard.current;
        bool pressed = keyboard != null
            ? keyboard.pKey.wasPressedThisFrame : Input.GetKeyDown(toggleKey);
        if (pressed) SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        if (!open)
        {
            isOpen = false;
            try { SetVisible(false); }
            finally { modalLease?.Release(); modalLease = null; }
            return;
        }
        if (!CanReadLocalInput()) return;
        if (isOpen) return;
        if (!isActiveAndEnabled || (canvas == null && canvasGroup == null)) return;
        RefreshPlayerReferences();
        // Detached bootstrap templates must be parented/bound before opening.
        if (playerContext == null) return;
        var options = PlayerModalControlContext.Options.None;
        if (disablePlayerControlWhenOpened) options |= PlayerModalControlContext.Options.Gameplay;
        if (unlockCursorWhenOpened) options |= PlayerModalControlContext.Options.Cursor;
        modalLease = playerContext.Acquire(this, options);
        if (modalLease == null || modalLease.IsReleased) return;
        isOpen = true;
        try
        {
            PartsSlot2GlobalBootstrap.EnsureEventSystem();
            SetVisible(true);
            RefreshEquipmentUI();
            if (!PlayerLocalInputOwnership.CanConsumeLocalInput(playerContext) || !isActiveAndEnabled || modalLease == null || modalLease.IsReleased) SetOpen(false);
        }
        catch { SetOpen(false); throw; }
    }

    private void SetVisible(bool open)
    {
        if (canvas != null) canvas.enabled = open;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = open ? 1f : 0f;
            canvasGroup.interactable = open;
            canvasGroup.blocksRaycasts = open;
        }
    }

    private void RefreshEquipmentUI()
    {
        EquipmentUIController[] controllers = GetComponentsInChildren<EquipmentUIController>(true);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null) controllers[i].RefreshFromInventory();
    }

    public void RefreshPlayerReferences()
    {
        // Use the actual parent supplied by the existing bootstrap, never a global first Player.
        var next = PlayerModalControlContext.For(gameObject, true);
        if (playerContext != next)
        {
            SetOpen(false);
            playerContext = next;
        }
    }

    private void LateUpdate()
    {
        if (!CanReadLocalInput()) return;
        if (isOpen && (playerContext == null || modalLease == null || modalLease.IsReleased
            || (canvas != null && !canvas.gameObject.activeInHierarchy)))
            SetOpen(false);
    }

    private void OnDisable() { SetOpen(false); }
    private void OnDestroy() { SetOpen(false); }

    private bool CanReadLocalInput()
    {
        RefreshPlayerReferences();
        if (localInputGate.CanRead(playerContext)) return true;
        if (isOpen || modalLease != null) SetOpen(false);
        return false;
    }
}
