using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player HUD(상호작용 UI) 전담:
/// - 크로스헤어(hover 시 색/크기/스프라이트 보간)
/// - 픽업 패널(F : 아이템명 + 아이콘) 표시/숨김
/// 
/// 주의:
/// - 이 스크립트는 "표시만" 담당한다.
/// - Interact 호출/거리 판정/대상 탐색은 PlayerInteractor가 담당한다.
/// </summary>
public class PlayerUIManager : MonoBehaviour
{
    [Header("Interactor (필수)")]
    public PlayerInteractor interactor;

    [Header("Pickup UI")]
    public GameObject pickupPanel;           // "F : [아이콘] [이름]" 패널
    public TextMeshProUGUI pickupText;       // "F : 아이템명"
    public Image pickupIcon;                 // 아이콘

    [Header("Crosshair UI")]
    public Image crosshairImage;             // 중앙 십자선
    public Color normalColor = Color.white;
    public Color hoverColor = Color.cyan;
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.25f;
    public Sprite normalSprite;              // 기본 스프라이트(없으면 현재 스프라이트 유지)
    public Sprite hoverSprite;               // 호버 시 스프라이트(없으면 교체 안 함)
    [Range(0.01f, 30f)]
    public float transitionSpeed = 12f;

    [Header("Item Name Text (선택)")]
    public Text itemNameText;                // 중앙 상/하단 표시용(원하면 사용)

    [Header("Quick Slot UIs")]
    public QuickSlotManager quickSlotManager;
    public QuickSlotUI[] quickSlotUIs; // 0~4 순서로 넣기

    // 내부 상태(보간용)
    private Color currentColor;
    private Vector3 currentScale;
    private readonly LocalPlayerUIBinding dataBinding = new LocalPlayerUIBinding();
    private PlayerInteractor subscribedInteractor;
    private Canvas[] ownCanvases;
    private bool[] canvasEnabled;
    private bool presentationSuppressed;

    private void Awake()
    {
        CachePresentation();
        RefreshDataBinding(true);
        if (dataBinding.Root == null) return;

        // 초기 UI 상태
        if (pickupPanel != null)
            pickupPanel.SetActive(false);

        ClearPickupContent();

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

        currentColor = normalColor;
        currentScale = normalScale;

        ApplyCrosshairImmediate();
    }

    private void OnEnable()
    {
        RefreshDataBinding(true);
    }

    private void OnDisable()
    {
        if (!ReferenceEquals(subscribedInteractor, null)) subscribedInteractor.OnTargetChanged -= RefreshInteractionUI;
        subscribedInteractor = null;
        interactor = null;
        quickSlotManager = null;
        dataBinding.Clear();
    }

    private void Start()
    {
        RefreshInteractionUI();
    }

    private void Update()
    {
        RefreshDataBinding();
        if (dataBinding.Root == null) return;
        UpdateCrosshairVisuals();
    }

    private void CachePresentation()
    {
        if (ownCanvases != null) return;
        var owner = LocalPlayerBindingResolver.PlayerRoot(this);
        if (owner == null) return;
        ownCanvases = owner.GetComponentsInChildren<Canvas>(true);
        canvasEnabled = new bool[ownCanvases.Length];
        for (int i = 0; i < ownCanvases.Length; i++) canvasEnabled[i] = ownCanvases[i].enabled;
    }

    private void ApplyPresentation()
    {
        CachePresentation();
        if (ownCanvases == null) return;
        bool suppress = dataBinding.Root == null;
        if (suppress == presentationSuppressed) return;
        var owner = LocalPlayerBindingResolver.PlayerRoot(this);
        for (int i = 0; i < ownCanvases.Length; i++)
        {
            Canvas c = ownCanvases[i];
            // Audited canonical screen HUD/internal puzzle canvas only. World markers stay visible.
            if (c == null || c.renderMode == RenderMode.WorldSpace || LocalPlayerBindingResolver.PlayerRoot(c) != owner) continue;
            if (c.gameObject.name != "Player_UI" && c.gameObject.name != "PuzzleUI") continue;
            if (suppress) { canvasEnabled[i] = c.enabled; c.enabled = false; }
            else c.enabled = canvasEnabled[i];
        }
        presentationSuppressed = suppress;
    }

