
using UnityEngine;
using UnityEngine.EventSystems;

public class DropSlot : MonoBehaviour, IDropHandler
{
    [Header("드롭 목적지 슬롯")]
    public InventorySlot targetSlot;
    public bool clearSourceOnDrop = true;

    // CraftingController 캐시
    private CraftingController craftingController;

    private void Awake()
    {
        // 씬에 하나만 있어야 함
        craftingController = FindObjectOfType<CraftingController>();
        if (craftingController == null)
        {
            Debug.LogError("[DropSlot] CraftingController를 찾을 수 없습니다.");
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (targetSlot == null) return;
        if (eventData.pointerDrag == null) return;

        DraggableSlot dragged =
            eventData.pointerDrag.GetComponent<DraggableSlot>();
        if (dragged == null) return;

        InventorySlot fromSlot = dragged.slot;
        if (fromSlot == null || fromSlot.IsEmpty()) return;

        // 같은 슬롯이면 무시
        if (fromSlot == targetSlot) return;
        if (!targetSlot.IsEmpty()) return;

        // 🔥 이동 처리 (선택지 B: 완전 이동)
        dragged.dropSucceeded = true;

        targetSlot.SetItem(fromSlot.itemData);
        if (clearSourceOnDrop)
            fromSlot.Clear();

        // 🔥 중요: 재료 변경 알림만 보냄 (제작 ❌)
        if (craftingController != null)
        {
            craftingController.OnMaterialsChanged();
        }
    }
}
