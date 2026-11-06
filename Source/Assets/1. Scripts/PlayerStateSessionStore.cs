using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Session lifecycle service. Snapshot entries never retain Player/Scene object references.
public sealed class PlayerStateSessionStore : MonoBehaviour
{
    public enum SessionState { Inactive, Active, Ending }
    public const string SourceScene = "GameScene";
    public const string DestinationScene = "05.폐허된 도시";
    public static PlayerStateSessionStore Current { get; private set; }
    public SessionState State { get; private set; }
    public string Generation { get; private set; }
    public PlayerStateItemRegistry NetworkItems { get; private set; }
    // Compatibility factory facade only. Durable lookup/commit always names its namespace explicitly.
    public PlayerStateItemRegistry Items => UsesLocalRouting ? LocalItems : NetworkItems;
    private NetworkManager manager;
    private string previousScene;
    private sealed class Entry { public long Revision; public PlayerStateSnapshot Snapshot; public string Error; }
    private readonly Dictionary<PlayerStateKey, Entry> entries = new Dictionary<PlayerStateKey, Entry>();
    public bool IsNetworkActive => State == SessionState.Active && manager != null && manager.IsListening && !manager.ShutdownInProgress;
    public bool IsActive => UsesLocalRouting ? IsLocalActive : IsNetworkActive;

    public enum RestoreResult { Pending, Applied, NoSnapshot, Rejected }
    // Plain transition metadata. Live reservation/destination references below are NOT snapshot data.
    public sealed class LocalTicket
    {
        public string Generation, FromScene, ToScene, OriginIncarnation;
        public PlayerStateCharacter Character;
        public long Revision;
        public bool Rejected;
    }
    public const string LocalStartScene = "04.Tutorial1";
    public const string LocalLabScene = "06.연구소 메인 장소";
    public const string LocalPartsScene = "07.파츠 제작실";
    public static bool IsLocalGameplayScene(string scene) => scene == LocalStartScene
        || scene == DestinationScene || scene == LocalLabScene || scene == LocalPartsScene;
    public const ulong SingleLocalSeat = 0;
    public static bool UsesLocalRouting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    public string LocalRunGeneration { get; private set; }
    public PlayerStateItemRegistry LocalItems { get; private set; }
    public bool IsLocalActive { get; private set; }
    private bool quitting;
    private readonly Dictionary<PlayerStateKey, Entry> localEntries = new Dictionary<PlayerStateKey, Entry>();
    private LocalTicket localTicket;
    private UnityEngine.Object reservation;
    private PlayerStatePersistenceBridge reservedOwner, destinationOwner;
    private string reservedTarget;
    private int reservedScene, destinationScene = -1, destinationFrames;
    private int ticketArrivalScene = -1;
    private bool loadStarted, destinationResolved;
    private RestoreResult destinationResult = RestoreResult.Pending;
    private Transform localCarriedRoot;
    private string localCarryTarget;
    private string localCarryGeneration;
    private static bool IsElevatorFloor(string scene) => scene == LocalLabScene
        || scene == "08.1구역_2" || scene == "09.2구역_2" || scene == "11.3구역_찐";

    public static bool IsLocalRoute(string target)
    {
        if (!UsesLocalRouting) return false;
        string scene = SceneManager.GetActiveScene().name;
        return scene == LocalStartScene && target == DestinationScene
            || scene == DestinationScene && (IsPuzzle(target) || target == LocalLabScene)
            || scene == LocalLabScene && target == LocalPartsScene
            || scene == LocalPartsScene && target == LocalLabScene;
    }
    private static bool IsPuzzle(string scene) => string.Equals(scene, "Puzzle2", StringComparison.OrdinalIgnoreCase);
    public void BeginLocalRun()
    {
        EndLocalRun();
        if (quitting || !UsesLocalRouting) return;
        LocalRunGeneration = "local:" + Guid.NewGuid().ToString("N");
        LocalItems = new PlayerStateItemRegistry(LocalRunGeneration);
        IsLocalActive = true;
    }
    public void EndLocalRun()
    {
        IsLocalActive = false; // Latch before clearing, including all late/destroy callbacks.
        localEntries.Clear();
        LocalItems?.Clear();
        LocalItems = null;
        localTicket = null;
        localCarriedRoot = null; localCarryTarget = localCarryGeneration = null;
        ticketArrivalScene = -1;
        reservation = null; reservedOwner = null; reservedTarget = null; loadStarted = false;
        destinationOwner = null; destinationResolved = true; destinationResult = RestoreResult.Rejected;
    }
    public bool AllowsLocal(PlayerStateKey key) => UsesLocalRouting && IsLocalActive && !quitting
        && key.SessionGeneration == LocalRunGeneration && key.OwnerClientId == SingleLocalSeat
        && (key.Character == PlayerStateCharacter.Hankyul || key.Character == PlayerStateCharacter.Yunseo);

