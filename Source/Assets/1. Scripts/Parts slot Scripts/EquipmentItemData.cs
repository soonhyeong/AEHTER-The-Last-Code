using UnityEngine;

public enum EquipmentPartType
{
    Arm,
    Leg
}

[CreateAssetMenu(menuName = "AETHER/Equipment Item Data", fileName = "NewEquipmentItem")]
public class EquipmentItemData : ScriptableObject
{
    [Header("�⺻ ����")]
    public string itemName;
    public EquipmentPartType partType;

    [Header("����")]
    public int attack;
    public int defense;
    public int speed;

    [Header("����")]
    [TextArea(3, 8)]
    public string description;

    [Header("UI ������(������ null ����)")]
    public Sprite icon;

    [Header("Crafted Part Link")]
    public PartData sourceCraftedPart;
    public PartType abilityType = PartType.None;
    public bool enableSuperJump;
    public float jumpForceAdd = 0f;
    public float jumpForceMultiplier = 1f;
}
