using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

[RequireComponent(typeof(Move_Hankyul))]
public class HangyeolPartAbilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Move_Hankyul moveHankyul;
    [SerializeField] private PlayerStatus playerStatus;

    [Header("Super Jump")]
    [SerializeField] private float minJumpForce = 12f;
    [SerializeField] private float maxJumpForce = 42f;
    [SerializeField] private float maxChargeTime = 1.2f;

    [Header("Shield")]
    [SerializeField] private ShieldController shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private float shieldPowerPerSecond = 18f;
    [SerializeField] private Vector3 runtimeShieldLocalPosition = new Vector3(0f, 1.1f, 1.0f);
    [SerializeField] private Vector3 runtimeShieldLocalScale = new Vector3(1.4f, 1.4f, 0.12f);

    [Header("Breaker")]
    [SerializeField] private KeyCode breakerKey = KeyCode.Q;
    [SerializeField] private float breakRange = 2.4f;
    [SerializeField] private float breakRadius = 0.55f;
    [SerializeField] private float breakForce = 1f;
    [SerializeField] private float breakCooldown = 0.7f;
    [SerializeField] private LayerMask breakableMask = ~0;

    [Header("Fallback Power")]
    [SerializeField] private float maxPower = 100f;
    [SerializeField] private float currentPower = 100f;

    private ShieldController shieldInstance;
    private float breakerCooldownTimer;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private bool userShieldActive;

    public float CurrentPowerRatio => maxPower > 0f ? currentPower / maxPower : 0f;
    internal float PersistentPower => currentPower;
    internal float PersistentMaxPower => maxPower;
    internal void RestorePersistentPower(float value) { currentPower = value; }

    private void Awake()
    {
        ResolveReferences();
        ApplyTuning();
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe(true);
        ApplyTuning();
        RefreshSuperJumpState();
    }

    private void OnDisable()
    {
        Subscribe(false);
    }

    private void OnDestroy()
    {
        Subscribe(false);
    }

    private void OnValidate()
    {
        minJumpForce = Mathf.Max(0.1f, minJumpForce);
        maxJumpForce = Mathf.Max(minJumpForce, maxJumpForce);
        maxChargeTime = Mathf.Max(0.05f, maxChargeTime);
        shieldPowerPerSecond = Mathf.Max(0f, shieldPowerPerSecond);
        breakRange = Mathf.Max(0.05f, breakRange);
        breakRadius = Mathf.Max(0.01f, breakRadius);
        breakForce = Mathf.Max(0.1f, breakForce);
        breakCooldown = Mathf.Max(0f, breakCooldown);
        maxPower = Mathf.Max(1f, maxPower);
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);

        if (Application.isPlaying)
            ApplyTuning();
    }

    private void Update()
    {
        bool canRead = CanReadAbilityInput();
        if (breakerCooldownTimer > 0f)
            breakerCooldownTimer -= Time.deltaTime;

        if (!canRead) return;
        HandleShieldInput();
        HandleBreakerInput();
    }

    private void ResolveReferences()
    {
        if (moveHankyul == null)
            moveHankyul = GetComponent<Move_Hankyul>();

        if (equipmentManager == null)
            equipmentManager = GetComponent<EquipmentManager>();

        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();
    }

    private void Subscribe(bool subscribe)
    {
        if (equipmentManager == null)
            return;

        equipmentManager.OnEquipmentChanged -= HandleEquipmentChanged;
        equipmentManager.OnPartEquipped -= HandlePartEquipped;
        equipmentManager.OnPartUnequipped -= HandlePartUnequipped;

        if (!subscribe)
            return;

        equipmentManager.OnEquipmentChanged += HandleEquipmentChanged;
        equipmentManager.OnPartEquipped += HandlePartEquipped;
        equipmentManager.OnPartUnequipped += HandlePartUnequipped;
    }

    private void HandleEquipmentChanged(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part == null || part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    private void HandlePartEquipped(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part != null && part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    private void HandlePartUnequipped(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part != null && part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    public void RefreshSuperJumpState()
    {
        ResolveReferences();
        ApplyTuning();

        if (moveHankyul == null || equipmentManager == null)
            return;

        PartData legPart = equipmentManager.GetEquipped(PartSlotType.Leg);
        bool directLegSuperJump = legPart != null && legPart.partType == PartType.SuperJump;
        bool externalSuperJump = equipmentManager.HasPart(PartType.SuperJump);

        moveHankyul.SetSuperJumpEnabled(directLegSuperJump || externalSuperJump);
    }

    private void ApplyTuning()
    {
        if (moveHankyul == null)
            return;

        moveHankyul.ConfigureSuperJump(minJumpForce, maxJumpForce, maxChargeTime);
    }

    private void HandleShieldInput()
    {
        bool wantsShield = Mouse.current != null
            ? Mouse.current.rightButton.isPressed
            : Input.GetMouseButton(1);

        bool shouldShield = HasShieldEquipped() && wantsShield && HasPower();
        userShieldActive = shouldShield;
        EnsureShield();

        if (shieldInstance != null)
            shieldInstance.SetActive(shouldShield);

        if (!shouldShield)
            return;

        if (!ConsumePower(shieldPowerPerSecond * Time.deltaTime) && shieldInstance != null)
            shieldInstance.SetActive(false);
    }

    private void HandleBreakerInput()
    {
        if (!HasBreakerEquipped() || breakerCooldownTimer > 0f)
            return;

        bool pressed;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && breakerKey == KeyCode.Q)
            pressed = keyboard.qKey.wasPressedThisFrame;
        else if (keyboard != null && breakerKey == KeyCode.E)
            pressed = keyboard.eKey.wasPressedThisFrame;
        else
            pressed = Input.GetKeyDown(breakerKey);

        if (!pressed)
            return;

        breakerCooldownTimer = breakCooldown;
        TryBreakForwardTarget();
    }

    private bool CanReadAbilityInput()
    {
        if (localInputGate.CanRead(this) && !PlayerModalControlContext.Blocks(gameObject)) return true;
        if (userShieldActive && shieldInstance != null) shieldInstance.SetActive(false);
        userShieldActive = false;
        return false;
    }

    private bool HasShieldEquipped()
    {
        if (equipmentManager == null)
            return false;

        PartData armPart = equipmentManager.GetEquipped(PartSlotType.Arm);
        return armPart != null && armPart.partType == PartType.Shield
            || equipmentManager.HasPart(PartType.Shield);
    }

    private bool HasBreakerEquipped()
    {
        if (equipmentManager == null)
            return false;

        PartData armPart = equipmentManager.GetEquipped(PartSlotType.Arm);
        PartData utilityPart = equipmentManager.GetEquipped(PartSlotType.Utility);
        return armPart != null && armPart.partType == PartType.Breaker
            || utilityPart != null && utilityPart.partType == PartType.Breaker
            || equipmentManager.HasPart(PartType.Breaker);
    }

    private bool HasPower()
    {
        if (TryReadPlayerStatusPower(out float statusPower))
            return statusPower > 0f;

        return currentPower > 0f;
    }

    private bool ConsumePower(float amount)
    {
        if (amount <= 0f)
            return true;

        if (TryReadPlayerStatusPower(out float statusPower))
        {
            if (statusPower <= 0f)
                return false;

            TryWritePlayerStatusPower(Mathf.Max(0f, statusPower - amount));
            return statusPower - amount > 0f;
        }

        if (currentPower <= 0f)
            return false;

        currentPower = Mathf.Max(0f, currentPower - amount);
        return currentPower > 0f;
    }

    private void EnsureShield()
    {
        if (shieldInstance != null)
            return;

        Transform parent = shieldAnchor != null ? shieldAnchor : transform;
        if (shieldPrefab != null)
        {
            shieldInstance = Instantiate(shieldPrefab, parent);
            shieldInstance.transform.localPosition = runtimeShieldLocalPosition;
            shieldInstance.transform.localRotation = Quaternion.identity;
            shieldInstance.SetActive(false);
            return;
        }

        GameObject shieldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shieldObject.name = "Runtime Shield";
        shieldObject.transform.SetParent(parent, false);
        shieldObject.transform.localPosition = runtimeShieldLocalPosition;
        shieldObject.transform.localRotation = Quaternion.identity;
        shieldObject.transform.localScale = runtimeShieldLocalScale;

        Collider shieldCollider = shieldObject.GetComponent<Collider>();
        if (shieldCollider != null)
            shieldCollider.isTrigger = true;

        shieldObject.AddComponent<ShieldHitbox>();
        shieldInstance = shieldObject.AddComponent<ShieldController>();
        shieldInstance.SetActive(false);
    }

    private void TryBreakForwardTarget()
    {
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 direction = transform.forward;

        if (!Physics.SphereCast(origin, breakRadius, direction, out RaycastHit hit, breakRange, breakableMask, QueryTriggerInteraction.Collide))
            return;

        IBreakable breakable = hit.collider.GetComponentInParent<IBreakable>();
        if (breakable != null)
        {
            breakable.TakeBreakDamage(breakForce);
            return;
        }

        hit.collider.SendMessageUpwards("TakeBreakDamage", breakForce, SendMessageOptions.DontRequireReceiver);
        hit.collider.SendMessageUpwards("Break", SendMessageOptions.DontRequireReceiver);
    }

    private bool TryReadPlayerStatusPower(out float value)
    {
        value = 0f;
        if (playerStatus == null)
            return false;

        FieldInfo field = FindPowerField();
        if (field != null && field.FieldType == typeof(float))
        {
            value = (float)field.GetValue(playerStatus);
            return true;
        }

        PropertyInfo property = FindPowerProperty();
        if (property != null && property.PropertyType == typeof(float) && property.CanRead)
        {
            value = (float)property.GetValue(playerStatus, null);
            return true;
        }

        return false;
    }

    private bool TryWritePlayerStatusPower(float value)
    {
        if (playerStatus == null)
            return false;

        FieldInfo field = FindPowerField();
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(playerStatus, Mathf.Max(0f, value));
            return true;
        }

        PropertyInfo property = FindPowerProperty();
        if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
        {
            property.SetValue(playerStatus, Mathf.Max(0f, value), null);
            return true;
        }

        return false;
    }

    private FieldInfo FindPowerField()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "currentPWR", "currentPwr", "currentPower", "pwr", "PWR", "power" };
        for (int i = 0; i < names.Length; i++)
        {
            FieldInfo field = typeof(PlayerStatus).GetField(names[i], flags);
            if (field != null)
                return field;
        }

        return null;
    }

    private PropertyInfo FindPowerProperty()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "CurrentPWR", "CurrentPwr", "CurrentPower", "PWR", "Power" };
        for (int i = 0; i < names.Length; i++)
        {
            PropertyInfo property = typeof(PlayerStatus).GetProperty(names[i], flags);
            if (property != null)
                return property;
        }

        return null;
    }
}