    private static Transform UniqueLocalRoot(Scene scene, out bool ambiguous)
    {
        Transform selected = null;
        ambiguous = false;
        var roots = new HashSet<Transform>();
        foreach (var h in UnityEngine.Object.FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (h.gameObject.scene.handle == scene.handle) roots.Add(h.transform);
        foreach (var y in UnityEngine.Object.FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (y.gameObject.scene.handle == scene.handle) roots.Add(y.transform);
        foreach (var root in roots)
        {
            if (selected != null || (root.GetComponent<Move_Hankyul>() == null) == (root.GetComponent<Move_Yunseo>() == null))
            { ambiguous = true; return null; }
            selected = root;
        }
        return selected;
    }
    public static bool TryReserveLocal(UnityEngine.Object requester, Component owner, string target)
    {
        if (requester == null || owner == null || !IsLocalRoute(target)) return false;
        Install();
        var service = Current;
        if (service.quitting || !service.IsLocalActive || service.reservation != null || service.loadStarted) return false;
        var root = LocalPlayerBindingResolver.PlayerRoot(owner);
        var scene = SceneManager.GetActiveScene();
        // Never replace an incoming full transaction with an authored/default partial state.
        if (QueryLocalRestore(scene, out _) == RestoreResult.Pending) return false;
        if (root == null || root != UniqueLocalRoot(scene, out _) || !root.gameObject.activeInHierarchy) return false;
        var bridge = ResolveLocalBridge(root, scene);
        if (bridge == null || !bridge.PrepareLocal(service)) return false;
        service.reservation = requester; service.reservedOwner = bridge;
        service.reservedScene = scene.handle; service.reservedTarget = target;
        return true;
    }
    public static void CancelLocal(UnityEngine.Object requester)
    {
        if (Current == null || Current.reservation != requester || Current.loadStarted) return;
        Current.reservation = null; Current.reservedOwner = null; Current.reservedTarget = null;
    }
    private static PlayerStatePersistenceBridge ResolveLocalBridge(Transform root, Scene scene)
    {
        if (root == null || root.gameObject.scene.handle != scene.handle) return null;
        var bridge = root.GetComponent<PlayerStatePersistenceBridge>();
        // Reuse the whole transaction; never alter a NetworkObject's behaviour layout.
        if (bridge == null && UsesLocalRouting && (scene.name == LocalLabScene || scene.name == LocalPartsScene)
            && root.GetComponent<NetworkObject>() == null && root.GetComponent<PlayerInventory>() != null
            && root.GetComponent<PlayerStatus>() != null)
            bridge = root.gameObject.AddComponent<PlayerStatePersistenceBridge>();
        return bridge;
    }
    public static bool TryLoadLocal(UnityEngine.Object requester, Action beforeLoad = null, Action<string> loadScene = null)
    {
        var service = Current;
        if (service == null || service.reservation != requester || requester == null || service.loadStarted
            || service.reservedScene != SceneManager.GetActiveScene().handle || !IsLocalRoute(service.reservedTarget)) return false;
        var owner = service.reservedOwner;
        if (owner == null || owner.transform != UniqueLocalRoot(SceneManager.GetActiveScene(), out _))
        { CancelLocal(requester); return false; }
        service.loadStarted = true;
        var key = owner.LocalKey;
        if (!service.AllowsLocal(key)) { service.loadStarted = false; CancelLocal(requester); return false; }
        if (!service.localEntries.TryGetValue(key, out var entry)) service.localEntries.Add(key, entry = new Entry());
        entry.Revision++;
        bool captured = owner.TryCaptureSnapshot(out var snapshot, out string error);
        entry.Snapshot = captured ? snapshot.Copy() : null;
        entry.Error = error;
        if (captured) entry.Snapshot.Revision = entry.Revision;
        else Debug.LogWarning("[PlayerState] Local capture rejected; destination/reward will remain rejected: " + error);
        service.localTicket = new LocalTicket { Generation = service.LocalRunGeneration,
            FromScene = SceneManager.GetActiveScene().name, ToScene = service.reservedTarget,
            Character = key.Character, Revision = entry.Revision, OriginIncarnation = owner.LocalIncarnation, Rejected = !captured };
        service.ticketArrivalScene = -1;
        try
        {
            beforeLoad?.Invoke();
            if (loadScene != null) loadScene(service.reservedTarget);
            else SceneManager.LoadScene(service.reservedTarget);
            return true;
        }
        catch (Exception exception)
        {
            // Keep the live Player untouched. A retry MUST capture current state and increment revision.
            service.loadStarted = false; service.localTicket = null;
            CancelLocal(requester);
            Debug.LogWarning("[PlayerState] Local Scene load failed; reservation released: " + exception.Message);
            return false;
        }
    }
    public static void PrepareLocalPuzzleReturn(string target)
    {
        if (!UsesLocalRouting || target != DestinationScene || !IsPuzzle(SceneManager.GetActiveScene().name)) return;
        Install();
        var ticket = Current.localTicket;
        if (ticket != null && IsPuzzle(ticket.ToScene) && ticket.FromScene == DestinationScene)
            ticket.ToScene = target; // Keep the entry revision, capture Scene and originating incarnation.
    }
    public static bool TryPrepareLocalCarry(Component owner, string target)
    {
        var service = Current;
        var scene = SceneManager.GetActiveScene();
        if (!UsesLocalRouting || service == null || !service.IsLocalActive || service.quitting
            || service.reservation != null || !IsElevatorFloor(scene.name) || !IsElevatorFloor(target)
            || QueryLocalRestore(scene, out _) != RestoreResult.Applied
                && QueryLocalRestore(scene, out _) != RestoreResult.NoSnapshot) return false;
        var root = LocalPlayerBindingResolver.PlayerRoot(owner);
        if (root == null || root != UniqueLocalRoot(scene, out _) || !root.gameObject.activeInHierarchy) return false;
        var bridge = ResolveLocalBridge(root, scene);
        if (bridge == null || !bridge.PrepareLocal(service, false)) return false;
        service.localCarriedRoot = root; service.localCarryTarget = target;
        service.localCarryGeneration = service.LocalRunGeneration;
        service.localTicket = null; // Live carry has no snapshot replay, even after earlier authored restores.
        return true;
    }
    private bool EnterLocalCarry(Scene scene)
    {
        if (localCarriedRoot == null || localCarryTarget != scene.name
            || localCarryGeneration != LocalRunGeneration || !IsLocalActive) return false;
        var carried = localCarriedRoot;
        bool h = carried.GetComponent<Move_Hankyul>() != null;
        var competitors = new HashSet<Transform>();
        bool invalid = false;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (candidate.transform != carried && candidate.gameObject.scene.handle == scene.handle)
            { if (!h || candidate.GetComponent<Move_Yunseo>() != null) invalid = true; else competitors.Add(candidate.transform); }
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (candidate.transform != carried && candidate.gameObject.scene.handle == scene.handle)
            { if (h || candidate.GetComponent<Move_Hankyul>() != null) invalid = true; else competitors.Add(candidate.transform); }
        foreach (var competitor in competitors)
        {
            var networkObject = competitor.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned) invalid = true;
        }
        // Never destroy or select a first Player. Suppress only exact Scene-owned same-character competitors.
        if (!invalid) foreach (var competitor in competitors) competitor.gameObject.SetActive(false);
        if (carried.parent == null) SceneManager.MoveGameObjectToScene(carried.gameObject, scene);
        destinationScene = scene.handle; destinationResolved = true; destinationFrames = 0;
        destinationOwner = carried.GetComponent<PlayerStatePersistenceBridge>();
        localCarriedRoot = null; localCarryTarget = localCarryGeneration = null;
        if (invalid || destinationOwner == null || !destinationOwner.PrepareLocal(this, false))
            RejectDestination("Invalid/foreign carried destination Player; live state retained without replay");
        else destinationResult = RestoreResult.Applied; // Same incarnation: ZERO TryRestoreSnapshot calls.
        return true;
    }
    private void EnterLocalScene(Scene scene)
    {
        if (!UsesLocalRouting) return;
        reservation = null; reservedOwner = null; reservedTarget = null; loadStarted = false;
        if (EnterLocalCarry(scene)) return;
        localCarriedRoot = null; localCarryTarget = localCarryGeneration = null;
        if (IsPuzzle(scene.name)) return; // No owner, capture, registry reset or default snapshot here.
        if (!IsLocalGameplayScene(scene.name)) { EndLocalRun(); return; }
        if (destinationScene == scene.handle) return;
        if (scene.name == LocalStartScene || !IsLocalActive) BeginLocalRun();
        // Unapproved loads into 05 cannot reapply an earlier entry just because a key matches.
        if (localTicket != null && (localTicket.ToScene != scene.name
            || ticketArrivalScene != -1 && ticketArrivalScene != scene.handle)) localTicket = null;
        if (localTicket != null) ticketArrivalScene = scene.handle;
        destinationScene = scene.handle; destinationFrames = 0;
        destinationOwner = null; destinationResolved = false; destinationResult = RestoreResult.Pending;
    }
    private void PollLocalDestination()
    {
        if (!UsesLocalRouting || !IsLocalActive || destinationResolved || quitting) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.handle != destinationScene || !IsLocalGameplayScene(scene.name)) return;
        var root = UniqueLocalRoot(scene, out bool ambiguous);
        if (!ambiguous && root == null && ++destinationFrames < 120) return;
        destinationResolved = true;
        if (root == null) { RejectDestination("Missing/ambiguous local destination Player"); return; }
        destinationOwner = ResolveLocalBridge(root, scene);
        if (destinationOwner == null || !destinationOwner.PrepareLocal(this, localTicket != null))
        { RejectDestination("Destination persistence component/config unavailable"); return; }
        if (localTicket == null) { destinationResult = RestoreResult.NoSnapshot; return; }
        var ticket = localTicket;
        var key = destinationOwner.LocalKey;
        if (ticket.Rejected || ticket.Generation != LocalRunGeneration || ticket.Character != key.Character
            || ticket.ToScene != scene.name || !localEntries.TryGetValue(key, out var entry)
            || entry.Snapshot == null || entry.Revision != ticket.Revision)
        { RejectDestination("Local transition ticket/snapshot mismatch"); return; }
        if (ticket.OriginIncarnation == destinationOwner.LocalIncarnation)
        { destinationResult = RestoreResult.Applied; return; } // DDOL same root: retain live state, zero replay.
        destinationOwner.BeginLocalRestore(entry.Snapshot.Copy());
    }
    private void RejectDestination(string error)
    {
        destinationResult = RestoreResult.Rejected;
        Debug.LogWarning("[PlayerState] " + error + "; reward/pose remain pending");
    }
    internal bool AuthorizesLocalRestore(PlayerStateSnapshot snapshot) => localTicket != null && snapshot != null
        && !localTicket.Rejected && AllowsLocal(snapshot.Key) && snapshot.Revision == localTicket.Revision
        && snapshot.SourceScene == localTicket.FromScene && snapshot.Key.Character == localTicket.Character
        && IsLocalGameplayScene(localTicket.ToScene) && localTicket.ToScene == SceneManager.GetActiveScene().name
        && destinationOwner != null
        && destinationScene == SceneManager.GetActiveScene().handle
        && destinationOwner.transform == UniqueLocalRoot(SceneManager.GetActiveScene(), out _);
    public static RestoreResult QueryLocalRestore(Scene scene, out Transform root)
    {
        root = null;
        var service = Current;
        if (service == null || service.destinationScene != scene.handle || !service.destinationResolved) return RestoreResult.Pending;
        if (!UsesLocalRouting || !service.IsLocalActive) return RestoreResult.Rejected;
        root = service.destinationOwner != null ? service.destinationOwner.transform : null;
        if (root != null && root != UniqueLocalRoot(scene, out _)) { root = null; return RestoreResult.Rejected; }
        if (service.destinationResult != RestoreResult.Pending) return service.destinationResult;
        return service.destinationOwner != null ? service.destinationOwner.LocalRestoreResult : RestoreResult.Rejected;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (Current != null)
        {
            Current.EndSession();
            Current.EndLocalRun();
            Current.Detach();
            SceneManager.sceneLoaded -= Current.OnSceneLoaded;
            Destroy(Current.gameObject);
        }
        Current = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Current != null) return;
        var go = new GameObject("PlayerStateSessionStore");
        Current = go.AddComponent<PlayerStateSessionStore>();
        DontDestroyOnLoad(go);
    }
    private void Awake()
    {
        if (Current != null && Current != this) { Destroy(gameObject); return; }
        Current = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    public static PlayerStateSessionStore For(NetworkManager network)
    {
        if (network == null) return null;
        Install();
        Current.Observe(network);
        return Current;
    }
    private void Update()
    {
        Observe(NetworkManager.Singleton);
        if (State == SessionState.Active && !IsNetworkActive) EndSession();
        PollLocalDestination();
    }
    private void Observe(NetworkManager network)
    {
        if (ReferenceEquals(manager, network)) return;
        EndSession();
        Detach();
        manager = network;
        if (manager == null) return;
        manager.OnServerStarted += OnStarted;
        manager.OnClientStarted += OnStarted;
        manager.OnPreShutdown += EndSession;
        manager.OnServerStopped += OnStopped;
        manager.OnClientStopped += OnStopped;
        manager.OnClientDisconnectCallback += OnDisconnected;
        // Covers late service attachment after NGO has already started, not restart of an ended manager.
        if (manager.IsListening && !manager.ShutdownInProgress) OnStarted();
    }
    private void Detach()
    {
        if (manager == null) return;
        manager.OnServerStarted -= OnStarted;
        manager.OnClientStarted -= OnStarted;
        manager.OnPreShutdown -= EndSession;
        manager.OnServerStopped -= OnStopped;
        manager.OnClientStopped -= OnStopped;
        manager.OnClientDisconnectCallback -= OnDisconnected;
        manager = null;
    }
    private void OnStarted()
    {
        if (manager == null || !manager.IsListening || manager.ShutdownInProgress || State == SessionState.Active) return;
        entries.Clear();
        NetworkItems?.Clear();
        Generation = Guid.NewGuid().ToString("N");
        NetworkItems = new PlayerStateItemRegistry(Generation);
        State = SessionState.Active;
    }
    public void EndSession()
    {
        State = SessionState.Ending; // Latch FIRST, before despawn callbacks or cleanup.
        entries.Clear();
        NetworkItems?.Clear();
        NetworkItems = null;
    }
    private void OnStopped(bool wasHost) => EndSession();
    private void OnDisconnected(ulong id) { if (manager == null || id == manager.LocalClientId) EndSession(); }
    private void OnApplicationQuit() { quitting = true; EndLocalRun(); EndSession(); }
    private void OnDestroy()
    {
        if (Current != this) return;
        EndSession();
        EndLocalRun();
        Detach();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Current = null;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (scene.name != SourceScene && scene.name != DestinationScene) EndSession();
        else if (scene.name == SourceScene && (previousScene == SourceScene || previousScene == DestinationScene))
        { EndSession(); OnStarted(); } // Explicit new game run; never reuse last run's DTO.
        previousScene = scene.name;
        EnterLocalScene(scene);
    }
    public bool Allows(NetworkManager network, PlayerStateKey key) => IsNetworkActive && manager == network
        && key.SessionGeneration == Generation && key.OwnerClientId == manager.LocalClientId
        && (key.Character == PlayerStateCharacter.Hankyul || key.Character == PlayerStateCharacter.Yunseo);

    public bool TryGet(NetworkManager network, PlayerStateKey key, out PlayerStateSnapshot snapshot)
    {
        snapshot = null;
        if (!Allows(network, key) || !entries.TryGetValue(key, out var entry) || entry.Snapshot == null) return false;
        snapshot = entry.Snapshot.Copy();
        return true;
    }
    public void Invalidate(NetworkManager network, PlayerStateKey key, string error)
    {
        if (!Allows(network, key)) return;
        if (!entries.TryGetValue(key, out var entry)) entries.Add(key, entry = new Entry());
        entry.Revision++;
        entry.Snapshot = null; // Failed latest capture must NOT fall back to an old valid snapshot.
        entry.Error = error;
    }
    public bool TryStore(NetworkManager network, PlayerStateSnapshot snapshot)
    {
        if (snapshot == null || !Allows(network, snapshot.Key)) return false;
        if (snapshot.SchemaVersion != PlayerStateSnapshot.CurrentSchema || snapshot.SourceScene != SourceScene)
        { Invalidate(network, snapshot.Key, "Invalid capture metadata"); return false; }
        if (!entries.TryGetValue(snapshot.Key, out var entry)) entries.Add(snapshot.Key, entry = new Entry());
        var copy = snapshot.Copy();
        copy.Revision = ++entry.Revision;
        entry.Snapshot = copy;
        entry.Error = null;
        return true;
    }
}
