using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Move_Hankyul : MonoBehaviour
{
    [Header("Movement Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;

    [Header("Crouch Collider")]
    [Range(0.3f, 0.8f)]
    public float crouchHeightRatio = 0.6f;

    [Header("Movement Basis")]
    public Transform orientationTransform;
    public bool moveRelativeToCamera = false;

    [Header("Movement Feel")]
    public float acceleration = 18f;
    public float deceleration = 24f;
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.45f;
    public float stopSnapSpeed = 0.08f;

    [Header("Scene Movement Scale")]
    [SerializeField] private float movementSpeedMultiplier = 1f;
    [SerializeField] private float jumpForceMultiplier = 1f;

    [Header("Stair Climb")]
    [SerializeField] private bool enableStairStepAssist = true;
    [SerializeField] private float maxStepHeight = 4f;
    [SerializeField] private float stepCheckDistance = 1.15f;
    [SerializeField] private float stepClimbSpeed = 55f;
    [SerializeField] private LayerMask stepMask = ~0;

    [Header("Animation Feel")]
    public float animatorDampTime = 0.08f;
    public float crouchAnimatorSpeed = 0.35f;
    public float walkAnimatorSpeed = 0.55f;
    public float runAnimatorSpeed = 1f;

    [Header("Camera")]
    public Transform camTransform;
    public float mouseSensitivity = 200f;
    public float minLookAngle = -60f;
    public float maxLookAngle = 60f;
    public bool smoothLook = true;
    [Range(0.005f, 0.12f)]
    public float lookSmoothTime = 0.025f;

    [Header("Camera Stance")]
    public bool adjustCameraForStance = true;
    public Vector3 standingCameraLocalPosition;
    public Vector3 crouchingCameraLocalPosition;
    public Vector3 sprintingCameraLocalPosition;
    public float cameraStanceLerpSpeed = 14f;
    [Range(0.75f, 0.98f)]
    public float minCameraHeightRatio = 0.9f;
    public float minCameraForwardOffset = 0.28f;

    [Header("View Model Clipping")]
    [Range(0.005f, 0.1f)]
    public float viewModelNearClip = 0.035f;
    public bool hideOwnBodyFromFirstPersonCamera = true;
    public string ownBodyLayerName = "Player";
    public string viewModelLayerName = "ViewModel";

    [Header("Fall Safety")]
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundMask;
    public float jumpCooldown = 0.5f;

    [Header("Super Jump Module")]
    [SerializeField] private bool canSuperJump;
    [SerializeField] private float superJumpMinForce = 5f;
    [SerializeField] private float superJumpMaxChargeTime = 1.5f;
    [SerializeField] private float superJumpMaxMultiplier = 2.5f;
    [SerializeField] private float superJumpMaxForce = 12.5f;
    [Range(0f, 1f)]
    [SerializeField] private float superJumpAlertChargeThreshold = 0.85f;
    [SerializeField] private int superJumpLandingAlertAmount = 5;
    [SerializeField] private float sceneLoadInputGraceTime = 0.2f;

    [Header("Input Allowed")]
    public bool allowMove = true;
    public bool allowLook = true;
    public bool allowInput = true;

    private float xRotation;
    private float targetXRotation;
    private float currentYaw;
    private float targetYaw;
    private float yawSmoothVelocity;
    private float pitchSmoothVelocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 actionMoveInput;
    private Vector2 actionLookInput;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator anim;
    private PlayerInput playerInput;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();

    private bool isSprinting;
    private bool isCrouching;
    private float originalHeight;
    private Vector3 originalCenter;
    private bool isGrounded = true;
    private bool wasGrounded = true;
    private float jumpCooldownTimer;
    private bool isChargingSuperJump;
    private float superJumpChargeTime;
    private bool pendingSuperJumpLandingAlert;
    private float inputResumeTime;
    private float lastMoveInputMagnitude;
    private Vector3 cameraDefaultLocalPosition;
    private bool hasCameraDefaultLocalPosition;
    private AetherCameraController duplicateAetherCameraController;

    // Reused only by stair queries; a full buffer is rejected rather than trusting a partial result.
    private readonly RaycastHit[] stepHitBuffer = new RaycastHit[32];
    private readonly Collider[] stepOverlapBuffer = new Collider[32];
    private const float StepSkin = 0.02f;

    private readonly Collider[] groundOverlapBuffer = new Collider[32];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        anim = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        DisableDuplicateLookControllers();

        if (anim != null)
            anim.applyRootMotion = false;

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            // 낙하 중 얇은 콜라이더 통과 방지
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        originalHeight = capsule != null ? capsule.height : 2f;
        originalCenter = capsule != null ? capsule.center : Vector3.up;

        ResolveOwnedControlReferences();

        currentYaw = orientationTransform.eulerAngles.y;
        targetYaw = currentYaw;
        targetXRotation = xRotation;

        if (camTransform != null)
        {
            cameraDefaultLocalPosition = camTransform.localPosition;
            hasCameraDefaultLocalPosition = true;

            cameraDefaultLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition);
            camTransform.localPosition = cameraDefaultLocalPosition;

            if (standingCameraLocalPosition == Vector3.zero)
                standingCameraLocalPosition = cameraDefaultLocalPosition;

            if (crouchingCameraLocalPosition == Vector3.zero)
                crouchingCameraLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition + new Vector3(0f, -0.45f, 0.02f), true);

            if (sprintingCameraLocalPosition == Vector3.zero)
                sprintingCameraLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition + new Vector3(0f, 0.06f, 0.04f));

            standingCameraLocalPosition = GetSafeCameraLocalPosition(standingCameraLocalPosition);
            sprintingCameraLocalPosition = GetSafeCameraLocalPosition(sprintingCameraLocalPosition);

            ApplyCameraClipSettings();
            ConfigureFirstPersonSelfVisibility();
        }

        if (PlayerLocalInputOwnership.CanConsumeLocalInput(this)) LockCursor();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SuppressInputBriefly();

        if (PlayerLocalInputOwnership.CanConsumeLocalInput(this) && playerInput != null && !playerInput.inputIsActive)
            playerInput.ActivateInput();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (orientationTransform != null && !IsOwnedTransform(orientationTransform))
            orientationTransform = transform;

        if (camTransform != null && !IsOwnedTransform(camTransform))
            camTransform = FindOwnedCameraTransform();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ClearTransientInputState();
    }

    private void Update()
    {
        if (!CanReadLocalInput()) return;
        bool inputReady = Time.unscaledTime >= inputResumeTime;
        bool canMove = allowInput && allowMove && inputReady;
        bool canLook = allowInput && allowLook && inputReady;

        if (canLook)
            LockCursor();
        else
            UnlockCursor();

        ReadFallbackInput(canMove, canLook);
        HandleLook(canLook);
        UpdateGrounded();

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && canMove)
        {
            isSprinting = keyboard.leftShiftKey.isPressed;

            if (keyboard.leftCtrlKey.wasPressedThisFrame)
                ToggleCrouch();

            if (canSuperJump)
                HandleSuperJumpInput(keyboard);
            else if (keyboard.spaceKey.wasPressedThisFrame)
                TryJump();
        }
        else if (!canMove)
        {
            // Keep held Action values through a temporary modal/scene-input grace period.
            ClearTransientInputState(false);
        }

        UpdateCameraStance();
        UpdateAnimator(canMove);
    }

    private void FixedUpdate()
    {
        if (!CanReadLocalInput()) return;
        HandleMovement();
        ClampFallSpeed();
    }

    private void ClampFallSpeed()
    {
        if (rb == null) return;
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = -maxFallSpeed;
            rb.linearVelocity = v;
        }
    }

    private void OnMove(InputValue value)
    {
        if (!CanReadLocalInput()) return;
        actionMoveInput = value.Get<Vector2>();
        if (!allowInput || !allowMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = actionMoveInput;
    }

    private void OnLook(InputValue value)
    {
        if (!CanReadLocalInput()) return;
        actionLookInput = value.Get<Vector2>();
        if (!allowInput || !allowLook)
        {
            lookInput = Vector2.zero;
            return;
        }

        lookInput = actionLookInput;
    }

    private bool CanUseActionInput(string actionName)
    {
        if (playerInput == null || !playerInput.enabled || !playerInput.inputIsActive)
            return false;

        // Unlike PlayerInput.actions, these getters do not initialize or enable actions.
        InputActionMap map = playerInput.currentActionMap;
        if (map == null || map.asset == null)
            return false;

        InputAction action = map.FindAction(actionName, false);
        return action != null && action.enabled;
    }

    private void ReadFallbackInput(bool canMove, bool canLook)
    {
        if (!CanReadLocalInput()) return;
        if (!canMove)
        {
            moveInput = Vector2.zero;
        }
        else if (CanUseActionInput("Move"))
        {
            moveInput = actionMoveInput;
        }
        else
        {
            actionMoveInput = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            Vector2 keyboardMove = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) keyboardMove.x -= 1f;
                if (keyboard.dKey.isPressed) keyboardMove.x += 1f;
                if (keyboard.sKey.isPressed) keyboardMove.y -= 1f;
                if (keyboard.wKey.isPressed) keyboardMove.y += 1f;
            }

            moveInput = Vector2.ClampMagnitude(keyboardMove, 1f);
        }

        if (!canLook)
        {
            lookInput = Vector2.zero;
        }
        else if (CanUseActionInput("Look"))
        {
            lookInput = actionLookInput;
        }
        else
        {
            actionLookInput = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null)
                lookInput = mouse.delta.ReadValue();
            else
                lookInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }
    }

    private void HandleMovement()
    {
        if (!CanReadLocalInput()) return;
        if (rb == null)
            return;

        if (!allowInput || !allowMove || Time.unscaledTime < inputResumeTime)
        {
            lastMoveInputMagnitude = 0f;
            Vector3 stopVelocity = rb.linearVelocity;
            stopVelocity.x = 0f;
            stopVelocity.z = 0f;
            rb.linearVelocity = stopVelocity;
            return;
        }

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        lastMoveInputMagnitude = inputDir.magnitude;
        float currentSpeed = (isCrouching ? crouchSpeed : (isSprinting ? runSpeed : walkSpeed)) * Mathf.Max(0.05f, movementSpeedMultiplier);
        GetMovementBasis(out Vector3 forward, out Vector3 right);
        Vector3 moveDir = forward * inputDir.z + right * inputDir.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (inputDir.sqrMagnitude <= 0.001f)
        {
            Vector3 stopVelocity = rb.linearVelocity;
            stopVelocity.x = 0f;
            stopVelocity.z = 0f;
            rb.linearVelocity = stopVelocity;
            return;
        }

        Vector3 nextPosition = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
        if (TryGetStepAdjustedPosition(moveDir, nextPosition, out Vector3 stepAdjustedPosition))
            nextPosition = stepAdjustedPosition;

        rb.MovePosition(nextPosition);
    }

    private bool TryGetStepAdjustedPosition(Vector3 moveDir, Vector3 nextPosition, out Vector3 adjustedPosition)
    {
        adjustedPosition = nextPosition;

        if (!enableStairStepAssist || capsule == null || rb == null || !isGrounded)
            return false;

        if (moveDir.sqrMagnitude <= 0.001f)
            return false;

        if (!IsFiniteStepValue(maxStepHeight) || maxStepHeight <= 0.01f
            || !IsFiniteStepValue(stepCheckDistance) || stepCheckDistance <= 0f
            || !IsFiniteStepValue(stepClimbSpeed) || stepClimbSpeed <= 0f
            || !IsFiniteStepVector(moveDir) || !IsFiniteStepVector(nextPosition))
            return false;

        if (!TryGetStepCapsule(out Vector3 bottomSphere, out Vector3 topSphere, out float radius))
            return false;

        Vector3 direction = new Vector3(moveDir.x, 0f, moveDir.z).normalized;
        if (direction.sqrMagnitude <= 0.001f)
            return false;

        // The foot is the bottom of the hemisphere, not its centre.
        Vector3 foot = bottomSphere - Vector3.up * radius;
        float forwardDistance = radius + stepCheckDistance;
        Vector3 lowerOrigin = foot + Vector3.up * StepSkin;
        if (!TryQueryStepRay(lowerOrigin, direction, forwardDistance, out RaycastHit obstacle)
            || obstacle.collider == null)
            return false;

        // Do not start rising toward an obstacle still far ahead of this physics step.
        Vector3 horizontalTravel = nextPosition - rb.position;
        horizontalTravel.y = 0f;
        if (obstacle.distance > radius + horizontalTravel.magnitude + StepSkin)
            return false;

        Vector3 upperOrigin = foot + Vector3.up * (maxStepHeight + StepSkin);
        if (!TryQueryStepRay(upperOrigin, direction, forwardDistance, out RaycastHit upperHit)
            || upperHit.collider != null)
            return false;

        // Probe just inside the actual low hit, not at the far end of the search ray.
        Vector3 topProbeOrigin = obstacle.point + direction * StepSkin;
        topProbeOrigin.y = foot.y + maxStepHeight + StepSkin;
        if (!TryQueryStepRay(topProbeOrigin, Vector3.down, maxStepHeight + StepSkin * 2f, out RaycastHit stepHit)
            || stepHit.collider == null || stepHit.collider != obstacle.collider
            || !IsFiniteStepVector(stepHit.point) || !IsFiniteStepVector(stepHit.normal)
            || stepHit.normal.y < 0.35f)
            return false;

        float heightDelta = stepHit.point.y - foot.y;
        if (heightDelta <= 0.01f || heightDelta > maxStepHeight)
            return false;

        float targetY = rb.position.y + heightDelta + StepSkin;
        float smoothedY = Mathf.MoveTowards(rb.position.y, targetY, stepClimbSpeed * Time.fixedDeltaTime);
        if (!IsFiniteStepValue(smoothedY) || smoothedY <= rb.position.y + 0.001f)
            return false;

        Vector3 landingPosition = new Vector3(nextPosition.x, targetY, nextPosition.z);
        Vector3 candidate = new Vector3(nextPosition.x, smoothedY, nextPosition.z);
        Vector3 landingOffset = landingPosition - rb.position;
        Vector3 candidateOffset = candidate - rb.position;
        if (!IsStepCapsuleClear(bottomSphere + landingOffset, topSphere + landingOffset, radius)
            || !IsStepCapsuleClear(bottomSphere + candidateOffset, topSphere + candidateOffset, radius))
            return false;

        // Check headroom through the lift, not just at the final position (thin ceilings).
        Vector3 lift = Vector3.up * (targetY - rb.position.y);
        if (!IsStepSweepClear(bottomSphere, topSphere, radius, lift)
            || !IsStepSweepClear(bottomSphere + lift, topSphere + lift, radius, horizontalTravel))
            return false;

        adjustedPosition = candidate;
        return true;
    }

    private bool TryGetStepCapsule(out Vector3 bottomSphere, out Vector3 topSphere, out float radius)
    {
        bottomSphere = topSphere = Vector3.zero;
        radius = 0f;
        if (!capsule.enabled || capsule.isTrigger)
            return false;

        Transform shape = capsule.transform;
        Vector3 x = shape.TransformVector(Vector3.right);
        Vector3 y = shape.TransformVector(Vector3.up);
        Vector3 z = shape.TransformVector(Vector3.forward);
        float sx = x.magnitude, sy = y.magnitude, sz = z.magnitude;
        if (!IsFiniteStepVector(x) || !IsFiniteStepVector(y) || !IsFiniteStepVector(z)
            || sx <= 0.0001f || sy <= 0.0001f || sz <= 0.0001f)
            return false;

        // Non-uniform/mirrored orthogonal scale is supported; shear is not approximated.
        if (Mathf.Abs(Vector3.Dot(x / sx, y / sy)) > 0.001f
            || Mathf.Abs(Vector3.Dot(x / sx, z / sz)) > 0.001f
            || Mathf.Abs(Vector3.Dot(y / sy, z / sz)) > 0.001f)
            return false;

        Vector3 axis;
        float axisScale, radiusScale;
        switch (capsule.direction)
        {
            case 0: axis = x / sx; axisScale = sx; radiusScale = Mathf.Max(sy, sz); break;
            case 1: axis = y / sy; axisScale = sy; radiusScale = Mathf.Max(sx, sz); break;
            case 2: axis = z / sz; axisScale = sz; radiusScale = Mathf.Max(sx, sy); break;
            default: return false;
        }

        // Stair assist is for an upright capsule. A tilted shape must use ordinary physics.
        if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9999f)
            return false;
        if (axis.y < 0f) axis = -axis;
        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        Vector3 center = shape.TransformPoint(capsule.center) + rb.position - transform.position;
        if (!IsFiniteStepValue(radius) || !IsFiniteStepValue(height) || !IsFiniteStepVector(center)
            || radius <= 0f || capsule.height <= 0f)
            return false;

        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        bottomSphere = center - axis * halfSegment;
        topSphere = center + axis * halfSegment;
        return true;
    }

    private bool IsOwnStepCollider(Collider other)
    {
        return other == capsule || other.attachedRigidbody == rb
            || other.transform == transform || other.transform.IsChildOf(transform);
    }

    // True means the query is complete; nearest.collider == null means no external solid hit.
    private bool TryQueryStepRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.RaycastNonAlloc(origin, direction, stepHitBuffer, distance, stepMask, QueryTriggerInteraction.Ignore);
        if (count >= stepHitBuffer.Length)
            return false;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = stepHitBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger || IsOwnStepCollider(hit.collider))
                continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                nearest = hit;
            }
        }
        return true;
    }

    private bool IsStepCapsuleClear(Vector3 bottomSphere, Vector3 topSphere, float radius)
    {
        // Clearance uses every layer, not just the layers eligible as steps.
        int count = Physics.OverlapCapsuleNonAlloc(bottomSphere, topSphere, radius, stepOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        if (count >= stepOverlapBuffer.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            Collider other = stepOverlapBuffer[i];
            if (other != null && !other.isTrigger && !IsOwnStepCollider(other))
                return false;
        }
        return true;
    }

    private bool IsStepSweepClear(Vector3 bottomSphere, Vector3 topSphere, float radius, Vector3 travel)
    {
        float distance = travel.magnitude;
        if (distance <= 0.0001f)
            return true;
        int count = Physics.CapsuleCastNonAlloc(bottomSphere, topSphere, radius, travel / distance, stepHitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count >= stepHitBuffer.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            Collider other = stepHitBuffer[i].collider;
            if (other != null && !other.isTrigger && !IsOwnStepCollider(other))
                return false;
        }
        return true;
    }

    private static bool IsFiniteStepValue(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFiniteStepVector(Vector3 value)
    {
        return IsFiniteStepValue(value.x) && IsFiniteStepValue(value.y) && IsFiniteStepValue(value.z);
    }

    public void SetMovementSpeedMultiplier(float multiplier)
    {
        movementSpeedMultiplier = Mathf.Max(0.05f, multiplier);
    }

    public void SetJumpForceMultiplier(float multiplier)
    {
        jumpForceMultiplier = Mathf.Max(0.05f, multiplier);
    }

    private void GetMovementBasis(out Vector3 forward, out Vector3 right)
    {
        ResolveOwnedControlReferences();
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        forward = yawRotation * Vector3.forward;
        right = yawRotation * Vector3.right;
        forward.Normalize();
        right.Normalize();
    }

    private void HandleLook(bool canLook)
    {
        if (!canLook)
            return;

        if (duplicateAetherCameraController != null && duplicateAetherCameraController.enabled)
            duplicateAetherCameraController.enabled = false;

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        ResolveOwnedControlReferences();
        Transform basis = orientationTransform;
        targetYaw += mouseX;
        targetXRotation = Mathf.Clamp(targetXRotation - mouseY, minLookAngle, maxLookAngle);

        if (smoothLook)
        {
            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawSmoothVelocity,
                Mathf.Max(0.001f, lookSmoothTime)
            );

            xRotation = Mathf.SmoothDampAngle(
                xRotation,
                targetXRotation,
                ref pitchSmoothVelocity,
                Mathf.Max(0.001f, lookSmoothTime)
            );
        }
        else
        {
            currentYaw = targetYaw;
            xRotation = targetXRotation;
        }

        basis.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void TryJump()
    {
        if (!allowInput || !allowMove || rb == null)
            return;

        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f)
            return;

        isGrounded = false;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce * Mathf.Max(0.05f, jumpForceMultiplier), ForceMode.Impulse);

        if (anim != null)
            anim.SetTrigger("JumpStart");
    }

    private void HandleSuperJumpInput(Keyboard keyboard)
    {
        if (keyboard == null)
            return;

        if (keyboard.spaceKey.wasPressedThisFrame)
            BeginSuperJumpCharge();

        if (isChargingSuperJump && keyboard.spaceKey.isPressed)
            superJumpChargeTime = Mathf.Min(superJumpChargeTime + Time.deltaTime, superJumpMaxChargeTime);

        if (isChargingSuperJump && keyboard.spaceKey.wasReleasedThisFrame)
            ReleaseSuperJump();
    }

    private void BeginSuperJumpCharge()
    {
        if (rb == null || !isGrounded || isCrouching || jumpCooldownTimer > 0f)
            return;

        isChargingSuperJump = true;
        superJumpChargeTime = 0f;
    }

    private void ReleaseSuperJump()
    {
        if (rb == null)
            return;

        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f)
        {
            isChargingSuperJump = false;
            superJumpChargeTime = 0f;
            return;
        }

        float normalizedCharge = superJumpMaxChargeTime > 0f
            ? Mathf.Clamp01(superJumpChargeTime / superJumpMaxChargeTime)
            : 1f;

        float configuredMinForce = Mathf.Max(0.1f, superJumpMinForce);
        float configuredMaxForce = Mathf.Max(configuredMinForce, superJumpMaxForce);
        float finalJumpForce = Mathf.Lerp(configuredMinForce, configuredMaxForce, normalizedCharge)
            * Mathf.Max(0.05f, jumpForceMultiplier);

        isChargingSuperJump = false;
        superJumpChargeTime = 0f;
        isGrounded = false;
        pendingSuperJumpLandingAlert = normalizedCharge >= superJumpAlertChargeThreshold;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * finalJumpForce, ForceMode.Impulse);

        if (anim != null)
            anim.SetTrigger("JumpStart");
    }

    private void UpdateGrounded()
    {
        wasGrounded = isGrounded;
        isGrounded = HasGroundContact();

        if (!wasGrounded && isGrounded)
        {
            jumpCooldownTimer = jumpCooldown;

            if (pendingSuperJumpLandingAlert)
            {
                pendingSuperJumpLandingAlert = false;
                if (SimpleAlertLevelManager.Instance != null)
                    SimpleAlertLevelManager.Instance.AddAlert(superJumpLandingAlertAmount);
            }

            if (anim != null)
                anim.SetTrigger("JumpLand");
        }
    }

    private bool HasGroundContact()
    {
        if (rb == null || capsule == null || !IsFiniteStepValue(groundCheckRadius)
            || groundCheckRadius <= 0f || groundMask.value == 0)
            return false;

        Vector3 probePosition;
        // Keep authored foot markers, but never trust a scene object or the root pivot.
        if (groundCheck != null && groundCheck != transform && groundCheck.IsChildOf(transform))
        {
            probePosition = groundCheck.position;
        }
        else
        {
            // Reuse O01's validated upright capsule geometry without changing stair logic.
            if (!TryGetStepCapsule(out Vector3 bottomSphere, out _, out float radius))
                return false;
            probePosition = bottomSphere - Vector3.up * radius;
        }

        if (!IsFiniteStepVector(probePosition))
            return false;

        int count = Physics.OverlapSphereNonAlloc(probePosition, groundCheckRadius, groundOverlapBuffer,
            groundMask, QueryTriggerInteraction.Ignore);
        if (count >= groundOverlapBuffer.Length)
            return false;

        for (int i = 0; i < count; i++)
        {
            Collider other = groundOverlapBuffer[i];
            if (other == null || !other.enabled || other.isTrigger || IsOwnStepCollider(other))
                continue;
            if ((groundMask.value & (1 << other.gameObject.layer)) != 0)
                return true;
        }
        return false;
    }

    private void UpdateAnimator(bool canMove)
    {
        if (anim == null)
            return;

        float inputMagnitude = canMove ? moveInput.magnitude : 0f;
        float horizontalSpeed = lastMoveInputMagnitude;
        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            horizontalSpeed = Mathf.Max(horizontalSpeed, velocity.magnitude);
        }

        float targetParam = 0f;
        if (inputMagnitude > 0.05f && horizontalSpeed > 0.02f)
        {
            if (isCrouching)
                targetParam = crouchAnimatorSpeed;
            else if (isSprinting)
                targetParam = runAnimatorSpeed;
            else
                targetParam = walkAnimatorSpeed;
        }

        anim.SetFloat("Speed", targetParam, Mathf.Max(0.001f, animatorDampTime), Time.deltaTime);
        anim.SetBool("IsCrouching", isCrouching);
        anim.SetBool("IsInAir", !isGrounded);
    }

    private void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        UpdateCrouchCollider();
    }

    private void UpdateCrouchCollider()
    {
        if (capsule == null)
            return;

        if (isCrouching)
        {
            capsule.height = originalHeight * crouchHeightRatio;
            capsule.center = new Vector3(originalCenter.x, originalCenter.y * crouchHeightRatio, originalCenter.z);
        }
        else
        {
            capsule.height = originalHeight;
            capsule.center = originalCenter;
        }
    }

    private void UpdateCameraStance()
    {
        if (!adjustCameraForStance || camTransform == null)
            return;

        Vector3 basePosition = hasCameraDefaultLocalPosition ? cameraDefaultLocalPosition : standingCameraLocalPosition;
        Vector3 target = isCrouching ? crouchingCameraLocalPosition : (isSprinting && moveInput.sqrMagnitude > 0.01f ? sprintingCameraLocalPosition : standingCameraLocalPosition);

        if (target == Vector3.zero)
            target = basePosition;

        target = GetSafeCameraLocalPosition(target, isCrouching);

        camTransform.localPosition = Vector3.Lerp(
            camTransform.localPosition,
            target,
            1f - Mathf.Exp(-cameraStanceLerpSpeed * Time.deltaTime)
        );
    }

    private void ApplyCameraClipSettings()
    {
        if (camTransform == null)
            return;

        Camera cameraComponent = camTransform.GetComponent<Camera>();
        if (cameraComponent == null)
            return;

        cameraComponent.nearClipPlane = Mathf.Min(cameraComponent.nearClipPlane, Mathf.Max(0.005f, viewModelNearClip));

        if (!cameraComponent.CompareTag("MainCamera"))
            cameraComponent.tag = "MainCamera";

        if (hideOwnBodyFromFirstPersonCamera)
        {
            int bodyLayer = LayerMask.NameToLayer(ownBodyLayerName);
            int viewModelLayer = LayerMask.NameToLayer(viewModelLayerName);

            if (bodyLayer >= 0)
                cameraComponent.cullingMask &= ~(1 << bodyLayer);

            if (viewModelLayer >= 0)
                cameraComponent.cullingMask |= 1 << viewModelLayer;
        }
    }

    private void ConfigureFirstPersonSelfVisibility()
    {
        if (!hideOwnBodyFromFirstPersonCamera)
            return;

        int bodyLayer = LayerMask.NameToLayer(ownBodyLayerName);
        int viewModelLayer = LayerMask.NameToLayer(viewModelLayerName);
        if (bodyLayer < 0)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool viewModel = viewModelLayer >= 0 && IsViewModelRenderer(renderer.transform);
            renderer.gameObject.layer = viewModel ? viewModelLayer : bodyLayer;
        }
    }

    private static bool IsViewModelRenderer(Transform rendererTransform)
    {
        string path = rendererTransform != null ? rendererTransform.name : string.Empty;
        Transform cursor = rendererTransform != null ? rendererTransform.parent : null;
        while (cursor != null)
        {
            path = cursor.name + "/" + path;
            cursor = cursor.parent;
        }

        return ContainsIgnoreCase(path, "hand")
            || ContainsIgnoreCase(path, "glove")
            || ContainsIgnoreCase(path, "gun")
            || ContainsIgnoreCase(path, "weapon")
            || ContainsIgnoreCase(path, "lowerarm")
            || ContainsIgnoreCase(path, "upperarm");
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(value)
            && source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Vector3 GetSafeCameraLocalPosition(Vector3 localPosition, bool crouched = false)
    {
        float height = capsule != null ? capsule.height : originalHeight;
        if (height <= 0.01f)
            height = 1.85f;

        float minY = height * (crouched ? Mathf.Clamp(crouchHeightRatio + 0.08f, 0.5f, 0.8f) : minCameraHeightRatio);
        localPosition.y = Mathf.Max(localPosition.y, minY);
        localPosition.z = Mathf.Max(localPosition.z, minCameraForwardOffset);
        return localPosition;
    }

    public void ResetPlayerControlState()
    {
        allowInput = true;
        allowMove = true;
        allowLook = true;
        ClearTransientInputState();
        SuppressInputBriefly();

        if (!CanReadLocalInput()) return;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (playerInput != null && !playerInput.inputIsActive)
            playerInput.ActivateInput();

        LockCursor();
    }

    public void SetSuperJumpEnabled(bool enabled)
    {
        canSuperJump = enabled;
        if (!enabled)
        {
            isChargingSuperJump = false;
            superJumpChargeTime = 0f;
            pendingSuperJumpLandingAlert = false;
        }
    }

    public void ConfigureSuperJump(float minJumpForce, float maxJumpForce, float maxChargeTime)
    {
        superJumpMinForce = Mathf.Max(0.1f, minJumpForce);
        superJumpMaxForce = Mathf.Max(superJumpMinForce, maxJumpForce);
        superJumpMaxChargeTime = Mathf.Max(0.05f, maxChargeTime);

        if (jumpForce > 0.01f)
            superJumpMaxMultiplier = Mathf.Max(1f, superJumpMaxForce / jumpForce);
    }

    public bool IsSuperJumpEnabled()
    {
        return canSuperJump;
    }

    public float GetSuperJumpCharge01()
    {
        return superJumpMaxChargeTime > 0f ? Mathf.Clamp01(superJumpChargeTime / superJumpMaxChargeTime) : 0f;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncLookStateFromCurrentTransforms();
        ClearTransientInputState();
        SuppressInputBriefly();
    }

    private void SyncLookStateFromCurrentTransforms()
    {
        ResolveOwnedControlReferences();

        currentYaw = orientationTransform.eulerAngles.y;
        targetYaw = currentYaw;
        yawSmoothVelocity = 0f;

        if (camTransform != null)
        {
            xRotation = Mathf.Clamp(NormalizeAngle(camTransform.localEulerAngles.x), minLookAngle, maxLookAngle);
            targetXRotation = xRotation;
            pitchSmoothVelocity = 0f;
        }
    }

    private void SuppressInputBriefly()
    {
        inputResumeTime = Time.unscaledTime + Mathf.Max(0f, sceneLoadInputGraceTime);
        ClearTransientInputState();
    }

    private void ClearTransientInputState(bool clearActionInput = true)
    {
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        if (clearActionInput)
        {
            actionMoveInput = Vector2.zero;
            actionLookInput = Vector2.zero;
        }
        isSprinting = false;
        isChargingSuperJump = false;
        superJumpChargeTime = 0f;
    }

    private bool CanReadLocalInput()
    {
        if (localInputGate.CanRead(this)) return true;
        ClearTransientInputState();
        lastMoveInputMagnitude = 0f;
        pendingSuperJumpLandingAlert = false;
        return false;
    }

    private void DisableDuplicateLookControllers()
    {
        duplicateAetherCameraController = GetComponent<AetherCameraController>();
        if (duplicateAetherCameraController == null)
            duplicateAetherCameraController = GetComponentInChildren<AetherCameraController>(true);

        if (duplicateAetherCameraController != null)
            duplicateAetherCameraController.enabled = false;

        // PlayerMouseLook이 같이 붙어있으면 이중 회전 처리 방지
        PlayerMouseLook mouseLook = GetComponent<PlayerMouseLook>();
        if (mouseLook == null)
            mouseLook = GetComponentInChildren<PlayerMouseLook>(true);
        if (mouseLook != null)
            mouseLook.enabled = false;
    }

    private void ResolveOwnedControlReferences()
    {
        // Movement must always use Hankyul's own look rig. Scene overrides can point
        // to another prefab, which makes WASD move in the wrong direction.
        if (!IsOwnedTransform(orientationTransform))
            orientationTransform = transform;

        if (!IsOwnedTransform(camTransform))
        {
            Transform ownedCamera = FindOwnedCameraTransform();
            if (ownedCamera != null)
                camTransform = ownedCamera;
        }
    }

    private bool IsOwnedTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        if (candidate == transform || candidate.IsChildOf(transform))
            return true;

        Move_Hankyul owner = candidate.GetComponentInParent<Move_Hankyul>();
        return owner == this;
    }

    private Transform FindOwnedCameraTransform()
    {
        Camera[] ownedCameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < ownedCameras.Length; i++)
        {
            if (ownedCameras[i] != null && ownedCameras[i].CompareTag("MainCamera"))
                return ownedCameras[i].transform;
        }

        if (ownedCameras.Length > 0 && ownedCameras[0] != null)
            return ownedCameras[0].transform;

        return null;
    }

    public void SnapLookToWorldRotation(Quaternion worldRotation, float pitch = 0f)
    {
        ResolveOwnedControlReferences();

        lookInput = Vector2.zero;
        yawSmoothVelocity = 0f;
        pitchSmoothVelocity = 0f;
        currentYaw = worldRotation.eulerAngles.y;
        targetYaw = currentYaw;
        xRotation = Mathf.Clamp(NormalizeAngle(pitch), minLookAngle, maxLookAngle);
        targetXRotation = xRotation;
        orientationTransform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public void OnStep_L()
    {
    }

    public void OnStep_R()
    {
    }

    public void Jump()
    {
    }

    public void Land()
    {
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