    private void RefreshDataBinding(bool force = false)
    {
        bool changed = dataBinding.Poll(this, interactor, force);
        ApplyPresentation();
        if (!changed && !force && !dataBinding.SourceChanged && !(dataBinding.Queried && dataBinding.Root != null && interactor == null)) return;
        var root = dataBinding.Root;
        var next = LocalPlayerBindingResolver.Owned<PlayerInteractor>(root);
        if (!ReferenceEquals(subscribedInteractor, next))
        {
            if (!ReferenceEquals(subscribedInteractor, null)) subscribedInteractor.OnTargetChanged -= RefreshInteractionUI;
            subscribedInteractor = next;
            if (subscribedInteractor != null) subscribedInteractor.OnTargetChanged += RefreshInteractionUI;
        }
        interactor = next;
        quickSlotManager = LocalPlayerBindingResolver.Owned<QuickSlotManager>(root);
        if (root != null && quickSlotUIs != null)
        {
            foreach (var ui in quickSlotUIs) if (ui != null) ui.Bind(quickSlotManager);
            if (quickSlotManager != null) quickSlotManager.BindUI(quickSlotUIs);
        }
        RefreshInteractionUI();
    }

    /// <summary>
    /// PlayerInteractor의 대상/거리 상태가 바뀌었을 때 호출되어
    /// 패널/텍스트/아이콘을 갱신한다.
    /// </summary>
    private void RefreshInteractionUI()
    {
        RefreshDataBinding();
        // Remote instances must not write even when a stale explicit reference points at shared UI.
        if (dataBinding.Root == null && LocalPlayerBindingResolver.PlayerRoot(this) != null) return;
        if (interactor == null)
        {
            HideAllInteractionUI();
            return;
        }

        var target = interactor.CurrentInteractable;
        bool showPanel = interactor.CanShowUI && target is IPickupInteractable;

        // 패널 표시/숨김
        if (pickupPanel != null)
            pickupPanel.SetActive(showPanel);

        // 아이템 이름 텍스트(선택)
        if (itemNameText != null)
        {
            bool showName = showPanel && target != null && !string.IsNullOrEmpty(SafeGetName(target));
            itemNameText.gameObject.SetActive(showName);
            if (showName)
                itemNameText.text = SafeGetName(target);
        }

        if (!showPanel || target == null)
        {
            ClearPickupContent();
            return;
        }

        // 패널 내부 텍스트
        if (pickupText != null)
        {
            string name = SafeGetName(target);
            pickupText.text = $"{interactor.interactKey} : {name}";
        }

        // 패널 내부 아이콘
        if (pickupIcon != null)
        {
            var icon = target.GetIcon();
            pickupIcon.sprite = icon;
            pickupIcon.enabled = (icon != null);
        }
    }

    /// <summary>
    /// 크로스헤어는 부드러운 전환이 필요하므로 매 프레임 보간 업데이트.
    /// (표시 조건은 "정면에 상호작용 대상이 있는가" 기준)
    /// </summary>
    private void UpdateCrosshairVisuals()
    {
        if (crosshairImage == null) return;

        // ✅ pickupPanel 표시 타이밍과 동일하게 hover
        bool hovering = (interactor != null && interactor.CanShowUI);

        Color targetColor = hovering ? hoverColor : normalColor;
        Vector3 targetScale = hovering ? hoverScale : normalScale;

        Sprite targetSprite = null;
        if (hovering && hoverSprite != null) targetSprite = hoverSprite;
        else if (!hovering && normalSprite != null) targetSprite = normalSprite;

        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);
        currentScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * transitionSpeed);

        crosshairImage.color = currentColor;
        crosshairImage.rectTransform.localScale = currentScale;

        if (targetSprite != null && crosshairImage.sprite != targetSprite)
            crosshairImage.sprite = targetSprite;
    }

    private void ApplyCrosshairImmediate()
    {
        if (crosshairImage == null) return;

        crosshairImage.color = normalColor;
        crosshairImage.rectTransform.localScale = normalScale;

        if (normalSprite != null)
            crosshairImage.sprite = normalSprite;
    }

    private void HideAllInteractionUI()
    {
        if (pickupPanel != null)
            pickupPanel.SetActive(false);

        ClearPickupContent();

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

        // 크로스헤어는 "숨김"이 아니라 기본 상태로 되돌림
        currentColor = normalColor;
        currentScale = normalScale;
        ApplyCrosshairImmediate();
    }

    private void ClearPickupContent()
    {
        if (pickupText != null) pickupText.text = string.Empty;
        if (pickupIcon != null)
        {
            pickupIcon.sprite = null;
            pickupIcon.enabled = false;
        }
        if (itemNameText != null) itemNameText.text = string.Empty;
    }

    private static string SafeGetName(IInteractable interactable)
    {
        try
        {
            return interactable.GetDisplayName();
        }
        catch
        {
            return "Item";
        }
    }
}
