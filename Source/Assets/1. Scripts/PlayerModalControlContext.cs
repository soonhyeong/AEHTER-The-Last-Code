using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runtime-only, one context on an explicitly resolved Player hierarchy. No global Player.
[DisallowMultipleComponent]
public sealed class PlayerModalControlContext : MonoBehaviour
{
    [Flags]
    public enum Options { None = 0, Gameplay = 1, PlayerInput = 2, Cursor = 4, Default = Gameplay | Cursor }

    public sealed class Lease : IDisposable
    {
        internal PlayerModalControlContext context;
        internal MonoBehaviour owner;
        internal Options options;
        internal CursorModalBroker.Lease cursor;
        public bool IsReleased { get; internal set; }
        public void Release() { if (!IsReleased && context != null) context.ReleaseLease(this); }
        public void Dispose() { Release(); }
    }

    private readonly List<Lease> leases = new List<Lease>();
    private int gameplayOwners;
    private int inputOwners;
    private Action restoreMove;
    private PlayerInput savedInput;
    private InputActionMap savedMap;
    private bool savedInputActive;
    private bool savedMapEnabled;
    private bool inputRestorePending;
    public event Action GameplayBlocked;

    public bool IsGameplayInputBlocked { get { PruneOwners(); return gameplayOwners > 0; } }
    public bool CanMove => !IsGameplayInputBlocked;
    public bool CanLook => !IsGameplayInputBlocked;
    public bool CanBattle => !IsGameplayInputBlocked;
    public bool CanInteract => !IsGameplayInputBlocked;

    public static PlayerModalControlContext For(GameObject anchor, bool create = false)
    {
        if (anchor == null) return null;
        // Only the supplied hierarchy is searched; never siblings or a scene-wide first Player.
        Transform root = null;
        for (Transform t = anchor.transform; t != null; t = t.parent)
        {
            if (t.GetComponent<Move_Hankyul>() != null || t.GetComponent<Move_Yunseo>() != null
                || t.GetComponent<Hankyul_Move>() != null || t.GetComponent<Yunseo_Move>() != null)
            { root = t; break; }
        }
        if (root == null)
        {
            for (Transform t = anchor.transform; t != null; t = t.parent)
                if (t.GetComponent<PlayerInteractor>() != null || t.GetComponent<PlayerInput>() != null)
                { root = t; break; }
        }
        if (root == null) return null;
        var context = root.GetComponent<PlayerModalControlContext>();
        return context != null || !create ? context : root.gameObject.AddComponent<PlayerModalControlContext>();
    }

    public static bool Blocks(GameObject anchor)
    {
        var context = For(anchor);
        return context != null && context.IsGameplayInputBlocked;
    }

    public Lease Acquire(MonoBehaviour owner, Options options = Options.Default)
    {
        if (owner == null || !owner.isActiveAndEnabled || !isActiveAndEnabled) return null;
        PruneOwners();
        foreach (var existing in leases)
            if (ReferenceEquals(existing.owner, owner)) return existing;
        var lease = new Lease { context = this, owner = owner, options = options };
        leases.Add(lease);
        bool firstGameplay = false;
        try
        {
            if ((options & Options.Gameplay) != 0 && gameplayOwners++ == 0)
            {
                firstGameplay = true;
                SnapshotAndLockMove();
            }
            if ((options & Options.PlayerInput) != 0 && inputOwners++ == 0)
            {
                // A remote release may still owe the original local snapshot. A new modal
                // must not replace it with the temporary ownership-disabled state.
                if (!inputRestorePending || savedInput == null)
                {
                    savedInput = GetComponent<PlayerInput>();
                    if (savedInput != null)
                    {
                        savedInputActive = savedInput.inputIsActive;
                        savedMap = savedInput.currentActionMap;
                        savedMapEnabled = savedMap != null && savedMap.enabled;
                    }
                }
                inputRestorePending = false;
                if (savedInput != null) savedInput.DeactivateInput();
            }
            if ((options & Options.Cursor) != 0)
                lease.cursor = CursorModalBroker.Acquire(lease, owner);
            if (firstGameplay) GameplayBlocked?.Invoke();
            return lease;
        }
        catch { lease.Release(); throw; }
    }

