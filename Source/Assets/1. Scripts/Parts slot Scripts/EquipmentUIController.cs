using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentUIController : MonoBehaviour
{
    public enum EquipSlot { LeftArm, RightArm, LeftLeg, RightLeg }

    [Header("Tab Buttons")]
    [SerializeField] private Button tabArmsButton;
    [SerializeField] private Button tabLegsButton;

    [Header("Inventory Slots")]
    [SerializeField] private InventoryItemButton[] inventorySlots;

    [Header("Equipment Item Pool")]
    [SerializeField] private List<EquipmentItemData> allArmItems = new List<EquipmentItemData>();
    [SerializeField] private List<EquipmentItemData> allLegItems = new List<EquipmentItemData>();
    [SerializeField] private bool autoLoadEquipmentItems = true;

    [Header("Crafted Inventory Source")]
    [SerializeField] private PlayerInventory sourceInventory;
    [SerializeField] private bool onlyShowCraftedPartsInInventory = true;

    [Header("Equipped Icons")]
    [SerializeField] private Image equippedLeftArmIcon;
    [SerializeField] private Image equippedRightArmIcon;
    [SerializeField] private Image equippedLeftLegIcon;
    [SerializeField] private Image equippedRightLegIcon;

    [Header("Info Panel")]
    [SerializeField] private TMP_Text textPartName;
    [SerializeField] private TMP_Text textType;
    [SerializeField] private TMP_Text textAttack;
    [SerializeField] private TMP_Text textDefense;
    [SerializeField] private TMP_Text textSpeed;
    [SerializeField] private TMP_Text textDescription;

    public System.Action<EquipSlot, EquipmentItemData> OnEquipmentChanged;

    private EquipmentPartType currentTab = EquipmentPartType.Arm;
    private EquipmentItemData selectedItem;
    private InventoryItemButton selectedSlotButton;
    private EquipmentItemData eqLeftArm, eqRightArm, eqLeftLeg, eqRightLeg;
    private EquipmentManager equipmentManager;
    private Transform boundPlayerRoot;
    private Transform stateOwner;
    private bool persistenceRestoring;
    private bool equipmentTradeInProgress;
    internal bool IsPersistenceOwner(Transform root) => stateOwner == root && LocalPlayerBindingResolver.PlayerRoot(this) == root;
    internal EquipmentItemData[] CopyPersistentSlots() => new[] { eqLeftArm, eqRightArm, eqLeftLeg, eqRightLeg };
    internal void BeginPersistenceRestore() { persistenceRestoring = true; }
    internal void RestorePersistentSlots(EquipmentItemData[] slots)
    {
        eqLeftArm = slots[0]; eqRightArm = slots[1]; eqLeftLeg = slots[2]; eqRightLeg = slots[3];
        craftedRuntimeItems.Clear();
        ClearSelection();
        // No equip event replay: preserve captured external/leg effective state, not slot iteration order.
    }
    internal void FinishPersistenceRestore()
    {
        persistenceRestoring = false;
        RestoreCraftedRuntimeItems();
        HandleSourceInventoryChanged();
        RefreshEquippedIcons();
    }
    public Transform BoundPlayerRoot => HasValidOwner() ? boundPlayerRoot : null;
    private readonly List<EquipmentItemData> remainingArms = new List<EquipmentItemData>();
    private readonly List<EquipmentItemData> remainingLegs = new List<EquipmentItemData>();
    private readonly List<EquipmentItemData> craftedRuntimeItems = new List<EquipmentItemData>();

    private void Awake()
    {
        var authoredInventory = sourceInventory;
        sourceInventory = null;
        if (authoredInventory != null) SetSourceInventory(authoredInventory);
        else RebindSourceInventory();

        if (tabArmsButton != null)
            tabArmsButton.onClick.AddListener(() => SetTab(EquipmentPartType.Arm));

        if (tabLegsButton != null)
            tabLegsButton.onClick.AddListener(() => SetTab(EquipmentPartType.Leg));

        if (inventorySlots != null)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                inventorySlots[i].Setup(this);
            }
        }

        EnsureEquipmentPools();
        ResetRemainingLists();
        SetTab(EquipmentPartType.Arm);
        ShowEmptyInfo();
        RefreshEquippedIcons();
    }

    private void OnEnable()
    {
        RebindSourceInventory();
        EnsureEquipmentPools();
        RestoreCraftedRuntimeItems();
        SelectBestTabForCurrentInventory();
        ResetRemainingLists();
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
        RefreshEquippedIcons();
    }

    private void OnDestroy()
    {
        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged -= HandleSourceInventoryChanged;
    }

    private void OnDisable() { SetSourceInventory(null); }

    private void LateUpdate()
    {
        // Parent/NGO identity validation only; no global scan every frame.
        if (sourceInventory != null && !HasValidOwner()) SetSourceInventory(null);
    }

    private bool HasValidOwner()
    {
        return sourceInventory != null && boundPlayerRoot != null
            && LocalPlayerBindingResolver.PlayerRoot(sourceInventory) == boundPlayerRoot
            && LocalPlayerBindingResolver.TryResolve(boundPlayerRoot, out var local, false) && local == boundPlayerRoot
            && (LocalPlayerBindingResolver.PlayerRoot(this) == null || LocalPlayerBindingResolver.PlayerRoot(this) == boundPlayerRoot);
    }

    public void SetSourceInventory(PlayerInventory inventory)
    {
        Transform next = null;
        if (inventory != null)
        {
            var parent = LocalPlayerBindingResolver.PlayerRoot(this);
            if (!LocalPlayerBindingResolver.TryResolve(inventory, out next, false)
                || LocalPlayerBindingResolver.PlayerRoot(inventory) != next || (parent != null && parent != next))
                inventory = null;
        }
        if (ReferenceEquals(sourceInventory, inventory))
            return;

        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged -= HandleSourceInventoryChanged;

        sourceInventory = inventory;
        boundPlayerRoot = inventory != null ? next : null;
        equipmentManager = LocalPlayerBindingResolver.Owned<EquipmentManager>(boundPlayerRoot);
        if (boundPlayerRoot != null && stateOwner != boundPlayerRoot)
        {
            stateOwner = boundPlayerRoot;
            craftedRuntimeItems.Clear();
            eqLeftArm = eqRightArm = eqLeftLeg = eqRightLeg = null;
            ClearSelection();
        }

        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged += HandleSourceInventoryChanged;

        HandleSourceInventoryChanged();
    }

    public void SetEquipmentPools(List<EquipmentItemData> armItems, List<EquipmentItemData> legItems)
    {
        allArmItems = armItems ?? new List<EquipmentItemData>();
        allLegItems = legItems ?? new List<EquipmentItemData>();
        HandleSourceInventoryChanged();
    }

    public void RefreshFromInventory()
    {
        RebindSourceInventory();

        EnsureEquipmentPools();
        RestoreCraftedRuntimeItems();
        SelectBestTabForCurrentInventory();
        HandleSourceInventoryChanged();
    }

    public void FocusCraftedPart(PartData craftedPart)
    {
        if (craftedPart != null)
            currentTab = ToEquipmentPartType(craftedPart.slotType);

        RefreshFromInventory();
    }

    public void AddCraftedPartToSlots(PartData craftedPart)
    {
        if (craftedPart == null || !HasValidOwner())
            return;

        EnsureEquipmentPools();

        EquipmentItemData equipmentItem = FindEquipmentItemForPart(craftedPart);
        if (equipmentItem == null)
        {
            Debug.LogWarning("[EquipmentUIController] Crafted part has no matching EquipmentItemData: " + craftedPart.name, this);
            return;
        }

        if (!craftedRuntimeItems.Contains(equipmentItem))
            craftedRuntimeItems.Add(equipmentItem);

        PartSystemRuntimeState.RegisterCraftedEquipmentPart(craftedPart, boundPlayerRoot);

        currentTab = equipmentItem.partType;
        ResetRemainingLists();
        AddCraftedRuntimeToRemaining(equipmentItem);
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
        RefreshEquippedIcons();
        SelectItem(equipmentItem, FindSlotButtonForItem(equipmentItem));
    }

    private void EnsureEquipmentPools()
    {
        if (!autoLoadEquipmentItems)
            return;

        EquipmentItemData[] loadedItems = Resources.LoadAll<EquipmentItemData>("AETHERParts");
        if (loadedItems == null)
            return;

        for (int i = 0; i < loadedItems.Length; i++)
        {
            EquipmentItemData item = loadedItems[i];
            if (item == null)
                continue;

            List<EquipmentItemData> targetList = item.partType == EquipmentPartType.Leg ? allLegItems : allArmItems;
            if (!ContainsEquipmentItem(targetList, item))
                targetList.Add(item);
        }
    }

    private void RestoreCraftedRuntimeItems()
    {
        if (!HasValidOwner()) return;
        IReadOnlyList<PartData> craftedParts = PartSystemRuntimeState.GetCraftedEquipmentParts(boundPlayerRoot);
        if (craftedParts == null)
            return;

        for (int i = 0; i < craftedParts.Count; i++)
        {
            EquipmentItemData equipmentItem = FindEquipmentItemForPart(craftedParts[i]);
            if (equipmentItem == null)
                continue;

            if (!craftedRuntimeItems.Contains(equipmentItem))
                craftedRuntimeItems.Add(equipmentItem);
        }
    }

    private static bool ContainsEquipmentItem(List<EquipmentItemData> list, EquipmentItemData item)
    {
        if (list == null || item == null)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            EquipmentItemData existing = list[i];
            if (existing == item)
                return true;

            if (existing != null
                && existing.sourceCraftedPart != null
                && item.sourceCraftedPart != null
                && existing.sourceCraftedPart == item.sourceCraftedPart)
                return true;
        }

        return false;
    }

    private static EquipmentPartType ToEquipmentPartType(PartSlotType slotType)
    {
        return slotType == PartSlotType.Leg ? EquipmentPartType.Leg : EquipmentPartType.Arm;
    }

    private void RebindSourceInventory()
    {
        // No global/unique fallback during detached template Awake/OnEnable.
        var owner = LocalPlayerBindingResolver.PlayerRoot(this);
        if (owner == null) owner = boundPlayerRoot;
        if (owner != null && LocalPlayerBindingResolver.TryResolve(owner, out var root, false) && root == owner)
            SetSourceInventory(LocalPlayerBindingResolver.Owned<PlayerInventory>(root));
        else SetSourceInventory(null);
    }

    private void SelectBestTabForCurrentInventory()
    {
        if (HasAvailableInventoryItem(EquipmentPartType.Leg))
        {
            currentTab = EquipmentPartType.Leg;
            return;
        }

        if (HasAvailableInventoryItem(EquipmentPartType.Arm))
            currentTab = EquipmentPartType.Arm;
    }

    private bool HasAvailableInventoryItem(EquipmentPartType partType)
    {
        List<EquipmentItemData> source = partType == EquipmentPartType.Leg ? allLegItems : allArmItems;
        if (source == null)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            if (CanShowEquipmentItem(source[i]))
                return true;
        }

        return false;
    }

    private void ResetRemainingLists()
    {
        remainingArms.Clear();
        remainingLegs.Clear();

        for (int i = 0; i < allArmItems.Count; i++)
        {
            if (CanShowEquipmentItem(allArmItems[i]))
                remainingArms.Add(allArmItems[i]);
        }

        for (int i = 0; i < allLegItems.Count; i++)
        {
            if (CanShowEquipmentItem(allLegItems[i]))
                remainingLegs.Add(allLegItems[i]);
        }
    }

    private bool CanShowEquipmentItem(EquipmentItemData item)
    {
        if (!HasValidOwner()) return false;
        if (item == null)
            return false;

        if (!onlyShowCraftedPartsInInventory)
            return true;

        if (craftedRuntimeItems.Contains(item))
            return true;

        if (item.sourceCraftedPart == null)
            return false;

        string id = ItemNameRuleUtility.GetBestItemId(item.sourceCraftedPart);
        string displayName = item.sourceCraftedPart.displayName;
        string bestName = ItemNameRuleUtility.GetBestItemName(null, item.sourceCraftedPart);

        bool isMaterial = ItemNameRuleUtility.IsModuleItem(id)
            || ItemNameRuleUtility.IsModuleItem(displayName)
            || ItemNameRuleUtility.IsModuleItem(bestName);

        bool isPaper = ItemNameRuleUtility.IsPaperObject(id)
            || ItemNameRuleUtility.IsPaperObject(displayName)
            || ItemNameRuleUtility.IsPaperObject(bestName);

        if (isMaterial || isPaper)
            return false;

        return sourceInventory != null && sourceInventory.HasItem(item.sourceCraftedPart, 1);
    }

    private void HandleSourceInventoryChanged()
    {
        if (persistenceRestoring) return;
        ResetRemainingLists();
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
    }

    private void RemoveEquippedFromRemaining()
    {
        RemoveFromRemaining(eqLeftArm);
        RemoveFromRemaining(eqRightArm);
        RemoveFromRemaining(eqLeftLeg);
        RemoveFromRemaining(eqRightLeg);
    }

    private void SetTab(EquipmentPartType tab)
    {
        currentTab = tab;
        ClearSelection();
        ShowEmptyInfo();
        RebuildInventoryUIForCurrentTab();
    }

    private void RebuildInventoryUIForCurrentTab()
    {
        if (inventorySlots == null)
            return;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
                continue;

            inventorySlots[i].item = null;
            inventorySlots[i].RefreshIcon();
            inventorySlots[i].RefreshInteractable();
        }

        List<EquipmentItemData> source = currentTab == EquipmentPartType.Arm ? remainingArms : remainingLegs;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
                continue;

            if (i < source.Count)
            {
                inventorySlots[i].item = source[i];
                inventorySlots[i].RefreshIcon();
                inventorySlots[i].RefreshInteractable();
            }
        }
    }

    public void SelectItem(EquipmentItemData item, InventoryItemButton slotButton)
    {
        if (item == null)
            return;

        selectedItem = item;
        selectedSlotButton = slotButton;
        ShowItemInfo(item);
    }

    private void ClearSelection()
    {
        selectedItem = null;
        selectedSlotButton = null;
    }

    public EquipmentItemData GetEquippedItem(EquipSlot slot)
    {
        if (!HasValidOwner()) return null;
        switch (slot)
        {
            case EquipSlot.LeftArm: return eqLeftArm;
            case EquipSlot.RightArm: return eqRightArm;
            case EquipSlot.LeftLeg: return eqLeftLeg;
            case EquipSlot.RightLeg: return eqRightLeg;
            default: return null;
        }
    }

    public void TryEquipFromDrag(EquipSlot slot, InventoryItemButton sourceInventorySlot)
    {
        if (equipmentTradeInProgress || !HasValidOwner() || !System.Enum.IsDefined(typeof(EquipSlot), slot)) return;
        if (sourceInventorySlot == null || sourceInventorySlot.item == null)
            return;

        EquipmentItemData draggedItem = sourceInventorySlot.item;
        bool isArmSlot = slot == EquipSlot.LeftArm || slot == EquipSlot.RightArm;
        bool isLegSlot = slot == EquipSlot.LeftLeg || slot == EquipSlot.RightLeg;

        if (isArmSlot && draggedItem.partType != EquipmentPartType.Arm)
            return;

        if (isLegSlot && draggedItem.partType != EquipmentPartType.Leg)
            return;

        EquipmentItemData previous = GetEquippedItem(slot);
        if (previous == draggedItem) return;
        equipmentTradeInProgress = true;
        try
        {
            if (!TryExchangeCraftedParts(draggedItem, previous, out bool inventoryChanged)) return;
            craftedRuntimeItems.Remove(draggedItem);
            if (previous != null && !craftedRuntimeItems.Contains(previous)) craftedRuntimeItems.Add(previous);
            EquipInternal(slot, draggedItem);
            RemoveFromRemaining(draggedItem);
            sourceInventorySlot.Consume();

            if (selectedSlotButton == sourceInventorySlot)
            {
                ClearSelection();
                ShowEmptyInfo();
            }
            if (inventoryChanged) sourceInventory.NotifyInventoryChanged();
            RebuildInventoryUIForCurrentTab();
        }
        finally { equipmentTradeInProgress = false; }
    }

    public void TryMoveEquippedToInventory(EquipSlot fromEquipSlot, InventoryItemButton targetInventorySlot)
    {
        if (equipmentTradeInProgress || !HasValidOwner()) return;
        if (targetInventorySlot == null || targetInventorySlot.item != null)
            return;

        EquipmentItemData item = GetEquippedItem(fromEquipSlot);
        if (item == null)
            return;

        TryReturnEquipped(fromEquipSlot, item);
    }

    public void TryMoveEquippedToFirstEmpty(EquipSlot fromEquipSlot)
    {
        if (equipmentTradeInProgress || !HasValidOwner()) return;
        EquipmentItemData item = GetEquippedItem(fromEquipSlot);
        if (item == null)
            return;

        TryReturnEquipped(fromEquipSlot, item);
    }

    private void EquipInternal(EquipSlot slot, EquipmentItemData item)
    {
        EquipmentItemData previous = GetEquippedItem(slot);
        if (previous != null && previous != item)
        {
            AddToRemaining(previous);
        }

        switch (slot)
        {
            case EquipSlot.LeftArm: eqLeftArm = item; break;
            case EquipSlot.RightArm: eqRightArm = item; break;
            case EquipSlot.LeftLeg: eqLeftLeg = item; break;
            case EquipSlot.RightLeg: eqRightLeg = item; break;
        }

        NotifyEquipmentChanged(slot, item);
        SyncEquipmentManager(slot, item);
        RefreshEquippedIcons();
    }

    private void ClearEquipped(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.LeftArm: eqLeftArm = null; break;
            case EquipSlot.RightArm: eqRightArm = null; break;
            case EquipSlot.LeftLeg: eqLeftLeg = null; break;
            case EquipSlot.RightLeg: eqRightLeg = null; break;
        }

        NotifyEquipmentChanged(slot, null);
        SyncEquipmentManager(slot, null);
    }

    private void SyncEquipmentManager(EquipSlot slot, EquipmentItemData item)
    {
        if (!HasValidOwner()) return;
        equipmentManager = LocalPlayerBindingResolver.Owned<EquipmentManager>(boundPlayerRoot);
        if (equipmentManager == null) return;
        PartSlotType partSlot = ConvertToPartSlot(slot);
        EquipmentItemData effective = ResolveEffectiveGroupItem(slot, item);
        PartType partType = effective != null ? effective.abilityType : PartType.None;
        PartSystemRuntimeState.BindEquipment(equipmentManager);
        if (equipmentManager.GetExternalEquipped(partSlot) != partType)
            equipmentManager.SetExternalEquipped(partSlot, partType);
        var hankyulMove = boundPlayerRoot.GetComponent<Move_Hankyul>();
        if (hankyulMove != null)
        {
            var ability = boundPlayerRoot.GetComponent<HangyeolPartAbilityController>();
            if (ability != null) ability.RefreshSuperJumpState();
            if (partSlot == PartSlotType.Leg)
                hankyulMove.SetSuperJumpEnabled(partType == PartType.SuperJump || equipmentManager.HasPart(PartType.SuperJump));
        }
    }

    // Keep the existing last-equipped policy. Clearing one side must not erase
    // the same group's still-equipped opposite side. Combining different
    // simultaneous abilities is a separate policy, not a slot-order choice here.
    public EquipmentItemData ResolveEffectiveGroupItem(EquipSlot changedSlot, EquipmentItemData changedItem)
    {
        if (!HasValidOwner()) return null;
        if (changedItem != null) return changedItem;
        switch (changedSlot)
        {
            case EquipSlot.LeftLeg: return GetEquippedItem(EquipSlot.RightLeg);
            case EquipSlot.RightLeg: return GetEquippedItem(EquipSlot.LeftLeg);
            case EquipSlot.LeftArm: return GetEquippedItem(EquipSlot.RightArm);
            case EquipSlot.RightArm: return GetEquippedItem(EquipSlot.LeftArm);
            default: return null;
        }
    }

    private static PartSlotType ConvertToPartSlot(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.LeftLeg:
            case EquipSlot.RightLeg:
                return PartSlotType.Leg;
            case EquipSlot.LeftArm:
            case EquipSlot.RightArm:
                return PartSlotType.Arm;
            default:
                return PartSlotType.Utility;
        }
    }

    private void RemoveFromRemaining(EquipmentItemData item)
    {
        if (item == null)
            return;

        if (item.partType == EquipmentPartType.Arm)
            remainingArms.Remove(item);
        else if (item.partType == EquipmentPartType.Leg)
            remainingLegs.Remove(item);
    }

    private void AddToRemaining(EquipmentItemData item)
    {
        if (item == null || !CanShowEquipmentItem(item))
            return;

        if (item.partType == EquipmentPartType.Arm && !remainingArms.Contains(item))
            remainingArms.Add(item);
        else if (item.partType == EquipmentPartType.Leg && !remainingLegs.Contains(item))
            remainingLegs.Add(item);
    }

    private void AddCraftedRuntimeToRemaining(EquipmentItemData item)
    {
        if (item == null)
            return;

        if (item.partType == EquipmentPartType.Arm && !remainingArms.Contains(item))
            remainingArms.Add(item);
        else if (item.partType == EquipmentPartType.Leg && !remainingLegs.Contains(item))
            remainingLegs.Add(item);
    }

    private bool TryExchangeCraftedParts(EquipmentItemData incoming, EquipmentItemData outgoing, out bool inventoryChanged)
    {
        var consumes = new List<PlayerInventory.ItemQuantity>();
        var gives = new List<PlayerInventory.ItemQuantity>();
        if (incoming != null && incoming.sourceCraftedPart != null)
            consumes.Add(new PlayerInventory.ItemQuantity(incoming.sourceCraftedPart, 1));
        if (outgoing != null && outgoing.sourceCraftedPart != null)
            gives.Add(new PlayerInventory.ItemQuantity(outgoing.sourceCraftedPart, 1));
        inventoryChanged = consumes.Count != 0 || gives.Count != 0;
        return !inventoryChanged || sourceInventory != null && sourceInventory.TryExchangeItems(consumes, gives, false);
    }

    private void TryReturnEquipped(EquipSlot slot, EquipmentItemData item)
    {
        equipmentTradeInProgress = true;
        try
        {
            if (!TryExchangeCraftedParts(null, item, out bool inventoryChanged)) return;
            if (!craftedRuntimeItems.Contains(item)) craftedRuntimeItems.Add(item);
            ClearEquipped(slot);
            AddToRemaining(item);
            if (inventoryChanged) sourceInventory.NotifyInventoryChanged();
            RefreshEquippedIcons();
            RebuildInventoryUIForCurrentTab();
        }
        finally { equipmentTradeInProgress = false; }
    }

    private void NotifyEquipmentChanged(EquipSlot slot, EquipmentItemData item)
    {
        var handlers = OnEquipmentChanged;
        if (handlers == null) return;
        foreach (System.Action<EquipSlot, EquipmentItemData> handler in handlers.GetInvocationList())
        {
            try { handler(slot, item); }
            catch (System.Exception error) { Debug.LogException(error, this); }
        }
    }

    private EquipmentItemData FindEquipmentItemForPart(PartData craftedPart)
    {
        if (craftedPart == null)
            return null;

        EquipmentItemData found = FindEquipmentItemForPart(allArmItems, craftedPart);
        if (found != null)
            return found;

        return FindEquipmentItemForPart(allLegItems, craftedPart);
    }

    private static EquipmentItemData FindEquipmentItemForPart(List<EquipmentItemData> source, PartData craftedPart)
    {
        if (source == null || craftedPart == null)
            return null;

        for (int i = 0; i < source.Count; i++)
        {
            EquipmentItemData item = source[i];
            if (item != null && item.sourceCraftedPart == craftedPart)
                return item;
        }

        return null;
    }

    private InventoryItemButton FindSlotButtonForItem(EquipmentItemData item)
    {
        if (inventorySlots == null || item == null)
            return null;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] != null && inventorySlots[i].item == item)
                return inventorySlots[i];
        }

        return null;
    }

    private void RefreshEquippedIcons()
    {
        ApplyEquippedIcon(equippedLeftArmIcon, eqLeftArm);
        ApplyEquippedIcon(equippedRightArmIcon, eqRightArm);
        ApplyEquippedIcon(equippedLeftLegIcon, eqLeftLeg);
        ApplyEquippedIcon(equippedRightLegIcon, eqRightLeg);
    }

    private void ApplyEquippedIcon(Image target, EquipmentItemData item)
    {
        if (target == null)
            return;

        Sprite icon = GetDisplayIcon(item);
        if (item != null && icon != null)
        {
            target.enabled = true;
            target.sprite = icon;
            target.preserveAspect = true;
        }
        else
        {
            target.enabled = false;
            target.sprite = null;
        }
    }

    private static Sprite GetDisplayIcon(EquipmentItemData item)
    {
        if (item == null)
            return null;

        if (item.icon != null)
            return item.icon;

        return item.sourceCraftedPart != null ? item.sourceCraftedPart.itemSprite : null;
    }

    private void ShowEmptyInfo()
    {
        if (textPartName != null) textPartName.text = "-";
        if (textType != null) textType.text = "-";
        if (textAttack != null) textAttack.text = "+0";
        if (textDefense != null) textDefense.text = "+0";
        if (textSpeed != null) textSpeed.text = "+0";
        if (textDescription != null) textDescription.text = "Select an item to view details";
    }

    private void ShowItemInfo(EquipmentItemData item)
    {
        if (item == null)
        {
            ShowEmptyInfo();
            return;
        }

        if (textPartName != null) textPartName.text = item.itemName;
        if (textType != null) textType.text = item.partType.ToString();
        if (textAttack != null) textAttack.text = item.attack >= 0 ? $"+{item.attack}" : item.attack.ToString();
        if (textDefense != null) textDefense.text = item.defense >= 0 ? $"+{item.defense}" : item.defense.ToString();
        if (textSpeed != null) textSpeed.text = item.speed >= 0 ? $"+{item.speed}" : item.speed.ToString();
        if (textDescription != null) textDescription.text = string.IsNullOrEmpty(item.description) ? "-" : item.description;
    }
}
