using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProximitySceneTransition : MonoBehaviour, IInteractionCandidateProvider, IHoldInteractable
{
    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform player;
    [SerializeField] private float interactDistance = 2.35f;

    [Header("Interaction UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private string title = "";
    [SerializeField] private string actionText = "F\uD0A4\uB97C \uAE38\uAC8C \uB20C\uB7EC \uC774\uB3D9";
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Scene")]
    [SerializeField] private string targetSceneName = "07.\uD30C\uCE20 \uC81C\uC791\uC2E4";
    [SerializeField] private float loadDelay = 0.05f;

    private readonly System.Collections.Generic.List<PlayerInteractor> registeredPlayers = new System.Collections.Generic.List<PlayerInteractor>();
    private readonly System.Collections.Generic.HashSet<Collider> playerContacts = new System.Collections.Generic.HashSet<Collider>();
    private PlayerInteractor viewOwner;
    public int PresentationPriority => 0;

    private void OnEnable()
    {
        foreach (var candidatePlayer in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
        {
            candidatePlayer.RegisterInteractionProvider(this);
            if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
        }
    }
    private void OnDisable()
    {
        PlayerStateSessionStore.CancelLocal(this);
        StopAllCoroutines();
        transitioning = false;
        foreach (var candidatePlayer in registeredPlayers)
            if (candidatePlayer != null)
            {
                candidatePlayer.Arbitration.InvalidateTarget(this);
                candidatePlayer.UnregisterInteractionProvider(this);
            }
        registeredPlayers.Clear();
        playerContacts.Clear();
        viewOwner = null;
        HidePrompt();
    }
    private void OnDestroy() => OnDisable();

    private bool HasContact(PlayerInteractor candidatePlayer)
    {
        foreach (var contact in playerContacts)
            if (contact != null && contact.enabled && contact.gameObject.activeInHierarchy
                && contact.GetComponentInParent<PlayerInteractor>() == candidatePlayer) return true;
        return false;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || other == null || !other.CompareTag(playerTag)) return;
        var candidatePlayer = other.GetComponentInParent<PlayerInteractor>();
        if (candidatePlayer == null) return;
        playerContacts.Add(other);
        candidatePlayer.RegisterInteractionProvider(this);
        if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
    }
    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);
    private void OnTriggerExit(Collider other)
    {
        if (other == null || !playerContacts.Remove(other)) return;
        var candidatePlayer = other.GetComponentInParent<PlayerInteractor>();
        if (candidatePlayer != null && !HasContact(candidatePlayer))
            candidatePlayer.Arbitration.InvalidateTarget(this);
    }

    public void BeginHold(GameObject interactor) { }
    public void UpdateHold(GameObject interactor, float progress) { }
    public void CancelHold(GameObject interactor) { }
    public float GetHoldDuration() => Mathf.Max(0.01f, holdTime);
    private bool transitioning;
    private Collider[] targetColliders;
    private Renderer[] targetRenderers;
    private void Awake()
    {
        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        CacheDistanceSources();
    }

    private void Start()
    {
        HidePrompt();
    }

    public string GetInteractionName() => title;
    public bool TryGetCandidate(PlayerInteractor candidatePlayer, out PlayerInteractionArbiter.Candidate candidate)
    {
        candidate = default;
        if (!isActiveAndEnabled || transitioning || candidatePlayer == null || !candidatePlayer.isActiveAndEnabled
            || candidatePlayer.interactKey != interactKey || PlayerModalControlContext.Blocks(candidatePlayer.gameObject)) return false;
        if (player != null && player.GetComponentInParent<PlayerInteractor>() != candidatePlayer
            && candidatePlayer.transform != player && !candidatePlayer.transform.IsChildOf(player)) return false;
        float distance = GetDistanceToTarget(candidatePlayer.transform.position);
        if (!HasContact(candidatePlayer) && distance > interactDistance) return false;
        if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
        candidate = new PlayerInteractionArbiter.Candidate { target = this, provider = this, distance = distance };
        return true;
    }
    public void CompleteHold(GameObject interactor)
    {
        var candidatePlayer = interactor != null ? interactor.GetComponent<PlayerInteractor>() : null;
        if (!TryGetCandidate(candidatePlayer, out _)) return;
        StartCoroutine(LoadSceneRoutine(candidatePlayer.transform));
    }
    public void Present(PlayerInteractor candidatePlayer, MonoBehaviour target, float progress, bool visible)
    {
        if (!visible)
        {
            if (viewOwner != null && viewOwner != candidatePlayer) return;
            viewOwner = null;
            HidePrompt();
            return;
        }
        if (!isActiveAndEnabled || (viewOwner != null && viewOwner != candidatePlayer)) return;
        bool changed = viewOwner != candidatePlayer;
        viewOwner = candidatePlayer;
        if (changed) ShowPrompt();
        SetProgress(progress);
    }

    private void CacheDistanceSources()
    {
        targetColliders = GetComponentsInChildren<Collider>(true);
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private float GetDistanceToTarget(Vector3 worldPosition)
    {
        Vector3 rootPosition = transform.position;
        rootPosition.y = worldPosition.y;
        float bestDistance = Vector3.Distance(rootPosition, worldPosition);

        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                Vector3 closestPoint = targetCollider.ClosestPoint(worldPosition);
                closestPoint.y = worldPosition.y;
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                Vector3 closestPoint = targetRenderer.bounds.ClosestPoint(worldPosition);
                closestPoint.y = worldPosition.y;
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        return bestDistance;
    }

    private IEnumerator LoadSceneRoutine(Transform committedPlayer)
    {
        if (transitioning)
            yield break;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("[ProximitySceneTransition] targetSceneName is empty.", this);
            yield break;
        }

        bool localRoute = PlayerStateSessionStore.IsLocalRoute(targetSceneName);
        if (localRoute && !PlayerStateSessionStore.TryReserveLocal(this, committedPlayer, targetSceneName))
            yield break;

        transitioning = true;
        HidePrompt();
        ResetPlayerMotionBeforeLoad(committedPlayer);

        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);

        if (localRoute)
        {
            if (!PlayerStateSessionStore.TryLoadLocal(this))
            {
                PlayerStateSessionStore.CancelLocal(this);
                transitioning = false;
            }
        }
        else SceneManager.LoadScene(targetSceneName);
    }

    private void ShowPrompt()
    {
        if (interactionUI != null)
            interactionUI.ShowFor(this, title, actionText);
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
            interactionUI.HideFor(this);
    }

    private void SetProgress(float progress)
    {
        if (interactionUI != null)
            interactionUI.UpdateProgressFor(this, progress);
    }

    private void ResetPlayerMotionBeforeLoad(Transform committedPlayer)
    {
        if (committedPlayer == null)
            return;

        Move_Hankyul hankyulMove = committedPlayer.GetComponentInParent<Move_Hankyul>();
        if (hankyulMove != null)
            hankyulMove.ResetPlayerControlState();

        Rigidbody playerRigidbody = committedPlayer.GetComponentInParent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

}
