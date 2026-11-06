using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Camera / Raycast")]
    public Camera playerCamera;
    public float detectDistance = 4.0f;     // 정면 후보 탐지 거리
    public float uiDistance = 2.2f;         // UI 표시 거리
    public float interactDistance = 2.2f;   // 실제 Interact 허용 거리
    public LayerMask interactableLayerMask = ~0;

    [Header("Keys")]
    public KeyCode interactKey = KeyCode.F;
    public KeyCode inventoryKey = KeyCode.I;

    [Header("SFX Key")]
    public string pickupSfxKey = "pickup";

    public PlayerInventory Inventory { get; private set; }

    // ===== UI에 전달할 상태 =====
    public IInteractable CurrentInteractable { get; private set; }
    public float CurrentDistance { get; private set; } = Mathf.Infinity;

    public bool SuppressInteractionUI { get; private set; }

    public bool CanShowUI => PlayerLocalInputOwnership.CanConsumeLocalInput(this) && !SuppressInteractionUI && CurrentInteractable != null && CurrentDistance <= uiDistance;
    public bool CanInteract => PlayerLocalInputOwnership.CanConsumeLocalInput(this) && !PlayerModalControlContext.Blocks(gameObject) && CurrentInteractable != null && CurrentDistance <= interactDistance;

    // UI 업데이트 이벤트(상태가 바뀌었을 때만 호출)
    public System.Action OnTargetChanged;

    private IInteractable prevInteractable;
    private bool prevCanShowUI;
    private bool detectedByRaycast;
    private Collider[] nearbyColliderBuffer = new Collider[32];
    private PlayerInteractionArbiter arbitration;
    private PlayerModalControlContext modalContext;
    internal PlayerInteractionArbiter Arbitration => arbitration ?? (arbitration = new PlayerInteractionArbiter(this));

    private void Reset()
    {
        playerCamera = Camera.main;
    }

    private void Awake()
    {
        Inventory = GetComponent<PlayerInventory>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        EnsureModalSubscription();
        if (!Arbitration.CanReadLocalInput())
        {
            ClearTarget();
            NotifyIfChanged();
            return;
        }
        RaycastDetect();
        NotifyIfChanged();

        // Target/UI callbacks can change ownership before the device read.
        if (!Arbitration.CanReadLocalInput()) return;
        ReadInteractionKey(out bool down, out bool held, out bool up);
        Arbitration.Tick(down, held, up, Time.deltaTime, Time.frameCount);

        if (Arbitration.CanReadLocalInput() && GetKeyDown(inventoryKey) && Inventory != null)
        {
            Inventory.DebugLogInventory();
        }
    }

    internal void RegisterInteractionProvider(IInteractionCandidateProvider provider) => Arbitration.Register(provider);
    internal void UnregisterInteractionProvider(IInteractionCandidateProvider provider) => arbitration?.Unregister(provider);

    private void OnEnable()
    {
        EnsureModalSubscription();
        foreach (var provider in FindObjectsByType<ProximityHoldPrompt>(FindObjectsSortMode.None))
            RegisterInteractionProvider(provider);
        foreach (var provider in GetComponentsInChildren<PlayerHoldInteractor>(true))
            if (provider.GetComponentInParent<PlayerInteractor>() == this) RegisterInteractionProvider(provider);
        SceneManager.sceneUnloaded += OnInteractionSceneUnloaded;
        if (!Arbitration.CanReadLocalInput()) return;
        ReadInteractionKey(out _, out bool held, out _);
        if (held) Arbitration.Cancel();
    }

    private void EnsureModalSubscription()
    {
        var context = PlayerModalControlContext.For(gameObject, true);
        if (ReferenceEquals(modalContext, context)) return;
        if (modalContext != null) modalContext.GameplayBlocked -= CancelInteraction;
        modalContext = context;
        if (modalContext != null) modalContext.GameplayBlocked += CancelInteraction;
    }

    private void CancelInteraction() => Arbitration.Cancel();
    private void OnInteractionSceneUnloaded(Scene scene) => CancelInteraction();
    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnInteractionSceneUnloaded;
        if (modalContext != null) modalContext.GameplayBlocked -= CancelInteraction;
        modalContext = null;
        CancelInteraction();
    }
    private void OnDestroy() => OnDisable();

    internal bool TryGetInteractionCandidate(out PlayerInteractionArbiter.Candidate candidate)
    {
        candidate = default;
        if (!CanInteract || !(CurrentInteractable is MonoBehaviour target) || target == null || !target.isActiveAndEnabled) return false;
        candidate = new PlayerInteractionArbiter.Candidate { target = target, raycast = detectedByRaycast, distance = CurrentDistance };
        return true;
    }

    internal void CommitInteraction(IInteractable target)
    {
        if (!PlayerLocalInputOwnership.CanConsumeLocalInput(this) || !isActiveAndEnabled || PlayerModalControlContext.Blocks(gameObject)
            || !(target is MonoBehaviour component) || component == null || !component.isActiveAndEnabled) return;
        // Capture before target code can destroy/disable itself. Keep the original void/SFX contract.
        string key = ResolveInteractSfxKey(target);
        if (!PlayerLocalInputOwnership.CanConsumeLocalInput(this) || Arbitration.CommitInterrupted || PlayerModalControlContext.Blocks(gameObject) || !isActiveAndEnabled
            || component == null || !component.isActiveAndEnabled) return;
        target.Interact(this);
        if (PlayerLocalInputOwnership.CanConsumeLocalInput(this)) PlaySfxKey(key);
        ClearTarget();
        NotifyIfChanged(force: true);
    }

    private void ReadInteractionKey(out bool down, out bool held, out bool up)
    {
        var kb = Keyboard.current;
        UnityEngine.InputSystem.Controls.KeyControl control = null;
        if (kb != null)
        {
            switch (interactKey)
            {
                case KeyCode.F: control = kb.fKey; break;
                case KeyCode.E: control = kb.eKey; break;
                case KeyCode.I: control = kb.iKey; break;
            }
        }
        down = control != null ? control.wasPressedThisFrame : Input.GetKeyDown(interactKey);
        held = control != null ? control.isPressed : Input.GetKey(interactKey);
        up = control != null ? control.wasReleasedThisFrame : Input.GetKeyUp(interactKey);
    }

    private void RaycastDetect()
    {
        CurrentInteractable = null;
        CurrentDistance = Mathf.Infinity;
        detectedByRaycast = false;

        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, detectDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    // Pickup uses the same owner-to-item distance as the nearby path.
                    // A large collider hit close to the camera must not extend pickup range.
                    bool pickup = interactable is IPickupInteractable;
                    float distance = pickup && interactable is Component component
                        ? Vector3.Distance(transform.position, component.transform.position)
                        : hit.distance;
                    if (!pickup || (!float.IsNaN(distance) && !float.IsInfinity(distance)
                        && distance <= Mathf.Max(uiDistance, interactDistance)))
                    {
                        detectedByRaycast = true;
                        CurrentInteractable = interactable;
                        CurrentDistance = distance;
                        return;
                    }
                }
            }
        }

        DetectNearbyPickupOrPaper();
    }

    private void DetectNearbyPickupOrPaper()
    {
        float searchDistance = Mathf.Max(uiDistance, interactDistance);
        Collider[] hits;
        int hitCount;
        while (true)
        {
            hitCount = Physics.OverlapSphereNonAlloc(transform.position, searchDistance,
                nearbyColliderBuffer, interactableLayerMask, QueryTriggerInteraction.Collide);
            if (hitCount < nearbyColliderBuffer.Length)
            {
                hits = nearbyColliderBuffer;
                break;
            }
            // NonAlloc truncation must not change nearest-candidate arbitration.
            if (nearbyColliderBuffer.Length >= 2048)
            {
                hits = Physics.OverlapSphere(transform.position, searchDistance,
                    interactableLayerMask, QueryTriggerInteraction.Collide);
                hitCount = hits.Length;
                break;
            }
            System.Array.Resize(ref nearbyColliderBuffer, nearbyColliderBuffer.Length * 2);
        }

        IInteractable nearest = null;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            var interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            bool isPickupLike = interactable is IPickupInteractable || interactable is ILongPressInteractable;
            if (!isPickupLike) continue;

            Component component = interactable as Component;
            if (!(component is MonoBehaviour behaviour) || behaviour == null || !behaviour.isActiveAndEnabled) continue;

            float distance = Vector3.Distance(transform.position, component.transform.position);
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance > searchDistance) continue;
            Component previous = nearest as Component;
            if (distance < nearestDistance || (distance == nearestDistance
                && (previous == null || component.GetInstanceID() < previous.GetInstanceID())))
            {
                nearest = interactable;
                nearestDistance = distance;
            }
        }

        if (nearest != null)
        {
            CurrentInteractable = nearest;
            CurrentDistance = nearestDistance;
        }
        // Do not retain removed world objects between queries.
        System.Array.Clear(nearbyColliderBuffer, 0, nearbyColliderBuffer.Length);
    }
    private void ClearTarget()
    {
        CurrentInteractable = null;
        CurrentDistance = Mathf.Infinity;
    }

    private void NotifyIfChanged(bool force = false)
    {
        if (!PlayerLocalInputOwnership.CanConsumeLocalInput(this))
        {
            ClearTarget();
            // Only retract a presentation this Player actually published; a remote Player
            // must not continuously send hide/refresh events to a shared HUD.
            if (prevInteractable == null && !prevCanShowUI) return;
        }
        bool canShow = CanShowUI;

        if (force ||
            prevInteractable != CurrentInteractable ||
            prevCanShowUI != canShow)
        {
            prevInteractable = CurrentInteractable;
            prevCanShowUI = canShow;
            OnTargetChanged?.Invoke();
        }
    }

    public void SetInteractionUISuppressed(bool suppressed)
    {
        if (SuppressInteractionUI == suppressed)
            return;

        SuppressInteractionUI = suppressed;
        NotifyIfChanged(force: true);
    }

    private string ResolveInteractSfxKey(IInteractable target)
    {
        var provider = target as IInteractSfxProvider;
        if (provider != null)
            return provider.GetInteractSfxKey();

        return pickupSfxKey;
    }

    private void PlaySfxKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (GlobalManager.Instance == null || GlobalManager.Audio == null) return;

        GlobalManager.Audio.PlaySFX(key);
    }

    private static bool GetKeyDown(KeyCode key)
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.F: return kb.fKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.I: return kb.iKey.wasPressedThisFrame;
                default: break;
            }
        }
        return Input.GetKeyDown(key);
    }
}
