using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class Hankyul_Fire : MonoBehaviour
{
    public enum FireMode
    {
        Single,
        Auto
    }

    [Header("References")]
    [SerializeField] private Camera playerCam;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private Vector3 fallbackMuzzleLocalOffset = new Vector3(0f, 0f, 0.65f);

    [Header("Battle")]
    [SerializeField] private bool isBattleMode = true;
    [SerializeField] private bool allowDirectInput = false;

    [Header("Shoot - Common")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float range = 200f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float bulletSpeed = 80f;
    [SerializeField] private float bulletLifeTime = 3f;
    [SerializeField] private LayerMask hitMask;

    [Header("Shoot - Single / Auto")]
    [SerializeField] private FireMode currentFireMode = FireMode.Single;
    [SerializeField] private float singleFireRate = 4f;
    [SerializeField] private float autoFireRate = 10f;

    [Header("Shoot - Shotgun")]
    [SerializeField] private int shotgunPelletCount = 5;
    [SerializeField] private float shotgunFireRate = 1.2f;
    [SerializeField] private float shotgunSpreadAngle = 15f;

    [Header("Ammo")]
    [SerializeField] private int maxTotalAmmo = 150;
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int currentMagazineAmmo = 30;
    [SerializeField] private int currentReserveAmmo = 120;
    [SerializeField] private float reloadTime = 1.5f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitImpactPrefab;

    [Header("Tracer")]
    [SerializeField] private LineRenderer tracerPrefab;
    [SerializeField] private float tracerTime = 0.05f;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip shotgunClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private AudioClip emptyClip;

    private float nextFireTime;
    private bool isReloading;
    private Coroutine reloadRoutine;
    private Hankyul_Battle battleInputOwner;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private GameObject runtimeFallbackBulletPrefab;

    public FireMode CurrentFireMode => currentFireMode;
    public int CurrentMagazineAmmo => currentMagazineAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MaxTotalAmmo => maxTotalAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    public bool IsBattleMode => isBattleMode;

    private void Reset()
    {
        playerCam = Camera.main;
        audioSource = GetComponent<AudioSource>();
        ownerRoot = transform.root;

        EnsureMuzzleReference();
    }

    private void Awake()
    {
        if (playerCam == null)
        {
            playerCam = GetComponentInParent<Camera>();
            if (playerCam == null) playerCam = Camera.main;
        }

        EnsureMuzzleReference();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (ownerRoot == null) ownerRoot = transform.root;
        EnsureBulletPrefab();

        ClampAmmo();
    }

    private void OnDisable()
    {
        // Hiding the rifle stops its GameObject coroutines. Do not keep a stale reload latch.
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
        isReloading = false;
    }

    private void Update()
    {
        if (!localInputGate.CanRead(this)) return;
        if (!allowDirectInput || Mouse.current == null || Keyboard.current == null) return;
        // Serialized legacy opt-in remains, but an active Battle is the sole input reader.
        if (HasBattleInputOwner() || IsGameplayBlocked()) return;

        if (Keyboard.current.vKey.wasPressedThisFrame)
            ToggleFireMode();

        if (!isBattleMode || isReloading) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ShootShotgun();
            return;
        }

        if (currentFireMode == FireMode.Single)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                ShootSingle();
        }
        else
        {
            if (Mouse.current.leftButton.isPressed)
                ShootAuto();
        }
    }

    public void SetBattleMode(bool value)
    {
        isBattleMode = value;
    }

    public void ToggleFireMode()
    {
        currentFireMode = currentFireMode == FireMode.Single ? FireMode.Auto : FireMode.Single;
        Debug.Log($"[Hankyul_Fire] FireMode Changed: {currentFireMode}");
    }

    // Explicit state restore/selection only: no fire, ammo, audio, animation or input side effects.
    public bool SetFireMode(FireMode mode)
    {
        if (mode != FireMode.Single && mode != FireMode.Auto) return false;
        currentFireMode = mode;
        return true;
    }

    public bool ShootOnce()
    {
        return ShootSingle();
    }

    public bool ShootSingle()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, singleFireRate), 1))
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();

        FireProjectile(spawnPos, aimPoint, damage);
        ConsumeAmmo(1);
        PlayShootFX(false);
        CheckAutoReload();

        return true;
    }

    public bool ShootAuto()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, autoFireRate), 1))
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();

        FireProjectile(spawnPos, aimPoint, damage);
        ConsumeAmmo(1);
        PlayShootFX(false);
        CheckAutoReload();

        return true;
    }

    public bool ShootShotgun()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, shotgunFireRate), 1))
            return false;

        int pelletCountToFire = Mathf.Min(currentMagazineAmmo, shotgunPelletCount);
        if (pelletCountToFire <= 0)
        {
            PlayEmptySound();
            return false;
        }

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();
        Vector3 baseDir = (aimPoint - spawnPos).normalized;

        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = transform.forward;

        for (int i = 0; i < pelletCountToFire; i++)
        {
            Vector3 spreadDir = GetSpreadDirection(baseDir, shotgunSpreadAngle);
            Vector3 pelletAimPoint = spawnPos + spreadDir * range;
            FireProjectile(spawnPos, pelletAimPoint, damage);
        }

        ConsumeAmmo(pelletCountToFire);
        PlayShootFX(true);
        CheckAutoReload();

        return true;
    }

    private bool CanShoot(float interval, int ammoNeeded)
    {
        if (IsGameplayBlocked()) return false;
        if (!isBattleMode) return false;
        if (isReloading) return false;
        EnsureRuntimeReferences();
        if (playerCam == null) return false;
        if (Time.time < nextFireTime) return false;

        if (currentMagazineAmmo < ammoNeeded)
        {
            PlayEmptySound();
            CheckAutoReload();
            return false;
        }

        nextFireTime = Time.time + interval;
        return true;
    }

    private void CheckAutoReload()
    {
        if (!isActiveAndEnabled) return;
        if (currentMagazineAmmo <= 0 && currentReserveAmmo > 0 && !isReloading)
        {
            reloadRoutine = StartCoroutine(ReloadRoutine());
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (audioSource != null && reloadClip != null)
            audioSource.PlayOneShot(reloadClip);

        yield return new WaitForSeconds(reloadTime);

        int needAmmo = magazineSize - currentMagazineAmmo;
        int loadAmmo = Mathf.Min(needAmmo, currentReserveAmmo);

        currentMagazineAmmo += loadAmmo;
        currentReserveAmmo -= loadAmmo;

        isReloading = false;
        reloadRoutine = null;
    }

    public bool TryReload()
    {
        if (!isActiveAndEnabled) return false;
        if (IsGameplayBlocked()) return false;
        if (!CanReload())
            return false;

        reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    private void ConsumeAmmo(int amount)
    {
        currentMagazineAmmo -= amount;
        if (currentMagazineAmmo < 0)
            currentMagazineAmmo = 0;
    }

    private void ClampAmmo()
    {
        currentMagazineAmmo = Mathf.Clamp(currentMagazineAmmo, 0, magazineSize);
        currentReserveAmmo = Mathf.Clamp(currentReserveAmmo, 0, maxTotalAmmo);

        int total = currentMagazineAmmo + currentReserveAmmo;
        if (total > maxTotalAmmo)
        {
            int overflow = total - maxTotalAmmo;
            currentReserveAmmo = Mathf.Max(0, currentReserveAmmo - overflow);
        }
    }

    private Vector3 GetAimPoint()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        Vector3 hitPoint = ray.origin + ray.direction * range;

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            hitPoint = hit.point;

        return hitPoint;
    }

    private Vector3 GetMuzzlePosition()
    {
        EnsureMuzzleReference();

        if (muzzle != null)
            return muzzle.position;

        if (TryGetRendererMuzzlePosition(out Vector3 rendererMuzzlePosition))
            return rendererMuzzlePosition;

        return transform.TransformPoint(fallbackMuzzleLocalOffset);
    }

    private void EnsureMuzzleReference()
    {
        Transform fireBase = FindFireBaseTransform();
        if (fireBase != null)
        {
            muzzle = fireBase;
            return;
        }

        if (muzzle == null)
            muzzle = FindMuzzleTransform();

        if (muzzle != null && !muzzle.name.Equals("firebase", System.StringComparison.OrdinalIgnoreCase))
            muzzle = CreateRuntimeFireBaseFrom(muzzle);
    }

    private void EnsureRuntimeReferences()
    {
        if (playerCam == null)
        {
            playerCam = GetComponentInParent<Camera>(true);
            if (playerCam == null) playerCam = Camera.main;
        }

        EnsureMuzzleReference();
        EnsureBulletPrefab();

        if (ownerRoot == null)
            ownerRoot = transform.root;
    }

    private void EnsureBulletPrefab()
    {
        if (bulletPrefab != null)
            return;

        bulletPrefab = Resources.Load<GameObject>("AETHERRuntime/Bullet");
        if (bulletPrefab != null)
            return;

        if (runtimeFallbackBulletPrefab != null)
        {
            bulletPrefab = runtimeFallbackBulletPrefab;
            return;
        }

        runtimeFallbackBulletPrefab = CreateRuntimeFallbackBulletPrefab();
        bulletPrefab = runtimeFallbackBulletPrefab;
    }

    private GameObject CreateRuntimeFallbackBulletPrefab()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = "Runtime_PlayerBullet_Fallback";
        prefab.transform.localScale = Vector3.one * 0.08f;

        Collider collider = prefab.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;

        Rigidbody rigidbody = prefab.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        prefab.AddComponent<PlayerBullet>();
        prefab.SetActive(false);
        DontDestroyOnLoad(prefab);
        return prefab;
    }

    private Transform FindMuzzleTransform()
    {
        Transform fireBase = FindFireBaseTransform();
        if (fireBase != null) return fireBase;

        Transform found = FindChildByNameContains(transform, "muzzle");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "firepoint");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "fire_point");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "barrel");
        if (found != null) return found;

        if (muzzleFlash != null)
            return muzzleFlash.transform;

        return null;
    }

    private Transform FindFireBaseTransform()
    {
        Transform found = FindChildByNameEquals(transform, "firebase");
        if (found != null) return found;

        found = FindChildByNameEquals(transform, "fire base");
        if (found != null) return found;

        found = FindChildByNameEquals(transform, "fire_base");
        if (found != null) return found;

        return null;
    }

    private Transform CreateRuntimeFireBaseFrom(Transform source)
    {
        if (source == null)
            return null;

        GameObject fireBaseObject = new GameObject("firebase");
        Transform fireBase = fireBaseObject.transform;
        fireBase.SetParent(transform, false);
        fireBase.position = source.position;
        fireBase.rotation = source.rotation;
        fireBase.localScale = Vector3.one;
        return fireBase;
    }

    private bool TryGetRendererMuzzlePosition(out Vector3 position)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            position = default(Vector3);
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 forward = transform.forward;
        Vector3 center = bounds.center;
        float forwardExtent =
            Mathf.Abs(forward.x) * bounds.extents.x +
            Mathf.Abs(forward.y) * bounds.extents.y +
            Mathf.Abs(forward.z) * bounds.extents.z;
        position = center + forward * Mathf.Max(0.2f, forwardExtent + 0.08f);
        return true;
    }

    private static Transform FindChildByNameContains(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        string normalizedToken = token.ToLowerInvariant();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == root || child == null)
                continue;

            if (child.name.ToLowerInvariant().Contains(normalizedToken))
                return child;
        }

        return null;
    }

    private static Transform FindChildByNameEquals(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        string normalizedName = targetName.ToLowerInvariant();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == root || child == null)
                continue;

            if (child.name.ToLowerInvariant() == normalizedName)
                return child;
        }

        return null;
    }

    private void FireProjectile(Vector3 spawnPos, Vector3 aimPoint, float appliedDamage)
    {
        EnsureRuntimeReferences();

        Vector3 shootDir = (aimPoint - spawnPos).normalized;
        if (shootDir.sqrMagnitude < 0.0001f)
            shootDir = transform.forward;

        Vector3 tracerEndPoint = spawnPos + shootDir * range;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(spawnPos, shootDir, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (hasHit)
            tracerEndPoint = hit.point;

        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(shootDir));
            bulletObj.SetActive(true);

            PlayerBullet playerBullet = bulletObj.GetComponent<PlayerBullet>();
            if (playerBullet != null)
            {
                playerBullet.Initialize(
                    shootDir,
                    bulletSpeed,
                    appliedDamage,
                    bulletLifeTime,
                    ownerRoot != null ? ownerRoot : transform.root,
                    hitImpactPrefab
                );
            }
            else
            {
                Rigidbody bulletRb = bulletObj.GetComponent<Rigidbody>();
                if (bulletRb != null)
                    bulletRb.linearVelocity = shootDir * bulletSpeed;

                Destroy(bulletObj, bulletLifeTime);
            }
        }
        else
        {
            if (hasHit)
            {
                Lab_DoorPointer doorPointer = hit.collider.GetComponent<Lab_DoorPointer>();
                if (doorPointer == null)
                    doorPointer = hit.collider.GetComponentInParent<Lab_DoorPointer>();

                if (doorPointer != null)
                    doorPointer.Hit();

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo damageInfo = new DamageInfo(
                        appliedDamage,
                        DamageType.Bullet,
                        gameObject,
                        ownerRoot != null ? ownerRoot.gameObject : gameObject,
                        hit.point
                    );

                    damageable.TakeDamage(damageInfo);
                }

                if (hitImpactPrefab != null)
                {
                    Quaternion rot = Quaternion.LookRotation(hit.normal);
                    GameObject impact = Instantiate(hitImpactPrefab, hit.point, rot);
                    Destroy(impact, 2f);
                }
            }
        }

        ShowTracerInstance(spawnPos, tracerEndPoint);
    }

    private void ShowTracerInstance(Vector3 start, Vector3 end)
    {
        if (tracerPrefab == null) return;

        LineRenderer tracerInstance = Instantiate(tracerPrefab, Vector3.zero, Quaternion.identity);
        tracerInstance.positionCount = 2;
        tracerInstance.SetPosition(0, start);
        tracerInstance.SetPosition(1, end);

        StartCoroutine(DestroyTracerAfter(tracerInstance.gameObject, tracerTime));
    }

    private IEnumerator DestroyTracerAfter(GameObject tracerObj, float time)
    {
        yield return new WaitForSeconds(time);

        if (tracerObj != null)
            Destroy(tracerObj);
    }

    private Vector3 GetSpreadDirection(Vector3 baseDir, float spreadAngle)
    {
        Vector3 right = Vector3.Cross(Vector3.up, baseDir).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = transform.right;

        Vector3 up = Vector3.Cross(baseDir, right).normalized;

        float yaw = Random.Range(-spreadAngle, spreadAngle);
        float pitch = Random.Range(-spreadAngle, spreadAngle);

        Vector3 spreadDir =
            baseDir +
            right * Mathf.Tan(yaw * Mathf.Deg2Rad) +
            up * Mathf.Tan(pitch * Mathf.Deg2Rad);

        return spreadDir.normalized;
    }

    private void PlayShootFX(bool isShotgun)
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (audioSource != null)
        {
            if (isShotgun && shotgunClip != null)
                audioSource.PlayOneShot(shotgunClip);
            else if (!isShotgun && fireClip != null)
                audioSource.PlayOneShot(fireClip);
        }
    }

    private void PlayEmptySound()
    {
        if (audioSource != null && emptyClip != null)
            audioSource.PlayOneShot(emptyClip);
    }

    public void SetOwnerRoot(Transform rootTransform)
    {
        ownerRoot = rootTransform;
        battleInputOwner = ownerRoot != null ? ownerRoot.GetComponent<Hankyul_Battle>() : null;
    }

    private bool HasBattleInputOwner()
    {
        if (battleInputOwner == null)
        {
            if (ownerRoot != null) battleInputOwner = ownerRoot.GetComponent<Hankyul_Battle>();
            if (battleInputOwner == null) battleInputOwner = GetComponentInParent<Hankyul_Battle>();
        }
        return battleInputOwner != null && battleInputOwner.OwnsFireInput(this);
    }

    private bool IsGameplayBlocked()
    {
        return PlayerModalControlContext.Blocks(gameObject)
            || (ownerRoot != null && PlayerModalControlContext.Blocks(ownerRoot.gameObject));
    }

    public string GetAmmoText()
    {
        return $"{currentMagazineAmmo} / {GetTotalCurrentAmmo()}";
    }

    public int GetTotalCurrentAmmo()
    {
        return currentMagazineAmmo + currentReserveAmmo;
    }

    public bool IsMagazineEmpty()
    {
        return currentMagazineAmmo <= 0;
    }

    public bool CanReload()
    {
        return !isReloading && currentMagazineAmmo < magazineSize && currentReserveAmmo > 0;
    }

    public void AddReserveAmmo(int amount)
    {
        currentReserveAmmo += amount;
        ClampAmmo();
    }

    public void SetAmmo(int magAmmo, int reserveAmmo)
    {
        currentMagazineAmmo = magAmmo;
        currentReserveAmmo = reserveAmmo;
        ClampAmmo();
    }

    public bool RefillAmmoByShop(int amount)
    {
        if (amount <= 0) return false;

        int totalAmmo = currentMagazineAmmo + currentReserveAmmo;
        if (totalAmmo >= maxTotalAmmo) return false;

        int addableAmount = Mathf.Min(amount, maxTotalAmmo - totalAmmo);
        currentReserveAmmo += addableAmount;

        ClampAmmo();
        return addableAmount > 0;
    }
    public bool RefillAmmoBattery()
    {
        return RefillAmmoByShop(30);
    }

    public bool TryBuyEnergyCharge(out string message)
    {
        message = "";

        bool reserveFull = currentReserveAmmo >= 120;
        bool magazineFull = currentMagazineAmmo >= 30;

        if (reserveFull && magazineFull)
        {
            message = "에너지가 가득 찼습니다.";
            return false;
        }

        if (reserveFull)
        {
            currentMagazineAmmo = 30;
            ClampAmmo();
            return true;
        }

        currentReserveAmmo += 30;
        ClampAmmo();
        return true;
    }

    public bool TryBuyEnergyPack(out string message)
    {
        return UseEnergyPack(out message);
    }

    public bool UseEnergyPack(out string message)
    {
        message = "";

        bool magazineFull = currentMagazineAmmo >= magazineSize;
        bool reserveFull = currentReserveAmmo >= (maxTotalAmmo - magazineSize);

        if (magazineFull && reserveFull)
        {
            message = "에너지가 가득 찼습니다.";
            return false;
        }

        currentMagazineAmmo = magazineSize;
        currentReserveAmmo = maxTotalAmmo - magazineSize;
        ClampAmmo();
        return true;
    }
}
