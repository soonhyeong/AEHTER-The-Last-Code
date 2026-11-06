using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ModulePickupInteractable : MonoBehaviour, IInteractable, IPickupInteractable
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;
    [SerializeField] private bool destroyOnPickup = true;

    private ItemData runtimeItemData;
    private bool pickupInProgress;
    private bool pickupCommitted;

    public void Configure(ItemData data, int pickupAmount = 1)
    {
        if (data != null)
            itemData = data;

        amount = Mathf.Max(1, pickupAmount);
    }

    private void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    public string GetDisplayName()
    {
        ItemData data = ResolveItemData();
        string name = ItemNameRuleUtility.GetPickupDisplayName(gameObject, data);
        return string.IsNullOrWhiteSpace(name) ? "Parts Module" : name;
    }

    public Sprite GetIcon()
    {
        ItemData data = ResolveItemData();
        return data != null ? data.itemSprite : null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (pickupInProgress || pickupCommitted)
            return;

        if (interactor == null || interactor.Inventory == null)
        {
            Debug.LogWarning($"[ModulePickup] Inventory missing: {name}", this);
            return;
        }

        ItemData data = ResolveItemData();
        if (data == null)
        {
            Debug.LogWarning($"[ModulePickup] ItemData missing: {name}", this);
            return;
        }

        if (!ItemNameRuleUtility.IsModuleObject(gameObject, data))
        {
            Debug.LogWarning($"[ModulePickup] Not a module item: {ItemNameRuleUtility.GetBestItemName(gameObject, data)}", this);
            return;
        }

        string itemName = ItemNameRuleUtility.GetBestItemName(gameObject, data);
        Debug.Log($"[ModulePickup] Try pickup: {itemName}", this);

        pickupInProgress = true;
        try
        {
            if (!interactor.Inventory.AddItem(data, Mathf.Max(1, amount)))
            {
                Debug.LogWarning($"[ModulePickup] AddItem failed: {itemName}", this);
                return;
            }

            pickupCommitted = true;
            Debug.Log($"[ModulePickup] AddItem success: {itemName}");

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
        finally
        {
            pickupInProgress = false;
        }
    }

    private ItemData ResolveItemData()
    {
        if (itemData != null)
            return itemData;

        itemData = PartResourceResolver.FindItemData(gameObject.name);
        if (itemData != null)
            return itemData;

        if (runtimeItemData != null)
        {
            PlayerStateItemRegistry.RegisterRuntimeFallback(runtimeItemData);
            return runtimeItemData;
        }

        runtimeItemData = ScriptableObject.CreateInstance<ItemData>();
        string normalizedName = ItemNameRuleUtility.NormalizeName(gameObject.name);
        runtimeItemData.id = normalizedName;
        runtimeItemData.displayName = string.IsNullOrWhiteSpace(normalizedName) ? gameObject.name : normalizedName;
        runtimeItemData.description = "Auto-detected parts crafting module.";
        PlayerStateItemRegistry.RegisterRuntimeFallback(runtimeItemData);
        return runtimeItemData;
    }
}
