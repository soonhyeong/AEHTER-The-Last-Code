using System.Collections.Generic;
using UnityEngine;

// Runtime-only state owned by one explicit PlayerInteractor. No global input/target state.
public sealed class PlayerInteractionArbiter
{
    public enum Capability { InstantOnly, HoldOnly, DualShortHold }
    public enum Phase { Idle, Pressed, Completed, Cancelled, AwaitRelease }

    public struct Candidate
    {
        public MonoBehaviour target;
        public IInteractionCandidateProvider provider;
        public bool raycast;
        public float distance;
        public int Identity => target != null ? target.gameObject.GetInstanceID() : 0;
        public Capability Kind => target is ILongPressInteractable ? Capability.DualShortHold
            : target is IHoldInteractable ? Capability.HoldOnly : Capability.InstantOnly;
        public bool IsAlive => target != null && target.isActiveAndEnabled;
    }

    private readonly PlayerInteractor owner;
    private readonly PlayerLocalInputOwnership.InputGate localInputGate = new PlayerLocalInputOwnership.InputGate();
    private bool ownershipSuppressed;
    private readonly List<IInteractionCandidateProvider> providers = new List<IInteractionCandidateProvider>();
    private readonly List<Candidate> candidates = new List<Candidate>();
    private Candidate pending;
    private IInteractionCandidateProvider presentation;
    private float elapsed;
    private float duration;
    private bool beganHold;
    private bool ticking;
    private int lastDownFrame = -1;
    private int lastTickFrame = -1;
    public Phase State { get; private set; }
    public ulong Generation { get; private set; }
    internal bool CommitInterrupted { get; private set; }

    public PlayerInteractionArbiter(PlayerInteractor owner)
    {
        this.owner = owner;
        // Also discover providers when the Player spawns after their OnEnable.
        foreach (var p in UnityEngine.Object.FindObjectsByType<ProximityPuzzleOpener>(FindObjectsSortMode.None)) Register(p);
        foreach (var p in UnityEngine.Object.FindObjectsByType<ProximitySceneTransition>(FindObjectsSortMode.None)) Register(p);
    }

    internal void InvalidateTarget(MonoBehaviour target)
    {
        if (State == Phase.Pressed && target != null && pending.Identity == target.gameObject.GetInstanceID()) Cancel();
    }

    private bool TargetAllows(Candidate candidate)
    {
        // A ray provider must not bypass a special target's range/trigger/transition guard.
        return !(candidate.target is IInteractionCandidateProvider targetProvider)
            || (ProviderAlive(targetProvider) && targetProvider.TryGetCandidate(owner, out _));
    }

    public void Register(IInteractionCandidateProvider provider)
    {
        if (provider != null && !providers.Contains(provider)) providers.Add(provider);
    }

    public void Unregister(IInteractionCandidateProvider provider)
    {
        providers.Remove(provider);
        if (ReferenceEquals(pending.provider, provider) && State == Phase.Pressed) Cancel();
        if (ReferenceEquals(presentation, provider)) HidePresentation();
    }

    public static bool ProviderAlive(IInteractionCandidateProvider provider)
    {
        return provider is MonoBehaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;
    }

    private bool Blocked => !PlayerLocalInputOwnership.CanConsumeLocalInput(owner) || owner == null || !owner.isActiveAndEnabled
        || PlayerModalControlContext.Blocks(owner.gameObject);

    internal bool CanReadLocalInput()
    {
        if (!localInputGate.CanRead(owner))
        {
            ownershipSuppressed = true;
            Cancel();
            return false;
        }
        if (ownershipSuppressed)
        {
            // InputGate has already observed neutral and excluded that entire frame.
            // The cancelled press is gone; only a fresh Down can start the next generation.
            ownershipSuppressed = false;
            State = Phase.Idle;
        }
        return true;
    }

