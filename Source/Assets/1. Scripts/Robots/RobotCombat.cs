using UnityEngine;

public struct RobotCombatConfig
{
    public Animator animator;
    public Transform owner;
    public Transform target;
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRange;
    public float fireInterval;
    public int burstShotCount;
    public float burstCooldown;
    public bool startWithBurstCooldown;
    public float bulletSpeed;
    public int bulletDamage;
    public float bulletLifeTime;
    public bool fireByAnimationEvent;
    public float fireEventFallbackDelay;
    public bool useAimingIdleDuringBurstCooldown;
}

public class RobotCombat : MonoBehaviour
{
    private static readonly int IsFiringHash = Animator.StringToHash("IsFiring");
    private static readonly int IsAimingIdleHash = Animator.StringToHash("IsAimingIdle");

    private Animator animator;
    private Transform owner;
    private Transform target;
    private GameObject bulletPrefab;
    private Transform firePoint;
    private float fireRange;
    private float fireInterval;
    private int burstShotCount;
    private float burstCooldown;
    private bool startWithBurstCooldown;
    private float bulletSpeed;
    private int bulletDamage;
    private float bulletLifeTime;
    private bool fireByAnimationEvent;
    private float fireEventFallbackDelay;
    private bool useAimingIdleDuringBurstCooldown;

    private bool fireEventPending;
    private bool inBurstCooldown;
    private float fireTimer;
    private float burstCooldownTimer;
    private float fireEventPendingTimer;
    private int burstShotsFired;

    public void Initialize(RobotCombatConfig config)
    {
        animator = config.animator;
        owner = config.owner;
        target = config.target;
        bulletPrefab = config.bulletPrefab;
        firePoint = config.firePoint;
        fireRange = config.fireRange;
        fireInterval = config.fireInterval;
        burstShotCount = Mathf.Max(1, config.burstShotCount);
        burstCooldown = config.burstCooldown;
        startWithBurstCooldown = config.startWithBurstCooldown;
        bulletSpeed = config.bulletSpeed;
        bulletDamage = config.bulletDamage;
        bulletLifeTime = config.bulletLifeTime;
        fireByAnimationEvent = config.fireByAnimationEvent;
        fireEventFallbackDelay = config.fireEventFallbackDelay;
        useAimingIdleDuringBurstCooldown = config.useAimingIdleDuringBurstCooldown;

        StopCombatAnimation();
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }

    public void EnterAttack()
    {
        fireTimer = 0f;
        burstCooldownTimer = 0f;
        fireEventPendingTimer = 0f;
        fireEventPending = false;
        inBurstCooldown = startWithBurstCooldown;
        burstShotsFired = 0;

        StopCombatAnimation();
    }

    public bool TickAttack(float distanceToTarget)
    {
        if (target == null) return true;
        if (distanceToTarget > fireRange) return true;

        if (inBurstCooldown)
        {
            if (useAimingIdleDuringBurstCooldown)
                SetAimingIdle(true);

            burstCooldownTimer += Time.deltaTime;
            if (burstCooldownTimer >= burstCooldown)
            {
                inBurstCooldown = false;
                burstCooldownTimer = 0f;
                fireTimer = 0f;
                SetAimingIdle(false);
            }

            return false;
        }

        SetAimingIdle(false);

        if (fireEventPending)
        {
            fireEventPendingTimer += Time.deltaTime;
            if (fireEventPendingTimer >= fireEventFallbackDelay)
            {
                FireBullet();
                AfterShotFired();
            }

            return false;
        }

        fireTimer += Time.deltaTime;
        if (fireTimer < fireInterval) return false;

        if (fireByAnimationEvent)
        {
            fireEventPending = true;
            fireEventPendingTimer = 0f;
            SetFiring(true);
            return false;
        }

        FireBullet();
        AfterShotFired();
        return false;
    }

    public void OnFireAnimationEvent(float distanceToTarget)
    {
        if (!fireByAnimationEvent) return;
        if (!fireEventPending) return;
        if (distanceToTarget > fireRange) return;

        FireBullet();
        AfterShotFired();
    }

    public void StopCombatAnimation()
    {
        fireEventPending = false;
        SetFiring(false);
        SetAimingIdle(false);
    }

    private void FireBullet()
    {
        if (bulletPrefab == null || target == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + transform.forward * 1.1f + Vector3.up * 1.2f;

        Vector3 targetPos = target.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - spawnPos).normalized;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        EnemyBullet bullet = bulletObj.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(dir, bulletSpeed, bulletDamage, bulletLifeTime, owner != null ? owner : transform);
            return;
        }

        RobotBullet robotBullet = bulletObj.GetComponent<RobotBullet>();
        if (robotBullet != null)
        {
            robotBullet.Initialize(dir, bulletSpeed, bulletDamage, bulletLifeTime, owner != null ? owner : transform);
        }
    }

    private void AfterShotFired()
    {
        fireEventPending = false;
        fireEventPendingTimer = 0f;
        fireTimer = 0f;
        SetFiring(false);

        burstShotsFired++;
        if (burstShotsFired >= burstShotCount)
        {
            burstShotsFired = 0;
            inBurstCooldown = true;
            burstCooldownTimer = 0f;
        }
    }

    private void SetFiring(bool firing)
    {
        if (animator != null)
            animator.SetBool(IsFiringHash, firing);
    }

    private void SetAimingIdle(bool aimingIdle)
    {
        if (animator != null)
            animator.SetBool(IsAimingIdleHash, aimingIdle);
    }
}
