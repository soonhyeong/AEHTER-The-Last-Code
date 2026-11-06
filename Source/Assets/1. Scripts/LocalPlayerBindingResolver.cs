using UnityEngine;
using Unity.Netcode;

// Data binding identity only. No input grants, component creation or state restoration.
public static class LocalPlayerBindingResolver
{
    public static Transform PlayerRoot(Component context)
    {
        for (Transform t = context != null ? context.transform : null; t != null; t = t.parent)
            if (t.GetComponent<Move_Hankyul>() != null || t.GetComponent<Move_Yunseo>() != null)
                return t;
        return null;
    }

    public static bool TryResolve(Component context, out Transform root, bool allowUniqueFallback = true)
    {
        root = null;
        // A destroyed explicit context is not an omitted context and must not choose another Player.
        if (!ReferenceEquals(context, null) && context == null) return false;
        Transform explicitRoot = PlayerRoot(context);
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            NetworkObject player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            if (player == null || !player.IsSpawned || !player.IsOwner || player.NetworkManager != manager)
                return false;
            Transform candidate = PlayerRoot(player);
            if (candidate != player.transform || !IsLive(candidate)) return false;
            if (explicitRoot != null && explicitRoot != candidate) return false;
            root = candidate;
            return true;
        }

        if (explicitRoot != null)
        {
            if (!IsLive(explicitRoot)) return false;
            root = explicitRoot;
            return true;
        }
        if (!allowUniqueFallback) return false;
        Transform unique = null;
        foreach (var move in UnityEngine.Object.FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (IsLive(move.transform) && !AcceptUnique(move.transform, ref unique)) return false;
        foreach (var move in UnityEngine.Object.FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (IsLive(move.transform) && !AcceptUnique(move.transform, ref unique)) return false;
        root = unique;
        return root != null;
    }

    private static bool AcceptUnique(Transform candidate, ref Transform unique)
    {
        if (unique != null && unique != candidate) return false;
        unique = candidate;
        return true;
    }

    public static bool IsLive(Transform root)
    {
        return root != null && root.gameObject.activeInHierarchy && root.gameObject.scene.IsValid()
            && root.gameObject.scene.isLoaded;
    }

    public static T Owned<T>(Transform root) where T : Component
    {
        if (root == null) return null;
        foreach (var component in root.GetComponentsInChildren<T>(true))
            if (PlayerRoot(component) == root) return component;
        return null;
    }
}
