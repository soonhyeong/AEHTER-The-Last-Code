using UnityEngine;
using Unity.Netcode;

// Per-consumer lifecycle cache. Identity policy belongs exclusively to LocalPlayerBindingResolver.
public sealed class LocalPlayerUIBinding
{
    public Transform Root { get; private set; }
    public bool Queried { get; private set; }
    public bool SourceChanged { get; private set; }
    private Component lastExplicitSource;
    private float nextQuery;

    public bool Poll(Component consumer, Component explicitSource = null, bool force = false)
    {
        Transform previous = Root;
        SourceChanged = !ReferenceEquals(lastExplicitSource, explicitSource);
        lastExplicitSource = explicitSource;
        force |= SourceChanged;
        Queried = false;
        Transform structural = LocalPlayerBindingResolver.PlayerRoot(consumer);
        bool valid = Root != null && LocalPlayerBindingResolver.TryResolve(Root, out var confirmed, false)
            && confirmed == Root && (structural == null || structural == Root);
        if (!valid && !ReferenceEquals(Root, null)) { Root = null; nextQuery = 0f; }
        if (force || Time.unscaledTime >= nextQuery)
        {
            Queried = true;
            nextQuery = Time.unscaledTime + 0.5f;
            var manager = NetworkManager.Singleton;
            Component context = consumer;
            if (manager == null || !manager.IsListening)
            {
                if (structural != null) context = structural;
                else if (explicitSource != null && LocalPlayerBindingResolver.IsLive(LocalPlayerBindingResolver.PlayerRoot(explicitSource))) context = explicitSource;
            }
            LocalPlayerBindingResolver.TryResolve(context, out var resolved);
            Root = resolved;
        }
        return !ReferenceEquals(previous, Root);
    }

    public void Clear() { Root = null; lastExplicitSource = null; nextQuery = 0f; }

    public static Transform CameraTransform(Transform root)
    {
        if (root == null) return null;
        var h = root.GetComponent<Move_Hankyul>();
        var y = root.GetComponent<Move_Yunseo>();
        Transform camera = h != null ? h.camTransform : y != null ? y.camTransform : null;
        if (camera != null && LocalPlayerBindingResolver.PlayerRoot(camera) == root) return camera;
        foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
            if (candidate.targetTexture == null && LocalPlayerBindingResolver.PlayerRoot(candidate) == root)
                return candidate.transform;
        return null;
    }
}
