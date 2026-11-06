using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class ThirdZoneRuntimeCombatBootstrap : MonoBehaviour
{
    private const string TargetSceneNamePrefix = "11.3";
    private const string GeneratedRootName = "AETHER_ThirdZone_RuntimeCombat";

    [SerializeField] private bool runOnlyInThirdZone = true;
    [SerializeField] private string robotResourcePath = "AETHERRuntime/Robot";
    [SerializeField] private string droneAttackerResourcePath = "AETHERRuntime/Drone_Attacker";
    [SerializeField] private string droneObserverResourcePath = "AETHERRuntime/Drone_Observer";
    [SerializeField] private string turretResourcePath = "AETHERRuntime/turret_1_1";
    [SerializeField] private string robotBulletResourcePath = "AETHERRuntime/RobotBullet";
    [SerializeField] private string enemyBulletResourcePath = "AETHERRuntime/EnemyBullet";

    private static readonly HashSet<int> completedScenes = new HashSet<int>();
    private const string AuthoredRootName = "AETHER_ThirdZone_CombatSetup";

    private static readonly Vector3[] RobotOffsets =
    {
        new Vector3(0f, 0f, 7f),
        new Vector3(-8f, 0f, 12f),
        new Vector3(9f, 0f, 14f),
        new Vector3(-18f, 0f, 26f),
        new Vector3(20f, 0f, 28f),
        new Vector3(0f, 0f, 36f),
        new Vector3(-28f, 0f, 42f),
        new Vector3(30f, 0f, 45f),
        new Vector3(-12f, 0f, -16f),
        new Vector3(14f, 0f, -18f),
        new Vector3(-32f, 0f, -30f),
        new Vector3(34f, 0f, -34f),
    };

    private static readonly Vector3[] DroneOffsets =
    {
        new Vector3(4f, 6f, 12f),
        new Vector3(-14f, 7f, 22f),
        new Vector3(18f, 7f, 25f),
        new Vector3(-24f, 8f, -22f),
        new Vector3(26f, 8f, -26f),
        new Vector3(0f, 9f, 46f),
    };

    private static readonly Vector3[] TurretOffsets =
    {
        new Vector3(-10f, 0f, 18f),
        new Vector3(12f, 0f, 22f),
        new Vector3(-24f, 0f, -24f),
        new Vector3(26f, 0f, -28f),
        new Vector3(3f, 0f, 52f),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        EnsureBootstrap(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        completedScenes.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallbacks()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureBootstrap(scene); }
    private static void HandleSceneUnloaded(Scene scene) { completedScenes.Remove(scene.handle); }

    private static void EnsureBootstrap(Scene scene)
    {
        if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded
            || !IsThirdZoneScene(scene.name) || completedScenes.Contains(scene.handle))
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<ThirdZoneRuntimeCombatBootstrap>(true) != null)
                return;

        GameObject host = new GameObject("AETHER_ThirdZone_RuntimeBootstrap");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<ThirdZoneRuntimeCombatBootstrap>();
    }

    private IEnumerator Start()
    {
        Scene scene = gameObject.scene;
        if ((runOnlyInThirdZone && !IsThirdZoneScene(scene.name))
            || completedScenes.Contains(scene.handle))
        {
            Destroy(gameObject);
            yield break;
        }

        // The current multiplayer flow ends at05; do not create client-owned AI or extra Players.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Destroy(gameObject);
            yield break;
        }

        for (int frame = 0; frame < 120; frame++)
        {
            if (Setup())
            {
                completedScenes.Add(scene.handle);
                Destroy(gameObject);
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("[ThirdZoneRuntimeCombat] Unique scene Player/resources not ready; no fallback Player or partial group installed.", this);
        Destroy(gameObject);
    }

    private static bool IsThirdZoneScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith(TargetSceneNamePrefix);
    }

    private GameObject SceneRoot(string name)
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (root.name == name && root.activeInHierarchy) return root;
        return null;
    }

    private bool Setup()
    {
        Transform player = FindOrCreatePlayer();
        if (player == null)
        {
            return false;
        }

        EnsurePlayerTag(player.gameObject);

        GameObject existing = SceneRoot(AuthoredRootName) ?? SceneRoot(GeneratedRootName);
        if (existing != null)
        {
            // Preserve authored enemy placements/counts/state; rebind targets copied during Awake.
            EnsurePlayerReferences(existing, player);
            GameObject robotBullet = Resources.Load<GameObject>(robotBulletResourcePath);
            foreach (Robots robot in existing.GetComponentsInChildren<Robots>(true))
                if (robotBullet != null) ConfigureProjectile(robot.gameObject, robotBullet);
            GameObject enemyBullet = Resources.Load<GameObject>(enemyBulletResourcePath);
            foreach (EnemyTurretController turret in existing.GetComponentsInChildren<EnemyTurretController>(true))
                if (enemyBullet != null) turret.Configure(enemyBullet);
            return true;
        }

        GameObject robotPrefab = Resources.Load<GameObject>(robotResourcePath);
        GameObject droneAttackerPrefab = Resources.Load<GameObject>(droneAttackerResourcePath);
        GameObject droneObserverPrefab = Resources.Load<GameObject>(droneObserverResourcePath);
        GameObject turretPrefab = Resources.Load<GameObject>(turretResourcePath);
        GameObject robotBulletPrefab = Resources.Load<GameObject>(robotBulletResourcePath);
        GameObject enemyBulletPrefab = Resources.Load<GameObject>(enemyBulletResourcePath);
        if (robotPrefab == null || droneAttackerPrefab == null || droneObserverPrefab == null
            || turretPrefab == null || robotBulletPrefab == null || enemyBulletPrefab == null)
            return false;

        Transform root = new GameObject(GeneratedRootName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);

        SpawnGroup(robotPrefab, RobotOffsets, root, player, "ThirdZone_Robot", robotBulletPrefab);
        SpawnDroneGroup(droneAttackerPrefab, droneObserverPrefab, DroneOffsets, root, player);
        SpawnGroup(turretPrefab, TurretOffsets, root, player, "ThirdZone_Turret", enemyBulletPrefab);
        return true;
    }

    private Transform FindOrCreatePlayer()
    {
        Transform unique = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (!root.activeInHierarchy) continue;
            foreach (Move_Hankyul move in root.GetComponentsInChildren<Move_Hankyul>(true))
            {
                if (!LocalPlayerBindingResolver.IsLive(move.transform)) continue;
                if (unique != null && unique != move.transform) return null;
                unique = move.transform;
            }
            foreach (Move_Yunseo move in root.GetComponentsInChildren<Move_Yunseo>(true))
            {
                if (!LocalPlayerBindingResolver.IsLive(move.transform)) continue;
                if (unique != null && unique != move.transform) return null;
                unique = move.transform;
            }
        }
        return unique;
    }

    private static void SpawnDroneGroup(GameObject attackerPrefab, GameObject observerPrefab, Vector3[] offsets, Transform parent, Transform player)
    {
        List<GameObject> variants = new List<GameObject>();
        if (attackerPrefab != null) variants.Add(attackerPrefab);
        if (observerPrefab != null) variants.Add(observerPrefab);
        if (variants.Count == 0)
            return;

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject prefab = variants[i % variants.Count];
            SpawnOne(prefab, player.position + offsets[i], parent, player, $"ThirdZone_Drone_{i + 1:00}", null, true, i);
        }
    }

    private static void SpawnGroup(GameObject prefab, Vector3[] offsets, Transform parent, Transform player, string namePrefix, GameObject bulletPrefab)
    {
        if (prefab == null)
            return;

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 position = player.position + offsets[i];
            bool isDrone = prefab.GetComponentInChildren<Drones>(true) != null;
            if (!isDrone)
                position = SnapToGround(position);

            SpawnOne(prefab, position, parent, player, $"{namePrefix}_{i + 1:00}", bulletPrefab, isDrone, i);
        }
    }

    private static GameObject SpawnOne(GameObject prefab, Vector3 position, Transform parent, Transform player, string objectName, GameObject bulletPrefab, bool isDrone, int index)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, parent);
        instance.name = objectName;

        Vector3 direction = player.position - instance.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            instance.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        EnsureEnemyTagAndLayer(instance);
        EnsurePlayerReferences(instance, player);
        ConfigureProjectile(instance, bulletPrefab);
        RandomizeEnemyStart(instance, isDrone, index);
        return instance;
    }

    private static Vector3 SnapToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 40f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
            return navHit.position;

        return position;
    }

    private static void EnsurePlayerReferences(GameObject enemyRoot, Transform player)
    {
        Robots[] robots = enemyRoot.GetComponentsInChildren<Robots>(true);
        foreach (Robots robot in robots)
        {
            SetPrivateField(robot, "player", player);
            RobotCombat combat = robot.GetComponent<RobotCombat>();
            if (combat != null) combat.SetTarget(player);
        }

        Drones[] drones = enemyRoot.GetComponentsInChildren<Drones>(true);
        foreach (Drones drone in drones)
            SetPrivateField(drone, "player", player);
        foreach (EnemyTurretController turret in enemyRoot.GetComponentsInChildren<EnemyTurretController>(true))
            SetPrivateField(turret, "player", player);
    }

    private static void ConfigureProjectile(GameObject enemyRoot, GameObject bulletPrefab)
    {
        if (bulletPrefab == null)
            return;

        Robots[] robots = enemyRoot.GetComponentsInChildren<Robots>(true);
        foreach (Robots robot in robots)
        {
            SetPrivateField(robot, "bulletPrefab", bulletPrefab);
            RobotCombat combat = robot.GetComponent<RobotCombat>();
            if (combat != null) SetPrivateField(combat, "bulletPrefab", bulletPrefab);
        }

        DroneAttackerCombat[] droneAttackers = enemyRoot.GetComponentsInChildren<DroneAttackerCombat>(true);
        foreach (DroneAttackerCombat droneAttacker in droneAttackers)
            droneAttacker.ConfigureLegacy(bulletPrefab, null, 18f, 10f, 0.8f, 18f, 10, 4f);

        EnemyTurretController[] turrets = enemyRoot.GetComponentsInChildren<EnemyTurretController>(true);
        foreach (EnemyTurretController turret in turrets)
            turret.Configure(bulletPrefab);
    }

    private static void RandomizeEnemyStart(GameObject enemyRoot, bool isDrone, int index)
    {
        Animator animator = enemyRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.speed = Mathf.Clamp(0.9f + (index % 5) * 0.05f, 0.9f, 1.12f);

        NavMeshAgent agent = enemyRoot.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.avoidancePriority = Mathf.Clamp(40 + index * 4, 0, 99);
            agent.Warp(enemyRoot.transform.position);
        }

        if (isDrone)
            enemyRoot.transform.position += Vector3.up * (index % 2 == 0 ? 0.5f : 1.2f);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        if (field != null && field.FieldType.IsInstanceOfType(value))
            field.SetValue(target, value);
    }

    private static void EnsurePlayerTag(GameObject player)
    {
        try
        {
            player.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[ThirdZoneRuntimeCombat] Player tag is missing.");
        }
    }

    private static void EnsureEnemyTagAndLayer(GameObject enemy)
    {
        try
        {
            enemy.tag = "Enemy";
        }
        catch (UnityException)
        {
        }

        SetLayerRecursively(enemy, LayerMask.NameToLayer("Enemy"));
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