    // Called once by the Player's input owner, not by provider Update order.
    public void Tick(bool down, bool held, bool up, float deltaTime, int frame)
    {
        // Also check before the same-frame guard: loss must cancel a pending hold immediately.
        if (!CanReadLocalInput()) return;
        if (ticking || lastTickFrame == frame) return;
        lastTickFrame = frame;
        ticking = true;
        try
        {
            bool freshDown = down && lastDownFrame != frame;
            if (freshDown) { lastDownFrame = frame; Generation++; }
            bool blocked = Blocked;
            if (blocked) { Cancel(); if (!held && !down) State = Phase.Idle; return; }
            Candidate best = SelectCandidate();
            if (Blocked) { Cancel(); return; }

            if (State == Phase.AwaitRelease)
            {
                HidePresentation();
                if (up || (!held && !down)) State = Phase.Idle;
                return;
            }
            if (State == Phase.Idle)
            {
                Present(best, 0f);
                if (State != Phase.Idle || Blocked) { Cancel(); return; }
                if (!freshDown)
                {
                    // Enabling/entering range while F is already held is never a new press.
                    if (held) { State = Phase.AwaitRelease; HidePresentation(); }
                    return;
                }
                if (!best.IsAlive) { State = Phase.AwaitRelease; HidePresentation(); return; }
                pending = best;
                elapsed = 0f;
                beganHold = false;
                State = Phase.Pressed;
                if (pending.Kind == Capability.InstantOnly) { Commit(false); if (up) State = Phase.Idle; return; }
                duration = pending.target is ILongPressInteractable dual ? dual.LongPressDuration
                    : ((IHoldInteractable)pending.target).GetHoldDuration();
                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
                { Cancel(); return; }
                if (!ValidPending(best)) { Cancel(); return; }
                if (pending.target is IHoldInteractable hold)
                {
                    beganHold = true;
                    hold.BeginHold(owner.gameObject);
                }
                if (State != Phase.Pressed || !ValidPending(best)) { Cancel(); return; }
                // A Down+Up sample is a short gesture, not a fabricated full frame of hold.
                if (up) FinishRelease();
                return;
            }
            if (State != Phase.Pressed) return;
            if (!ValidPending(best)) { Cancel(); return; }
            elapsed += Mathf.Max(0f, deltaTime);
            if (pending.target is IHoldInteractable progressTarget)
                progressTarget.UpdateHold(owner.gameObject, Mathf.Clamp01(elapsed / duration));
            if (State != Phase.Pressed || !ValidPending(best)) { Cancel(); return; }
            Present(pending, Mathf.Clamp01(elapsed / duration));
            if (State != Phase.Pressed || !ValidPending(best)) { Cancel(); return; }
            // Equality belongs to Hold, including an Up sample in the same update.
            if (elapsed >= duration) { Commit(true); if (up || !held) State = Phase.Idle; }
            else if (up || !held) FinishRelease();
        }
        catch { Cancel(); throw; }
        finally
        {
            if (up && State == Phase.AwaitRelease) State = Phase.Idle;
            ticking = false;
        }
    }

    private void FinishRelease()
    {
        if (pending.Kind == Capability.DualShortHold) Commit(false);
        else Cancel();
        // The release was observed. A new Down is still required by Idle.
        State = Phase.Idle;
    }

    private bool ValidPending(Candidate best)
    {
        if (Blocked || !pending.IsAlive || !best.IsAlive || pending.Identity != best.Identity || !TargetAllows(pending)) return false;
        Candidate refreshed;
        if (pending.provider == null)
        {
            if (!owner.TryGetInteractionCandidate(out refreshed)) return false;
        }
        else if (!ProviderAlive(pending.provider) || !pending.provider.TryGetCandidate(owner, out refreshed)) return false;
        bool aliasRay = pending.raycast && pending.provider is IInteractionCandidateAlias alias
            && owner.TryGetInteractionCandidate(out Candidate direct) && direct.raycast && alias.OwnsTarget(direct.target);
        return !Blocked && refreshed.IsAlive && refreshed.Identity == pending.Identity
            && (!pending.raycast || refreshed.raycast || aliasRay);
    }

    private void Commit(bool hold)
    {
        if (Blocked || !pending.IsAlive) { Cancel(); return; }
        Candidate selected = pending;
        CommitInterrupted = false;
        // Commit before calling external code (modal events, disable, Destroy, reentry).
        State = Phase.Completed;
        pending = default;
        beganHold = false;
        try
        {
            if (hold)
            {
                if (selected.target is ILongPressInteractable dual) dual.CompleteLongPress(owner);
                else if (selected.target is IHoldInteractable target) target.CompleteHold(owner.gameObject);
            }
            else if (selected.target is IInteractable instant) owner.CommitInteraction(instant);
        }
        finally { State = Phase.AwaitRelease; HidePresentation(); }
    }

