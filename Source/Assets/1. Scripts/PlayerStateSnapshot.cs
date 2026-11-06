using System;

// Plain process/session data only. No UnityEngine.Object or network transport state.
public enum PlayerStateCharacter { Unsupported, Hankyul, Yunseo }
[Flags]
public enum PlayerStatePresence { None = 0, Inventory = 1, Status = 2, Shield = 4, Ammo = 8, QuickSlot = 16, Parts = 32 }

public sealed class PlayerQuickSlotSnapshot
{
    public int SlotCount;
    public int[] Slots;
    public PlayerQuickSlotSnapshot Copy() => new PlayerQuickSlotSnapshot { SlotCount = SlotCount, Slots = Slots == null ? null : (int[])Slots.Clone() };
}

public sealed class PlayerPartsSnapshot
{
    public bool HasEquipment, HasFourSlots, HasLeg, HasPower;
    public string[] Crafted, Core, FourSlots;
    public int[] External;
    public string CurrentLeg;
    public float Power;
    public PlayerPartsSnapshot Copy()
    {
        var copy = (PlayerPartsSnapshot)MemberwiseClone();
        copy.Crafted = Crafted == null ? null : (string[])Crafted.Clone();
        copy.Core = Core == null ? null : (string[])Core.Clone();
        copy.FourSlots = FourSlots == null ? null : (string[])FourSlots.Clone();
        copy.External = External == null ? null : (int[])External.Clone();
        return copy;
    }
}

public readonly struct PlayerStateKey : IEquatable<PlayerStateKey>
{
    public readonly string SessionGeneration;
    public readonly ulong OwnerClientId;
    public readonly PlayerStateCharacter Character;
    public PlayerStateKey(string generation, ulong owner, PlayerStateCharacter character)
    { SessionGeneration = generation; OwnerClientId = owner; Character = character; }
    public bool Equals(PlayerStateKey other) => SessionGeneration == other.SessionGeneration
        && OwnerClientId == other.OwnerClientId && Character == other.Character;
    public override bool Equals(object obj) => obj is PlayerStateKey other && Equals(other);
    public override int GetHashCode() => ((SessionGeneration?.GetHashCode() ?? 0) * 397 ^ OwnerClientId.GetHashCode()) * 397 ^ (int)Character;
}

public sealed class ItemDefinitionSnapshot
{
    public string Key;
    public string Id;
    public bool RuntimeFallback;
    public bool IsPart;
    // Only the two existing fallback factories' definition fields. Assets stay in the registry.
    public string DisplayName;
    public string Description;
    public ItemDefinitionSnapshot Copy() => (ItemDefinitionSnapshot)MemberwiseClone();
}

public sealed class InventorySlotSnapshot
{
    public int Index;
    public string ItemKey; // null + count 0 is an explicit empty slot.
    public int Count;
    public InventorySlotSnapshot Copy() => (InventorySlotSnapshot)MemberwiseClone();
}

public sealed class PlayerStateSnapshot
{
    public const int CurrentSchema = 2;
    public int SchemaVersion = CurrentSchema;
    public PlayerStateKey Key;
    public long Revision;
    public string SourceScene;
    public PlayerStatePresence Presence;
    public int SlotCount;
    public InventorySlotSnapshot[] Slots;
    public ItemDefinitionSnapshot[] Definitions;
    public float HP, Thirst, Shield;
    public int Magazine, Reserve, FireMode;
    public PlayerQuickSlotSnapshot QuickSlot;
    public PlayerPartsSnapshot Parts;

    public PlayerStateSnapshot Copy()
    {
        var copy = (PlayerStateSnapshot)MemberwiseClone();
        copy.Slots = Slots == null ? null : Array.ConvertAll(Slots, s => s?.Copy());
        copy.Definitions = Definitions == null ? null : Array.ConvertAll(Definitions, d => d?.Copy());
        copy.QuickSlot = QuickSlot?.Copy();
        copy.Parts = Parts?.Copy();
        return copy;
    }
}
