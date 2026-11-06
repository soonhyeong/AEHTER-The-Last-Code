using System;
using System.Collections.Generic;
using UnityEngine;

// Two-phase local-root hydration. Not a binding resolver, transaction/trade, or replication layer.
internal sealed class PlayerStatePartsTransfer
{
    private Transform root;
    private QuickSlotManager quick;
    private EquipmentManager equipment;
    private EquipmentUIController ui;
    private YunseoEquipmentRuntimeBridge leg;
    private HangyeolPartAbilityController hPower;
    private YunseoPartAbilityController yPower;
    private PlayerQuickSlotSnapshot quickData;
    private PlayerPartsSnapshot parts;
    private PartData[] crafted, core;
    private PartType[] external;
    private EquipmentItemData[] four;
    private EquipmentItemData legItem;

    private static T Unique<T>(Transform owner, ref string error) where T : Component
    {
        T result = null;
        foreach (var candidate in owner.GetComponentsInChildren<T>(true))
        {
            if (LocalPlayerBindingResolver.PlayerRoot(candidate) != owner) continue;
            if (result != null) { error = "Ambiguous persistence component: " + typeof(T).Name; return null; }
            result = candidate;
        }
        return result;
    }
    private static PlayerStatePartsTransfer Context(Transform root, out string error)
    {
        error = null;
        var p = new PlayerStatePartsTransfer { root = root };
        p.quick = Unique<QuickSlotManager>(root, ref error);
        p.equipment = Unique<EquipmentManager>(root, ref error);
        p.ui = Unique<EquipmentUIController>(root, ref error);
        p.leg = Unique<YunseoEquipmentRuntimeBridge>(root, ref error);
        p.hPower = Unique<HangyeolPartAbilityController>(root, ref error);
        p.yPower = Unique<YunseoPartAbilityController>(root, ref error);
        return p;
    }
    internal static bool IsReady(Transform root, PlayerStateSnapshot snapshot)
    {
        var p = Context(root, out string error);
        if (error != null) return true; // Let validation reject ambiguity, do not wait forever.
        if (snapshot.QuickSlot != null && p.quick == null) return false;
        var s = snapshot.Parts;
        if (s == null) return true;
        return (!s.HasEquipment || p.equipment != null)
            && (!s.HasFourSlots || p.ui != null && p.ui.IsPersistenceOwner(root))
            && (!s.HasLeg || p.leg != null)
            && (!s.HasPower || (snapshot.Key.Character == PlayerStateCharacter.Hankyul ? p.hPower != null : p.yPower != null));
    }

