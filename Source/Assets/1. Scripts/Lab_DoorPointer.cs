using System.Collections.Generic;
using UnityEngine;

public class Lab_DoorPointer : MonoBehaviour
{
    [Header("Door Reference")]
    [SerializeField] private Lab_Door door;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color originalColor = Color.white;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color clearedColor = Color.black;

    [Header("Linked Lasers")]
    [SerializeField] private List<GameObject> linkedLasers = new List<GameObject>();

    [Header("Auto Setup")]
    [SerializeField] private bool autoFindDoorFromParent = true;
    [SerializeField] private bool autoReadOriginalColorOnAwake = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private bool isCleared;
    private bool hasCachedOriginalColor;
    private Material runtimeMaterial;

    public bool IsCleared => isCleared;
    public Lab_Door Door => door;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    public void AssignDoor(Lab_Door ownerDoor)
    {
        if (ownerDoor == null) return;
        door = ownerDoor;
    }

    public void Hit()
    {
        if (isCleared) return;
        if (door == null) return;
        if (door.IsNeutralized) return;
        if (!door.IsCurrentPointer(this)) return;

        MarkCleared();
        door.RegisterHit(this);
    }

    public void ResetPointer()
    {
        EnsureSetup();
        isCleared = false;
        SetLaserState(true);
        SetToOriginalVisual();
    }

    public void ResetPointerState()
    {
        ResetPointer();
    }

    public void SetActiveVisual(bool isActive)
    {
        if (isCleared) return;

        SetColor(isActive ? activeColor : originalColor);
    }

    public void SetToOriginalVisual()
    {
        if (isCleared) return;

        SetColor(originalColor);
    }

    public void MarkCleared()
    {
        if (isCleared) return;

        isCleared = true;
        SetColor(clearedColor);
        SetLaserState(false);
    }

    private void SetLaserState(bool state)
    {
        for (int i = 0; i < linkedLasers.Count; i++)
        {
            GameObject laser = linkedLasers[i];
            if (laser == null) continue;

            laser.SetActive(state);
        }
    }

    private void CacheRuntimeMaterial()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer == null) return;

        runtimeMaterial = targetRenderer.material;
    }

    private void TryCacheOriginalColorFromMaterial()
    {
        if (hasCachedOriginalColor) return;
        if (runtimeMaterial == null) return;

        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            originalColor = runtimeMaterial.GetColor(BaseColorId);
            hasCachedOriginalColor = true;
            return;
        }

        if (runtimeMaterial.HasProperty(ColorId))
        {
            originalColor = runtimeMaterial.GetColor(ColorId);
            hasCachedOriginalColor = true;
        }
    }

    private void SetColor(Color targetColor)
    {
        EnsureReferences();
        if (targetRenderer == null) return;

        if (runtimeMaterial == null)
            CacheRuntimeMaterial();

        if (runtimeMaterial == null) return;

        if (runtimeMaterial.HasProperty(BaseColorId))
            runtimeMaterial.SetColor(BaseColorId, targetColor);

        if (runtimeMaterial.HasProperty(ColorId))
            runtimeMaterial.SetColor(ColorId, targetColor);
    }

    private void EnsureSetup()
    {
        EnsureReferences();

        if (runtimeMaterial == null)
            CacheRuntimeMaterial();

        if (autoReadOriginalColorOnAwake)
            TryCacheOriginalColorFromMaterial();
    }

    private void EnsureReferences()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (autoFindDoorFromParent && door == null)
            door = GetComponentInParent<Lab_Door>();
    }
}
