using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShopInteractable : MonoBehaviour, IInteractable
{
    public ShopManager shopManager;
    public string displayName = "Shop";
    public Sprite icon;

    public string GetDisplayName()
    {
        return displayName;
    }

    public Sprite GetIcon()
    {
        return icon;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null || shopManager == null)
            return;

        shopManager.OpenShopFromInteractor(interactor.gameObject);
    }
}
