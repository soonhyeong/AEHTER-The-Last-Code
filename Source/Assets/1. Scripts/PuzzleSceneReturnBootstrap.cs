using System.Collections;
using CircuitPuzzle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PuzzleSceneReturnBootstrap : MonoBehaviour
{
    private static PuzzleSceneReturnBootstrap instance;
    private CircuitPuzzleManager currentManager;
    private CircuitPuzzleManager_Final currentFinalManager;
    private CircuitPuzzleManager_Final.PuzzleEvent finalSuccessEvent;
    // Final has no result duration. Retain the legacy manager's 1.2-second default.
    private const float FinalResultShowTime = 1.2f;
    private Coroutine returnRoutine;
    private int returnGeneration;
    private bool isReturning;
    private CursorModalBroker.Lease puzzleCursorLease;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null)
            return;

        GameObject runner = new GameObject("PuzzleSceneReturnBootstrap");
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<PuzzleSceneReturnBootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookCurrentScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnhookCurrentManager();
        CancelPendingReturn();
        ReleasePuzzleCursor();
    }

    private void OnDestroy()
    {
        UnhookCurrentManager();
        CancelPendingReturn();
        ReleasePuzzleCursor();
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // An unrelated additive load must not cancel an already accepted return.
        if (scene.handle != SceneManager.GetActiveScene().handle)
            return;
        ApplyCursorStateForSceneFlow();
        RestorePlayerPoseIfNeeded(scene.name);
        HookCurrentScene();
        StartCoroutine(TriggerRewardSpawnersAcrossFrames());
    }

    private void HookCurrentScene()
    {
        UnhookCurrentManager();
        CancelPendingReturn();
        isReturning = false;

        if (!PuzzleSceneFlowState.HasPendingReturn)
            return;

        // Prefer the actual Final manager in this Scene; never subscribe to both versions.
        foreach (var manager in Resources.FindObjectsOfTypeAll<CircuitPuzzleManager_Final>())
        {
            if (manager == null || !manager.isActiveAndEnabled
                || manager.gameObject.scene.handle != SceneManager.GetActiveScene().handle)
                continue;
            if (currentFinalManager != null)
            {
                currentFinalManager = null;
                Debug.LogWarning("[PuzzleSceneReturn] Multiple active Final managers; success binding deferred.");
                return;
            }
            currentFinalManager = manager;
        }
        if (currentFinalManager != null)
        {
            if (currentFinalManager.OnPuzzleSuccess == null)
                currentFinalManager.OnPuzzleSuccess = new CircuitPuzzleManager_Final.PuzzleEvent();
            finalSuccessEvent = currentFinalManager.OnPuzzleSuccess;
            finalSuccessEvent.AddListener(HandleFinalPuzzleCleared);
            return;
        }

        currentManager = FindCircuitPuzzleManager();
        if (currentManager == null)
            return;

        currentManager.OnPuzzleCleared += HandlePuzzleCleared;
    }

    private void UnhookCurrentManager()
    {
        // Store the event itself so replacing/destroying its component cannot strand a listener.
        if (finalSuccessEvent != null)
            finalSuccessEvent.RemoveListener(HandleFinalPuzzleCleared);
        finalSuccessEvent = null;
        currentFinalManager = null;
        if (currentManager != null)
            currentManager.OnPuzzleCleared -= HandlePuzzleCleared;
        currentManager = null;
    }

    private void HandlePuzzleCleared()
    {
        if (currentManager != null)
            BeginReturn(currentManager.ResultShowTime);
    }

    private void HandleFinalPuzzleCleared(PuzzleDifficulty_Final difficulty)
    {
        if (currentFinalManager != null)
            BeginReturn(FinalResultShowTime);
    }

    private void BeginReturn(float resultShowTime)
    {
        if (!isActiveAndEnabled || isReturning || !PuzzleSceneFlowState.HasPendingReturn
            || PuzzleSceneFlowState.IsPuzzleCleared(PuzzleSceneFlowState.ActivePuzzleId))
            return;

        isReturning = true;
        returnRoutine = StartCoroutine(ReturnAfterDelay(Mathf.Max(0.01f, resultShowTime)));
    }

    private void CancelPendingReturn()
    {
        returnGeneration++;
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    private CircuitPuzzleManager FindCircuitPuzzleManager()
    {
        CircuitPuzzleManager[] managers = Resources.FindObjectsOfTypeAll<CircuitPuzzleManager>();
        CircuitPuzzleManager selected = null;
        foreach (CircuitPuzzleManager manager in managers)
        {
            if (manager == null)
                continue;

            GameObject managerObject = manager.gameObject;
            if (managerObject == null || !manager.isActiveAndEnabled
                || managerObject.scene.handle != SceneManager.GetActiveScene().handle)
                continue;

            if (selected != null)
            {
                Debug.LogWarning("[PuzzleSceneReturn] Multiple active legacy managers; success binding deferred.");
                return null;
            }
            selected = manager;
        }

        return selected;
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        int generation = returnGeneration;
        int puzzleSceneHandle = SceneManager.GetActiveScene().handle;
        string puzzleId = PuzzleSceneFlowState.ActivePuzzleId;
        string expectedReturnScene = PuzzleSceneFlowState.ReturnSceneName;
        if (!string.IsNullOrWhiteSpace(puzzleId))
            PuzzleSceneFlowState.MarkPuzzleCleared(puzzleId);

        yield return new WaitForSeconds(delay);

        // A disabled/destroyed runner, a new Scene or a replacement flow must not load stale data.
        if (generation != returnGeneration || !isActiveAndEnabled
            || SceneManager.GetActiveScene().handle != puzzleSceneHandle
            || PuzzleSceneFlowState.ActivePuzzleId != puzzleId
            || PuzzleSceneFlowState.ReturnSceneName != expectedReturnScene)
            yield break;

        string returnSceneName = PuzzleSceneFlowState.ConsumeReturnScene();
        if (!string.IsNullOrWhiteSpace(returnSceneName))
        {
            PlayerStateSessionStore.PrepareLocalPuzzleReturn(returnSceneName);
            ReleasePuzzleCursor();
            SceneManager.LoadScene(returnSceneName);
        }
    }

    private IEnumerator TriggerRewardSpawnersAcrossFrames()
    {
        if (!PuzzleSceneFlowState.HasAnyPendingReward)
            yield break;

        var rewardScene = SceneManager.GetActiveScene();
        if (PlayerStateSessionStore.UsesLocalRouting && rewardScene.name == PlayerStateSessionStore.DestinationScene)
        {
            PlayerStateSessionStore.RestoreResult result;
            while ((result = PlayerStateSessionStore.QueryLocalRestore(rewardScene, out _)) == PlayerStateSessionStore.RestoreResult.Pending)
            {
                yield return null;
                if (SceneManager.GetActiveScene().handle != rewardScene.handle) yield break;
            }
            if (result == PlayerStateSessionStore.RestoreResult.Rejected)
            {
                Debug.LogWarning("[PuzzleSceneReturn] Player restore rejected; pending reward retained.");
                yield break;
            }
        }

        for (int frame = 0; frame < 10; frame++)
        {
            yield return null;

            if (SceneManager.GetActiveScene().handle != rewardScene.handle) yield break;
            if (PlayerStateSessionStore.UsesLocalRouting && rewardScene.name == PlayerStateSessionStore.DestinationScene
                && PlayerStateSessionStore.QueryLocalRestore(rewardScene, out _) == PlayerStateSessionStore.RestoreResult.Rejected) yield break;

            RewardGunSpawner[] rewardSpawners = Resources.FindObjectsOfTypeAll<RewardGunSpawner>();
            for (int i = 0; i < rewardSpawners.Length; i++)
            {
                RewardGunSpawner spawner = rewardSpawners[i];
                if (spawner == null)
                    continue;

                GameObject spawnerObject = spawner.gameObject;
                if (spawnerObject == null || !spawnerObject.scene.IsValid())
                    continue;

                spawner.TrySpawnFromPuzzleState();
            }
        }
    }

    private void ApplyCursorStateForSceneFlow()
    {
        if (!PuzzleSceneFlowState.HasPendingReturn)
        {
            ReleasePuzzleCursor();
            return;
        }

        if (puzzleCursorLease == null || puzzleCursorLease.IsReleased)
            puzzleCursorLease = CursorModalBroker.Acquire(this, this);
    }

    private void ReleasePuzzleCursor()
    {
        puzzleCursorLease?.Release();
        puzzleCursorLease = null;
    }

    private void RestorePlayerPoseIfNeeded(string sceneName)
    {
        if (PlayerStateSessionStore.UsesLocalRouting && sceneName == PlayerStateSessionStore.DestinationScene)
        {
            StartCoroutine(RestoreLocalPlayerPose(SceneManager.GetActiveScene()));
            return;
        }
        if (!PuzzleSceneFlowState.TryConsumeReturnPose(sceneName, out Vector3 position, out Quaternion rotation))
            return;

        StartCoroutine(RestorePlayerPoseAcrossFrames(position, rotation));
    }

    private IEnumerator RestoreLocalPlayerPose(Scene scene)
    {
        Transform owner;
        PlayerStateSessionStore.RestoreResult result;
        while ((result = PlayerStateSessionStore.QueryLocalRestore(scene, out owner)) == PlayerStateSessionStore.RestoreResult.Pending)
        {
            yield return null;
            if (SceneManager.GetActiveScene().handle != scene.handle) yield break;
        }
        if (result == PlayerStateSessionStore.RestoreResult.Rejected || owner == null) yield break;
        if (!PuzzleSceneFlowState.TryConsumeReturnPose(scene.name, out Vector3 position, out Quaternion rotation)) yield break;
        for (int frame = 0; frame < 6; frame++)
        {
            yield return null;
            if (SceneManager.GetActiveScene().handle != scene.handle
                || PlayerStateSessionStore.QueryLocalRestore(scene, out var current) == PlayerStateSessionStore.RestoreResult.Rejected
                || current != owner) yield break;
            ApplyPlayerPose(owner, position, rotation);
        }
    }

    private IEnumerator RestorePlayerPoseAcrossFrames(Vector3 position, Quaternion rotation)
    {
        Transform playerTransform = null;

        for (int frame = 0; frame < 6; frame++)
        {
            yield return null;

            if (playerTransform == null)
                playerTransform = FindPlayerTransform();

            if (playerTransform == null)
                continue;

            // Return data contains only a pose, not a Player/owner identity.
            // Do not lock an arbitrary Player or restore another owner's controls.
            // Ownership-aware temporary pose locking is deferred to O07.
            ApplyPlayerPose(playerTransform, position, rotation);
        }
    }

    private void ApplyPlayerPose(Transform playerTransform, Vector3 position, Quaternion rotation)
    {
        if (playerTransform == null)
            return;

        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool hadController = characterController != null && characterController.enabled;
        if (hadController)
            characterController.enabled = false;

        Rigidbody rigidbody = playerTransform.GetComponent<Rigidbody>();
        bool hadRigidbody = rigidbody != null;
        bool wasKinematic = hadRigidbody && rigidbody.isKinematic;

        if (hadRigidbody)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
            rigidbody.position = position;
            rigidbody.rotation = rotation;
        }

        playerTransform.SetPositionAndRotation(position, rotation);

        Move_Hankyul moveHankyul = playerTransform.GetComponent<Move_Hankyul>() ?? playerTransform.GetComponentInChildren<Move_Hankyul>(true);
        if (moveHankyul != null)
            moveHankyul.SnapLookToWorldRotation(rotation);

        if (hadRigidbody)
            rigidbody.isKinematic = wasKinematic;

        if (hadController)
            characterController.enabled = true;
    }

    private Transform FindPlayerTransform()
    {
        Move_Hankyul moveHankyul = FindObjectOfType<Move_Hankyul>();
        if (moveHankyul != null)
            return moveHankyul.transform;

        Hankyul_Move hankyulMove = FindObjectOfType<Hankyul_Move>();
        if (hankyulMove != null)
            return hankyulMove.transform;

        Yunseo_Move yunseoMove = FindObjectOfType<Yunseo_Move>();
        if (yunseoMove != null)
            return yunseoMove.transform;

        PlayerInput playerInput = FindObjectOfType<PlayerInput>();
        if (playerInput != null)
            return playerInput.transform;

        return Camera.main != null ? Camera.main.transform.root : null;
    }

}
