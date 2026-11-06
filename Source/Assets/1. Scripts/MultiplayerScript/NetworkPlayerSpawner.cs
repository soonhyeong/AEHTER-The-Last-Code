using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [Header("ĳ���� Prefab")]
    [SerializeField] private GameObject yunseoPrefab;
    [SerializeField] private GameObject hangyeolPrefab;

    [Header("Spawn ��ġ")]
    [SerializeField] private Transform yunseoSpawnPoint;
    [SerializeField] private Transform hangyeolSpawnPoint;

    private NetworkManager subscribedManager;
    private bool started;

    private void Start()
    {
        started = true;
        SubscribeAndCatchUp();
    }

    private void OnEnable()
    {
        if (started)
            SubscribeAndCatchUp();
    }

    private void SubscribeAndCatchUp()
    {
        Unsubscribe();
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager�� �����ϴ�.");
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (!manager.IsServer || !manager.IsListening)
            return;

        subscribedManager = manager;
        subscribedManager.OnClientConnectedCallback += OnClientConnected;

        // �̹� Host�� ���ӵ� ���¿��� GameScene���� �Ѿ�Դٸ�
        // ����� �÷��̾ Ȯ��
        if (subscribedManager.IsServer)
        {
            foreach (ulong clientId in subscribedManager.ConnectedClientsIds)
            {
                SpawnPlayerIfNeeded(clientId);
            }
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        // Unsubscribe from the instance we actually subscribed to, even if
        // Singleton was cleared or replaced during scene teardown.
        if (subscribedManager != null)
        {
            subscribedManager.OnClientConnectedCallback -= OnClientConnected;
        }
        subscribedManager = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        SpawnPlayerIfNeeded(clientId);
    }

    private void SpawnPlayerIfNeeded(ulong clientId)
    {
        NetworkManager manager = subscribedManager;
        if (!isActiveAndEnabled || manager == null || manager != NetworkManager.Singleton
            || !manager.IsServer || !manager.IsListening)
        {
            return;
        }

        // NGO assigns PlayerObject synchronously before spawn callbacks.
        // Catch-up, repeated callbacks and another scene-local spawner all
        // observe the same server-owned assignment.
        if (!manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            || client == null || client.PlayerObject != null)
            return;

        // Host(ClientId 0) = ����
        bool isHostPlayer =
            clientId == NetworkManager.ServerClientId;

        GameObject selectedPrefab;
        Transform selectedSpawnPoint;

        if (isHostPlayer)
        {
            selectedPrefab = yunseoPrefab;
            selectedSpawnPoint = yunseoSpawnPoint;
        }
        else
        {
            selectedPrefab = hangyeolPrefab;
            selectedSpawnPoint = hangyeolSpawnPoint;
        }

        if (selectedPrefab == null)
        {
            Debug.LogError($"NetworkPlayerSpawner: missing player prefab for client {clientId}.");
            return;
        }

        if (selectedPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"NetworkPlayerSpawner: prefab '{selectedPrefab.name}' requires a root NetworkObject.");
            return;
        }

        Vector3 spawnPosition =
            selectedSpawnPoint != null
            ? selectedSpawnPoint.position
            : Vector3.zero;

        Quaternion spawnRotation =
            selectedSpawnPoint != null
            ? selectedSpawnPoint.rotation
            : Quaternion.identity;

        GameObject player =
            Instantiate(
                selectedPrefab,
                spawnPosition,
                spawnRotation
            );

        NetworkObject networkObject =
            player.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(
            clientId,
            true
        );

        Debug.Log(
            $"�÷��̾� ���� �Ϸ� / ClientId: {clientId} / " +
            $"Character: {selectedPrefab.name}"
        );
    }
}
