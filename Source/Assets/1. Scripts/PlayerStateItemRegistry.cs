using System;
using System.Collections.Generic;
using UnityEngine;

// Same-process definition cache, not a save file and not an authority layer.
public sealed class PlayerStateItemRegistry
{
    private sealed class Entry { public ItemData Item; public ItemDefinitionSnapshot Definition; }
    private sealed class ReferenceComparer : IEqualityComparer<ItemData>
    {
        public bool Equals(ItemData a, ItemData b) => ReferenceEquals(a, b);
        public int GetHashCode(ItemData item) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item);
    }
    private readonly Dictionary<ItemData, Entry> byObject = new Dictionary<ItemData, Entry>(new ReferenceComparer());
    private readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private readonly List<ItemData> reconstructed = new List<ItemData>();
    private readonly string generation;
    private long runtimeSequence;
    private string catalogError;
    private readonly Dictionary<string, EquipmentItemData> equipment = new Dictionary<string, EquipmentItemData>(StringComparer.Ordinal);

    private bool EquipmentKey(EquipmentItemData item, out string key)
    {
        key = null;
        if (item == null || string.IsNullOrWhiteSpace(item.name)
            || !Enum.IsDefined(typeof(EquipmentPartType), item.partType)
            || !Enum.IsDefined(typeof(PartType), item.abilityType)
            || float.IsNaN(item.jumpForceAdd) || float.IsInfinity(item.jumpForceAdd)
            || float.IsNaN(item.jumpForceMultiplier) || float.IsInfinity(item.jumpForceMultiplier)) return false;
        string partKey = "-";
        if (item.sourceCraftedPart != null)
        {
            if (!TryDescribe(item.sourceCraftedPart, out var definition, out _)) return false;
            partKey = definition.Key;
        }
        key = "equipment:" + partKey + ":" + (int)item.partType + ":" + item.name;
        return true;
    }
    internal bool TryRegisterEquipment(EquipmentItemData item, out string key)
    {
        key = null;
        if (item == null) return true; // Explicit empty slot, not unknown fallback.
        if (!EquipmentKey(item, out key)) return false;
        if (equipment.TryGetValue(key, out var existing) && !ReferenceEquals(existing, item)) return false;
        equipment[key] = item;
        return true;
    }
    internal bool TryResolveEquipment(string key, out EquipmentItemData item)
    {
        item = null;
        if (key == null) return true;
        return equipment.TryGetValue(key, out item) && EquipmentKey(item, out string current) && current == key;
    }

    public PlayerStateItemRegistry(string sessionGeneration) { generation = sessionGeneration; }

    // Authored 06/07 receivers share the already validated run catalog, not a second catalog.
    internal bool HasStaticCatalog
    {
        get
        {
            if (catalogError != null) return false;
            foreach (var entry in byKey.Values)
                if (!entry.Definition.RuntimeFallback && entry.Item != null) return true;
            return false;
        }
    }

    // Called only by the two existing factories, after their plain definition is populated.
    // Standalone does not create a persistence service or change pickup behavior.
    public static void RegisterRuntimeFallback(ItemData item)
    {
        var store = PlayerStateSessionStore.Current;
        if (store != null && store.IsActive && !store.Items.TryRegisterRuntime(item, out string error))
            Debug.LogWarning("[PlayerState] Runtime definition rejected: " + error);
    }

    public bool RegisterAssets(ItemData[] assets, out string error)
    {
        error = catalogError;
        if (error != null) return false;
        if (assets == null || assets.Length == 0) return FailCatalog("Missing static item catalog", out error);
        var pending = new Dictionary<string, ItemData>(StringComparer.Ordinal);
        foreach (var item in assets)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id)
                || (item.GetType() != typeof(ItemData) && item.GetType() != typeof(PartData)))
                return FailCatalog("Invalid static definition", out error);
            string key = "asset:" + item.id;
            if (pending.TryGetValue(key, out var other) && !ReferenceEquals(other, item)
                || byKey.TryGetValue(key, out var previous) && !ReferenceEquals(previous.Item, item)
                || byObject.TryGetValue(item, out var known) && known.Definition.Key != key)
                return FailCatalog("Duplicate or changed static id: " + item.id, out error);
            pending[key] = item;
        }
        foreach (var pair in pending)
        {
            if (byKey.ContainsKey(pair.Key)) continue;
            var entry = new Entry { Item = pair.Value, Definition = new ItemDefinitionSnapshot
                { Key = pair.Key, Id = pair.Value.id, IsPart = pair.Value is PartData } };
            byKey.Add(pair.Key, entry);
            byObject.Add(pair.Value, entry);
        }
        error = null;
        return true;
    }

    private bool FailCatalog(string reason, out string error) { error = catalogError = reason; return false; }
    private static bool SupportedFallback(ItemData item) => item != null && item.GetType() == typeof(ItemData)
        && !string.IsNullOrWhiteSpace(item.id) && item.itemSprite == null && (item.effects == null || item.effects.Length == 0);

    public bool TryRegisterRuntime(ItemData item, out string error)
    {
        error = null;
        if (!SupportedFallback(item)) { error = "Unsupported fallback fields/type/id"; return false; }
        if (byObject.TryGetValue(item, out var existing))
        {
            if (!existing.Definition.RuntimeFallback) { error = "Static asset cannot become runtime fallback"; return false; }
            return true;
        }
        // Distinct objects with the same id must remain distinct definitions/references.
        string key = "runtime:" + generation + ":" + (++runtimeSequence);
        var entry = new Entry { Item = item, Definition = new ItemDefinitionSnapshot
        { Key = key, Id = item.id, RuntimeFallback = true, DisplayName = item.displayName, Description = item.description } };
        byObject.Add(item, entry);
        byKey.Add(key, entry);
        return true;
    }

    public bool TryDescribe(ItemData item, out ItemDefinitionSnapshot definition, out string error)
    {
        definition = null;
        error = catalogError;
        if (error != null) return false;
        if (item == null || !byObject.TryGetValue(item, out var entry)) { error = "Unregistered item reference"; return false; }
        var d = entry.Definition;
        if (item.id != d.Id || d.IsPart != (item is PartData)
            || d.RuntimeFallback && (!SupportedFallback(item) || item.displayName != d.DisplayName || item.description != d.Description))
        { error = "Definition changed after registration"; return false; }
        definition = d.Copy();
        return true;
    }

    public bool TryResolve(ItemDefinitionSnapshot definition, out ItemData item, out string error)
    {
        item = null;
        error = catalogError;
        if (error != null) return false;
        if (definition == null || string.IsNullOrEmpty(definition.Key)
            || !byKey.TryGetValue(definition.Key, out var entry) || !Same(entry.Definition, definition))
        { error = "Unknown or mismatched item definition"; return false; }
        if (entry.Item == null)
        {
            if (!definition.RuntimeFallback) { error = "Static asset no longer resolves"; return false; }
            // Reconstruct only a previously registered factory definition, never arbitrary keys.
            var recreated = ScriptableObject.CreateInstance<ItemData>();
            recreated.id = definition.Id;
            recreated.displayName = definition.DisplayName;
            recreated.description = definition.Description;
            entry.Item = recreated;
            byObject.Add(recreated, entry);
            reconstructed.Add(recreated);
        }
        if (!TryDescribe(entry.Item, out _, out error)) return false;
        item = entry.Item;
        return true;
    }

    private static bool Same(ItemDefinitionSnapshot a, ItemDefinitionSnapshot b) => a.Key == b.Key && a.Id == b.Id
        && a.RuntimeFallback == b.RuntimeFallback && a.IsPart == b.IsPart && a.DisplayName == b.DisplayName && a.Description == b.Description;

    public void Clear()
    {
        foreach (var item in reconstructed) if (item != null) UnityEngine.Object.Destroy(item);
        reconstructed.Clear();
        byObject.Clear();
        byKey.Clear();
        equipment.Clear();
        catalogError = null;
    }
}
