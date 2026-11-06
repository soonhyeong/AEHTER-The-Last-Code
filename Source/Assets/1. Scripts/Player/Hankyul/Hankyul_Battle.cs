using UnityEngine;
using UnityEngine.InputSystem;

// Default-order modal/UI owners acquire their leases before gameplay polls input.
[DefaultExecutionOrder(100)]
public class Hankyul_Battle : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Hankyul_Move move;
    [SerializeField] private Animator anim;

    [Header("Weapon")]
    [SerializeField] private GameObject rifleObject;
    [SerializeField] private Hankyul_Fire gunShoot;

    [Header("Aim Zoom")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float aimFieldOfView = 32f;
    [SerializeField] private float aimZoomSpeed = 18f;
    [SerializeField] private bool allowRightMouseHoldAim = true;

    [Header("Animation Event Fallback")]
    [SerializeField] private float pendingTimeout = 0.2f;

    private PlayerModalControlContext modalContext;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private bool isBattle;
    private bool isAimHeld;
    private bool pendingShoot;
    private float pendingShootExpireTime;
    private float defaultFieldOfView;
    private bool hasDefaultFieldOfView;

    private static readonly int HashIsBattle = Animator.StringToHash("IsBattle");
    private static readonly int HashIsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int HashFire = Animator.StringToHash("Fire");
    private static readonly int HashCrouchFire = Animator.StringToHash("CrouchFire");

    private void Awake()
    {
        modalContext = PlayerModalControlContext.For(gameObject, true);
        if (modalContext != null) modalContext.GameplayBlocked += OnModalBlocked;
        if (move == null) move = GetComponent<Hankyul_Move>();
        if (anim == null) anim = GetComponent<Animator>();

        if (gunShoot == null)
        {
            if (rifleObject != null) gunShoot = rifleObject.GetComponentInChildren<Hankyul_Fire>(true);
            if (gunShoot == null) gunShoot = GetComponentInChildren<Hankyul_Fire>(true);
        }

        if (gunShoot != null)
            gunShoot.SetOwnerRoot(transform);

        EnsureAimCamera();

        SetRifleVisible(false);

        if (anim != null)
            anim.SetBool(HashIsBattle, false);
    }

    private void Update()
    {
        if (!CanReadLocalInput()) return;
        if (PlayerModalControlContext.Blocks(gameObject))
        { OnModalBlocked(); ApplyAimZoom(); return; }
        HandleBattleToggle();
        HandleAimInput();
        ApplyAimZoom();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && gunShoot != null)
            gunShoot.TryReload();

        if (!IsCombatActive()) return;

        if (pendingShoot && Time.time > pendingShootExpireTime)
            ShootPendingRound();

        HandleFireInput();
    }

    private void HandleBattleToggle()
    {
        if (Keyboard.current == null) return;

        if (!Keyboard.current.digit1Key.wasPressedThisFrame) return;
        // Reserve number keys for this Player's QuickSlot, even when its slot is empty.
        if (QuickSlotManager.OwnsNumberKeys(gameObject)) return;

        isBattle = !isBattle;

        RefreshCombatPresentation();
    }

    private void HandleAimInput()
    {
        if (!allowRightMouseHoldAim || Mouse.current == null)
        {
            if (isAimHeld)
            {
                isAimHeld = false;
                RefreshCombatPresentation();
            }

            return;
        }

        bool newAimHeld = Mouse.current.rightButton.isPressed;
        if (newAimHeld == isAimHeld) return;

        isAimHeld = newAimHeld;
        RefreshCombatPresentation();
    }

    private void HandleFireInput()
    {
        if (Mouse.current == null || anim == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (pendingShoot) return;

        pendingShoot = true;
        pendingShootExpireTime = Time.time + Mathf.Max(0.05f, pendingTimeout);

        bool isCrouching = anim.GetBool(HashIsCrouching);
        anim.SetTrigger(isCrouching ? HashCrouchFire : HashFire);
    }

    public void AnimEvent_Shoot()
    {
        // Observe loss even if this event precedes Update; cancel only the user reservation.
        CanReadLocalInput();
        if (!IsCombatActive())
        {
            pendingShoot = false;
            return;
        }

        if (!pendingShoot) return;

        ShootPendingRound();
    }

    private void ShootPendingRound()
    {
        if (PlayerModalControlContext.Blocks(gameObject)) { OnModalBlocked(); return; }
        pendingShoot = false;

        if (gunShoot != null)
            gunShoot.ShootOnce();
    }

    private void SetRifleVisible(bool visible)
    {
        if (rifleObject != null)
            rifleObject.SetActive(visible);
    }

    private bool IsCombatActive()
    {
        return isBattle || isAimHeld;
    }

    private void RefreshCombatPresentation()
    {
        bool active = IsCombatActive();

        if (anim != null)
            anim.SetBool(HashIsBattle, active);

        if (gunShoot != null)
            gunShoot.SetBattleMode(active);

        SetRifleVisible(active);

        if (!active)
            pendingShoot = false;
    }

    private void EnsureAimCamera()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main;

        if (aimCamera != null && !hasDefaultFieldOfView)
        {
            defaultFieldOfView = aimCamera.fieldOfView;
            hasDefaultFieldOfView = true;
        }
    }

    private void ApplyAimZoom()
    {
        EnsureAimCamera();
        if (aimCamera == null || !hasDefaultFieldOfView) return;

        float targetFov = isAimHeld ? aimFieldOfView : defaultFieldOfView;
        aimCamera.fieldOfView = Mathf.Lerp(
            aimCamera.fieldOfView,
            targetFov,
            1f - Mathf.Exp(-aimZoomSpeed * Time.deltaTime)
        );
    }

    private void OnDisable()
    {
        OnModalBlocked();
        if (aimCamera != null && hasDefaultFieldOfView)
            aimCamera.fieldOfView = defaultFieldOfView;
    }

    private void OnModalBlocked()
    {
        pendingShoot = false;
        if (isAimHeld)
        {
            isAimHeld = false;
            RefreshCombatPresentation();
        }
    }

    private bool CanReadLocalInput()
    {
        if (localInputGate.CanRead(this)) return true;
        pendingShoot = false;
        pendingShootExpireTime = 0f;
        isAimHeld = false;
        // Do not reset isBattle, the Animator, or network/scripted weapon state.
        return false;
    }

    private void OnDestroy()
    {
        if (modalContext != null) modalContext.GameplayBlocked -= OnModalBlocked;
    }

    public bool IsBattle()
    {
        return IsCombatActive();
    }

    public bool OwnsFireInput(Hankyul_Fire fire)
    {
        return isActiveAndEnabled && fire != null && gunShoot == fire;
    }
}
