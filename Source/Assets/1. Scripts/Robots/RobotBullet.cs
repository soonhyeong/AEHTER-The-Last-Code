using UnityEngine;

public class RobotBullet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Vector3 modelRotationOffset = new Vector3(0f, 0f, -90f);

    private Vector3 direction;
    private float speed;
    private int damage;
    private Transform ownerRoot;
    private Rigidbody cachedRigidbody;

    public void Initialize(Vector3 dir, float moveSpeed, int hitDamage, float lifeTime, Transform owner)
    {
        direction = dir.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0, hitDamage);
        ownerRoot = owner;

        // Correct the model rotation if the prefab forward axis does not match +Z.
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(modelRotationOffset);

        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = direction * speed;
        }

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));
    }

    private void Update()
    {
        if (cachedRigidbody != null && !cachedRigidbody.isKinematic)
            return;

        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot)) return;

        if (BelongsToEnemy(other.transform)) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            var damageInfo = new DamageInfo(
                damage,
                DamageType.Enemy_Bullet,
                gameObject,
                ownerRoot != null ? ownerRoot.gameObject : null,
                transform.position
            );
            damageable.TakeDamage(damageInfo);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
            Destroy(gameObject);
    }

    private static bool BelongsToEnemy(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.CompareTag("Enemy"))
                return true;
        }

        return false;
    }
}
