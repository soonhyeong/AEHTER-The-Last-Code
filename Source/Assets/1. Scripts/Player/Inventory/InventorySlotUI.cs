using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    [Header("UI ����")]
    public Image iconImage;             // ItemImage
    public TextMeshProUGUI countText;   // Badge�� TMP
    public Image checkmarkImage;        // Checkmark �̹���

    [Header("üũ ����")]
    public Color normalCheckColor = Color.white;
    public Color selectedCheckColor = Color.cyan;

    private PlayerInventory inventory;
    private InventoryUI inventoryUI;
    private int slotIndex;

    private Canvas rootCanvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalAnchoredPos;

    private static InventorySlotUI draggingSlot;
    private const float DoubleClickWindow = 0.35f;
    private float lastClickTime = -10f;

    public int SlotIndex => slotIndex;

    public void Initialize(PlayerInventory inventory, int index, InventoryUI ui)
    {
        this.inventory = inventory;
        this.slotIndex = index;
        this.inventoryUI = ui;

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();

        SetSelected(false);
        Refresh();
    }

    public void Refresh()
    {
        if (inventory == null) return;

        var slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (countText != null)
                countText.text = "";
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = slot.item.itemSprite;
                iconImage.enabled = (slot.item.itemSprite != null);
            }

            if (countText != null)
                countText.text = slot.count > 1 ? slot.count.ToString() : "";
        }
    }

    public void SetSelected(bool selected)
    {
        if (checkmarkImage == null) return;
        checkmarkImage.color = selected ? selectedCheckColor : normalCheckColor;
    }

public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        bool doubleClick = Time.unscaledTime - lastClickTime <= DoubleClickWindow;
        lastClickTime = Time.unscaledTime;

        if (doubleClick && inventoryUI != null && inventoryUI.OnSlotDoubleClicked(slotIndex))
            return;

        if (inventoryUI != null)
            inventoryUI.OnSlotClicked(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventory == null) return;

        var slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
            return;

        draggingSlot = this;
        originalAnchoredPos = rectTransform.anchoredPosition;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.7f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingSlot != this) return;
        if (rootCanvas == null) return;

        float scaleFactor = rootCanvas.scaleFactor == 0 ? 1 : rootCanvas.scaleFactor;
        rectTransform.anchoredPosition += eventData.delta / scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingSlot != this) return;

        draggingSlot = null;

        rectTransform.anchoredPosition = originalAnchoredPos;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggingSlot == null || draggingSlot == this) return;
        if (inventoryUI == null) return;

        int fromIndex = draggingSlot.slotIndex;
        int toIndex = this.slotIndex;

        inventoryUI.RequestSwapSlots(fromIndex, toIndex);
    }
}
