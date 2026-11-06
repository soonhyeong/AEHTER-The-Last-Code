using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Move_Yunseo))]
public class YunseoPartAbilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private ShieldController shieldPrefab;
    [SerializeField] private LayerMask breakableMask = ~0;

    [Header("Power")]
    [SerializeField] private float maxPower = 100f;
    [SerializeField] private float currentPower = 100f;
    [SerializeField] private float shieldPowerPerSecond = 18f;
    [SerializeField] private float superJumpPowerCost = 10f;

    [Header("Super Jump")]
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float minJumpForce = 5f;
    [SerializeField] private float maxJumpForce = 13f;

    [Header("Breaker")]
    [SerializeField] private KeyCode breakerKey = KeyCode.Q;
    [SerializeField] private float breakRange = 2.4f;
    [SerializeField] private float breakRadius = 0.55f;
    [SerializeField] private float breakForce = 1f;
    [SerializeField] private float breakCooldown = 0.7f;

    private Move_Yunseo move;
    private ShieldController shieldInstance;
    private float chargeTime;
    private bool chargingSuperJump;
    private float breakerCooldownTimer;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private bool userShieldActive;

    public float CurrentPowerRatio => maxPower > 0f ? currentPower / maxPower : 0f;
    internal float PersistentPower => currentPower;
    internal float PersistentMaxPower => maxPower;
    internal void RestorePersistentPower(float value) { currentPower = value; }

    private void Awake()
    {
        move = GetComponent<Move_Yunseo>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (equipmentManager == null)
            equipmentManager = GetComponent<EquipmentManager>();
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);
    }

    private void Update()
    {
        bool canRead = CanReadAbilityInput();
        if (breakerCooldownTimer > 0f)
            breakerCooldownTimer -= Time.deltaTime;

        if (!canRead) return;
        HandleShield();
        HandleBreaker();
    }

    public bool HandleJumpInput(Keyboard keyboard)
    {
        if (!CanReadAbilityInput()) return false;
        if (keyboard == null || !HasPart(PartType.SuperJump))
            return false;

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded() && currentPower >= superJumpPowerCost)
        {
            chargingSuperJump = true;
            chargeTime = 0f;
            return true;
        }

        if (!chargingSuperJump)
            return false;

        if (keyboard.spaceKey.isPressed)
        {
            chargeTime = Mathf.Min(maxChargeTime, chargeTime + Time.deltaTime);
            return true;
        }

        if (keyboard.spaceKey.wasReleasedThisFrame)
        {
            ExecuteSuperJump();
            chargingSuperJump = false;
            chargeTime = 0f;
            return true;
        }

        return true;
    }

    private void ExecuteSuperJump()
    {
        if (rb == null || !IsGrounded())
            return;

        currentPower = Mathf.Max(0f, currentPower - superJumpPowerCost);
        float ratio = maxChargeTime > 0f ? Mathf.Clamp01(chargeTime / maxChargeTime) : 0f;
        float jumpForce = Mathf.Lerp(minJumpForce, maxJumpForce, ratio);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void HandleShield()
    {
        bool wantsShield = Mouse.current != null
            ? Mouse.current.rightButton.isPressed
            : Input.GetMouseButton(1);

        bool active = HasPart(PartType.Shield) && wantsShield && currentPower > 0f;
        userShieldActive = active;
        EnsureShield();

        if (shieldInstance != null)
            shieldInstance.SetActive(active);

        if (active)
            currentPower = Mathf.Max(0f, currentPower - shieldPowerPerSecond * Time.deltaTime);
    }

    private void HandleBreaker()
    {
        if (!HasPart(PartType.Breaker) || breakerCooldownTimer > 0f)
            return;

        Keyboard keyboard = Keyboard.current;
        bool pressed = keyboard != null
            ? keyboard.qKey.wasPressedThisFrame
            : Input.GetKeyDown(breakerKey);

        if (!pressed)
            return;

        breakerCooldownTimer = breakCooldown;
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 direction = transform.forward;

        if (Physics.SphereCast(origin, breakRadius, direction, out RaycastHit hit, breakRange, breakableMask, QueryTriggerInteraction.Collide))
        {
            IBreakable breakable = hit.collider.GetComponentInParent<IBreakable>();
            if (breakable != null)
                breakable.TakeBreakDamage(breakForce);
        }
    }

    private bool IsGrounded()
    {
        if (move != null && move.groundCheck != null)
        {
            return Physics.CheckSphere(
                move.groundCheck.position,
                move.groundCheckRadius,
                move.groundMask,
                QueryTriggerInteraction.Ignore);
        }

        return rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.05f;
    }

    private bool CanReadAbilityInput()
    {
        if (localInputGate.CanRead(this) && !PlayerModalControlContext.Blocks(gameObject)) return true;
        if (userShieldActive && shieldInstance != null) shieldInstance.SetActive(false);
        userShieldActive = false;
        chargingSuperJump = false;
        chargeTime = 0f;
        return false;
    }

    private bool HasPart(PartType type)
    {
        return equipmentManager != null && equipmentManager.HasPart(type);
    }

    private void EnsureShield()
    {
        if (shieldInstance != null)
            return;

        Transform parent = shieldAnchor != null ? shieldAnchor : transform;
        if (shieldPrefab != null)
        {
            shieldInstance = Instantiate(shieldPrefab, parent);
            shieldInstance.transform.localPosition = new Vector3(0f, 1.1f, 1.0f);
            shieldInstance.transform.localRotation = Quaternion.identity;
            return;
        }

        GameObject shieldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shieldObject.name = "Runtime Shield";
        shieldObject.transform.SetParent(parent, false);
        shieldObject.transform.localPosition = new Vector3(0f, 1.1f, 1.0f);
        shieldObject.transform.localScale = new Vector3(1.4f, 1.4f, 0.12f);
        Collider collider = shieldObject.GetComponent<Collider>();
        collider.isTrigger = true;
        shieldObject.AddComponent<ShieldHitbox>();
        shieldInstance = shieldObject.AddComponent<ShieldController>();
    }
}
