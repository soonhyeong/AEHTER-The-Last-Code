using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

    private Vector3 direction;
    private float speed;
    private float damage;
    private Transform ownerRoot;
    private GameObject hitImpactPrefab;
    private bool hasImpacted;
    private Rigidbody cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody != null)
        {
            cachedRigidbody.useGravity = false;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public void Initialize(
        Vector3 dir,
        float moveSpeed,
        float hitDamage,
        float lifeTime,
        Transform owner,
        GameObject impactPrefab = null)
    {
        direction = dir.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0f, hitDamage);
        ownerRoot = owner;
        hitImpactPrefab = impactPrefab;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(modelRotationOffset);

        if (cachedRigidbody != null)
            cachedRigidbody.linearVelocity = direction * speed;

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));
    }

    private void FixedUpdate()
    {
        if (cachedRigidbody == null)
            transform.position += direction * speed * Time.fixedDeltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;
        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot)) return;

        TryApplyDamage(other, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        if (ownerRoot != null && collision.transform.IsChildOf(ownerRoot)) return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        TryApplyDamage(collision.collider, hitPoint);
    }

    private void TryApplyDamage(Collider other, Vector3 hitPoint)
    {
        hasImpacted = true;

        Lab_DoorPointer doorPointer = other.GetComponent<Lab_DoorPointer>();
        if (doorPointer == null)
            doorPointer = other.GetComponentInParent<Lab_DoorPointer>();

        if (doorPointer != null)
            doorPointer.Hit();

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            var damageInfo = new DamageInfo(
                damage,
                DamageType.Bullet,
                gameObject,
                ownerRoot != null ? ownerRoot.gameObject : null,
                hitPoint
            );
            damageable.TakeDamage(damageInfo);
        }

        SpawnImpact(hitPoint);
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 hitPoint)
    {
        if (hitImpactPrefab == null) return;

        GameObject impact = Instantiate(hitImpactPrefab, hitPoint, Quaternion.LookRotation(-direction, Vector3.up));
        Destroy(impact, 2f);
    }
}