    public void Cancel()
    {
        // A modal acquired by a completed callback must not cancel that completed hold.
        if (State == Phase.Completed) { CommitInterrupted = true; return; }
        if (State == Phase.Cancelled) return;
        Candidate old = pending;
        bool notify = State == Phase.Pressed && beganHold;
        State = Phase.Cancelled;
        pending = default;
        beganHold = false;
        elapsed = 0f;
        try
        {
            if (notify && old.target != null && old.target is IHoldInteractable hold && owner != null)
                hold.CancelHold(owner.gameObject);
        }
        finally { State = Phase.AwaitRelease; HidePresentation(); }
    }

    private Candidate SelectCandidate()
    {
        candidates.Clear();
        if (owner.TryGetInteractionCandidate(out Candidate direct)) AddCandidate(direct);
        // Registration happens on lifecycle, selection is pulled synchronously here.
        for (int i = providers.Count - 1; i >= 0; i--)
        {
            var provider = providers[i];
            if (!ProviderAlive(provider)) continue;
            if (provider.TryGetCandidate(owner, out Candidate candidate)) AddCandidate(candidate);
        }
        Candidate best = default;
        foreach (var candidate in candidates)
            if (!best.IsAlive || Compare(candidate, best) < 0) best = candidate;
        return best;
    }

    private void AddCandidate(Candidate candidate)
    {
        if (!candidate.IsAlive) return;
        // Remap only an explicit same-object legacy alias; preserve its ray priority and distance.
        IInteractionCandidateProvider aliasProvider = null;
        foreach (var provider in providers)
        {
            if (!ProviderAlive(provider) || !(provider is IInteractionCandidateAlias alias) || !alias.OwnsTarget(candidate.target)) continue;
            if (aliasProvider == null || ((MonoBehaviour)provider).GetInstanceID() < ((MonoBehaviour)aliasProvider).GetInstanceID())
                aliasProvider = provider;
        }
        if (aliasProvider != null)
        {
            if (!aliasProvider.TryGetCandidate(owner, out Candidate mapped)) return;
            mapped.raycast = candidate.raycast;
            mapped.distance = candidate.distance;
            candidate = mapped;
        }
        if (candidate.IsAlive && !float.IsNaN(candidate.distance) && !float.IsInfinity(candidate.distance)
            && TargetAllows(candidate)) candidates.Add(candidate);
    }

    private static int Compare(Candidate a, Candidate b)
    {
        if (a.raycast != b.raycast) return a.raycast ? -1 : 1;
        int d = a.distance.CompareTo(b.distance);
        if (d != 0) return d;
        d = a.Identity.CompareTo(b.Identity);
        if (d != 0) return d;
        d = a.target.GetInstanceID().CompareTo(b.target.GetInstanceID());
        if (d != 0) return d;
        int ai = a.provider is MonoBehaviour ap ? ap.GetInstanceID() : 0;
        int bi = b.provider is MonoBehaviour bp ? bp.GetInstanceID() : 0;
        return ai.CompareTo(bi);
    }

    private void Present(Candidate selected, float progress)
    {
        IInteractionCandidateProvider next = null;
        // One logical GameObject identity; duplicated Paper components never get a second dispatch.
        foreach (var c in candidates)
        {
            if (!selected.IsAlive || c.Identity != selected.Identity || !ProviderAlive(c.provider)) continue;
            if (next == null || c.provider.PresentationPriority < next.PresentationPriority
                || (c.provider.PresentationPriority == next.PresentationPriority
                    && ((MonoBehaviour)c.provider).GetInstanceID() < ((MonoBehaviour)next).GetInstanceID())) next = c.provider;
        }
        if (!ReferenceEquals(next, presentation)) { HidePresentation(); presentation = next; }
        if (ProviderAlive(presentation)) presentation.Present(owner, selected.target, progress, true);
    }

    private void HidePresentation()
    {
        var old = presentation;
        presentation = null;
        if (old is MonoBehaviour behaviour && behaviour != null) old.Present(owner, null, 0f, false);
    }
}

public interface IInteractionCandidateProvider
{
    int PresentationPriority { get; }
    bool TryGetCandidate(PlayerInteractor player, out PlayerInteractionArbiter.Candidate candidate);
    void Present(PlayerInteractor player, MonoBehaviour target, float progress, bool visible);
}

public interface IInteractionCandidateAlias
{
    bool OwnsTarget(MonoBehaviour target);
}
