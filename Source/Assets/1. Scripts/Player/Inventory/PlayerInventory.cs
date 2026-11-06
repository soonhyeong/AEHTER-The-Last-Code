using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 슬롯 설정")]
    public int slotCount = 30;

    [Serializable]
    public class Slot
    {
        public ItemData item;
        public int count;
    }

    public List<Slot> slots = new List<Slot>();

    public event Action OnInventoryChanged;
    private bool applyingItemEffect;

    private void Awake()
    {
        // 슬롯리스트 크기 정렬
        if (slots.Count < slotCount)
        {
            int add = slotCount - slots.Count;
            for (int i = 0; i < add; i++)
            {
                slots.Add(new Slot());
            }
        }
        else if (slots.Count > slotCount)
        {
            slots.RemoveRange(slotCount, slots.Count - slotCount);
        }

        RaiseChanged();
    }

    public int SlotCount => slots != null ? slots.Count : 0;

    public Slot GetSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public bool AddItem(ItemData data, int amount)
    {
        return TryExchangeItems(null, new[] { new ItemQuantity(data, amount) });
    }


    public bool HasItem(ItemData data, int amount = 1)
    {
        if (data == null || amount <= 0 || slots == null) return false;

        long total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.item == data && slot.count > 0)
            {
                total += slot.count;
                if (total >= amount)
                    return true;
            }
        }

        return false;
    }

    public bool ConsumeItem(ItemData data, int amount = 1)
    {
        return TryExchangeItems(new[] { new ItemQuantity(data, amount) }, null);
    }

    public void NotifyInventoryChanged()
    {
        RaiseChanged();
    }
    // index 슬롯에서 amount만큼 사용/버리기
    public bool ConsumeAt(int index, int amount)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.item == null || slot.count <= 0) return false;
        if (amount <= 0 || amount > slot.count || !TryPrepareGameplaySlots(out Slot[] prepared)) return false;

        slot = prepared[index];
        slot.count -= amount;
        if (slot.count <= 0)
        {
            slot.item = null;
            slot.count = 0;
        }

        slots = new List<Slot>(prepared);
        RaiseChanged();
        return true;
    }

    // Reserve an exact gameplay slot while applying an existing, validated item
    // effect. Re-entrant inventory API writes cannot consume/replace this slot.
    public bool TryUseAt(int index, ItemData expectedItem, Func<bool> applyEffect)
    {
        Slot current = GetSlot(index);
        if (current == null || expectedItem == null || current.item != expectedItem || current.count <= 0
            || applyEffect == null || !TryPrepareGameplaySlots(out Slot[] prepared)) return false;
        prepared[index].count--;
        if (prepared[index].count == 0) prepared[index].item = null;
        applyingItemEffect = true;
        bool applied;
        try { applied = applyEffect(); }
        finally { applyingItemEffect = false; }
        if (!applied) return false;
        slots = new List<Slot>(prepared);
        RaiseChanged();
        return true;
    }

    public readonly struct ItemQuantity
    {
        public ItemData Item { get; }
        public int Amount { get; }
        public ItemQuantity(ItemData item, int amount) { Item = item; Amount = amount; }
    }

    // Gameplay exchange, deliberately separate from persistence replacement APIs.
    // Stage all validation, consumption and output capacity before touching live slots.
    public bool TryExchangeItems(IReadOnlyList<ItemQuantity> consumes, IReadOnlyList<ItemQuantity> gives, bool notify = true)
    {
        if ((consumes == null || consumes.Count == 0) && (gives == null || gives.Count == 0)) return false;
        if (!TryPrepareGameplaySlots(out Slot[] prepared)) return false;
        if (consumes != null)
        {
            foreach (ItemQuantity quantity in consumes)
            {
                if (quantity.Item == null || quantity.Amount <= 0) return false;
                int remaining = quantity.Amount;
                foreach (Slot slot in prepared)
                {
                    if (slot.item != quantity.Item || slot.count <= 0) continue;
                    int used = Mathf.Min(slot.count, remaining);
                    slot.count -= used;
                    remaining -= used;
                    if (slot.count == 0) slot.item = null;
                    if (remaining == 0) break;
                }
                if (remaining != 0) return false;
            }
        }
        if (gives != null)
        {
            foreach (ItemQuantity quantity in gives)
            {
                if (quantity.Item == null || quantity.Amount <= 0) return false;
                Slot target = null;
                foreach (Slot slot in prepared)
                    if (slot.item == quantity.Item && slot.count > 0) { target = slot; break; }
                if (target != null)
                {
                    if ((long)target.count + quantity.Amount > int.MaxValue) return false;
                    target.count += quantity.Amount;
                }
                else
                {
                    foreach (Slot slot in prepared)
                        if (slot.item == null) { target = slot; break; }
                    if (target == null) return false;
                    target.item = quantity.Item;
                    target.count = quantity.Amount;
                }
            }
        }
        slots = new List<Slot>(prepared);
        if (notify) RaiseChanged();
        return true;
    }

    private bool TryPrepareGameplaySlots(out Slot[] prepared)
    {
        prepared = null;
        if (applyingItemEffect || slots == null || slotCount < 0 || slots.Count != slotCount) return false;
        var staged = new Slot[slots.Count];
        for (int i = 0; i < staged.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.count < 0 || (slot.item == null ? slot.count != 0 : slot.count == 0)) return false;
            staged[i] = new Slot { item = slot.item, count = slot.count };
        }
        prepared = staged;
        return true;
    }

    public void DebugLogInventory()
    {
        Debug.Log("=== Inventory Debug ===");

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (slot.item == null || slot.count <= 0)
            {
                Debug.Log($"Slot {i}: (빈 슬롯)");
            }
            else
            {
                Debug.Log($"Slot {i}: {slot.item.displayName} x {slot.count}");
            }
        }

        Debug.Log("========================");
    }

    public void SwapSlots(int indexA, int indexB)
    {
        if (applyingItemEffect || slots == null) return;
        if (indexA < 0 || indexA >= slots.Count) return;
        if (indexB < 0 || indexB >= slots.Count) return;
        if (indexA == indexB) return;

        var temp = slots[indexA];
        slots[indexA] = slots[indexB];
        slots[indexB] = temp;

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        Action handlers = OnInventoryChanged;
        if (handlers == null) return;
        // State is already committed. An observer exception must not imply that
        // a pickup/craft failed and is safe to apply a second time.
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
    }

    // Exact state transfer: no AddItem/stacking and no notification on validation failure.
    public bool TryCopySlotsExact(out Slot[] copy, out string error)
    {
        copy = null;
        if (slots == null) { error = "Missing inventory slots"; return false; }
        return TryValidateExact(slots.ToArray(), out copy, out error);
    }

    private bool TryValidateExact(Slot[] source, out Slot[] copy, out string error)
    {
        copy = null;
        error = null;
        if (slotCount < 0 || source == null || source.Length != slotCount)
        { error = "Inventory slotCount mismatch"; return false; }
        var prepared = new Slot[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var slot = source[i];
            if (slot == null || slot.count < 0 || (slot.item == null ? slot.count != 0 : slot.count == 0))
            { error = "Invalid inventory slot/count"; return false; }
            prepared[i] = new Slot { item = slot.item, count = slot.count };
        }
        copy = prepared;
        return true;
    }

    // notify=false is for the core multi-resource restore transaction; caller notifies once after commit.
    public bool TryReplaceSlotsExact(Slot[] replacement, out string error, bool notify = true)
    {
        if (!TryValidateExact(replacement, out var prepared, out error)) return false;
        slots = new List<Slot>(prepared);
        if (notify) RaiseChanged();
        return true;
    }


}