    public void Release(MonoBehaviour owner)
    {
        for (int i = leases.Count - 1; i >= 0; i--)
            if (ReferenceEquals(leases[i].owner, owner)) ReleaseLease(leases[i]);
    }

    private void ReleaseLease(Lease lease)
    {
        if (lease.IsReleased || !leases.Remove(lease)) return;
        lease.IsReleased = true;
        lease.context = null;
        try
        {
            if ((lease.options & Options.PlayerInput) != 0 && --inputOwners == 0)
                RestoreInput();
        }
        finally
        {
            try
            {
                if ((lease.options & Options.Gameplay) != 0 && --gameplayOwners == 0)
                {
                    var restore = restoreMove;
                    restoreMove = null;
                    restore?.Invoke();
                }
            }
            finally { lease.cursor?.Release(); }
        }
    }

    private void SnapshotAndLockMove()
    {
        restoreMove = null;
        var h = GetComponent<Move_Hankyul>();
        if (h != null)
        {
            bool input = h.allowInput, move = h.allowMove, look = h.allowLook;
            restoreMove += () => { if (h != null) { h.allowInput = input; h.allowMove = move; h.allowLook = look; } };
            h.allowInput = h.allowMove = h.allowLook = false;
        }
        var y = GetComponent<Move_Yunseo>();
        if (y != null)
        {
            bool move = y.allowMove, look = y.allowLook;
            restoreMove += () => { if (y != null) { y.SetMoveEnabled(move); y.SetLookEnabled(look); } };
            y.SetMoveEnabled(false); y.SetLookEnabled(false);
        }
        // Preserve PuzzleOpener's serialized legacy contract without adding legacy components.
        var lh = GetComponent<Hankyul_Move>();
        if (lh != null)
        {
            bool move = lh.allowMove, look = lh.allowLook;
            restoreMove += () => { if (lh != null) { lh.allowMove = move; lh.allowLook = look; } };
            lh.allowMove = lh.allowLook = false;
        }
        var ly = GetComponent<Yunseo_Move>();
        if (ly != null)
        {
            bool move = ly.allowMove, look = ly.allowLook;
            restoreMove += () => { if (ly != null) { ly.allowMove = move; ly.allowLook = look; } };
            ly.allowMove = ly.allowLook = false;
        }
    }

    private void RestoreInput()
    {
        if (savedInput != null && !PlayerLocalInputOwnership.CanConsumeLocalInput(savedInput))
        {
            // currentActionMap's setter enables its map. Do not use it on a remote restore.
            savedInput.DeactivateInput();
            savedMap?.Disable();
            inputRestorePending = true;
            return;
        }
        else if (savedInput != null)
        {
            // ActivateInput can select the default map. Restore the actual saved map afterwards.
            if (savedInputActive) savedInput.ActivateInput(); else savedInput.DeactivateInput();
            savedInput.currentActionMap = savedMap;
            if (savedMap != null)
            {
                if (savedMapEnabled) savedMap.Enable(); else savedMap.Disable();
            }
        }
        savedInput = null;
        savedMap = null;
        inputRestorePending = false;
    }

    private void PruneOwners()
    {
        for (int i = leases.Count - 1; i >= 0; i--)
            if (leases[i].owner == null || !leases[i].owner.isActiveAndEnabled)
                ReleaseLease(leases[i]);
    }
    private void OnSceneUnloaded(Scene scene)
    {
        for (int i = leases.Count - 1; i >= 0; i--)
            if (leases[i].owner == null || leases[i].owner.gameObject.scene.handle == scene.handle)
                ReleaseLease(leases[i]);
    }
    private void OnEnable() { SceneManager.sceneUnloaded += OnSceneUnloaded; }
    private void Update()
    {
        PruneOwners();
        // Only finish our deferred snapshot, never generally force owner PlayerInput on.
        if (inputRestorePending && inputOwners == 0
            && PlayerLocalInputOwnership.CanConsumeLocalInput(this)) RestoreInput();
    }
    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        ReleaseAll();
    }
    private void OnDestroy() { ReleaseAll(); }
    private void ReleaseAll()
    {
        while (leases.Count > 0) ReleaseLease(leases[leases.Count - 1]);
    }
}
