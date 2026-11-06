using UnityEngine;

public class PartsSlot2EquipmentBridge : MonoBehaviour
{
    [SerializeField] private EquipmentUIController controller;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private YunseoEquipmentRuntimeBridge yunseoRuntimeBridge;

    private bool subscribed;
    private Transform owner;

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void ResolveReferences()
    {
        controller = GetComponent<EquipmentUIController>();
        owner = LocalPlayerBindingResolver.PlayerRoot(this);
        if (owner == null || !LocalPlayerBindingResolver.TryResolve(owner, out var local, false) || local != owner)
        { owner = null; equipmentManager = null; yunseoRuntimeBridge = null; return; }
        equipmentManager = LocalPlayerBindingResolver.Owned<EquipmentManager>(owner);
        yunseoRuntimeBridge = owner.GetComponent<Move_Yunseo>() != null
            ? LocalPlayerBindingResolver.Owned<YunseoEquipmentRuntimeBridge>(owner) : null;
    }

    private void Subscribe()
    {
        if (subscribed || controller == null)
            return;

        controller.OnEquipmentChanged += HandleEquipmentChanged;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && controller != null)
            controller.OnEquipmentChanged -= HandleEquipmentChanged;

        subscribed = false;
    }

    private void OnDisable()
    {
        OnDestroy();
        owner = null;
        equipmentManager = null;
        yunseoRuntimeBridge = null;
    }

    private void HandleEquipmentChanged(EquipmentUIController.EquipSlot slot, EquipmentItemData item)
    {
        ResolveReferences();
        if (owner == null || controller == null || controller.BoundPlayerRoot != owner) return;
        EquipmentItemData effective = controller.ResolveEffectiveGroupItem(slot, item);
        PartSlotType slotType = ConvertSlot(slot);
        PartType partType = effective != null ? effective.abilityType : PartType.None;

        if (equipmentManager != null && equipmentManager.GetExternalEquipped(slotType) != partType)
            equipmentManager.SetExternalEquipped(slotType, partType);

        if (slot == EquipmentUIController.EquipSlot.LeftLeg || slot == EquipmentUIController.EquipSlot.RightLeg)
        {
            if (yunseoRuntimeBridge != null)
            {
                if (effective != null && effective.partType == EquipmentPartType.Leg)
                    yunseoRuntimeBridge.ApplyLegItem(effective);
                else
                    yunseoRuntimeBridge.ClearLegItem();
            }
        }
    }

    private static PartSlotType ConvertSlot(EquipmentUIController.EquipSlot slot)
    {
        switch (slot)
        {
            case EquipmentUIController.EquipSlot.LeftLeg:
            case EquipmentUIController.EquipSlot.RightLeg:
                return PartSlotType.Leg;

            case EquipmentUIController.EquipSlot.LeftArm:
            case EquipmentUIController.EquipSlot.RightArm:
                return PartSlotType.Arm;

            default:
                return PartSlotType.Utility;
        }
    }
}
