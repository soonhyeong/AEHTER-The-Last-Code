using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Fixed canonical prefab NetworkBehaviour layout. Local process continuity only, not replication.
public sealed class PlayerStatePersistenceBridge : NetworkBehaviour
{
    [SerializeField] private ItemData[] staticItemCatalog = new ItemData[0];
    private PlayerStateSessionStore store;
    private NetworkManager cachedManager;
    private PlayerStateKey key;
    private PlayerInventory inventory;
    private PlayerStatus status;
    private Hankyul_Fire fire;
    private string sourceScene;
    private bool spawnObserved, localOwner, attemptedRestore;
    private string preparationError;
    private PlayerStateSnapshot pendingRestore;
    private int pendingFrames;
    public string LastValidationError { get; private set; }
    public long RestoredRevision { get; private set; }
    private bool localMode;
    public string LocalIncarnation { get; } = Guid.NewGuid().ToString("N");
    internal PlayerStateKey LocalKey => key;
    public PlayerStateSessionStore.RestoreResult LocalRestoreResult { get; private set; }
    private PlayerStateItemRegistry Registry => localMode ? store.LocalItems : store.NetworkItems;

    internal bool PrepareLocal(PlayerStateSessionStore service, bool requireCatalog = true)
    {
        var h = GetComponent<Move_Hankyul>();
        var y = GetComponent<Move_Yunseo>();
        if (service == null || !service.IsLocalActive || !PlayerStateSessionStore.UsesLocalRouting || (h == null) == (y == null)) return false;
        var nextKey = new PlayerStateKey(service.LocalRunGeneration, PlayerStateSessionStore.SingleLocalSeat,
            h != null ? PlayerStateCharacter.Hankyul : PlayerStateCharacter.Yunseo);
        if (!localMode || !key.Equals(nextKey))
        {
            RestoredRevision = 0; attemptedRestore = false; pendingRestore = null; pendingFrames = 0;
            LocalRestoreResult = PlayerStateSessionStore.RestoreResult.NoSnapshot;
        }
        localMode = true; store = service; key = nextKey;
        sourceScene = gameObject.scene.name;
        PrepareComponents(h, requireCatalog);
        return preparationError == null;
    }
    private void PrepareComponents(Move_Hankyul h, bool requireCatalog = true)
    {
        inventory = GetComponent<PlayerInventory>();
        status = GetComponent<PlayerStatus>();
        fire = null; preparationError = null;
        if (h != null)
        {
            foreach (var candidate in GetComponentsInChildren<Hankyul_Fire>(true))
            {
                if (candidate.GetComponentInParent<Move_Hankyul>() != h) continue;
                if (fire != null) { preparationError = "Ambiguous H fire component"; break; }
                fire = candidate;
            }
        }
        // A fresh live root with no incoming snapshot needs no definition reconstruction.
        // Explicit nonempty catalogs still validate; capture/restore never use this exemption.
        if (localMode && !requireCatalog && (staticItemCatalog == null || staticItemCatalog.Length == 0)
            && !Registry.HasStaticCatalog) return;
        bool inheritedCatalog = localMode && (staticItemCatalog == null || staticItemCatalog.Length == 0)
            && Registry.HasStaticCatalog;
        if (!inheritedCatalog && !Registry.RegisterAssets(staticItemCatalog, out string catalogError))
            preparationError = catalogError;
    }
    internal void BeginLocalRestore(PlayerStateSnapshot snapshot)
    {
        if (!localMode || attemptedRestore) return;
        attemptedRestore = true;
        LocalRestoreResult = PlayerStateSessionStore.RestoreResult.Pending;
        if (!PlayerStatePartsTransfer.IsReady(transform, snapshot)) { pendingRestore = snapshot; return; }
        if (!TryRestoreSnapshot(snapshot, out string error)) RejectRestore(error);
    }
    private void RejectRestore(string error)
    {
        if (localMode) LocalRestoreResult = PlayerStateSessionStore.RestoreResult.Rejected;
        else store.Invalidate(cachedManager, key, error);
        Warn(error);
    }

    public override void OnNetworkSpawn()
    {
        if (spawnObserved) return; // Same live spawn: repeated callbacks never reload its state.
        spawnObserved = true;
        localMode = false;
        attemptedRestore = false;
        pendingRestore = null;
        pendingFrames = 0;
        RestoredRevision = 0;
        localOwner = false;
        LastValidationError = null;
        if (!IsOwner || NetworkManager == null || OwnerClientId != NetworkManager.LocalClientId) return;
        var h = GetComponent<Move_Hankyul>();
        var y = GetComponent<Move_Yunseo>();
        if ((h == null) == (y == null)) { Warn("Unsupported character identity"); return; }
        cachedManager = NetworkManager;
        store = PlayerStateSessionStore.For(cachedManager);
        if (store == null || !store.IsNetworkActive) return;
        key = new PlayerStateKey(store.Generation, OwnerClientId, h != null ? PlayerStateCharacter.Hankyul : PlayerStateCharacter.Yunseo);
        localOwner = true;
        sourceScene = gameObject.scene.name;
        PrepareComponents(h);
        if (preparationError != null)
        { store.Invalidate(cachedManager, key, preparationError); Warn(preparationError); return; }
        if (sourceScene != PlayerStateSessionStore.DestinationScene) return;
        attemptedRestore = true;
        if (!store.TryGet(cachedManager, key, out var snapshot)) return; // First spawn: no state writes.
        // B2a creates local Parts/UI after NGO spawn. Do not call Bind as a restore side effect.
        if (!PlayerStatePartsTransfer.IsReady(transform, snapshot)) { pendingRestore = snapshot; return; }
        if (!TryRestoreSnapshot(snapshot, out string error))
        { store.Invalidate(cachedManager, key, error); Warn(error); }
    }

