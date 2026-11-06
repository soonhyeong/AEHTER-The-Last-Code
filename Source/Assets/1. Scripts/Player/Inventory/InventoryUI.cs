using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("참조")]
    public PlayerInventory inventory;
    public GameObject inventoryPanel;
    public Transform slotsParent;
    public InventoryDetailUI detailUI;
    public PlayerStatus playerStatus;
    public Hankyul_Fire hankyulFire;
    public Yunseo_Move playerMoveScript;
    public Move_Hankyul hankyulMoveScript;

    [Header("버튼들")]
    public Button useButton;
    public Button dropButton;

    [Header("버리기 패널(2단계 구조)")]
    public GameObject dropPanel;
    public TextMeshProUGUI dropTitleText;
    public GameObject dropAmountGroup;
    public TMP_InputField dropAmountInput;

    private readonly List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private bool isOpen = false;
    private int selectedIndex = -1;
    private bool dropAmountMode = false;
    private PlayerModalControlContext.Lease modalLease;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private readonly LocalPlayerUIBinding dataBinding = new LocalPlayerUIBinding();
    private PlayerInventory subscribedInventory;
    private bool slotsInitialized;

    private void Awake()
    {
        RefreshDataBinding(true);

        ModernInventorySkin.Apply(this);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        if (dropPanel != null)
            dropPanel.SetActive(false);

        CacheSlotsAndInitialize();
        slotsInitialized = true;
        RefreshAllSlots();

        if (detailUI != null)
            detailUI.Clear();

        UpdateButtons();
    }

    private void OnDestroy()
    {
        CloseInventory(false);
        ReleaseDataBinding();
    }

    private void OnEnable() { RefreshDataBinding(true); }

    private void ReleaseDataBinding()
    {
        if (!ReferenceEquals(subscribedInventory, null)) subscribedInventory.OnInventoryChanged -= RefreshAllSlots;
        subscribedInventory = null;
        inventory = null; playerStatus = null; hankyulFire = null;
        hankyulMoveScript = null; playerMoveScript = null;
        dataBinding.Clear();
        RebindSlotData();
    }

    private void RefreshDataBinding(bool force = false)
    {
        bool changed = dataBinding.Poll(this, inventory != null ? (Component)inventory : playerStatus, force);
        if (!changed && !force && !dataBinding.SourceChanged && !(dataBinding.Queried && dataBinding.Root != null && (inventory == null || playerStatus == null))) return;
        if (changed) CloseInventory(false);
        var root = dataBinding.Root;
        var next = LocalPlayerBindingResolver.Owned<PlayerInventory>(root);
        if (!ReferenceEquals(subscribedInventory, next))
        {
            if (!ReferenceEquals(subscribedInventory, null)) subscribedInventory.OnInventoryChanged -= RefreshAllSlots;
            subscribedInventory = next;
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += RefreshAllSlots;
        }
        inventory = next;
        playerStatus = LocalPlayerBindingResolver.Owned<PlayerStatus>(root);
        hankyulMoveScript = root != null ? root.GetComponent<Move_Hankyul>() : null;
        playerMoveScript = LocalPlayerBindingResolver.Owned<Yunseo_Move>(root);
        hankyulFire = hankyulMoveScript != null ? LocalPlayerBindingResolver.Owned<Hankyul_Fire>(root) : null;
        if (slotsInitialized) RebindSlotData();
    }

    private void RebindSlotData()
    {
        selectedIndex = -1;
        if (detailUI != null) detailUI.Clear();
        for (int i = 0; i < slotUIs.Count; i++)
        {
            var slot = slotUIs[i];
            if (slot == null) continue;
            slot.Initialize(inventory, i, this);
            if (inventory == null)
            {
                if (slot.iconImage != null) { slot.iconImage.sprite = null; slot.iconImage.enabled = false; }
                if (slot.countText != null) slot.countText.text = string.Empty;
            }
        }
        UpdateButtons();
    }

    private void CacheSlotsAndInitialize()
    {
        slotUIs.Clear();

        if (slotsParent == null)
        {
            Debug.LogError("InventoryUI: slotsParent가 설정되지 않았습니다.");
            return;
        }

        var found = slotsParent.GetComponentsInChildren<InventorySlotUI>(true);
        if (found.Length == 0)
        {
            Debug.LogWarning("InventoryUI: InventorySlotUI를 찾지 못했습니다.");
            return;
        }

        if (inventory != null)
            inventory.slotCount = found.Length;

        for (int i = 0; i < found.Length; i++)
        {
            var slotUI = found[i];
            slotUIs.Add(slotUI);
            slotUI.Initialize(inventory, i, this);
        }
    }

    public void RefreshAllSlots()
    {
        RefreshDataBinding();
        foreach (var s in slotUIs)
            s.Refresh();

        if (selectedIndex >= 0)
            OnSlotClicked(selectedIndex, false);
    }

    private void Update()
    {
        RefreshDataBinding();
        if (!CanReadLocalInput()) return;
        bool iPressed = false;
        var kb = Keyboard.current;
        if (kb != null)
            iPressed = kb.iKey.wasPressedThisFrame;
        else
            iPressed = Input.GetKeyDown(KeyCode.I);

        if (iPressed)
            ToggleInventoryPanel();

        if (isOpen && (inventoryPanel == null || !inventoryPanel.activeInHierarchy
            || modalLease == null || modalLease.IsReleased)) CloseInventory(false);
    }

    public void ToggleInventoryPanel()
    {
        if (!CanReadLocalInput()) return;
        // Preserve the existing I-key nested-open policy; always allow this owner's Close.
        if (isOpen) { CloseInventory(true); return; }
        if (!isActiveAndEnabled || inventoryPanel == null) return;
        var context = ResolvePlayerContext();
        if (context == null) return;
        modalLease = context.Acquire(this);
        if (modalLease == null || modalLease.IsReleased) return;
        isOpen = true;
        try
        {
            inventoryPanel.SetActive(true);
            if (!PlayerLocalInputOwnership.CanConsumeLocalInput(context) || !isOpen || !isActiveAndEnabled || !inventoryPanel.activeInHierarchy)
            { CloseInventory(false); return; }
            UIInteractionRuntimeGuard.EnsureInteractive(inventoryPanel);
            RefreshAllSlots();
            if (GlobalManager.Audio != null) GlobalManager.Audio.PlaySFX("inventory_Open");
        }
        catch { CloseInventory(false); throw; }
    }

    private void CloseInventory(bool playSound)
    {
        bool wasOpen = isOpen;
        isOpen = false;
        try
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (wasOpen)
            {
                ClearSelection();
                if (detailUI != null) detailUI.Clear();
                CloseDropPanel();
            }
        }
        finally { modalLease?.Release(); modalLease = null; }
        if (wasOpen && playSound && GlobalManager.Audio != null) GlobalManager.Audio.PlaySFX("inventory_Close");
    }

    private void OnDisable() { CloseInventory(false); ReleaseDataBinding(); }

    private PlayerModalControlContext ResolvePlayerContext()
    {
        var parent = PlayerModalControlContext.For(gameObject, true);
        if (parent != null) return parent;
        GameObject player = inventory != null ? inventory.gameObject
            : hankyulMoveScript != null ? hankyulMoveScript.gameObject
            : playerMoveScript != null ? playerMoveScript.gameObject : gameObject;
        return PlayerModalControlContext.For(player, true);
    }

    private bool CanReadLocalInput()
    {
        RefreshDataBinding();
        if (dataBinding.Root == null) return false;
        if (localInputGate.CanRead(ResolvePlayerContext())) return true;
        // Lifecycle cleanup, not a remote I-key Close. Item data and other leases survive.
        if (isOpen || modalLease != null) CloseInventory(false);
        return false;
    }

    public void OnClickCloseButton()
    {
        if (isOpen)
            ToggleInventoryPanel();
    }

    public void OnSlotClicked(int index)
    {
        if (!CanReadLocalInput()) return;
        GlobalManager.Audio.PlaySFX("select");
        OnSlotClicked(index, true);
    }

