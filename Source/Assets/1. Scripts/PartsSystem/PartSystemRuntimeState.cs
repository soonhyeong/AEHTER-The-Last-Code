using System;
using System.Collections.Generic;
using UnityEngine;

public static class PartSystemRuntimeState
{
    // A live root is a scope, NOT a persistent client/account/character identity.
    private sealed class Scope
    {
        public Transform root;
        public PlayerInventory inventory;
        public EquipmentManager equipment;
        public readonly List<PartData> crafted = new List<PartData>();
        public readonly List<PlayerInventory.Slot> inventorySnapshot = new List<PlayerInventory.Slot>();
        public readonly Dictionary<PartSlotType, PartData> equippedSnapshot = new Dictionary<PartSlotType, PartData>();
        public readonly Dictionary<PartSlotType, PartType> externalSnapshot = new Dictionary<PartSlotType, PartType>();

        public void CaptureInventory()
        {
            inventorySnapshot.Clear();
            if (inventory == null) return;
            foreach (var slot in inventory.slots)
                inventorySnapshot.Add(slot == null ? null : new PlayerInventory.Slot { item = slot.item, count = slot.count });
        }
        public void CaptureEquipment(PartSlotType slot, PartData part)
        {
            equippedSnapshot.Clear();
            externalSnapshot.Clear();
            if (equipment == null) return;
            foreach (PartSlotType key in Enum.GetValues(typeof(PartSlotType)))
            {
                equippedSnapshot[key] = equipment.GetEquipped(key);
                externalSnapshot[key] = equipment.GetExternalEquipped(key);
            }
        }
        public void Unbind()
        {
            if (inventory != null) inventory.OnInventoryChanged -= CaptureInventory;
            if (equipment != null)
            {
                equipment.OnEquipmentChanged -= CaptureEquipment;
                equipment.OnPartEquipped -= CaptureEquipment;
                equipment.OnPartUnequipped -= CaptureEquipment;
            }
            inventory = null;
            equipment = null;
        }
    }

    private static readonly Dictionary<Transform, Scope> scopes = new Dictionary<Transform, Scope>();
    private static readonly List<Transform> expired = new List<Transform>();
    private static readonly PartData[] emptyParts = new PartData[0];
    private static Scope live;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var scope in scopes.Values) scope.Unbind();
        scopes.Clear();
        live = null;
    }

    private static void Prune()
    {
        expired.Clear();
        foreach (var pair in scopes)
            if (pair.Key == null || !pair.Key.gameObject.scene.IsValid() || !pair.Key.gameObject.scene.isLoaded)
                expired.Add(pair.Key);
        foreach (var key in expired)
        {
            var scope = scopes[key];
            scope.Unbind();
            if (live == scope) live = null;
            scopes.Remove(key);
        }
        if (live != null && (!LocalPlayerBindingResolver.TryResolve(live.root, out var valid, false) || valid != live.root))
            ClearLiveBinding();
    }

    public static void ClearLiveBinding()
    {
        if (live != null) live.Unbind();
        live = null;
    }

    private static Scope ScopeFor(Transform root)
    {
        Prune();
        if (!scopes.TryGetValue(root, out var scope))
        {
            scope = new Scope { root = root };
            scopes.Add(root, scope);
        }
        return scope;
    }

    private static Scope BindScope(Transform root)
    {
        var scope = ScopeFor(root);
        if (live != scope)
        {
            ClearLiveBinding();
            live = scope;
        }
        return scope;
    }

    public static PlayerInventory FindBestInventory()
    {
        return LocalPlayerBindingResolver.TryResolve(null, out var root)
            ? LocalPlayerBindingResolver.Owned<PlayerInventory>(root) : null;
    }

    public static PlayerInventory GetOrBindBestInventory()
    {
        Prune();
        var inventory = FindBestInventory();
        BindInventory(inventory);
        return inventory; // Never return a stale previous binding on a failed query.
    }

    public static void BindInventory(PlayerInventory inventory)
    {
        Prune();
        if (inventory == null) { ClearLiveBinding(); return; }
        if (!LocalPlayerBindingResolver.TryResolve(inventory, out var root, false)
            || LocalPlayerBindingResolver.PlayerRoot(inventory) != root) return;
        var scope = BindScope(root);
        if (scope.inventory == inventory) return;
        if (scope.inventory != null) scope.inventory.OnInventoryChanged -= scope.CaptureInventory;
        scope.inventory = inventory;
        scope.CaptureInventory(); // Read only: binding never writes inventory contents.
        inventory.OnInventoryChanged += scope.CaptureInventory;
    }

    public static void BindEquipment(EquipmentManager equipment)
    {
        Prune();
        if (equipment == null) { ClearLiveBinding(); return; }
        if (!LocalPlayerBindingResolver.TryResolve(equipment, out var root, false)
            || LocalPlayerBindingResolver.PlayerRoot(equipment) != root) return;
        var scope = BindScope(root);
        if (scope.equipment == equipment) return;
        if (scope.equipment != null)
        {
            scope.equipment.OnEquipmentChanged -= scope.CaptureEquipment;
            scope.equipment.OnPartEquipped -= scope.CaptureEquipment;
            scope.equipment.OnPartUnequipped -= scope.CaptureEquipment;
        }
        scope.equipment = equipment;
        scope.CaptureEquipment(default, null); // No RestoreEquippedPart/SetExternalEquipped here.
        equipment.OnEquipmentChanged += scope.CaptureEquipment;
        equipment.OnPartEquipped += scope.CaptureEquipment;
        equipment.OnPartUnequipped += scope.CaptureEquipment;
    }

    public static void RegisterCraftedEquipmentPart(PartData part) => RegisterCraftedEquipmentPart(part, null);
    public static void RegisterCraftedEquipmentPart(PartData part, Component owner)
    {
        if (part == null || !LocalPlayerBindingResolver.TryResolve(owner, out var root)) return;
        var scope = ScopeFor(root);
        if (!scope.crafted.Contains(part)) scope.crafted.Add(part);
    }

    public static IReadOnlyList<PartData> GetCraftedEquipmentParts() => GetCraftedEquipmentParts(null);

    // Cached root boundary during despawn; never prune/resolve another player here.
    internal static PartData[] CopyCraftedForPersistence(Transform root)
    {
        return root != null && scopes.TryGetValue(root, out var scope) ? scope.crafted.ToArray() : emptyParts;
    }
    internal static void HydrateCraftedForPersistence(Transform root, PartData[] parts)
    {
        var scope = ScopeFor(root);
        scope.crafted.Clear();
        scope.crafted.AddRange(parts);
    }
    public static IReadOnlyList<PartData> GetCraftedEquipmentParts(Component owner)
    {
        Prune();
        if (!LocalPlayerBindingResolver.TryResolve(owner, out var root)) return emptyParts;
        return scopes.TryGetValue(root, out var scope) ? scope.crafted.AsReadOnly() : emptyParts;
    }
}