    public override void OnLostOwnership() { localOwner = false; pendingRestore = null; }
    private void LateUpdate()
    {
        if (pendingRestore == null) return;
        if (!CanUseStore()) { if (localMode) RejectRestore("Local run/owner ended during pending restore"); pendingRestore = null; return; }
        if (!PlayerStatePartsTransfer.IsReady(transform, pendingRestore))
        {
            if (++pendingFrames < 120) return;
            const string unavailable = "Persistence destination Parts bootstrap unavailable after 120 frames; no state applied";
            RejectRestore(unavailable); pendingRestore = null; return;
        }
        var snapshot = pendingRestore; pendingRestore = null;
        if (!TryRestoreSnapshot(snapshot, out string error))
        { RejectRestore(error); }
    }
    public override void OnGainedOwnership()
    {
        // No state handoff/restore on ownership callbacks. Same cached owner may resume capture.
        if (spawnObserved && cachedManager != null && IsOwner && OwnerClientId == key.OwnerClientId
            && cachedManager.LocalClientId == key.OwnerClientId) localOwner = true;
    }
    private bool CanUseStore() => localMode ? store != null && store.AllowsLocal(key) && gameObject.activeInHierarchy
        : localOwner && cachedManager != null && store != null
        && !cachedManager.ShutdownInProgress && cachedManager.IsListening && cachedManager.IsConnectedClient
        && OwnerClientId == key.OwnerClientId && cachedManager.LocalClientId == key.OwnerClientId
        && store.Allows(cachedManager, key);

    public override void OnNetworkDespawn()
    {
        // NGO sets NetworkBehaviour.IsSpawned=false before this callback. Use cached identity/refs.
        if (cachedManager != null && cachedManager.ShutdownInProgress) store?.EndSession();
        if (spawnObserved && sourceScene == PlayerStateSessionStore.SourceScene && CanUseStore())
        {
            if (TryCaptureSnapshot(out var snapshot, out string error)) store.TryStore(cachedManager, snapshot);
            else { store.Invalidate(cachedManager, key, error); Warn(error); }
        }
        spawnObserved = localOwner = false;
        pendingRestore = null;
        inventory = null; status = null; fire = null; cachedManager = null; store = null;
    }

    private void Warn(string message)
    {
        LastValidationError = message;
        Debug.LogWarning("[PlayerState] " + message, this);
    }
    private static bool InRange(float value, float max) => !float.IsNaN(value) && !float.IsInfinity(value)
        && !float.IsNaN(max) && !float.IsInfinity(max) && max >= 0f && value >= 0f && value <= max;
    private bool ValidateResources(PlayerStateSnapshot s, out string error)
    {
        error = null;
        var expected = PlayerStatePresence.Inventory | PlayerStatePresence.Status;
        if (status != null && status.useShield) expected |= PlayerStatePresence.Shield;
        if (key.Character == PlayerStateCharacter.Hankyul) expected |= PlayerStatePresence.Ammo;
        if (inventory == null || status == null || (s.Presence & ~(PlayerStatePresence.QuickSlot | PlayerStatePresence.Parts)) != expected)
        { error = "Missing core component or presence/config mismatch"; return false; }
        if (!InRange(s.HP, status.maxHP) || !InRange(s.Thirst, status.maxThirst)
            || status.useShield && !InRange(s.Shield, status.maxShield))
        { error = "Status outside destination max range"; return false; }
        if ((expected & PlayerStatePresence.Ammo) != 0 && (fire == null || s.Magazine < 0 || s.Reserve < 0
            || s.Magazine > fire.MagazineSize || (long)s.Magazine + s.Reserve > fire.MaxTotalAmmo
            || (s.FireMode != (int)Hankyul_Fire.FireMode.Single && s.FireMode != (int)Hankyul_Fire.FireMode.Auto)))
        { error = "Invalid ammo/fire mode or missing H fire component"; return false; }
        return true;
    }

