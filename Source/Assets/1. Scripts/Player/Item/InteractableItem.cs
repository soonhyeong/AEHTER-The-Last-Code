using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour, IInteractable, IPickupInteractable
{
    [Header("아이템 데이터")]
    public ItemData itemData;
    public int amount = 1;              // 한 번에 줍는 개수 (기본 1개)
    private bool pickupInProgress;
    private bool pickupCommitted;

    private void Reset()
    {
        // 아이템은 트리거 콜라이더로 사용하는 것을 기본값으로
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public string GetDisplayName()
    {
        if (itemData != null && !string.IsNullOrEmpty(itemData.displayName))
            return itemData.displayName;

        return gameObject.name;
    }

    public Sprite GetIcon()
    {
        if (itemData != null)
            return itemData.itemSprite;

        return null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (pickupInProgress || pickupCommitted)
            return;

        if (interactor == null || interactor.Inventory == null || itemData == null)
            return;

        pickupInProgress = true;
        try
        {
            if (!interactor.Inventory.AddItem(itemData, amount))
                return;

            pickupCommitted = true;
            Debug.Log($"{GetDisplayName()} {amount}개를 획득했습니다.");
            Destroy(gameObject);
        }
        finally
        {
            pickupInProgress = false;
        }
    }
}
