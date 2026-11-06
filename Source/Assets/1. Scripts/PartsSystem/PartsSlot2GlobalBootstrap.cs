using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PartsSlot2GlobalBootstrap : MonoBehaviour
{
    private const string PrefabPath = "AETHERParts/PartsSlot2_EquipmentCanvas";
    private const string SceneInstanceName = "PartsSlot2_PlayerEquipmentCanvas";
    private static PartsSlot2GlobalBootstrap monitor;
    private bool isMonitor;
    private float nextCheck;
    private int standaloneMisses;
    private bool warned;
    private GameObject currentUI;
    private Transform boundRoot;
    private PlayerInventory boundInventory;
    private EquipmentManager boundEquipment;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleStaticSceneLoaded;
        monitor = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAfterSceneLoad()
    {
        SceneManager.sceneLoaded -= HandleStaticSceneLoaded;
        SceneManager.sceneLoaded += HandleStaticSceneLoaded;
        if (monitor == null)
        {
            var helper = new GameObject("PartsLocalBindingMonitor");
            monitor = helper.AddComponent<PartsSlot2GlobalBootstrap>();
            monitor.isMonitor = true;
            DontDestroyOnLoad(helper);
        }
        monitor.standaloneMisses = 0;
        monitor.PollBinding();
    }

    private static void HandleStaticSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (monitor == null) EnsureAfterSceneLoad();
        else { monitor.standaloneMisses = 0; monitor.PollBinding(); }
    }

    private void Update()
    {
        if (!isMonitor || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        var manager = Unity.Netcode.NetworkManager.Singleton;
        // NGO query is constant-time and keeps waiting for delayed spawn/respawn.
        // Standalone no-player/ambiguous discovery has a bounded retry window per Scene.
        if (manager != null && manager.IsListening || standaloneMisses < 20)
            PollBinding();
    }

    private void PollBinding()
    {
        if (!LocalPlayerBindingResolver.TryResolve(null, out var root))
        {
            standaloneMisses++;
            if (!warned)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager == null || !manager.IsListening)
                    Debug.LogWarning("[Parts binding] No unique active current Player. Supply an explicit owner or retry after Scene load.");
                warned = true;
            }
            if (currentUI != null) currentUI.SetActive(false);
            currentUI = null;
            PartSystemRuntimeState.ClearLiveBinding();
            return;
        }
        standaloneMisses = 0;
        warned = false;
        if (currentUI != null && LocalPlayerBindingResolver.PlayerRoot(currentUI.transform) == root
            && currentUI.activeInHierarchy) return;
        if (currentUI != null) currentUI.SetActive(false);
        PartSystemRuntimeState.ClearLiveBinding();
        currentUI = EnsureScenePlayerUI();
    }

    public static GameObject EnsureScenePlayerUI()
    {
        if (!LocalPlayerBindingResolver.TryResolve(null, out var root)) return null;
        return EnsureForPlayer(root);
    }

    public static GameObject EnsureForPlayer(Transform root)
    {
        if (root == null || !LocalPlayerBindingResolver.TryResolve(root, out var resolved, false) || resolved != root)
            return null;
        if (!EnsurePlayerComponents(root)) return null;
        EnsureEventSystem();
        Transform existing = root.Find(SceneInstanceName);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            var bootstrap = existing.GetComponent<PartsSlot2GlobalBootstrap>();
            if (bootstrap != null) bootstrap.BindToCurrentScene();
            return existing.gameObject;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[PartsSlot2GlobalBootstrap] Parts slot2 prefab was not found in Resources/AETHERParts.");
            return null;
        }
        // Parent is supplied to Instantiate: Awake/OnEnable can see the owner immediately.
        GameObject ui = Instantiate(prefab, root, false);
        ui.name = SceneInstanceName;
        var added = ui.GetComponent<PartsSlot2GlobalBootstrap>();
        if (added == null) added = ui.AddComponent<PartsSlot2GlobalBootstrap>();
        added.BindToCurrentScene();
        return ui;
    }

    public static bool EnsurePlayerComponents(Transform root)
    {
        if (root == null || !LocalPlayerBindingResolver.TryResolve(root, out var local, false) || local != root) return false;
        var inventory = LocalPlayerBindingResolver.Owned<PlayerInventory>(root);
        var status = LocalPlayerBindingResolver.Owned<PlayerStatus>(root);
        if (inventory == null || status == null) return false;
        var equipment = LocalPlayerBindingResolver.Owned<EquipmentManager>(root);
        if (equipment == null) equipment = root.gameObject.AddComponent<EquipmentManager>();
        equipment.SetInventory(inventory);
        PartSystemRuntimeState.BindInventory(inventory);
        PartSystemRuntimeState.BindEquipment(equipment);
        if (root.GetComponent<Move_Hankyul>() != null)
        {
            var ability = root.GetComponent<HangyeolPartAbilityController>();
            if (ability == null) ability = root.gameObject.AddComponent<HangyeolPartAbilityController>();
            ability.RefreshSuperJumpState();
        }
        else if (root.GetComponent<Move_Yunseo>() != null)
        {
            if (root.GetComponent<YunseoPartAbilityController>() == null)
                root.gameObject.AddComponent<YunseoPartAbilityController>();
            if (root.GetComponent<YunseoEquipmentRuntimeBridge>() == null)
                root.gameObject.AddComponent<YunseoEquipmentRuntimeBridge>();
        }
        return true;
    }

    private void OnEnable() { if (!isMonitor) BindToCurrentScene(); }
    private void OnDisable()
    {
        foreach (var controller in GetComponentsInChildren<EquipmentUIController>(true))
            controller.SetSourceInventory(null);
        boundRoot = null;
        boundInventory = null;
        boundEquipment = null;
        if (isMonitor) PartSystemRuntimeState.ClearLiveBinding();
    }

    public void BindToCurrentScene()
    {
        Transform owner = LocalPlayerBindingResolver.PlayerRoot(this);
        if (owner == null || !LocalPlayerBindingResolver.TryResolve(owner, out var root, false) || root != owner) return;
        var inventory = LocalPlayerBindingResolver.Owned<PlayerInventory>(root);
        var equipment = LocalPlayerBindingResolver.Owned<EquipmentManager>(root);
        if (boundRoot == root && boundInventory == inventory && boundEquipment == equipment && equipment != null) return;
        if (!EnsurePlayerComponents(root)) return;
        boundRoot = root;
        boundInventory = LocalPlayerBindingResolver.Owned<PlayerInventory>(root);
        boundEquipment = LocalPlayerBindingResolver.Owned<EquipmentManager>(root);
        foreach (var controller in GetComponentsInChildren<EquipmentUIController>(true))
        {
            controller.SetSourceInventory(boundInventory);
            if (controller.GetComponent<PartsSlot2EquipmentBridge>() == null)
                controller.gameObject.AddComponent<PartsSlot2EquipmentBridge>();
        }
        var toggle = GetComponent<PartsSlot2ToggleController>();
        if (toggle == null) toggle = gameObject.AddComponent<PartsSlot2ToggleController>();
        toggle.RefreshPlayerReferences();
    }

    public static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (Application.isPlaying) DontDestroyOnLoad(eventSystem);
    }
}
