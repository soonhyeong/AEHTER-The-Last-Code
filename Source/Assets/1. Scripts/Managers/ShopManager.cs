using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class ShopManager : MonoBehaviour
{
    public enum ShopItemType
    {
        Food,
        Drink,
        EnergyCharge,
        EnergyPack,
        ShieldCharge,
        ShieldPack
    }

    [System.Serializable]
    public class ShopItemEntry
    {
        [Header("Item Info")]
        public string displayName;
        public ShopItemType itemType;
        public int cost = 10;
        public ItemData itemData;

        [Header("UI")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;
        public Button button;
    }

    [Header("Shop Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button closeShopButton;

    [Header("Confirm Panel")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TextMeshProUGUI confirmMessageText;
    [SerializeField] private Button confirmBuyButton;
    [SerializeField] private Button confirmCancelButton;

    [Header("Result Message")]
    [SerializeField] private TextMeshProUGUI resultMessageText;

    [Header("Interaction Panel")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private Image interactionIconImage;
    [SerializeField] private Sprite interactionIconSprite;
    [SerializeField] private string interactionMessage = "F - Shop";

    [Header("Shop Items")]
    [SerializeField] private ShopItemEntry[] shopItems;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    private GameObject currentPlayerInRange;
    private ShopItemEntry currentSelectedItem;
    private bool isShopOpen;
    private bool isResultMessageVisible;
    private int resultMessageShownFrame = -1;

    private Hankyul_Move cachedHankyulMove;
    private Move_Hankyul cachedMoveHankyul;
    private Yunseo_Move cachedYunseoMove;
    private bool cachedHankyulAllowLook;
    private bool cachedMoveHankyulAllowLook;
    private bool cachedYunseoAllowLook;
    private bool hasCachedHankyulMove;
    private bool hasCachedMoveHankyul;
    private bool hasCachedYunseoMove;

    private void Start()
    {
        InitializeShopItems();
        InitializeConfirmUI();
        InitializeCloseButton();
        InitializeInteractionPanel();
        CloseShopImmediate();
    }

    private void Update()
    {
        if (isShopOpen && TryDismissResultMessage())
            return;

        if (isShopOpen)
            ApplyShopCursorState(true);

        if (isShopOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
            return;
        }

        if (currentPlayerInRange == null) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (isShopOpen)
                CloseShop();
            else
                OpenShop();
        }
    }

    private void InitializeShopItems()
    {
        if (shopItems == null) return;

        for (int i = 0; i < shopItems.Length; i++)
        {
            ShopItemEntry item = shopItems[i];
            if (item == null) continue;

            if (item.nameText != null)
            {
                item.nameText.text = string.IsNullOrEmpty(item.displayName)
                    ? GetItemDisplayName(item.itemType)
                    : item.displayName;
            }

            if (item.costText != null)
                item.costText.text = item.cost.ToString();

            if (item.button != null)
            {
                int index = i;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => OnClickItemButton(shopItems[index]));
            }
        }
    }

    private void InitializeConfirmUI()
    {
        if (confirmBuyButton != null)
        {
            confirmBuyButton.onClick.RemoveAllListeners();
            confirmBuyButton.onClick.AddListener(OnClickConfirmBuy);
        }

        if (confirmCancelButton != null)
        {
            confirmCancelButton.onClick.RemoveAllListeners();
            confirmCancelButton.onClick.AddListener(OnClickConfirmCancel);
        }

        CloseConfirm();
    }

    private void InitializeCloseButton()
    {
        if (closeShopButton != null)
        {
            closeShopButton.onClick.RemoveAllListeners();
            closeShopButton.onClick.AddListener(CloseShop);
        }
    }

    private void InitializeInteractionPanel()
    {
        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        if (interactionText != null)
            interactionText.text = interactionMessage;

        if (interactionIconImage != null)
        {
            interactionIconImage.sprite = interactionIconSprite;
            interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
        }
    }

    private void OnClickItemButton(ShopItemEntry item)
    {
        if (!isShopOpen) return;
        if (item == null) return;

        OpenConfirm(item);
    }

    private void OnClickConfirmBuy()
    {
        if (currentSelectedItem == null)
        {
            CloseConfirm();
            return;
        }

        ShopItemEntry selectedItem = currentSelectedItem;
        CloseConfirm();
        TryBuy(selectedItem);
    }

    private void OnClickConfirmCancel()
    {
        CloseConfirm();
    }

    private void OpenShop()
    {
        isShopOpen = true;

        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        CachePlayerLookState();
        ClearResultMessage();
        CloseConfirm();
        SetShopInputState(true);
    }

    public void OpenShopFromInteractor(GameObject player)
    {
        if (player != null)
            currentPlayerInRange = player;

        if (!isShopOpen)
            OpenShop();
    }

    public void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        CloseConfirm();
        ClearResultMessage();
        SetShopInputState(false);

        if (currentPlayerInRange != null && interactionPanel != null)
        {
            interactionPanel.SetActive(true);

            if (interactionText != null)
                interactionText.text = interactionMessage;

            if (interactionIconImage != null)
            {
                interactionIconImage.sprite = interactionIconSprite;
                interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
            }
        }
    }

    private void CloseShopImmediate()
    {
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        ClearResultMessage();
        currentSelectedItem = null;
        SetShopInputState(false);
    }

    private void OpenConfirm(ShopItemEntry item)
    {
        currentSelectedItem = item;

        if (confirmPanel != null)
            confirmPanel.SetActive(true);

        if (confirmMessageText != null)
        {
            string itemName = string.IsNullOrEmpty(item.displayName)
                ? GetItemDisplayName(item.itemType)
                : item.displayName;

            confirmMessageText.text = $"{itemName}을(를) {item.cost} 재화로 구매하시겠습니까?";
        }
    }

    private void CloseConfirm()
    {
        currentSelectedItem = null;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    private void TryBuy(ShopItemEntry item)
    {
        if (item == null) return;
        if (currentPlayerInRange == null) return;
        if (GlobalManager.Currency == null) return;

        ClearResultMessage();

        if (!GlobalManager.Currency.SpendGold(item.cost))
        {
            ShowResultMessage("재화가 부족합니다.");
            return;
        }

        string failMessage;
        bool success = ApplyPurchase(item, out failMessage);

        if (!success)
        {
            GlobalManager.Currency.AddGold(item.cost);
            ShowResultMessage(string.IsNullOrEmpty(failMessage) ? "구매에 실패했습니다." : failMessage);
            return;
        }

        ShowResultMessage("구매 완료");
    }

    private bool ApplyPurchase(ShopItemEntry item, out string message)
    {
        message = "";

        switch (item.itemType)
        {
            case ShopItemType.Food:
            case ShopItemType.Drink:
            case ShopItemType.EnergyPack:
            case ShopItemType.ShieldPack:
                return AddItemToCurrentPlayerInventory(item.itemData, 1, out message);

            case ShopItemType.EnergyCharge:
                return RechargeEnergyCharge(out message);

            case ShopItemType.ShieldCharge:
                return RechargeShieldCharge(out message);
        }

        message = "구매할 수 없는 품목입니다.";
        return false;
    }

    private bool AddItemToCurrentPlayerInventory(ItemData itemData, int amount, out string message)
    {
        message = "";

        if (currentPlayerInRange == null || itemData == null)
        {
            message = "아이템 정보를 찾을 수 없습니다.";
            return false;
        }

        PlayerInventory inventory = currentPlayerInRange.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = currentPlayerInRange.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
        {
            message = "인벤토리를 찾을 수 없습니다.";
            return false;
        }

        bool added = inventory.AddItem(itemData, amount);
        if (!added)
        {
            message = "인벤토리가 가득 찼습니다.";
            return false;
        }

        return true;
    }

    private bool RechargeEnergyCharge(out string message)
    {
        message = "";

        Hankyul_Fire fire = currentPlayerInRange.GetComponent<Hankyul_Fire>();
        if (fire == null)
            fire = currentPlayerInRange.GetComponentInParent<Hankyul_Fire>();

        if (fire == null)
        {
            message = "한결만 구매할 수 있습니다.";
            return false;
        }

        return fire.TryBuyEnergyCharge(out message);
    }

    private bool RechargeShieldCharge(out string message)
    {
        message = "";

        PlayerStatus status = currentPlayerInRange.GetComponent<PlayerStatus>();
        if (status == null)
            status = currentPlayerInRange.GetComponentInParent<PlayerStatus>();

        if (status == null)
        {
            message = "상태 정보를 찾을 수 없습니다.";
            return false;
        }

        return status.TryBuyShieldCharge(out message);
    }

    private string GetItemDisplayName(ShopItemType itemType)
    {
        switch (itemType)
        {
            case ShopItemType.Food: return "Food";
            case ShopItemType.Drink: return "Water";
            case ShopItemType.EnergyCharge: return "Energy Charge";
            case ShopItemType.EnergyPack: return "Energy Pack";
            case ShopItemType.ShieldCharge: return "Shield Charge";
            case ShopItemType.ShieldPack: return "Shield Pack";
        }

        return "Item";
    }

    private void ShowResultMessage(string message)
    {
        if (resultMessageText != null)
            resultMessageText.text = message;

        isResultMessageVisible = !string.IsNullOrEmpty(message);
        resultMessageShownFrame = Time.frameCount;
    }

    private void ClearResultMessage()
    {
        if (resultMessageText != null)
            resultMessageText.text = "";

        isResultMessageVisible = false;
        resultMessageShownFrame = -1;
    }

    private void SetShopInputState(bool open)
    {
        ApplyShopCursorState(open);
        SetPlayerLookEnabled(!open);
    }

    private void ApplyShopCursorState(bool open)
    {
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        UIInteractionRuntimeGuard.SetCursorVisibleRequest("ShopManager", open);

        if (open)
            UIInteractionRuntimeGuard.EnsureInteractive(shopPanel != null ? shopPanel : gameObject);
    }

    private bool TryDismissResultMessage()
    {
        if (!isResultMessageVisible) return false;
        if (Time.frameCount <= resultMessageShownFrame) return false;
        if (!HasDismissMessageInput()) return false;

        ClearResultMessage();
        return true;
    }

    private bool HasDismissMessageInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return Input.anyKeyDown || Input.GetMouseButtonDown(0);
    }

    private void CachePlayerLookState()
    {
        hasCachedHankyulMove = false;
        hasCachedMoveHankyul = false;
        hasCachedYunseoMove = false;
        cachedHankyulMove = null;
        cachedMoveHankyul = null;
        cachedYunseoMove = null;

        if (currentPlayerInRange == null) return;

        cachedHankyulMove = currentPlayerInRange.GetComponent<Hankyul_Move>();
        if (cachedHankyulMove == null)
            cachedHankyulMove = currentPlayerInRange.GetComponentInParent<Hankyul_Move>();

        if (cachedHankyulMove != null)
        {
            cachedHankyulAllowLook = cachedHankyulMove.allowLook;
            hasCachedHankyulMove = true;
        }

        cachedMoveHankyul = currentPlayerInRange.GetComponent<Move_Hankyul>();
        if (cachedMoveHankyul == null)
            cachedMoveHankyul = currentPlayerInRange.GetComponentInParent<Move_Hankyul>();

        if (cachedMoveHankyul != null)
        {
            cachedMoveHankyulAllowLook = cachedMoveHankyul.allowLook;
            hasCachedMoveHankyul = true;
        }

        cachedYunseoMove = currentPlayerInRange.GetComponent<Yunseo_Move>();
        if (cachedYunseoMove == null)
            cachedYunseoMove = currentPlayerInRange.GetComponentInParent<Yunseo_Move>();

        if (cachedYunseoMove != null)
        {
            cachedYunseoAllowLook = cachedYunseoMove.allowLook;
            hasCachedYunseoMove = true;
        }
    }

    private void SetPlayerLookEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (hasCachedHankyulMove && cachedHankyulMove != null)
                cachedHankyulMove.allowLook = false;

            if (hasCachedMoveHankyul && cachedMoveHankyul != null)
                cachedMoveHankyul.allowLook = false;

            if (hasCachedYunseoMove && cachedYunseoMove != null)
                cachedYunseoMove.allowLook = false;

            return;
        }

        if (hasCachedHankyulMove && cachedHankyulMove != null)
            cachedHankyulMove.allowLook = cachedHankyulAllowLook;

        if (hasCachedMoveHankyul && cachedMoveHankyul != null)
            cachedMoveHankyul.allowLook = cachedMoveHankyulAllowLook;

        if (hasCachedYunseoMove && cachedYunseoMove != null)
            cachedYunseoMove.allowLook = cachedYunseoAllowLook;

        hasCachedHankyulMove = false;
        hasCachedMoveHankyul = false;
        hasCachedYunseoMove = false;
        cachedHankyulMove = null;
        cachedMoveHankyul = null;
        cachedYunseoMove = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        currentPlayerInRange = other.gameObject;

        if (!isShopOpen && interactionPanel != null)
        {
            interactionPanel.SetActive(true);

            if (interactionText != null)
                interactionText.text = interactionMessage;

            if (interactionIconImage != null)
            {
                interactionIconImage.sprite = interactionIconSprite;
                interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (currentPlayerInRange == other.gameObject)
        {
            CloseShop();
            currentPlayerInRange = null;
        }

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        if (interactionIconImage != null)
            interactionIconImage.gameObject.SetActive(false);
    }
}
