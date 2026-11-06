using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs before authored Player Awake/Start and NGO's active scene-object sweep.
// Explicit Scene references only: never search/disable all unspawned NetworkObjects.
[DefaultExecutionOrder(-10000)]
public sealed class NetworkScenePlayerBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject[] authoredPlayers;

    private void Awake()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (gameObject.scene.name != GameSceneNetworkTransition.Destination
            || manager == null || !manager.IsListening || authoredPlayers == null)
            return;

        foreach (GameObject player in authoredPlayers)
        {
            if (player == null || player.scene != gameObject.scene)
                continue;
            NetworkObject identity = player.GetComponent<NetworkObject>();
            bool currentPlayer = player.GetComponent<Move_Hankyul>() != null
                || player.GetComponent<Move_Yunseo>() != null;
            if (identity == null || identity.IsSpawned || !currentPlayer
                || player.GetComponent<PlayerInteractor>() == null)
                continue;

            // Also retires the Scene-added Camera/Listener under this root.
            // Standalone saved active states and prefab assets are untouched.
            player.SetActive(false);
        }
    }
}

// Only the approved GameScene -> 05 route; other Scene flows remain local.
// One reservation is shared by the immediate and fade readers.
internal static class GameSceneNetworkTransition
{
    internal const string Destination = "05.폐허된 도시";
    private static NetworkManager manager;
    private static UnityEngine.Object owner;
    private static int sceneHandle;
    private static bool reserved;
    private static bool loadStarted;

    internal static bool IsNetworkRoute(string destination)
    {
        NetworkManager current = NetworkManager.Singleton;
        return SceneManager.GetActiveScene().name == "GameScene"
            && destination == Destination && current != null && current.IsListening;
    }

    internal static bool CanTrigger(Collider other)
    {
        NetworkManager current = NetworkManager.Singleton;
        NetworkObject player = other.GetComponentInParent<NetworkObject>();
        // Host-only contact policy; a remote replica touching the host's trigger
        // is not a request. No client local load or new RPC is introduced.
        return current != null && current.IsListening && current.IsServer
            && player != null && player.IsSpawned && player.IsPlayerObject
            && player.OwnerClientId == NetworkManager.ServerClientId;
    }

    internal static bool HasPending
    {
        get { ForgetOldContext(); return reserved; }
    }

    private static void ForgetOldContext()
    {
        if (manager != NetworkManager.Singleton
            || sceneHandle != SceneManager.GetActiveScene().handle
            || manager == null || !manager.IsListening)
        {
            reserved = false;
            loadStarted = false;
            owner = null;
            manager = null;
        }
    }

    internal static bool TryReserve(UnityEngine.Object requester, string destination)
    {
        ForgetOldContext();
        NetworkManager current = NetworkManager.Singleton;
        if (!IsNetworkRoute(destination) || !current.IsServer || reserved)
            return false;
        if (current.SceneManager == null || !current.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogWarning("[NetworkTransition] NGO scene management is unavailable.");
            return false;
        }
        manager = current;
        owner = requester;
        sceneHandle = SceneManager.GetActiveScene().handle;
        reserved = true;
        loadStarted = false;
        return true;
    }

    internal static bool TryLoad(UnityEngine.Object requester, string destination)
    {
        if (!reserved || owner != requester || loadStarted)
            return false;
        if (!IsNetworkRoute(destination) || manager != NetworkManager.Singleton
            || !manager.IsServer || manager.SceneManager == null
            || !manager.NetworkConfig.EnableSceneManagement)
        {
            Cancel(requester);
            Debug.LogWarning("[NetworkTransition] Session changed before scene load; request cancelled.");
            return false;
        }
        // Hold the shared guard through NGO's synchronous callbacks.
        loadStarted = true;
        try
        {
            SceneEventProgressStatus status = manager.SceneManager.LoadScene(destination, LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
                return true;
            Debug.LogWarning($"[NetworkTransition] Scene load not started: {status}.");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[NetworkTransition] Scene load failed: {exception.Message}");
        }
        loadStarted = false;
        Cancel(requester);
        return false;
    }

    internal static void Cancel(UnityEngine.Object requester)
    {
        if (!reserved || owner != requester || loadStarted)
            return;
        reserved = false;
        owner = null;
        manager = null;
    }
}
