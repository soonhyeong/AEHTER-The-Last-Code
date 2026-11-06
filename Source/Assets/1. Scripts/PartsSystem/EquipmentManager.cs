using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    private readonly Dictionary<PartSlotType, PartData> equipped = new Dictionary<PartSlotType, PartData>();
    private readonly Dictionary<PartSlotType, PartType> externalEquippedTypes = new Dictionary<PartSlotType, PartType>();
    private bool tradeInProgress;

    public event Action<PartSlotType, PartData> OnEquipmentChanged;
    public event Action<PartSlotType, PartData> OnPartEquipped;
    public event Action<PartSlotType, PartData> OnPartUnequipped;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
    }

    public void SetInventory(PlayerInventory sourceInventory)
    {
        if (sourceInventory != null)
            inventory = sourceInventory;
    }

    public PartData GetEquipped(PartSlotType slot)
    {
        return equipped.TryGetValue(slot, out PartData part) ? part : null;
    }

    public bool HasPart(PartType type)
    {
        foreach (PartData part in equipped.Values)
        {
            if (part != null && part.partType == type)
                return true;
        }

        foreach (PartType externalType in externalEquippedTypes.Values)
        {
            if (externalType == type)
                return true;
        }

        return false;
    }

    public void SetExternalEquipped(PartSlotType slot, PartType type)
    {
        if (type == PartType.None)
            externalEquippedTypes.Remove(slot);
        else
            externalEquippedTypes[slot] = type;

        OnEquipmentChanged?.Invoke(slot, null);
    }

    public PartType GetExternalEquipped(PartSlotType slot)
    {
        return externalEquippedTypes.TryGetValue(slot, out PartType type) ? type : PartType.None;
    }

    public void RestoreEquippedPart(PartSlotType slot, PartData part)
    {
        if (part == null)
            equipped.Remove(slot);
        else
            equipped[slot] = part;

        OnEquipmentChanged?.Invoke(slot, part);
    }

    internal void RestorePersistentState(PartData[] core, PartType[] external)
    {
        equipped.Clear();
        externalEquippedTypes.Clear();
        for (int i = 0; i < core.Length; i++)
        {
            if (core[i] != null) equipped[(PartSlotType)i] = core[i];
            if (external[i] != PartType.None) externalEquippedTypes[(PartSlotType)i] = external[i];
        }
    }
    internal void NotifyPersistenceRestored() => OnEquipmentChanged?.Invoke(PartSlotType.Leg, null);

    public bool EquipPart(PartData part)
    {
        if (tradeInProgress || part == null || !Enum.IsDefined(typeof(PartSlotType), part.slotType))
            return false;

        PartSlotType slot = part.slotType;

        if (equipped.TryGetValue(slot, out PartData current) && current == part)
            return true;

        tradeInProgress = true;
        try
        {
            PartData previous = GetEquipped(slot);
            var consumes = new[] { new PlayerInventory.ItemQuantity(part, 1) };
            var returns = previous != null ? new[] { new PlayerInventory.ItemQuantity(previous, 1) } : null;
            if (inventory != null && !inventory.TryExchangeItems(consumes, returns, false))
                return false;

            equipped[slot] = part;
            // Inventory observers see the final equipment and returned item together.
            if (inventory != null) inventory.NotifyInventoryChanged();
            if (previous != null && inventory != null) NotifyTrade(OnPartUnequipped, slot, previous);
            NotifyTrade(OnEquipmentChanged, slot, part);
            NotifyTrade(OnPartEquipped, slot, part);
            return true;
        }
        finally { tradeInProgress = false; }
    }

    public void Unequip(PartSlotType slot)
    {
        TryUnequip(slot);
    }

    public bool TryUnequip(PartSlotType slot)
    {
        if (tradeInProgress || !equipped.TryGetValue(slot, out PartData part) || part == null) return false;
        tradeInProgress = true;
        try
        {
            if (inventory != null && !inventory.TryExchangeItems(null,
                new[] { new PlayerInventory.ItemQuantity(part, 1) }, false)) return false;
            equipped.Remove(slot);
            if (inventory != null) inventory.NotifyInventoryChanged();
            NotifyTrade(OnEquipmentChanged, slot, null);
            NotifyTrade(OnPartUnequipped, slot, part);
            return true;
        }
        finally { tradeInProgress = false; }
    }

    private void NotifyTrade(Action<PartSlotType, PartData> handlers, PartSlotType slot, PartData part)
    {
        if (handlers == null) return;
        foreach (Action<PartSlotType, PartData> handler in handlers.GetInvocationList())
        {
            try { handler(slot, part); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}