    internal bool TryCaptureSnapshot(out PlayerStateSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = preparationError;
        if (!CanUseStore() || error != null || inventory == null || status == null)
        { error = error ?? "Core capture unavailable"; return false; }
        if (!inventory.TryCopySlotsExact(out var slots, out error)) return false;
        var s = new PlayerStateSnapshot { Key = key, SourceScene = sourceScene,
            Presence = PlayerStatePresence.Inventory | PlayerStatePresence.Status, SlotCount = slots.Length,
            Slots = new InventorySlotSnapshot[slots.Length], HP = status.currentHP, Thirst = status.currentThirst };
        if (status.useShield) { s.Presence |= PlayerStatePresence.Shield; s.Shield = status.currentShield; }
        if (key.Character == PlayerStateCharacter.Hankyul)
        {
            if (fire == null) { error = "Missing H fire component"; return false; }
            s.Presence |= PlayerStatePresence.Ammo;
            s.Magazine = fire.CurrentMagazineAmmo; s.Reserve = fire.CurrentReserveAmmo; s.FireMode = (int)fire.CurrentFireMode;
        }
        if (!ValidateResources(s, out error)) return false;
        var definitions = new Dictionary<string, ItemDefinitionSnapshot>(StringComparer.Ordinal);
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var saved = new InventorySlotSnapshot { Index = i, Count = slot.count };
            if (slot.item != null)
            {
                if (!Registry.TryDescribe(slot.item, out var definition, out error)) return false;
                definitions[definition.Key] = definition;
                saved.ItemKey = definition.Key;
            }
            s.Slots[i] = saved;
        }
        s.Definitions = new List<ItemDefinitionSnapshot>(definitions.Values).ToArray();
        if (!PlayerStatePartsTransfer.TryCapture(transform, Registry, s, out error)) return false;
        snapshot = s;
        return true;
    }

    internal bool TryRestoreSnapshot(PlayerStateSnapshot s, out string error)
    {
        error = null;
        if (!CanUseStore() || !attemptedRestore || (localMode
            ? !PlayerStateSessionStore.IsLocalGameplayScene(sourceScene)
            : sourceScene != PlayerStateSessionStore.DestinationScene))
        { error = "Restore outside approved local spawn"; return false; }
        if (s == null || s.SchemaVersion != PlayerStateSnapshot.CurrentSchema || !key.Equals(s.Key)
            || (localMode ? !store.AuthorizesLocalRestore(s) : s.SourceScene != PlayerStateSessionStore.SourceScene) || s.Revision <= 0)
        { error = "Invalid snapshot metadata/identity"; return false; }
        if (RestoredRevision != 0) return RestoredRevision == s.Revision; // Same live spawn never overwrites subsequent play.
        if (!ValidateResources(s, out error)) return false;
        if (s.SlotCount != inventory.slotCount || s.Slots == null || s.Slots.Length != s.SlotCount || s.Definitions == null)
        { error = "Inventory shape mismatch"; return false; }
        var resolved = new Dictionary<string, ItemData>(StringComparer.Ordinal);
        foreach (var definition in s.Definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Key) || resolved.ContainsKey(definition.Key))
            { error = "Duplicate/invalid definition key"; return false; }
            if (!Registry.TryResolve(definition, out var item, out error)) return false;
            resolved.Add(definition.Key, item);
        }
        var replacement = new PlayerInventory.Slot[s.SlotCount];
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < s.Slots.Length; i++)
        {
            var slot = s.Slots[i];
            if (slot == null || slot.Index != i || slot.Count < 0)
            { error = "Invalid slot index/count"; return false; }
            ItemData item = null;
            if (slot.ItemKey == null)
            {
                if (slot.Count != 0) { error = "Empty slot with nonzero count"; return false; }
            }
            else if (slot.Count == 0 || !resolved.TryGetValue(slot.ItemKey, out item))
            { error = "Unknown key or zero item count"; return false; }
            else used.Add(slot.ItemKey);
            replacement[i] = new PlayerInventory.Slot { item = item, count = slot.Count };
        }
        if (!PlayerStatePartsTransfer.TryPrepare(transform, Registry, s, resolved, used, out var extended, out error)) return false;
        if (used.Count != resolved.Count) { error = "Unused definition in snapshot"; return false; }
        // All supported state is validated before the first destination write. Notify once, after all setters.
        if (!inventory.TryReplaceSlotsExact(replacement, out error, false)) return false;
        extended.CommitEquipment();
        status.SetHP(s.HP);
        status.SetThirst(s.Thirst);
        if ((s.Presence & PlayerStatePresence.Shield) != 0) status.SetShield(s.Shield);
        extended.CommitPower();
        if ((s.Presence & PlayerStatePresence.Ammo) != 0)
        { fire.SetAmmo(s.Magazine, s.Reserve); fire.SetFireMode((Hankyul_Fire.FireMode)s.FireMode); }
        RestoredRevision = s.Revision;
        if (localMode) LocalRestoreResult = PlayerStateSessionStore.RestoreResult.Applied;
        try { inventory.NotifyInventoryChanged(); }
        finally { extended.NotifyAfterInventory(); }
        return true;
    }
}
