using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yunseo_Battle
/// - Animator 전환도:
///   Locomotion <-> Battle
///   Locomotion -> SprintAttack -> Battle
///
/// - 좌클릭(일반/연속):
///   punch1~4 콤보 사용
///   - 클릭 즉시 다음 애니메이션으로 강제 전환하지 않음
///   - 현재 타격 애니메이션 중 다음 입력을 예약(queue)해두고
///   - 각 punch 상태의 Has Exit Time으로 자연스럽게 다음 타로 연결
///
/// - 달리는 중 좌클릭:
///   SprintAttack(DoSprintAttack Trigger) 실행
///
/// - 우클릭 홀드(1~3초):
///   1초 미만은 취소
///   - 1초: 파랑(티어1)
///   - 2초: 노랑(티어2)
///   - 3초 이상: 빨강(티어3)
///   - PowerTier(int)로 VFX/분기 표시, DoPowerPunch Trigger 실행
///
/// - 공격/행동 발생 시 InBattle=true 유지
///   마지막 행동 후 battleKeepTime 동안 유지 후 InBattle=false
///
/// - 데미지:
///   AE_PunchHit / AE_PowerPunchHit 시점에 OverlapSphere로 판정
///   양손(left/right) 히트 포인트를 모두 검사
///   맞은 대상이 IDamageable이면 TakeDamage(DamageInfo) 호출
/// </summary>
public class Yunseo_Battle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string inBattleBool = "InBattle";
    [SerializeField] private string comboStepInt = "ComboStep";          // 0~4
    [SerializeField] private string doPunchTrigger = "DoPunch";
    [SerializeField] private string doSprintAttackTrigger = "DoSprintAttack";
    [SerializeField] private string doPowerPunchTrigger = "DoPowerPunch";
    [SerializeField] private string powerTierInt = "PowerTier";          // 0~3
    [SerializeField] private string speedFloat = "Speed";                // Locomotion용 이동 속도 float

    [Header("Running 판정(둘 중 하나 사용)")]
    [Tooltip("true면 Animator의 Speed 파라미터로 달리기 판정")]
    [SerializeField] private bool useAnimatorSpeedForRunning = true;
    [Tooltip("Speed 값이 이 값 이상이면 달리는 중으로 판정")]
    [SerializeField] private float runSpeedThreshold = 0.6f;
    [Tooltip("false면 외부에서 SetRunning(true/false)로 직접 전달")]
    [SerializeField] private bool runningState = false;

    [Header("Punch Combo")]
    [SerializeField] private float firstPunchCooldown = 0.05f;
    [SerializeField] private float comboInputWindow = 1.0f;
    [SerializeField] private float afterPunch4Lock = 1.0f;
    [SerializeField] private float battleKeepTime = 3.0f;

    [Header("Punch Damage")]
    [SerializeField] private float punch1Damage = 10f;
    [SerializeField] private float punch2Damage = 12f;
    [SerializeField] private float punch3Damage = 15f;
    [SerializeField] private float punch4Damage = 20f;

    [Header("Power Punch Charge (Tier)")]
    [SerializeField] private float tier1Time = 1f;
    [SerializeField] private float tier2Time = 2f;
    [SerializeField] private float tier3Time = 3f;

    [SerializeField] private float damageTier1 = 50f;
    [SerializeField] private float damageTier2 = 90f;
    [SerializeField] private float damageTier3 = 140f;

    [Header("Hit Settings")]
    [SerializeField] private Transform leftPunchHitPoint;
    [SerializeField] private Transform rightPunchHitPoint;
    [SerializeField] private float punchHitRadius = 0.6f;
    [SerializeField] private LayerMask hitTargetLayers;

    [Header("Charge VFX (optional)")]
    [SerializeField] private GameObject vfxBlue;
    [SerializeField] private GameObject vfxYellow;
    [SerializeField] private GameObject vfxRed;

    [Header("Optional: Charge Pose Hold (advanced)")]
    [Tooltip("차징 중 특정 애니메이션 프레임에서 멈춰 자세 유지(해당 상태가 실제로 있어야 함)")]
    [SerializeField] private bool holdChargePose = false;
    [SerializeField] private string chargeStateName = "PowerCharge";
    [Range(0f, 1f)][SerializeField] private float chargeHoldNormalizedTime = 0.45f;
    [SerializeField] private int chargeLayer = 0;

    // ---------- 내부 상태 ----------
    private bool inputLocked = false;
    private bool scriptedCharge;
    private PlayerModalControlContext modalContext;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();

    // 펀치 콤보 상태
    private bool comboActive = false;
    private bool comboInputQueued = false;
    private bool canQueueNext = false;
    private int currentComboStep = 0;
    private float comboExpireTime = 0f;
    private float nextFirstPunchAllowedTime = 0f;
    private bool comboDecisionPending = false;

    // PowerPunch 차징 상태
    private bool isCharging = false;
    private float chargeTime = 0f;
    private int currentChargeTier = 0;

    private float queuedPowerDamage = 0f;
    private int queuedPowerTier = 0;

    private Coroutine lockRoutine;
    private Coroutine battleKeepRoutine;

    // 히트 판정용
    private readonly Collider[] hitResults = new Collider[16];
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

    // ---------- Animator hashes ----------
    private int hInBattle;
    private int hComboStep;
    private int hDoPunch;
    private int hDoSprint;
    private int hDoPower;
    private int hPowerTier;
    private int hSpeed;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        modalContext = PlayerModalControlContext.For(gameObject, true);
        if (modalContext != null) modalContext.GameplayBlocked += OnModalBlocked;
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        hInBattle = Animator.StringToHash(inBattleBool);
        hComboStep = Animator.StringToHash(comboStepInt);
        hDoPunch = Animator.StringToHash(doPunchTrigger);
        hDoSprint = Animator.StringToHash(doSprintAttackTrigger);
        hDoPower = Animator.StringToHash(doPowerPunchTrigger);
        hPowerTier = Animator.StringToHash(powerTierInt);
        hSpeed = Animator.StringToHash(speedFloat);

        SetChargeVfx(0);
        SafeSetPowerTier(0);

        if (animator != null)
            animator.SetInteger(hComboStep, 0);
    }

    private void Update()
    {
        bool localInput = CanReadLocalInput();
        if (animator == null) return;

        bool modalBlocked = PlayerModalControlContext.Blocks(gameObject);
        if (modalBlocked) OnModalBlocked();

        if (holdChargePose && isCharging)
        {
            animator.Play(chargeStateName, chargeLayer, chargeHoldNormalizedTime);
            animator.Update(0f);
        }

        if (localInput && !modalBlocked && Input.GetMouseButtonDown(1))
            StartCharge();

        if (isCharging)
            TickCharge();

        if (localInput && !modalBlocked && !scriptedCharge && Input.GetMouseButtonUp(1))
            ReleaseCharge();

        if (localInput && !modalBlocked && Input.GetMouseButtonDown(0))
        {
            if (!isCharging)
                TryLeftClickAttack();
        }

        if (comboActive && Time.time > comboExpireTime)
            comboInputQueued = false;
    }

    private void LateUpdate()
    {
        if (!CanReadLocalInput()) return;
        if (!comboDecisionPending) return;

        comboDecisionPending = false;

        if (!comboActive) return;

        if (comboInputQueued)
            AdvanceCombo();
        else
            EndCombo(false);
    }

    public void SetRunning(bool isRunning)
    {
        runningState = isRunning;
    }

    private bool CanReadLocalInput()
    {
        if (localInputGate.CanRead(this)) return true;
        comboInputQueued = false;
        comboDecisionPending = false;
        if (isCharging && !scriptedCharge) CancelDoorBreachCharge();
        return false;
    }

    public void BeginDoorBreachCharge()
    {
        if (animator == null) return;
        if (isCharging) return;

        StartCharge();
        scriptedCharge = isCharging;
    }

    public void CancelDoorBreachCharge()
    {
        scriptedCharge = false;
        if (animator == null) return;

        isCharging = false;
        inputLocked = false;
        chargeTime = 0f;

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    public void ExecuteDoorBreachPowerPunch()
    {
        scriptedCharge = false;
        if (animator == null) return;

        isCharging = false;
        inputLocked = false;

        queuedPowerTier = 3;
        queuedPowerDamage = damageTier3;

        SafeSetPowerTier(queuedPowerTier);
        EnterBattle();

        animator.ResetTrigger(hDoPower);
        animator.SetTrigger(hDoPower);

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    private bool IsRunningNow()
    {
        if (!useAnimatorSpeedForRunning)
            return runningState;

        float s = animator.GetFloat(hSpeed);
        return s >= runSpeedThreshold;
    }

    // -------------------------
    // Left Click: SprintAttack or Punch Combo
    // -------------------------
    private void TryLeftClickAttack()
    {
        if (inputLocked) return;

        if (IsRunningNow())
        {
            EnterBattle();
            animator.ResetTrigger(hDoSprint);
            animator.SetTrigger(hDoSprint);
            return;
        }

        TryPunchCombo();
    }

    private void TryPunchCombo()
    {
        float now = Time.time;

        if (!comboActive)
        {
            if (now < nextFirstPunchAllowedTime)
                return;

            StartPunchCombo();
            return;
        }

        if (canQueueNext)
        {
            comboInputQueued = true;
            comboExpireTime = now + comboInputWindow;
            EnterBattle();
        }
    }

    private void StartPunchCombo()
    {
        comboActive = true;
        comboInputQueued = false;
        canQueueNext = false;
        currentComboStep = 1;

        animator.SetInteger(hComboStep, currentComboStep);

        EnterBattle();
        animator.ResetTrigger(hDoPunch);
        animator.SetTrigger(hDoPunch);

        nextFirstPunchAllowedTime = Time.time + firstPunchCooldown;
        comboExpireTime = Time.time + comboInputWindow;
    }

    private void AdvanceCombo()
    {
        currentComboStep++;

        if (currentComboStep > 4)
        {
            EndCombo(true);
            return;
        }

        animator.SetInteger(hComboStep, currentComboStep);
        comboInputQueued = false;
        canQueueNext = false;
        comboExpireTime = Time.time + comboInputWindow;

        EnterBattle();
    }

    private void EndCombo(bool applyFinalLock)
    {
        comboActive = false;
        comboInputQueued = false;
        canQueueNext = false;
        currentComboStep = 0;
        comboExpireTime = 0f;
        comboDecisionPending = false;

        animator.SetInteger(hComboStep, 0);

        if (applyFinalLock)
            ApplyInputLock(afterPunch4Lock);
    }

    // -------------------------
    // Right Click: Power Punch Charge (Tier)
    // -------------------------
    private void OnModalBlocked()
    {
        comboInputQueued = false;
        // Cancel only user charge; scripted door/QTE charge retains its existing API.
        if (isCharging && !scriptedCharge) CancelDoorBreachCharge();
    }

    private void OnDestroy()
    {
        if (modalContext != null) modalContext.GameplayBlocked -= OnModalBlocked;
    }

    private void StartCharge()
    {
        if (inputLocked) return;
        if (comboActive) return;

        isCharging = true;
        chargeTime = 0f;
        UpdateChargeTier(0);

        EnterBattle();
        inputLocked = true;
    }

    private void TickCharge()
    {
        chargeTime += Time.deltaTime;

        int tier = GetChargeTier(chargeTime);
        UpdateChargeTier(tier);

        if (chargeTime > tier3Time)
            chargeTime = tier3Time;
    }

    private void ReleaseCharge()
    {
        if (!isCharging) return;

        isCharging = false;
        inputLocked = false;

        int tier = GetChargeTier(chargeTime);

        if (tier == 0)
        {
            UpdateChargeTier(0);
            SetChargeVfx(0);
            SafeSetPowerTier(0);
            return;
        }

        queuedPowerTier = tier;
        queuedPowerDamage = tier switch
        {
            1 => damageTier1,
            2 => damageTier2,
            _ => damageTier3
        };

        SafeSetPowerTier(queuedPowerTier);
        EnterBattle();

        animator.ResetTrigger(hDoPower);
        animator.SetTrigger(hDoPower);

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    private int GetChargeTier(float t)
    {
        if (t < tier1Time) return 0;
        if (t < tier2Time) return 1;
        if (t < tier3Time) return 2;
        return 3;
    }

    private void UpdateChargeTier(int tier)
    {
        if (currentChargeTier == tier) return;

        currentChargeTier = tier;
        SetChargeVfx(tier);
        SafeSetPowerTier(tier);
    }

    private void SetChargeVfx(int tier)
    {
        if (vfxBlue) vfxBlue.SetActive(tier == 1);
        if (vfxYellow) vfxYellow.SetActive(tier == 2);
        if (vfxRed) vfxRed.SetActive(tier == 3);
    }

    private void SafeSetPowerTier(int tier)
    {
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == hPowerTier && p.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(hPowerTier, tier);
                return;
            }
        }
    }

    // -------------------------
    // Damage / Hit Detection
    // -------------------------
    private float GetCurrentPunchDamage()
    {
        switch (currentComboStep)
        {
            case 1: return punch1Damage;
            case 2: return punch2Damage;
            case 3: return punch3Damage;
            case 4: return punch4Damage;
            default: return punch1Damage;
        }
    }

    private void PerformPunchHit(float damage)
    {
        damagedTargets.Clear();

        HitFromPoint(leftPunchHitPoint, damage);
        HitFromPoint(rightPunchHitPoint, damage);
    }

    private void HitFromPoint(Transform hitPointTransform, float damage)
    {
        if (hitPointTransform == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            hitPointTransform.position,
            punchHitRadius,
            hitResults,
            hitTargetLayers
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = hitResults[i];
            if (col == null) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            if (damagedTargets.Contains(damageable))
                continue;

            damagedTargets.Add(damageable);

            Vector3 hitPoint = col.ClosestPoint(hitPointTransform.position);

            DamageInfo damageInfo = new DamageInfo(
                damage,
                DamageType.punch,
                gameObject,
                gameObject,
                hitPoint
            );

            damageable.TakeDamage(damageInfo);
        }
    }

    // -------------------------
    // InBattle 유지 / Input Lock
    // -------------------------
    private void EnterBattle()
    {
        animator.SetBool(hInBattle, true);

        if (battleKeepRoutine != null)
            StopCoroutine(battleKeepRoutine);

        battleKeepRoutine = StartCoroutine(BattleKeepCoroutine());
    }

    private IEnumerator BattleKeepCoroutine()
    {
        float end = Time.time + battleKeepTime;

        while (Time.time < end)
            yield return null;

        animator.SetBool(hInBattle, false);
        battleKeepRoutine = null;
    }

    private void ApplyInputLock(float seconds)
    {
        if (lockRoutine != null)
            StopCoroutine(lockRoutine);

        lockRoutine = StartCoroutine(InputLockCoroutine(seconds));
    }

    private IEnumerator InputLockCoroutine(float seconds)
    {
        inputLocked = true;
        yield return new WaitForSeconds(seconds);
        inputLocked = false;
        lockRoutine = null;
    }

    // -------------------------
    // Animation Events
    // -------------------------
    public void AE_OpenComboWindow()
    {
        if (!comboActive) return;
        canQueueNext = true;
    }

    public void AE_ComboDecision()
    {
        if (!comboActive) return;
        comboDecisionPending = true;
    }

    public void AE_EndComboFinal()
    {
        EndCombo(true);
    }

    public void AE_PunchHit()
    {
        float damage = GetCurrentPunchDamage();
        PerformPunchHit(damage);
    }

    public void AE_PowerPunchHit()
    {
        PerformPunchHit(queuedPowerDamage);
    }

    // -------------------------
    // Debug Gizmo
    // -------------------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (leftPunchHitPoint != null)
            Gizmos.DrawWireSphere(leftPunchHitPoint.position, punchHitRadius);

        if (rightPunchHitPoint != null)
            Gizmos.DrawWireSphere(rightPunchHitPoint.position, punchHitRadius);
    }
}
