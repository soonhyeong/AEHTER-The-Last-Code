using UnityEngine;
using UnityEngine.SceneManagement;

public class PartSceneAutoBinder : MonoBehaviour
{
    [SerializeField] private bool bindOnStart = true;
    [SerializeField] private bool addTriggerColliderWhenMissing = true;
    [SerializeField] private Vector3 defaultTriggerSize = Vector3.one;
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubscription()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        subscribed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAutoBinding()
    {
        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        BindLoadedScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindLoadedScene();
    }

    private static void BindLoadedScene()
    {
        PartSceneAutoBinder binder = FindObjectOfType<PartSceneAutoBinder>();
        if (binder != null)
        {
            binder.BindSceneObjects();
            return;
        }

        GameObject helper = new GameObject("PartSceneAutoBinder_Runtime");
        PartSceneAutoBinder runtimeBinder = helper.AddComponent<PartSceneAutoBinder>();
        runtimeBinder.bindOnStart = false;
        runtimeBinder.BindSceneObjects();
        Destroy(helper);
    }

    private void Start()
    {
        if (bindOnStart)
            BindSceneObjects();
    }

    [ContextMenu("Bind Scene Objects")]
    public void BindSceneObjects()
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            string normalized = ItemNameRuleUtility.NormalizeName(target.name);
            InteractableItem pickupItem = target.GetComponent<InteractableItem>();
            ItemData itemData = pickupItem != null ? pickupItem.itemData : null;

            bool isModule = ItemNameRuleUtility.IsModuleItem(normalized)
                || ItemNameRuleUtility.IsModuleObject(target, itemData);
            bool isPaper = ItemNameRuleUtility.IsPaperObject(normalized)
                || ItemNameRuleUtility.IsPaperObject(target, itemData);

            if (isPaper)
                EnsurePaper(target, itemData, pickupItem);
            else if (isModule)
                EnsureModule(target, itemData, pickupItem);
        }

        EnsureRuntimePlayerParts();
    }

    private void EnsureModule(GameObject target, ItemData itemData, InteractableItem existingPickup)
    {
        EnsureCollider(target);

        if (existingPickup != null)
            existingPickup.enabled = false;

        ModulePickupInteractable pickup = target.GetComponent<ModulePickupInteractable>();
        if (pickup == null)
        {
            pickup = target.AddComponent<ModulePickupInteractable>();
            Debug.Log($"[PartSceneAutoBinder] Bound module pickup: {target.name}", target);
        }

        if (pickup != null)
            pickup.Configure(itemData);
        else
            Debug.LogWarning($"[PartSceneAutoBinder] Failed to add ModulePickupInteractable: {target.name}", target);

        ModuleWorldMarker marker = target.GetComponent<ModuleWorldMarker>();
        if (marker == null)
            marker = target.AddComponent<ModuleWorldMarker>();

        if (marker != null)
            marker.Configure(itemData);
    }

    private void EnsurePaper(GameObject target, ItemData itemData, InteractableItem existingPickup)
    {
        EnsureCollider(target);

        PaperInteractable paper = target.GetComponentInParent<PaperInteractable>();
        if (paper == null)
            paper = target.AddComponent<PaperInteractable>();
        if (paper != null)
            paper.Configure(itemData);

        if (target.GetComponentInParent<ProximityHoldPrompt>() == null)
            target.AddComponent<ProximityHoldPrompt>();

        if (existingPickup != null)
            existingPickup.enabled = false;
    }

    private void EnsureCollider(GameObject target)
    {
        if (!addTriggerColliderWhenMissing)
            return;

        Collider existing = target.GetComponentInChildren<Collider>(true);
        if (existing != null)
            return;

        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = defaultTriggerSize;
    }

    private void EnsureRuntimePlayerParts()
    {
        // World pickups above are independent; only local user-facing Parts binding is installed here.
        // Existing remote gameplay/representation components are left intact.
        if (LocalPlayerBindingResolver.TryResolve(null, out var root))
            PartsSlot2GlobalBootstrap.EnsureForPlayer(root);
    }
}
