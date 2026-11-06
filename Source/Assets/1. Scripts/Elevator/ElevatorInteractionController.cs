using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ElevatorInteractionController : MonoBehaviour, IHoldInteractable
{
    [Header("?곹샇?묒슜")]
    [SerializeField] private string interactionName = "?섎━踰좎씠???묒듅";
    [SerializeField] private float holdDuration = 1.2f;

    [Header("Door Animation")]
    [SerializeField] private Animation doorAnimation;
    [SerializeField] private string openClipName = "OpenDoors";
    [SerializeField] private string closeClipName = "CloseDoors";
    [SerializeField] private float fallbackDoorAnimationTime = 1f;

    [Header("?뚮젅?댁뼱 ?묒듅")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform boardingPoint;
    [SerializeField] private Transform boardingLookTarget;
    [SerializeField] private float boardMoveDuration = 0.65f;
    [SerializeField] private MonoBehaviour[] controlsToDisable;

    [Header("痢??좏깮 UI")]
    [SerializeField] private ElevatorFloorSelectionUI floorSelectionUI;
    [SerializeField] private bool firstBoardingOnlyRooftop = true;
    [SerializeField] private string firstBoardingPlayerPrefsKey = "AETHER_ELEVATOR_FIRST_BOARDING";
    [SerializeField] private ElevatorFloorOption[] firstBoardingFloors =
    {
        new ElevatorFloorOption { floorTitle = "옥상", floorSubtitle = "1구역", sceneName = "08.1구역_2", unlocked = true },
        new ElevatorFloorOption { floorTitle = "2구역", floorSubtitle = "SECTION 02", sceneName = "09.2구역_2", unlocked = false },
        new ElevatorFloorOption { floorTitle = "3구역", floorSubtitle = "SECTION 03", sceneName = "11.3구역_찐", unlocked = false },
        new ElevatorFloorOption { floorTitle = "1층 연구실", floorSubtitle = "LAB 1F", sceneName = "06.연구소 메인 장소", unlocked = true }
    };

    [Header("?대룞 ?곗텧")]
    [SerializeField] private Transform elevatorCabinRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float travelDuration = 4.5f;
    [SerializeField] private float cabinShakeAmplitude = 0.025f;
    [SerializeField] private float cabinVerticalMotion = 0.045f;
    [SerializeField] private float cameraShakeAmplitude = 0.01f;
    [SerializeField] private float cameraTravelPitchAmplitude = 0.75f;
    [SerializeField] private float cameraTravelRollAmplitude = 0.45f;
    [SerializeField] private Light[] cabinLights;
    [SerializeField] private float lightFlickerSpeed = 11f;
    [SerializeField] private float lightIntensityMultiplier = 0.18f;

    [Header("???꾪솚")]
    [SerializeField] private string fallbackTargetSceneName = "08.1구역_2";
    [SerializeField] private bool useSceneFaderIfAvailable = true;
    [SerializeField] private float sceneFadeOutTime = 0.75f;
    [SerializeField] private float sceneFadeInTime = 0.75f;
    [SerializeField] private float arrivalDoorOpenDelay = 0.45f;
    [SerializeField] private string pendingArrivalSceneKey = "AETHER_ELEVATOR_PENDING_ARRIVAL_SCENE";
    [SerializeField] private bool carryPlayerAcrossElevatorSceneLoad = true;
    [SerializeField] private bool carryElevatorVisualAcrossSceneLoad = true;
    [SerializeField] private bool boostInteriorLightingAfterSceneLoad = true;
    [SerializeField] private float fallbackArrivalInsideDistance = 1.7f;
    [SerializeField] private bool overrideArrivalPlayerScale;
    [SerializeField] private Vector3 arrivalPlayerScale = new Vector3(1.6f, 1.6f, 1.6f);
    [SerializeField] private bool overrideArrivalMovementSpeedMultiplier;
    [SerializeField] private float arrivalMovementSpeedMultiplier = 1f;
    [SerializeField] private bool overrideArrivalJumpForceMultiplier;
    [SerializeField] private float arrivalJumpForceMultiplier = 1f;

    [Header("Sound")]
    [SerializeField] private AudioSource movementLoopAudio;
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField] private AudioClip arrivalDingClip;

    [Header("Events")]
    [SerializeField] private UnityEvent onBoardingStarted;
    [SerializeField] private UnityEvent onFloorUIOpened;
    [SerializeField] private UnityEvent onTravelStarted;
    [SerializeField] private UnityEvent onArrived;

    private Vector3 cabinInitialLocalPosition;
    private Vector3 cameraInitialLocalPosition;
    private Quaternion cameraInitialLocalRotation;
    private float[] baseLightIntensities;
    private bool busy;
    private bool boarded;
    private bool savedUseGravity;
    private bool savedIsKinematic;
    private RigidbodyConstraints savedConstraints;
    private Move_Hankyul lockedHankyulMove;
    private bool savedAllowInput;
    private bool savedAllowMove;
    private bool savedAllowLook;
    private bool arrivalPlayerPreparedBeforeStart;
    private static Transform carriedPlayerRoot;
    private static string carriedTargetSceneName;
    private static ElevatorInteractionController carriedElevatorController;
    private static string carriedElevatorTargetSceneName;

    public bool CanInteract => !busy && !boarded;

    public string GetInteractionName()
    {
        return interactionName;
    }

    public float GetHoldDuration()
    {
        return holdDuration;
    }

    public void BeginHold(GameObject interactor)
    {
    }

    public void UpdateHold(GameObject interactor, float holdProgress)
    {
    }

    public void CancelHold(GameObject interactor)
    {
    }

    public void CompleteHold(GameObject interactor)
    {
        if (busy || boarded)
            return;

        if (interactor == null || !LocalPlayerBindingResolver.TryResolve(interactor.transform, out var committedRoot, false))
            return;
        if (playerRoot != null && playerRoot != committedRoot) return;
        playerRoot = committedRoot;

        if (playerRigidbody == null && playerRoot != null)
            playerRigidbody = playerRoot.GetComponent<Rigidbody>();

        StartCoroutine(BoardingRoutine());
    }

    private void Awake()
    {
        NormalizeFloorOptions();

        if (elevatorCabinRoot != null)
            cabinInitialLocalPosition = elevatorCabinRoot.localPosition;

        if (playerCamera != null)
        {
            cameraInitialLocalPosition = playerCamera.transform.localPosition;
            cameraInitialLocalRotation = playerCamera.transform.localRotation;
        }

        if (cabinLights != null && cabinLights.Length > 0)
        {
            baseLightIntensities = new float[cabinLights.Length];
            for (int i = 0; i < cabinLights.Length; i++)
                baseLightIntensities[i] = cabinLights[i] != null ? cabinLights[i].intensity : 1f;
        }

        if (floorSelectionUI != null)
        {
            floorSelectionUI.FloorSelected -= HandleFloorSelected;
            floorSelectionUI.FloorSelected += HandleFloorSelected;
            floorSelectionUI.HideImmediate();
        }
    }

    private void Start()
    {
        NormalizeFloorOptions();
        ForceDoorClosedState();

        if (ShouldOpenDoorForArrivalScene())
        {
            if (TryHandOffArrivalToCarriedElevator())
                return;

            StartCoroutine(OpenDoorForArrivalSceneRoutine());
        }
    }

    private void OnDestroy()
    {
        if (floorSelectionUI != null)
            floorSelectionUI.FloorSelected -= HandleFloorSelected;
    }

    private IEnumerator BoardingRoutine()
    {
        busy = true;
        onBoardingStarted?.Invoke();

        LockPlayer();

        PlayDoor(openClipName, doorOpenClip);
        yield return new WaitForSeconds(GetDoorClipLength(openClipName));

        yield return MovePlayerToBoardingPoint();

        PlayDoor(closeClipName, doorCloseClip);
        yield return new WaitForSeconds(GetDoorClipLength(closeClipName));

        boarded = true;
        busy = false;

        OpenFloorSelectionUI();
    }

    private IEnumerator MovePlayerToBoardingPoint()
    {
        if (playerRoot == null || boardingPoint == null)
            yield break;

        Vector3 startPosition = playerRoot.position;
        Quaternion startRotation = playerRoot.rotation;
        Quaternion targetRotation = GetBoardingTargetRotation(startRotation);
        float time = 0f;

        while (time < boardMoveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, boardMoveDuration));
            float smoothT = t * t * (3f - 2f * t);

            playerRoot.position = Vector3.Lerp(startPosition, boardingPoint.position, smoothT);
            playerRoot.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            yield return null;
        }

        playerRoot.SetPositionAndRotation(boardingPoint.position, targetRotation);
        SyncPlayerLook(targetRotation);
    }

    private void OpenFloorSelectionUI()
    {
        if (floorSelectionUI == null)
        {
            GameObject uiObject = new GameObject("Elevator Floor Selection UI");
            floorSelectionUI = uiObject.AddComponent<ElevatorFloorSelectionUI>();
            floorSelectionUI.FloorSelected += HandleFloorSelected;
        }

        NormalizeFloorOptions();

        if (firstBoardingFloors != null && firstBoardingFloors.Length > 0)
            floorSelectionUI.ConfigureFloors(firstBoardingFloors);

        floorSelectionUI.Open();
        onFloorUIOpened?.Invoke();
    }

    private void HandleFloorSelected(ElevatorFloorOption floor)
    {
        if (floor == null || busy)
            return;

        string targetScene = string.IsNullOrEmpty(floor.sceneName) ? fallbackTargetSceneName : floor.sceneName;
        if (string.IsNullOrEmpty(targetScene))
            return;

        StartCoroutine(TravelRoutine(targetScene));
    }

    private void NormalizeFloorOptions()
    {
        firstBoardingFloors = new[]
        {
            new ElevatorFloorOption { floorTitle = "옥상", floorSubtitle = "1구역", sceneName = "08.1구역_2", unlocked = true },
            new ElevatorFloorOption { floorTitle = "2구역", floorSubtitle = "SECTION 02", sceneName = "09.2구역_2", unlocked = ElevatorZoneProgress.IsZoneUnlocked(2) },
            new ElevatorFloorOption { floorTitle = "3구역", floorSubtitle = "SECTION 03", sceneName = "11.3구역_찐", unlocked = ElevatorZoneProgress.IsZoneUnlocked(3) },
            new ElevatorFloorOption { floorTitle = "1층 연구실", floorSubtitle = "LAB 1F", sceneName = "06.연구소 메인 장소", unlocked = true }
        };

        fallbackTargetSceneName = "08.1구역_2";
    }

    private IEnumerator TravelRoutine(string targetScene)
    {
        busy = true;

        if (floorSelectionUI != null)
            floorSelectionUI.Close();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayDoor(closeClipName, doorCloseClip);
        yield return new WaitForSeconds(GetDoorClipLength(closeClipName));

        if (movementLoopAudio != null)
        {
            movementLoopAudio.loop = true;
            movementLoopAudio.Play();
        }

        onTravelStarted?.Invoke();

        float elapsed = 0f;
        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            UpdateTravelMotion(elapsed);
            yield return null;
        }

        ResetTravelMotion();

        if (movementLoopAudio != null && movementLoopAudio.isPlaying)
            movementLoopAudio.Stop();

        if (oneShotAudio != null && arrivalDingClip != null)
            oneShotAudio.PlayOneShot(arrivalDingClip);

        onArrived?.Invoke();

        if (carryPlayerAcrossElevatorSceneLoad && PlayerStateSessionStore.UsesLocalRouting
            && !PlayerStateSessionStore.TryPrepareLocalCarry(playerRoot, targetScene))
        {
            UnlockPlayerForCancelOrDebug();
            busy = false;
            Debug.LogWarning("[Elevator] Local carry owner/run could not be validated; travel load cancelled.", this);
            yield break;
        }
        MarkFirstBoardingCompleted();
        MarkPendingArrival(targetScene);
        PreparePlayerForSceneLoad();
        LoadTargetScene(targetScene);
    }

    private void UpdateTravelMotion(float elapsed)
    {
        if (elevatorCabinRoot != null)
        {
            float x = Mathf.Sin(elapsed * 18f) * cabinShakeAmplitude;
            float y = Mathf.Sin(elapsed * 2.2f) * cabinVerticalMotion + Mathf.Cos(elapsed * 15f) * cabinShakeAmplitude;
            float z = Mathf.Cos(elapsed * 13f) * cabinShakeAmplitude;
            elevatorCabinRoot.localPosition = cabinInitialLocalPosition + new Vector3(x, y, z);
        }

        if (playerCamera != null)
        {
            float x = Mathf.Sin(elapsed * 22f) * cameraShakeAmplitude;
            float y = Mathf.Cos(elapsed * 19f) * cameraShakeAmplitude;
            playerCamera.transform.localPosition = cameraInitialLocalPosition + new Vector3(x, y, 0f);

            float pitch = Mathf.Sin(elapsed * 3.1f) * cameraTravelPitchAmplitude;
            float roll = Mathf.Sin(elapsed * 4.7f) * cameraTravelRollAmplitude;
            playerCamera.transform.localRotation = cameraInitialLocalRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        if (cabinLights != null && baseLightIntensities != null)
        {
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * lightFlickerSpeed, 0.31f) - 0.5f) * lightIntensityMultiplier;

            for (int i = 0; i < cabinLights.Length; i++)
            {
                if (cabinLights[i] != null)
                    cabinLights[i].intensity = baseLightIntensities[i] * flicker;
            }
        }
    }

    private void ResetTravelMotion()
    {
        if (elevatorCabinRoot != null)
            elevatorCabinRoot.localPosition = cabinInitialLocalPosition;

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = cameraInitialLocalPosition;
            playerCamera.transform.localRotation = cameraInitialLocalRotation;
        }

        if (cabinLights != null && baseLightIntensities != null)
        {
            for (int i = 0; i < cabinLights.Length; i++)
            {
                if (cabinLights[i] != null)
                    cabinLights[i].intensity = baseLightIntensities[i];
            }
        }
    }

    private void LockPlayer()
    {
        if (playerRigidbody != null)
        {
            savedUseGravity = playerRigidbody.useGravity;
            savedIsKinematic = playerRigidbody.isKinematic;
            savedConstraints = playerRigidbody.constraints;

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
        }

        if (controlsToDisable != null)
        {
            for (int i = 0; i < controlsToDisable.Length; i++)
            {
                if (controlsToDisable[i] != null)
                    controlsToDisable[i].enabled = false;
            }
        }

        LockKnownPlayerControls();
    }

    public void UnlockPlayerForCancelOrDebug()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = savedIsKinematic;
            playerRigidbody.useGravity = savedUseGravity;
            playerRigidbody.constraints = savedConstraints;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (controlsToDisable != null)
        {
            for (int i = 0; i < controlsToDisable.Length; i++)
            {
                if (controlsToDisable[i] != null)
                    controlsToDisable[i].enabled = true;
            }
        }

        RestoreKnownPlayerControls();
    }

    private Quaternion GetBoardingTargetRotation(Quaternion fallbackRotation)
    {
        if (boardingPoint == null)
            return fallbackRotation;

        Transform lookTarget = boardingLookTarget != null ? boardingLookTarget : FindDoorLookTarget();
        if (lookTarget == null)
            return boardingPoint.rotation;

        Vector3 direction = lookTarget.position - boardingPoint.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return boardingPoint.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Transform FindDoorLookTarget()
    {
        string[] doorNames = { "elevDoorInner", "elevDoorOuter", "elevFramDoorR", "elevFrameDoorL" };

        for (int i = 0; i < doorNames.Length; i++)
        {
            Transform found = FindDeepChild(transform, doorNames[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SyncPlayerLook(Quaternion targetRotation)
    {
        if (playerRoot == null)
            return;

        Move_Hankyul hankyulMove = playerRoot.GetComponent<Move_Hankyul>();
        if (hankyulMove != null)
            hankyulMove.SnapLookToWorldRotation(targetRotation);
    }

    private void LockKnownPlayerControls()
    {
        if (playerRoot == null)
            return;

        lockedHankyulMove = playerRoot.GetComponent<Move_Hankyul>();
        if (lockedHankyulMove == null)
            return;

        savedAllowInput = lockedHankyulMove.allowInput;
        savedAllowMove = lockedHankyulMove.allowMove;
        savedAllowLook = lockedHankyulMove.allowLook;

        lockedHankyulMove.allowInput = false;
        lockedHankyulMove.allowMove = false;
        lockedHankyulMove.allowLook = false;
    }

    private void RestoreKnownPlayerControls()
    {
        if (lockedHankyulMove == null)
            return;

        lockedHankyulMove.allowInput = savedAllowInput;
        lockedHankyulMove.allowMove = savedAllowMove;
        lockedHankyulMove.allowLook = savedAllowLook;
        lockedHankyulMove = null;
    }

    private void PlayDoor(string clipName, AudioClip audioClip)
    {
        if (oneShotAudio != null && audioClip != null)
            oneShotAudio.PlayOneShot(audioClip);

        if (doorAnimation == null || string.IsNullOrEmpty(clipName))
            return;

        if (doorAnimation.GetClip(clipName) == null)
            return;

        doorAnimation.Stop();
        doorAnimation[clipName].wrapMode = WrapMode.ClampForever;
        doorAnimation.Play(clipName);
    }

    private void ForceDoorClosedState()
    {
        if (doorAnimation == null || string.IsNullOrEmpty(closeClipName))
            return;

        AnimationClip closeClip = doorAnimation.GetClip(closeClipName);
        if (closeClip == null)
            return;

        doorAnimation.Stop();
        doorAnimation[closeClipName].wrapMode = WrapMode.ClampForever;
        doorAnimation.Play(closeClipName);
        doorAnimation[closeClipName].time = closeClip.length;
        doorAnimation.Sample();
        doorAnimation.Stop();
    }

    private float GetDoorClipLength(string clipName)
    {
        if (doorAnimation == null || string.IsNullOrEmpty(clipName))
            return fallbackDoorAnimationTime;

        AnimationClip clip = doorAnimation.GetClip(clipName);
        return clip != null ? clip.length : fallbackDoorAnimationTime;
    }

    private void LoadTargetScene(string sceneName)
    {
        if (useSceneFaderIfAvailable && SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeOutAndLoadScene(sceneName, sceneFadeOutTime, sceneFadeInTime);
            return;
        }

        if (useSceneFaderIfAvailable && SceneFadeController.Instance != null)
        {
            SceneFadeController.Instance.FadeToScene(sceneName, sceneFadeOutTime, 0.1f);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void MarkPendingArrival(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        PlayerPrefs.SetString(pendingArrivalSceneKey, sceneName);
        PlayerPrefs.Save();
    }

    private bool ShouldOpenDoorForArrivalScene()
    {
        string pendingScene = PlayerPrefs.GetString(pendingArrivalSceneKey, string.Empty);
        string activeScene = SceneManager.GetActiveScene().name;
        bool prefsPending = !string.IsNullOrEmpty(pendingScene) && pendingScene == activeScene;
        bool carriedPending = carriedPlayerRoot != null && carriedTargetSceneName == activeScene;
        bool carriedElevatorPending = carriedElevatorController != null && carriedElevatorTargetSceneName == activeScene;
        return prefsPending || carriedPending || carriedElevatorPending;
    }

    private bool TryHandOffArrivalToCarriedElevator()
    {
        if (carriedElevatorController == null || carriedElevatorController == this)
            return false;

        if (carriedElevatorTargetSceneName != SceneManager.GetActiveScene().name)
            return false;

        StartCoroutine(HandOffArrivalToCarriedElevatorRoutine(carriedElevatorController));
        return true;
    }

    private IEnumerator HandOffArrivalToCarriedElevatorRoutine(ElevatorInteractionController carriedElevator)
    {
        PlayerPrefs.DeleteKey(pendingArrivalSceneKey);
        PlayerPrefs.Save();

        carriedElevator.transform.SetPositionAndRotation(transform.position, transform.rotation);
        carriedElevator.transform.localScale = transform.localScale;
        SceneManager.MoveGameObjectToScene(carriedElevator.gameObject, SceneManager.GetActiveScene());

        carriedElevator.playerRoot = carriedPlayerRoot != null ? carriedPlayerRoot : carriedElevator.FindPlayerRoot();
        carriedElevator.playerRigidbody = carriedElevator.playerRoot != null ? carriedElevator.playerRoot.GetComponent<Rigidbody>() : null;
        carriedElevator.ForceDoorClosedState();

        SetElevatorVisualsEnabled(false);
        yield return carriedElevator.OpenDoorForArrivalSceneRoutine();
    }

    private void SetElevatorVisualsEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = enabled;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            lights[i].enabled = enabled;
    }

    private IEnumerator OpenDoorForArrivalSceneRoutine()
    {
        PlayerPrefs.DeleteKey(pendingArrivalSceneKey);
        PlayerPrefs.Save();

        yield return PlacePlayerInArrivalElevatorRoutine();
        yield return new WaitForSeconds(arrivalDoorOpenDelay);

        if (oneShotAudio != null && arrivalDingClip != null)
            oneShotAudio.PlayOneShot(arrivalDingClip);

        PlayDoor(openClipName, doorOpenClip);
        yield return new WaitForSeconds(GetDoorClipLength(openClipName));

        RestoreArrivalPlayerControls();
    }

    private void PreparePlayerForSceneLoad()
    {
        if (!carryPlayerAcrossElevatorSceneLoad || playerRoot == null)
            return;

        playerRoot.SetParent(null, true);
        DontDestroyOnLoad(playerRoot.gameObject);
        carriedPlayerRoot = playerRoot;
        carriedTargetSceneName = PlayerPrefs.GetString(pendingArrivalSceneKey, string.Empty);

        if (carryElevatorVisualAcrossSceneLoad)
        {
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            carriedElevatorController = this;
            carriedElevatorTargetSceneName = carriedTargetSceneName;
            SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedElevator;
            SceneManager.sceneLoaded += HandleSceneLoadedForCarriedElevator;
        }
    }

    private static void HandleSceneLoadedForCarriedElevator(Scene scene, LoadSceneMode mode)
    {
        if (carriedElevatorController == null || carriedElevatorTargetSceneName != scene.name)
            return;

        ElevatorInteractionController destinationElevator = FindDestinationElevatorInScene(scene);
        if (destinationElevator == null)
            return;

        carriedElevatorController.PrepareCarriedElevatorBeforeFirstFrame(destinationElevator, scene);
        SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedElevator;
    }

    private static ElevatorInteractionController FindDestinationElevatorInScene(Scene scene)
    {
        ElevatorInteractionController[] elevators = FindObjectsByType<ElevatorInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < elevators.Length; i++)
        {
            ElevatorInteractionController elevator = elevators[i];
            if (elevator == null || elevator == carriedElevatorController)
                continue;

            if (elevator.gameObject.scene == scene)
                return elevator;
        }

        return null;
    }

    private void PrepareCarriedElevatorBeforeFirstFrame(ElevatorInteractionController destinationElevator, Scene destinationScene)
    {
        transform.SetPositionAndRotation(destinationElevator.transform.position, destinationElevator.transform.rotation);
        transform.localScale = destinationElevator.transform.localScale;
        SceneManager.MoveGameObjectToScene(gameObject, destinationScene);
        SetElevatorVisualsEnabled(true);
        ApplyArrivalInteriorLightingBoost();

        playerRoot = carriedPlayerRoot != null ? carriedPlayerRoot : FindPlayerRoot();
        playerRigidbody = playerRoot != null ? playerRoot.GetComponent<Rigidbody>() : null;
        overrideArrivalPlayerScale = destinationElevator.overrideArrivalPlayerScale;
        arrivalPlayerScale = destinationElevator.arrivalPlayerScale;
        overrideArrivalMovementSpeedMultiplier = destinationElevator.overrideArrivalMovementSpeedMultiplier;
        arrivalMovementSpeedMultiplier = destinationElevator.arrivalMovementSpeedMultiplier;
        ForceDoorClosedState();
        PlacePlayerInArrivalElevatorImmediate();

        destinationElevator.SetElevatorVisualsEnabled(false);
    }

    private void ApplyArrivalInteriorLightingBoost()
    {
        if (!boostInteriorLightingAfterSceneLoad)
            return;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (light.name == "Interior Cool Ceiling")
            {
                light.color = new Color(0.6708f, 0.846f, 0.91f, 1f);
                light.intensity = 10.85f;
                light.range = 4.1f;
            }
            else if (light.name == "Interior Front Wash")
            {
                light.color = new Color(0.7396f, 0.8836f, 0.91f, 1f);
                light.intensity = 5.642f;
                light.range = 3.4f;
            }
            else if (light.name == "Interior Floor Soft Fill")
            {
                light.color = new Color(0.602f, 0.7708f, 0.91f, 1f);
                light.intensity = 2.4304f;
                light.range = 2.8f;
            }
        }
    }

    private void PlacePlayerInArrivalElevatorImmediate()
    {
        if (playerRoot == null)
            return;

        playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        LockArrivalPlayerControls();
        ApplyArrivalPlayerScale();
        ApplyArrivalMovementSpeedMultiplier();

        Vector3 arrivalPosition = GetArrivalPlayerPosition();
        Quaternion arrivalRotation = GetArrivalPlayerRotation();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        playerRoot.SetPositionAndRotation(arrivalPosition, arrivalRotation);
        SyncPlayerLook(arrivalRotation);
        ActivatePlayerCameraForGameView();
        arrivalPlayerPreparedBeforeStart = true;
    }

    private IEnumerator PlacePlayerInArrivalElevatorRoutine()
    {
        if (arrivalPlayerPreparedBeforeStart)
        {
            arrivalPlayerPreparedBeforeStart = false;
            yield break;
        }

        for (int i = 0; i < 10 && playerRoot == null; i++)
        {
            playerRoot = FindPlayerRoot();
            yield return null;
        }

        if (playerRoot == null)
            yield break;

        playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        LockArrivalPlayerControls();
        ApplyArrivalPlayerScale();
        ApplyArrivalMovementSpeedMultiplier();

        Vector3 arrivalPosition = GetArrivalPlayerPosition();
        Quaternion arrivalRotation = GetArrivalPlayerRotation();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        playerRoot.SetPositionAndRotation(arrivalPosition, arrivalRotation);
        SyncPlayerLook(arrivalRotation);
        ActivatePlayerCameraForGameView();
    }

    private void ApplyArrivalPlayerScale()
    {
        if (!overrideArrivalPlayerScale || playerRoot == null)
            return;

        playerRoot.localScale = arrivalPlayerScale;
    }

    private void ApplyArrivalMovementSpeedMultiplier()
    {
        if (playerRoot == null)
            return;

        Move_Hankyul moveHankyul = playerRoot.GetComponent<Move_Hankyul>();
        if (moveHankyul == null)
            moveHankyul = playerRoot.GetComponentInChildren<Move_Hankyul>(true);

        if (moveHankyul != null)
        {
            moveHankyul.SetMovementSpeedMultiplier(
                overrideArrivalMovementSpeedMultiplier ? arrivalMovementSpeedMultiplier : 1f);
            moveHankyul.SetJumpForceMultiplier(
                overrideArrivalJumpForceMultiplier ? arrivalJumpForceMultiplier : 1f);
        }
    }

    private Transform FindPlayerRoot()
    {
        if (carriedPlayerRoot != null)
            return carriedPlayerRoot;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer.transform;

        Move_Hankyul moveHankyul = FindFirstObjectByType<Move_Hankyul>();
        if (moveHankyul != null)
            return moveHankyul.transform;

        return null;
    }

    private Vector3 GetArrivalPlayerPosition()
    {
        if (boardingPoint != null)
            return boardingPoint.position;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        return transform.position - flatForward.normalized * fallbackArrivalInsideDistance;
    }

    private Quaternion GetArrivalPlayerRotation()
    {
        if (boardingPoint != null)
            return GetBoardingTargetRotation(boardingPoint.rotation);

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private void LockArrivalPlayerControls()
    {
        if (playerRigidbody != null)
        {
            savedUseGravity = playerRigidbody.useGravity;
            savedIsKinematic = playerRigidbody.isKinematic;
            savedConstraints = playerRigidbody.constraints;

            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        LockKnownPlayerControls();
    }

    private void RestoreArrivalPlayerControls()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.useGravity = true;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        Move_Hankyul moveHankyul = playerRoot != null ? playerRoot.GetComponent<Move_Hankyul>() : null;
        if (moveHankyul != null)
        {
            moveHankyul.allowInput = true;
            moveHankyul.allowMove = true;
            moveHankyul.allowLook = true;
            moveHankyul.enabled = true;
        }

        PlayerInput playerInput = playerRoot != null ? playerRoot.GetComponent<PlayerInput>() : null;
        if (playerInput != null)
            playerInput.ActivateInput();

        if (playerRoot != null)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerRoot.gameObject, SceneManager.GetActiveScene());

        if (carriedPlayerRoot == playerRoot)
        {
            carriedPlayerRoot = null;
            carriedTargetSceneName = string.Empty;
        }

        if (carriedElevatorController == this)
        {
            carriedElevatorController = null;
            carriedElevatorTargetSceneName = string.Empty;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ActivatePlayerCameraForGameView()
    {
        if (playerRoot == null)
            return;

        Camera playerViewCamera = playerRoot.GetComponentInChildren<Camera>(true);
        if (playerViewCamera == null)
            return;

        EnsureCameraCanSeeElevator(playerViewCamera);

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera targetCamera = cameras[i];
            if (targetCamera == null)
                continue;

            bool isPlayerCamera = targetCamera == playerViewCamera;
            targetCamera.enabled = isPlayerCamera;

            if (isPlayerCamera)
                targetCamera.tag = "MainCamera";
            else if (targetCamera.CompareTag("MainCamera"))
                targetCamera.tag = "Untagged";
        }

        AudioListener playerListener = playerViewCamera.GetComponent<AudioListener>();
        if (playerListener == null)
            playerListener = playerViewCamera.gameObject.AddComponent<AudioListener>();

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
                listeners[i].enabled = listeners[i] == playerListener;
        }
    }

    private void EnsureCameraCanSeeElevator(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        int elevatorLayer = LayerMask.NameToLayer("ElevatorInterior");
        if (elevatorLayer < 0)
            return;

        targetCamera.cullingMask |= 1 << elevatorLayer;
    }

    private bool HasCompletedFirstBoarding()
    {
        return PlayerPrefs.GetInt(firstBoardingPlayerPrefsKey, 0) == 1;
    }

    private void MarkFirstBoardingCompleted()
    {
        if (!firstBoardingOnlyRooftop)
            return;

        PlayerPrefs.SetInt(firstBoardingPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }
}
