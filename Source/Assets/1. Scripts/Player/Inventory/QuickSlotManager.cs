using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class QuickSlotManager : MonoBehaviour
{
    [Header("참조(인스펙터 연결 권장)")]
    public PlayerInventory inventory;
    public PlayerStatus playerStatus;

    [Tooltip("퀵슬롯 개수 (지금은 5칸)")]
    public int slotCount = 5;

    // 각 퀵슬롯 -> 인벤토리 슬롯 인덱스 (-1 비어있음)
    [SerializeField] private int[] quickSlots;

    // UI는 PlayerUIManager가 소유하고 여기엔 주입만 됨
    private QuickSlotUI[] slotUIs;
    private PlayerModalControlContext playerContext;
    private int lastInputFrame = -1;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private readonly LocalPlayerUIBinding dataBinding = new LocalPlayerUIBinding();
    private PlayerInventory subscribedInventory;
    private bool persistenceRestoring;
    internal int[] CopyPersistentSlots() => quickSlots == null ? null : (int[])quickSlots.Clone();
    internal void BeginPersistenceRestore() { persistenceRestoring = true; }
    internal void FinishPersistenceRestore(int[] exactSlots)
    {
        quickSlots = (int[])exactSlots.Clone();
        RefreshDataBinding();
        persistenceRestoring = false;
        RefreshAllSlots();
    }

    private void Awake()
    {
        bool initializedEmpty = quickSlots == null || quickSlots.Length != slotCount;
        if (initializedEmpty)
        {
            quickSlots = new int[slotCount];
            for (int i = 0; i < quickSlots.Length; i++) quickSlots[i] = -1;
        }

        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] < -1) quickSlots[i] = -1;

        RefreshDataBinding(true);
        // Keep the existing auto-assignment policy for an already populated,
        // resolved inventory, without treating every unassigned slot as index 0.
        // Remote scripted executors keep their own data refs; UI writes remain
        // guarded by dataBinding.Root in RefreshAllSlots.
        if (initializedEmpty && inventory != null)
        {
            AutoAssignFromInventory();
            RefreshAllSlots();
        }
    }

    private void OnDestroy()
    {
        ClearDataBinding();
    }

    private void OnEnable() { RefreshDataBinding(true); }
    private void OnDisable() { ClearDataBinding(); }

    private void ClearDataBinding()
    {
        if (!ReferenceEquals(subscribedInventory, null)) subscribedInventory.OnInventoryChanged -= HandleInventoryChanged;
        subscribedInventory = null;
        inventory = null;
        playerStatus = null;
        playerContext = null;
        dataBinding.Clear();
        ClearVisibleSlots();
    }

    private void ClearVisibleSlots()
    {
        var owner = LocalPlayerBindingResolver.PlayerRoot(this);
        if (slotUIs != null) foreach (var ui in slotUIs)
            if (ui != null && (owner == null || LocalPlayerBindingResolver.PlayerRoot(ui) == owner)) ui.SetEmpty();
    }

    private void RefreshDataBinding(bool force = false)
    {
        bool changed = dataBinding.Poll(this, inventory != null ? (Component)inventory : playerStatus, force);
        var root = dataBinding.Root;
        if (!changed && !force && !dataBinding.SourceChanged && !(dataBinding.Queried && dataBinding.Root != null && (inventory == null || playerStatus == null))) return;
        // Remote scripted UseSlot is an existing executor contract, not local UI authority.
        // Keep its own component refs, but never subscribe or render it as the local HUD.
        var executorRoot = root != null ? root : LocalPlayerBindingResolver.PlayerRoot(this);
        if (!LocalPlayerBindingResolver.IsLive(executorRoot)) executorRoot = null;
        var next = LocalPlayerBindingResolver.Owned<PlayerInventory>(root);
        if (!ReferenceEquals(subscribedInventory, next))
        {
            if (!ReferenceEquals(subscribedInventory, null)) subscribedInventory.OnInventoryChanged -= HandleInventoryChanged;
            subscribedInventory = next;
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += HandleInventoryChanged;
        }
        inventory = LocalPlayerBindingResolver.Owned<PlayerInventory>(executorRoot);
        playerStatus = LocalPlayerBindingResolver.Owned<PlayerStatus>(executorRoot);
        playerContext = executorRoot != null ? PlayerModalControlContext.For(executorRoot.gameObject, true) : null;
        if (root != null && quickSlots != null) RefreshAllSlots();
        else ClearVisibleSlots();
    }

    // PlayerUIManager에서 호출 (UI 주입)
    public void BindUI(QuickSlotUI[] uis)
    {
        slotUIs = uis;
        RefreshDataBinding();
        if (dataBinding.Root == null) return;
        RefreshAllSlots();
    }

    private void HandleInventoryChanged()
    {
        if (persistenceRestoring) return;
        RefreshDataBinding();
        if (dataBinding.Root == null) return;
        AutoAssignFromInventory();  // ✅ 자동 배치
        RefreshAllSlots();          // ✅ UI 갱신
    }

    /// <summary>
    /// 인벤토리에 존재하는 아이템을 빈 퀵슬롯에 자동으로 배치한다.
    /// 규칙:
    /// - 이미 어떤 퀵슬롯이 참조 중인 인벤 인덱스는 중복 배치하지 않음
    /// - 빈 퀵슬롯(-1)에만 채움
    /// </summary>
    private void AutoAssignFromInventory()
    {
        if (inventory == null) return;

        // 1) 현재 quickSlots에 걸려있는데, 인벤에서 비어버린 슬롯은 해제
        for (int q = 0; q < quickSlots.Length; q++)
        {
            int invIndex = quickSlots[q];
            if (invIndex < 0 || invIndex >= inventory.SlotCount) { quickSlots[q] = -1; continue; }

            var slot = inventory.GetSlot(invIndex);
            if (slot == null || slot.item == null || slot.count <= 0)
                quickSlots[q] = -1;
        }

        // 2) 인벤을 순회하면서, 아직 퀵슬롯에 없는 아이템을 빈 퀵슬롯에 채움
        for (int inv = 0; inv < inventory.SlotCount; inv++)
        {
            var s = inventory.GetSlot(inv);
            if (s == null || s.item == null || s.count <= 0) continue;

            // 이미 어떤 퀵슬롯이 이 inv 인덱스를 쓰고 있으면 스킵
            if (IsInventoryIndexAlreadyAssigned(inv)) continue;

            // 빈 퀵슬롯 찾기
            int emptyQ = FindEmptyQuickSlot();
            if (emptyQ == -1) break; // 더 이상 빈칸 없음

            quickSlots[emptyQ] = inv;
        }
    }

    private bool IsInventoryIndexAlreadyAssigned(int invIndex)
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == invIndex) return true;
        return false;
    }

    private int FindEmptyQuickSlot()
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == -1) return i;
        return -1;
    }

    public void AssignSlot(int quickSlotIndex, int inventorySlotIndex)
    {
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (inventory == null) return;
        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventory.SlotCount) return;

        quickSlots[quickSlotIndex] = inventorySlotIndex;
        RefreshSlot(quickSlotIndex);
    }

    public void RefreshAllSlots()
    {
        if (persistenceRestoring) return;
        if (dataBinding.Root == null) return;
        for (int i = 0; i < quickSlots.Length; i++)
            RefreshSlot(i);
    }

    public void RefreshSlot(int quickSlotIndex)
    {
        if (dataBinding.Root == null) return;
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (slotUIs == null || quickSlotIndex >= slotUIs.Length) return;

        var qsUI = slotUIs[quickSlotIndex];
        if (qsUI == null) return;
        if (inventory == null) { qsUI.SetEmpty(); return; }

        int invIndex = quickSlots[quickSlotIndex];

        if (invIndex < 0 || invIndex >= inventory.SlotCount)
        {
            qsUI.SetEmpty();
            quickSlots[quickSlotIndex] = -1;
            return;
        }

        var slot = inventory.GetSlot(invIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
        {
            qsUI.SetEmpty();
            quickSlots[quickSlotIndex] = -1;
            return;
        }

        qsUI.SetItem(slot.item, slot.count);
    }

    public void UseSlot(int quickSlotIndex)
    {
        if (!CanReadGameplayInput()) return;
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (inventory == null) return;

        int invIndex = quickSlots[quickSlotIndex];
        if (invIndex < 0 || invIndex >= inventory.SlotCount) return;

        var slot = inventory.GetSlot(invIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        if (playerStatus == null) return;
        ItemData item = slot.item;
        inventory.TryUseAt(invIndex, item, () => playerStatus.ApplyItem(item, out _));
    }

    private void Update()
    {
        RefreshDataBinding();
        if (!localInputGate.CanRead(playerContext) || !CanReadGameplayInput()) return;
        var kb = Keyboard.current;
        if (kb == null || !CanReadGameplayInput() || lastInputFrame == Time.frameCount) return;
        lastInputFrame = Time.frameCount;

        if (kb.digit1Key.wasPressedThisFrame) UseSlot(0);
        if (kb.digit2Key.wasPressedThisFrame) UseSlot(1);
        if (kb.digit3Key.wasPressedThisFrame) UseSlot(2);
        if (kb.digit4Key.wasPressedThisFrame) UseSlot(3);
        if (kb.digit5Key.wasPressedThisFrame) UseSlot(4);
    }

    private bool IsValidQuickIndex(int i) => i >= 0 && i < quickSlots.Length;

    private bool HasPlayerReferences()
    {
        return isActiveAndEnabled && playerContext != null && inventory != null && playerStatus != null
            && PlayerModalControlContext.For(inventory.gameObject) == playerContext
            && PlayerModalControlContext.For(playerStatus.gameObject) == playerContext;
    }

    private bool CanReadGameplayInput()
    {
        return HasPlayerReferences() && !playerContext.IsGameplayInputBlocked;
    }

    public static bool OwnsNumberKeys(GameObject player)
    {
        var context = PlayerModalControlContext.For(player);
        if (context == null) return false;
        foreach (var slots in context.GetComponentsInChildren<QuickSlotManager>(true))
            if (slots.playerContext == context && slots.HasPlayerReferences()) return true;
        return false;
    }
}
