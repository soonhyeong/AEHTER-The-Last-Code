using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// OS-global cursor requests, deliberately separate from per-Player gameplay ownership.
public sealed class CursorModalBroker : MonoBehaviour
{
    public sealed class Lease : IDisposable
    {
        internal object identity;
        internal UnityEngine.Object lifetime;
        internal bool hasLifetime;
        internal int sceneHandle;
        public bool IsReleased { get; internal set; }
        public void Release()
        {
            if (IsReleased) return;
            IsReleased = true;
            owners.Remove(this);
            RestoreIfEmpty();
        }
        public void Dispose() { Release(); }
    }

    private static readonly List<Lease> owners = new List<Lease>();
    private static readonly Dictionary<string, Lease> legacy = new Dictionary<string, Lease>();
    private static CursorModalBroker runner;
    private static CursorLockMode savedLock;
    private static bool savedVisible;
    private static bool hasSnapshot;

    public static Lease Acquire(object identity, UnityEngine.Object lifetime = null)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        Prune();
        foreach (var existing in owners)
            if (ReferenceEquals(existing.identity, identity)) return existing;
        if (!hasSnapshot)
        {
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            hasSnapshot = true;
        }
        var lease = new Lease { identity = identity, lifetime = lifetime, hasLifetime = lifetime != null,
            sceneHandle = lifetime is Component component ? component.gameObject.scene.handle : SceneManager.GetActiveScene().handle };
        owners.Add(lease);
        if (runner == null)
        {
            var go = new GameObject("CursorModalBroker");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<CursorModalBroker>();
        }
        Apply();
        return lease;
    }

    // Compatibility only for non-migrated Shop. Core callers never use string identities.
    public static void SetLegacyRequest(string key, bool visible)
    {
        key = string.IsNullOrEmpty(key) ? "UI" : key;
        if (visible)
        {
            if (!legacy.TryGetValue(key, out var lease) || lease.IsReleased)
                legacy[key] = Acquire(new object());
            Apply();
        }
        else if (legacy.TryGetValue(key, out var lease))
        {
            legacy.Remove(key);
            lease.Release();
        }
    }

    private static void Apply()
    {
        if (owners.Count == 0) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private static void RestoreIfEmpty()
    {
        if (owners.Count != 0) { Apply(); return; }
        if (!hasSnapshot) return;
        hasSnapshot = false;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }
    private static void Prune()
    {
        for (int i = owners.Count - 1; i >= 0; i--)
        {
            var lease = owners[i];
            if (lease.hasLifetime && (lease.lifetime == null ||
                (lease.lifetime is Behaviour behaviour && !behaviour.isActiveAndEnabled))) lease.Release();
        }
    }
    private void OnEnable() { SceneManager.sceneUnloaded += OnSceneUnloaded; }
    private void OnSceneUnloaded(Scene scene)
    {
        for (int i = owners.Count - 1; i >= 0; i--)
        {
            var lease = owners[i];
            int handle = lease.lifetime is Component component && component != null
                ? component.gameObject.scene.handle : lease.sceneHandle;
            if (handle == scene.handle) lease.Release();
        }
    }
    private void LateUpdate() { Prune(); Apply(); }
    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Clear();
    }
    private static void Clear()
    {
        while (owners.Count > 0) owners[owners.Count - 1].Release();
        legacy.Clear();
        runner = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Clear(); }
}