public bool OnSlotDoubleClicked(int index)
    {
        if (!CanReadLocalInput()) return false;
        if (inventory == null)
            return false;

        var slot = inventory.GetSlot(index);
        if (slot == null || slot.item == null || slot.count <= 0)
            return false;

        if (!IsPaperItem(slot.item))
            return false;

        selectedIndex = index;
        OnSlotClicked(index, false);
        PaperInventoryPreviewUI.Toggle(slot.item, PlayerModalControlContext.For(inventory.gameObject, true));
        return true;
    }

    private static bool IsPaperItem(ItemData item)
    {
        if (item == null)
            return false;

        return ItemNameRuleUtility.IsPaperObject(item.id)
            || ItemNameRuleUtility.IsPaperObject(item.displayName)
            || ItemNameRuleUtility.IsPaperObject(item.name);
    }


    private void OnSlotClicked(int index, bool fromClick)
    {
        selectedIndex = index;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            bool selected = (i == selectedIndex);
            slotUIs[i].SetSelected(selected);
        }

        if (inventory == null || detailUI == null)
        {
            UpdateButtons();
            return;
        }

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
        {
            detailUI.Clear();
        }
        else
        {
            detailUI.ShowItem(slot.item);
        }

        UpdateButtons();
    }

    private void ClearSelection()
    {
        selectedIndex = -1;
        foreach (var s in slotUIs)
            s.SetSelected(false);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasItem = false;
        bool usable = false;

        if (inventory != null && selectedIndex >= 0)
        {
            var slot = inventory.GetSlot(selectedIndex);
            hasItem = (slot != null && slot.item != null && slot.count > 0);

            if (hasItem && slot.item.effects != null && slot.item.effects.Length > 0)
                usable = true;
        }

        if (useButton != null) useButton.interactable = hasItem && usable;
        if (dropButton != null) dropButton.interactable = hasItem;
    }

    public void OnClickUseButton()
    {
        if (!CanReadLocalInput()) return;
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;
        if (slot.item.effects == null || slot.item.effects.Length == 0) return;

        int useIndex = selectedIndex;
        ItemData useItem = slot.item;
        string message = string.Empty;
        bool used = inventory.TryUseAt(useIndex, useItem, () => TryUseSelectedItem(useItem, out message));

        if (!used)
        {
            if (!string.IsNullOrEmpty(message))
                Debug.LogWarning(message);
            return;
        }

        RefreshAllSlots();
    }

    private bool TryUseSelectedItem(ItemData itemData, out string message)
    {
        message = "";

        if (itemData == null || itemData.effects == null || itemData.effects.Length == 0)
        {
            message = "사용할 수 없는 아이템입니다.";
            return false;
        }

        bool hasEnergyPack = false;

        for (int i = 0; i < itemData.effects.Length; i++)
        {
            if (itemData.effects[i] == null)
            {
                message = "아이템 효과 데이터가 올바르지 않습니다.";
                return false;
            }
            if (itemData.effects[i].type == EffectType.EnergyPack)
            {
                hasEnergyPack = true;
                break;
            }
        }

        if (hasEnergyPack)
        {
            if (hankyulFire == null)
                hankyulFire = GetComponentInParent<Hankyul_Fire>();

            if (hankyulFire == null)
            {
                message = "한결의 무기를 찾을 수 없습니다.";
                return false;
            }

            return hankyulFire.UseEnergyPack(out message);
        }

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();

        if (playerStatus == null)
        {
            message = "상태 정보를 찾을 수 없습니다.";
            return false;
        }

        return playerStatus.ApplyItem(itemData, out message);
    }

    public void OnClickDropButton()
    {
        if (!CanReadLocalInput()) return;
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0 || dropPanel == null) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        dropPanel.SetActive(true);
        dropAmountMode = false;

        if (dropTitleText != null)
            dropTitleText.gameObject.SetActive(true);

        if (dropAmountGroup != null)
            dropAmountGroup.SetActive(false);

        if (dropAmountInput != null)
        {
            dropAmountInput.text = "1";
            dropAmountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }
    }

    public void OnClickDropCancel()
    {
        if (!CanReadLocalInput()) return;
        GlobalManager.Audio.PlaySFX("btn_Click");
        CloseDropPanel();
    }

    private void CloseDropPanel()
    {
        dropAmountMode = false;
        if (dropPanel != null)
            dropPanel.SetActive(false);
    }

    public void OnClickDropConfirm()
    {
        if (!CanReadLocalInput()) return;
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        if (!dropAmountMode)
        {
            if (slot.count <= 1)
            {
                inventory.ConsumeAt(selectedIndex, 1);
                CloseDropPanel();
                RefreshAllSlots();
            }
            else
            {
                dropAmountMode = true;

                if (dropTitleText != null)
                    dropTitleText.gameObject.SetActive(false);

                if (dropAmountGroup != null)
                    dropAmountGroup.SetActive(true);

                if (dropAmountInput != null)
                    dropAmountInput.text = "1";
            }
        }
        else
        {
            int amount = 1;
            if (dropAmountInput != null && !string.IsNullOrEmpty(dropAmountInput.text))
                int.TryParse(dropAmountInput.text, out amount);

            amount = Mathf.Clamp(amount, 1, slot.count);

            inventory.ConsumeAt(selectedIndex, amount);

            CloseDropPanel();
            RefreshAllSlots();
        }
    }

    public void RequestSwapSlots(int fromIndex, int toIndex)
    {
        if (!CanReadLocalInput()) return;
        if (inventory == null) return;
        inventory.SwapSlots(fromIndex, toIndex);
    }
}
