using UnityEngine;

public class DroneAttackerCombat : DroneCombatBase
{
    [Header("Attack")]
    [SerializeField] private float fireRange = 16f;
    [SerializeField] private float preferredDistance = 10f;
    [SerializeField] private float fireInterval = 0.8f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 18f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletLifeTime = 4f;

    [Header("Individual Variation")]
    [SerializeField] private Vector2 fireIntervalMultiplierRange = new Vector2(0.82f, 1.18f);

    private float fireTimer;
    private float individualFireInterval;

    public override void Initialize(Drones drone)
    {
        base.Initialize(drone);
        individualFireInterval = fireInterval * Random.Range(
            Mathf.Min(fireIntervalMultiplierRange.x, fireIntervalMultiplierRange.y),
            Mathf.Max(fireIntervalMultiplierRange.x, fireIntervalMultiplierRange.y));
    }

    public void ConfigureLegacy(GameObject nextBulletPrefab, Transform nextFirePoint, float nextAttackRange, float nextPreferredDistance, float nextFireInterval, float nextBulletSpeed, int nextBulletDamage, float nextBulletLifeTime)
    {
        bulletPrefab = nextBulletPrefab;
        firePoint = nextFirePoint;
        fireRange = nextAttackRange;
        preferredDistance = nextPreferredDistance;
        fireInterval = nextFireInterval;
        bulletSpeed = nextBulletSpeed;
        bulletDamage = nextBulletDamage;
        bulletLifeTime = nextBulletLifeTime;
    }

    public override void OnEnterCombat()
    {
        float interval = Mathf.Max(0.1f, individualFireInterval);
        fireTimer = -Random.Range(0f, interval * 0.75f);
    }

    public override void OnExitCombat()
    {
        fireTimer = 0f;
    }

    public override void TickCombat()
    {
        if (owner == null || owner.Player == null) return;

        float distanceToTarget = owner.DistanceToPlayerXZ();
        Vector3 toPlayer = owner.DirectionToPlayerXZ();

        if (distanceToTarget > fireRange)
        {
            Vector3 approachTarget = owner.Player.position;
            if (toPlayer.sqrMagnitude > 0.001f)
                approachTarget = owner.Player.position - toPlayer * preferredDistance;

            owner.MoveToPoint(approachTarget, owner.Player.position, 1.1f);
            return;
        }

        owner.FaceTo(owner.Player.position);

        fireTimer += Time.deltaTime;
        float interval = individualFireInterval > 0f ? individualFireInterval : fireInterval;
        if (fireTimer >= Mathf.Max(0.1f, interval))
        {
            FireSingleShot();
        }
    }

    private void FireSingleShot()
    {
        if (bulletPrefab == null || owner == null || owner.Player == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : owner.transform.position + owner.transform.forward * 1f;

        Vector3 targetPos = owner.Player.position + Vector3.up;
        Vector3 toTarget = targetPos - spawnPos;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        Vector3 dir = toTarget.normalized;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        EnemyBullet bullet = bulletObj.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(dir, bulletSpeed, bulletDamage, bulletLifeTime, owner.transform);
            fireTimer = 0f;
        }
        else
        {
            Destroy(bulletObj);
        }
    }
}
