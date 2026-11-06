using UnityEngine;
using UnityEngine.SceneManagement;

public class ProximityPuzzleOpener : MonoBehaviour, IInteractionCandidateProvider, IInteractionCandidateAlias, IHoldInteractable
{
    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float interactDistance = 3.2f;
    [SerializeField] private bool useDistanceFallback = false;

    [Header("Interaction UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string promptTitle = "데이터 연결회로";
    [SerializeField] private string promptActionText = "[F키를 길게 누르기] 데이터 연결회로 작동";
    [SerializeField] private bool hidePromptText = false;

    [Header("Local Puzzle Link")]
    [SerializeField] private PuzzleOpener puzzleOpener;

    [Header("Scene Puzzle Link")]
    [SerializeField] private bool openPuzzleScene = false;
    [SerializeField] private string puzzleSceneName = "puzzle2";
    [SerializeField] private string puzzleId = "";

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
    private void Awake()
    {
        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        if (puzzleOpener == null)
            puzzleOpener = GetComponent<PuzzleOpener>();
    }

    private void Start()
    {
        HidePrompt();
    }

    public string GetInteractionName() => promptTitle;
    // Separate-scene mode owns the same authored puzzle, not a second Instant action.
    public bool OwnsTarget(MonoBehaviour target) => openPuzzleScene && puzzleOpener != null
        && target == puzzleOpener && target.gameObject == gameObject;
    public bool TryGetCandidate(PlayerInteractor candidatePlayer, out PlayerInteractionArbiter.Candidate candidate)
    {
        candidate = default;
        if (!isActiveAndEnabled || candidatePlayer == null || !candidatePlayer.isActiveAndEnabled
            || candidatePlayer.interactKey != interactKey || PlayerModalControlContext.Blocks(candidatePlayer.gameObject)) return false;
        float distance = Vector3.Distance(candidatePlayer.transform.position, transform.position);
        if (!HasContact(candidatePlayer) && !(useDistanceFallback && distance <= interactDistance)) return false;
        if (!openPuzzleScene && (puzzleOpener == null || puzzleOpener.IsPuzzleOpen)) return false;
        if (!string.IsNullOrWhiteSpace(puzzleId) && PuzzleSceneFlowState.IsPuzzleCleared(puzzleId)) return false;
        if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
        candidate = new PlayerInteractionArbiter.Candidate { target = this, provider = this, distance = distance };
        return true;
    }
    public void CompleteHold(GameObject interactor)
    {
        var candidatePlayer = interactor != null ? interactor.GetComponent<PlayerInteractor>() : null;
        if (!TryGetCandidate(candidatePlayer, out _)) return;
        if (openPuzzleScene) BeginPuzzleSceneFlow(candidatePlayer);
        else puzzleOpener.Interact(candidatePlayer);
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
        UpdateProgress(progress);
    }

    private void BeginPuzzleSceneFlow(PlayerInteractor candidatePlayer)
    {
        if (string.IsNullOrWhiteSpace(puzzleSceneName))
        {
            Debug.LogError("[ProximityPuzzleOpener] puzzleSceneName is empty.", this);
            return;
        }

        Transform playerTransform = candidatePlayer.transform;

        Vector3 returnPosition = playerTransform != null ? playerTransform.position : transform.position;
        Quaternion returnRotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity;
        string returnSceneName = SceneManager.GetActiveScene().name;

        if (PlayerStateSessionStore.IsLocalRoute(puzzleSceneName))
        {
            if (PlayerStateSessionStore.TryReserveLocal(this, candidatePlayer, puzzleSceneName))
                PlayerStateSessionStore.TryLoadLocal(this, () =>
                    PuzzleSceneFlowState.BeginPuzzle(puzzleId, returnSceneName, returnPosition, returnRotation));
            return;
        }
        PuzzleSceneFlowState.BeginPuzzle(puzzleId, returnSceneName, returnPosition, returnRotation);
        SceneManager.LoadScene(puzzleSceneName);
    }

    private void ShowPrompt()
    {
        if (interactionUI != null)
            interactionUI.ShowFor(this, promptTitle, promptActionText, hidePromptText);
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
            interactionUI.HideFor(this);
    }

    private void UpdateProgress(float progress)
    {
        if (interactionUI != null)
            interactionUI.UpdateProgressFor(this, progress);
    }
}