    internal static bool TryCapture(Transform root, PlayerStateItemRegistry registry, PlayerStateSnapshot snapshot, out string error)
    {
        var p = Context(root, out error);
        if (error != null) return false;
        var definitions = new Dictionary<string, ItemDefinitionSnapshot>(StringComparer.Ordinal);
        foreach (var d in snapshot.Definitions) definitions.Add(d.Key, d);
        bool PartKey(PartData part, out string key)
        {
            key = null;
            if (part == null) return true;
            if (!registry.TryDescribe(part, out var d, out _)) return false;
            key = d.Key;
            definitions[key] = d;
            return true;
        }
        if (p.quick != null)
        {
            snapshot.Presence |= PlayerStatePresence.QuickSlot;
            snapshot.QuickSlot = new PlayerQuickSlotSnapshot { SlotCount = p.quick.slotCount, Slots = p.quick.CopyPersistentSlots() };
        }
        var made = PartSystemRuntimeState.CopyCraftedForPersistence(root);
        if (p.equipment != null || p.ui != null || p.leg != null || p.hPower != null || p.yPower != null || made.Length != 0)
        {
            var s = new PlayerPartsSnapshot { HasEquipment = p.equipment != null, HasFourSlots = p.ui != null,
                HasLeg = p.leg != null, HasPower = p.hPower != null || p.yPower != null, Crafted = new string[made.Length] };
            snapshot.Presence |= PlayerStatePresence.Parts;
            snapshot.Parts = s;
            for (int i = 0; i < made.Length; i++)
                if (made[i] == null || !PartKey(made[i], out s.Crafted[i])) { error = "Unknown crafted Part"; return false; }
            if (s.HasEquipment)
            {
                s.Core = new string[4]; s.External = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!PartKey(p.equipment.GetEquipped((PartSlotType)i), out s.Core[i])) { error = "Unknown core Part"; return false; }
                    s.External[i] = (int)p.equipment.GetExternalEquipped((PartSlotType)i);
                }
            }
            if (s.HasFourSlots)
            {
                if (!p.ui.IsPersistenceOwner(root)) { error = "Equipment UI has no matching cached owner"; return false; }
                var slots = p.ui.CopyPersistentSlots(); s.FourSlots = new string[4];
                for (int i = 0; i < 4; i++)
                    if (!registry.TryRegisterEquipment(slots[i], out s.FourSlots[i])) { error = "Unknown/ambiguous equipment identity"; return false; }
            }
            if (s.HasLeg && !registry.TryRegisterEquipment(p.leg.GetCurrentLegItem(), out s.CurrentLeg))
            { error = "Unknown effective leg identity"; return false; }
            if (s.HasPower) s.Power = p.hPower != null ? p.hPower.PersistentPower : p.yPower.PersistentPower;
        }
        snapshot.Definitions = new List<ItemDefinitionSnapshot>(definitions.Values).ToArray();
        var resolved = new Dictionary<string, ItemData>(StringComparer.Ordinal);
        foreach (var d in snapshot.Definitions)
        {
            if (!registry.TryResolve(d, out var item, out error)) return false;
            resolved.Add(d.Key, item);
        }
        // Same validation on capture. Failure invalidates the previous revision in the bridge.
        return TryPrepare(root, registry, snapshot, resolved, new HashSet<string>(), out _, out error);
    }

    internal static bool TryPrepare(Transform root, PlayerStateItemRegistry registry, PlayerStateSnapshot snapshot,
        Dictionary<string, ItemData> resolved, HashSet<string> used, out PlayerStatePartsTransfer prepared, out string error)
    {
        prepared = null;
        var p = Context(root, out error);
        if (error != null) return false;
        p.quickData = snapshot.QuickSlot; p.parts = snapshot.Parts;
        if (((snapshot.Presence & PlayerStatePresence.QuickSlot) != 0) != (p.quickData != null)
            || ((snapshot.Presence & PlayerStatePresence.Parts) != 0) != (p.parts != null))
        { error = "Extended presence mismatch"; return false; }
        if (p.quickData != null)
        {
            if (p.quick == null || p.quickData.Slots == null || p.quickData.SlotCount != p.quick.slotCount
                || p.quickData.Slots.Length != p.quick.slotCount)
            { error = "QuickSlot shape/component mismatch"; return false; }
            foreach (int index in p.quickData.Slots)
                if (index < -1 || index >= snapshot.SlotCount || index >= 0 && (snapshot.Slots[index].ItemKey == null || snapshot.Slots[index].Count <= 0))
                { error = "Invalid or empty Inventory QuickSlot index"; return false; }
        }
        if (p.parts == null) { prepared = p; return true; }
        var s = p.parts;
        bool y = snapshot.Key.Character == PlayerStateCharacter.Yunseo;
        if (s.Crafted == null || s.HasEquipment && p.equipment == null
            || s.HasFourSlots && (p.ui == null || !p.ui.IsPersistenceOwner(root))
            || s.HasLeg && (!y || p.leg == null || root.GetComponent<Move_Yunseo>() == null)
            || s.HasPower && (y ? p.yPower == null : p.hPower == null)
            || p.hPower != null && p.yPower != null
            || !s.HasEquipment && (s.Core != null || s.External != null)
            || !s.HasFourSlots && s.FourSlots != null || !s.HasLeg && s.CurrentLeg != null)
        { error = "Parts presence/owner/component mismatch"; return false; }
        PartData ResolvePart(string key)
        {
            if (key == null) return null;
            if (!resolved.TryGetValue(key, out var item) || !(item is PartData part)) return null;
            used.Add(key); return part;
        }
        p.crafted = new PartData[s.Crafted.Length]; var distinct = new HashSet<string>();
        for (int i = 0; i < s.Crafted.Length; i++)
        {
            if (s.Crafted[i] == null || !distinct.Add(s.Crafted[i]) || (p.crafted[i] = ResolvePart(s.Crafted[i])) == null)
            { error = "Unknown/duplicate crafted key"; return false; }
        }
        if (s.HasEquipment)
        {
            if (s.Core == null || s.External == null || s.Core.Length != 4 || s.External.Length != 4)
            { error = "Core equipment shape mismatch"; return false; }
            p.core = new PartData[4]; p.external = new PartType[4];
            for (int i = 0; i < 4; i++)
            {
                p.core[i] = ResolvePart(s.Core[i]);
                if (s.Core[i] != null && (p.core[i] == null || (int)p.core[i].slotType != i)
                    || !Enum.IsDefined(typeof(PartType), s.External[i]))
                { error = "Unknown core key / wrong slot / invalid external type"; return false; }
                p.external[i] = (PartType)s.External[i];
            }
        }
        if (s.HasFourSlots)
        {
            if (s.FourSlots == null || s.FourSlots.Length != 4) { error = "Four-slot shape mismatch"; return false; }
            p.four = new EquipmentItemData[4];
            for (int i = 0; i < 4; i++)
                if (!registry.TryResolveEquipment(s.FourSlots[i], out p.four[i])
                    || p.four[i] != null && p.four[i].partType != (i < 2 ? EquipmentPartType.Arm : EquipmentPartType.Leg))
                { error = "Unknown four-slot key / wrong side type"; return false; }
        }
        if (s.HasLeg && (!registry.TryResolveEquipment(s.CurrentLeg, out p.legItem)
            || p.legItem != null && p.legItem.partType != EquipmentPartType.Leg))
        { error = "Unknown effective leg key / non-leg item"; return false; }
        if (s.HasPower)
        {
            float max = y ? p.yPower.PersistentMaxPower : p.hPower.PersistentMaxPower;
            if (float.IsNaN(s.Power) || float.IsInfinity(s.Power) || float.IsNaN(max) || float.IsInfinity(max) || max < 0 || s.Power < 0 || s.Power > max)
            { error = "Power outside destination configuration"; return false; }
        }
        prepared = p;
        return true;
    }
    internal void CommitEquipment()
    {
        if (quickData != null) quick.BeginPersistenceRestore();
        if (parts == null) return;
        if (parts.HasFourSlots) ui.BeginPersistenceRestore();
        PartSystemRuntimeState.HydrateCraftedForPersistence(root, crafted);
        if (parts.HasEquipment) equipment.RestorePersistentState(core, external);
        if (parts.HasFourSlots) ui.RestorePersistentSlots(four);
        if (parts.HasLeg)
        {
            if (legItem == null) leg.ClearLegItem(); else leg.ApplyLegItem(legItem);
        }
        if (hPower != null && parts.HasEquipment) hPower.RefreshSuperJumpState();
    }
    internal void CommitPower()
    {
        if (parts == null || !parts.HasPower) return;
        if (hPower != null) hPower.RestorePersistentPower(parts.Power); else yPower.RestorePersistentPower(parts.Power);
    }
    internal void NotifyAfterInventory()
    {
        try
        {
            if (parts != null && parts.HasEquipment) equipment.NotifyPersistenceRestored();
        }
        finally
        {
            try { if (parts != null && parts.HasFourSlots) ui.FinishPersistenceRestore(); }
            finally { if (quickData != null) quick.FinishPersistenceRestore(quickData.Slots); }
        }
    }
}
