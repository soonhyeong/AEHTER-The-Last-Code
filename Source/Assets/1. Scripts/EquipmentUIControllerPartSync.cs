using UnityEngine;

public class EquipmentUIControllerPartSync : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private YunseoEquipmentRuntimeBridge runtimeBridge;
    [SerializeField] private EquipmentUIController controller;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EquipmentUIController>();

        if (runtimeBridge == null)
            runtimeBridge = FindObjectOfType<YunseoEquipmentRuntimeBridge>();
    }

    public void SyncLegSlot(EquipmentItemData equippedLegItem)
    {
        if (runtimeBridge == null)
            return;

        if (equippedLegItem == null)
        {
            runtimeBridge.ClearLegItem();
            return;
        }

        if (equippedLegItem.partType != EquipmentPartType.Leg)
        {
            runtimeBridge.ClearLegItem();
            return;
        }

        runtimeBridge.ApplyLegItem(equippedLegItem);
    }

    public void ClearLegSlot()
    {
        if (runtimeBridge == null)
            return;

        runtimeBridge.ClearLegItem();
    }
}