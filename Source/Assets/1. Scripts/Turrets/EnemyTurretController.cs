using UnityEngine;

public class EnemyTurretController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform leftMuzzle;
    [SerializeField] private Transform rightMuzzle;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Targeting")]
    [SerializeField] private float detectionRange = 34f;
    [SerializeField] private float turnSpeed = 150f;
    [SerializeField] private float fireAngle = 7f;
    [SerializeField] private LayerMask obstacleMask = 1;

    [Header("Weapon")]
    [SerializeField] private float initialFireDelay = 1.2f;
    [SerializeField] private float fireInterval = 0.65f;
    [SerializeField] private float bulletSpeed = 22f;
    [SerializeField] private int bulletDamage = 12;
    [SerializeField] private float bulletLifeTime = 4f;

    private Transform player;
    private RobotHealth health;
    private float nextFireTime;
    private bool useLeftMuzzle = true;
    [SerializeField, HideInInspector] private int shotsFired;

    public int ShotsFired => shotsFired;

    public void Configure(GameObject projectilePrefab)
    {
        bulletPrefab = projectilePrefab;
    }

    private void Awake()
    {
        ResolveVisualReferences();
        FindPlayer();

        health = GetComponent<RobotHealth>();
        if (health != null)
        {
            health.Configure(160, 1.5f);
            health.Died += HandleDeath;
        }

        nextFireTime = Time.time + initialFireDelay + Random.Range(0f, 0.75f);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        if (player == null)
            return;

        Vector3 targetPosition = player.position + Vector3.up;
        Vector3 targetDirection = targetPosition - AimOrigin();
        if (targetDirection.sqrMagnitude > detectionRange * detectionRange)
            return;

        RotateToward(targetPosition);

        Vector3 flatTargetDirection = targetPosition - yawPivot.position;
        float angle = Vector3.Angle(yawPivot.forward, flatTargetDirection.normalized);
        if (angle <= fireAngle && Time.time >= nextFireTime && HasClearShot(targetPosition))
            Fire(targetPosition);
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void ResolveVisualReferences()
    {
        if (yawPivot == null)
            yawPivot = FindChildContaining(transform, "turret_head1") ?? transform;

        if (leftMuzzle == null)
            leftMuzzle = CreateMuzzle("TurretMuzzleLeft", FindChildContaining(yawPivot, "barrel_l"), -0.45f);

        if (rightMuzzle == null)
            rightMuzzle = CreateMuzzle("TurretMuzzleRight", FindChildContaining(yawPivot, "barrel_r"), 0.45f);
    }

    private Transform CreateMuzzle(string muzzleName, Transform barrel, float fallbackX)
    {
        GameObject muzzleObject = new GameObject(muzzleName);
        Transform muzzle = muzzleObject.transform;

        if (barrel != null)
        {
            muzzle.SetParent(barrel, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.25f);
        }
        else
        {
            muzzle.SetParent(yawPivot, false);
            muzzle.localPosition = new Vector3(fallbackX, 0f, 2.1f);
        }

        muzzle.localRotation = Quaternion.identity;
        return muzzle;
    }

    private static Transform FindChildContaining(Transform root, string token)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return children[i];
        }

        return null;
    }

    private Vector3 AimOrigin()
    {
        if (useLeftMuzzle && leftMuzzle != null)
            return leftMuzzle.position;
        if (!useLeftMuzzle && rightMuzzle != null)
            return rightMuzzle.position;
        return yawPivot.position + yawPivot.forward * 1.5f;
    }

    private void RotateToward(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - yawPivot.position;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        yawPivot.rotation = Quaternion.RotateTowards(
            yawPivot.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }

    private bool HasClearShot(Vector3 targetPosition)
    {
        Vector3 origin = AimOrigin();
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return false;

        return !Physics.Raycast(
            origin,
            direction / distance,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void Fire(Vector3 targetPosition)
    {
        nextFireTime = Time.time + fireInterval + Random.Range(-0.08f, 0.12f);

        if (bulletPrefab == null)
            return;

        Transform muzzle = useLeftMuzzle ? leftMuzzle : rightMuzzle;
        useLeftMuzzle = !useLeftMuzzle;
        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : yawPivot.position + yawPivot.forward * 1.5f;
        Vector3 direction = (targetPosition - spawnPosition).normalized;

        GameObject bulletObject = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(direction, Vector3.up));
        shotsFired++;

        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(direction, bulletSpeed, bulletDamage, bulletLifeTime, transform);
            return;
        }

        RobotBullet robotBullet = bulletObject.GetComponent<RobotBullet>();
        if (robotBullet != null)
            robotBullet.Initialize(direction, bulletSpeed, bulletDamage, bulletLifeTime, transform);
    }

    private void HandleDeath(RobotHealth deadHealth)
    {
        enabled = false;
        Destroy(gameObject, deadHealth.DeathDespawnDelay);
    }
}
