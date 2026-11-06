using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ElevatorIntroLegacy : MonoBehaviour
{
    [Header("엘리베이터")]
    [SerializeField] private Transform elevatorCabinRoot;
    [SerializeField] private Animation doorAnimation;
    [SerializeField] private string closedClipName = "CloseDoors";
    [SerializeField] private string openClipName = "OpenDoors";

    [Header("플레이어")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private MonoBehaviour[] controlScriptsToDisable;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform playerStartPoint;

    [Header("시퀀스 시간")]
    [SerializeField] private float introDuration = 10f;
    [SerializeField] private float doorOpenExtraDelay = 0.2f;
    [SerializeField] private float controlReturnDelay = 0.3f;

    [Header("가짜 이동 연출")]
    [SerializeField] private bool useCabinShake = true;
    [SerializeField] private float shakeAmplitudeX = 0.015f;
    [SerializeField] private float shakeAmplitudeY = 0.02f;
    [SerializeField] private float shakeAmplitudeZ = 0.015f;
    [SerializeField] private float shakeFrequency = 12f;

    [Header("카메라 흔들림")]
    [SerializeField] private bool useCameraShake = true;
    [SerializeField] private float cameraShakeAmplitudeX = 0.005f;
    [SerializeField] private float cameraShakeAmplitudeY = 0.004f;
    [SerializeField] private float cameraShakeFrequency = 14f;

    [Header("상하 이동감")]
    [SerializeField] private bool useSubtleVerticalMotion = true;
    [SerializeField] private float verticalMotionAmount = 0.03f;
    [SerializeField] private float verticalMotionFrequency = 1.5f;

    [Header("조명 깜빡임")]
    [SerializeField] private Light[] cabinLights;
    [SerializeField] private bool useLightFlicker = true;
    [SerializeField] private float lightMinIntensity = 0.9f;
    [SerializeField] private float lightMaxIntensity = 1.15f;
    [SerializeField] private float lightFlickerSpeed = 10f;

    [Header("사운드")]
    [SerializeField] private AudioSource movementLoopAudio;
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip arrivalDingClip;

    [Header("이벤트")]
    [SerializeField] private UnityEvent onIntroStarted;
    [SerializeField] private UnityEvent onElevatorArrived;
    [SerializeField] private UnityEvent onDoorOpened;
    [SerializeField] private UnityEvent onControlReturned;

    private Vector3 cabinInitialLocalPosition;
    private Vector3 cameraInitialLocalPosition;
    private bool isSequenceRunning;

    private bool savedUseGravity;
    private bool savedIsKinematic;
    private RigidbodyConstraints savedConstraints;
    private Vector3 savedVelocity;
    private Vector3 savedAngularVelocity;

    private void Awake()
    {
        if (elevatorCabinRoot != null)
            cabinInitialLocalPosition = elevatorCabinRoot.localPosition;

        if (playerCamera != null)
            cameraInitialLocalPosition = playerCamera.transform.localPosition;

        PrepareDoorAnimation();
    }

    private void Start()
    {
        SnapPlayerToStartPoint();
        StartIntro();
    }

    private void PrepareDoorAnimation()
    {
        if (doorAnimation == null)
            return;

        if (doorAnimation.GetClip(closedClipName) != null)
        {
            doorAnimation[closedClipName].layer = 0;
            doorAnimation[closedClipName].wrapMode = WrapMode.ClampForever;
        }

        if (doorAnimation.GetClip(openClipName) != null)
        {
            doorAnimation[openClipName].layer = 0;
            doorAnimation[openClipName].wrapMode = WrapMode.ClampForever;
        }
    }

    public void StartIntro()
    {
        if (isSequenceRunning)
            return;

        StartCoroutine(IntroSequenceCoroutine());
    }

    private IEnumerator IntroSequenceCoroutine()
    {
        isSequenceRunning = true;

        LockPlayer();

        ForceDoorClosedState();

        if (movementLoopAudio != null)
        {
            movementLoopAudio.loop = true;
            movementLoopAudio.Play();
        }

        onIntroStarted?.Invoke();

        float elapsed = 0f;

        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;

            UpdateElevatorMotion(elapsed);
            UpdateLightFlicker();

            yield return null;
        }

        ResetMotion();

        if (movementLoopAudio != null && movementLoopAudio.isPlaying)
            movementLoopAudio.Stop();

        if (oneShotAudio != null && arrivalDingClip != null)
            oneShotAudio.PlayOneShot(arrivalDingClip);

        onElevatorArrived?.Invoke();

        yield return new WaitForSeconds(doorOpenExtraDelay);

        PlayOpenDoorAnimation();

        float openLength = GetClipLength(openClipName);
        if (openLength > 0f)
            yield return new WaitForSeconds(openLength);
        else
            yield return new WaitForSeconds(1f);

        onDoorOpened?.Invoke();

        yield return new WaitForSeconds(controlReturnDelay);

        UnlockPlayer();

        onControlReturned?.Invoke();

        isSequenceRunning = false;
    }

    private void UpdateElevatorMotion(float elapsed)
    {
        if (elevatorCabinRoot != null)
        {
            Vector3 offset = Vector3.zero;

            if (useCabinShake)
            {
                float x = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitudeX;
                float y = Mathf.Cos(elapsed * shakeFrequency * 0.85f) * shakeAmplitudeY;
                float z = Mathf.Sin(elapsed * shakeFrequency * 0.65f) * shakeAmplitudeZ;
                offset += new Vector3(x, y, z);
            }

            if (useSubtleVerticalMotion)
            {
                float vertical = Mathf.Sin(elapsed * verticalMotionFrequency) * verticalMotionAmount;
                offset += new Vector3(0f, vertical, 0f);
            }

            elevatorCabinRoot.localPosition = cabinInitialLocalPosition + offset;
        }

        if (useCameraShake && playerCamera != null)
        {
            float x = Mathf.Sin(elapsed * cameraShakeFrequency) * cameraShakeAmplitudeX;
            float y = Mathf.Cos(elapsed * cameraShakeFrequency * 0.9f) * cameraShakeAmplitudeY;
            playerCamera.transform.localPosition = cameraInitialLocalPosition + new Vector3(x, y, 0f);
        }
    }

    private void UpdateLightFlicker()
    {
        if (!useLightFlicker || cabinLights == null || cabinLights.Length == 0)
            return;

        float noise = Mathf.PerlinNoise(Time.time * lightFlickerSpeed, 0.37f);
        float intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, noise);

        for (int i = 0; i < cabinLights.Length; i++)
        {
            if (cabinLights[i] != null)
                cabinLights[i].intensity = intensity;
        }
    }

    private void ResetMotion()
    {
        if (elevatorCabinRoot != null)
            elevatorCabinRoot.localPosition = cabinInitialLocalPosition;

        if (playerCamera != null)
            playerCamera.transform.localPosition = cameraInitialLocalPosition;
    }

    private void SnapPlayerToStartPoint()
    {
        if (playerRoot == null || playerStartPoint == null)
            return;

        if (playerRigidbody != null)
        {
            bool oldKinematic = playerRigidbody.isKinematic;
            playerRigidbody.isKinematic = true;

            playerRoot.position = playerStartPoint.position;
            playerRoot.rotation = playerStartPoint.rotation;

            playerRigidbody.isKinematic = oldKinematic;
        }
        else
        {
            playerRoot.position = playerStartPoint.position;
            playerRoot.rotation = playerStartPoint.rotation;
        }
    }

    private void LockPlayer()
    {
        if (playerRigidbody != null)
        {
            savedUseGravity = playerRigidbody.useGravity;
            savedIsKinematic = playerRigidbody.isKinematic;
            savedConstraints = playerRigidbody.constraints;
            savedVelocity = playerRigidbody.linearVelocity;
            savedAngularVelocity = playerRigidbody.angularVelocity;

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
        }

        if (controlScriptsToDisable != null)
        {
            for (int i = 0; i < controlScriptsToDisable.Length; i++)
            {
                if (controlScriptsToDisable[i] != null)
                    controlScriptsToDisable[i].enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockPlayer()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = savedIsKinematic;
            playerRigidbody.useGravity = savedUseGravity;
            playerRigidbody.constraints = savedConstraints;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (controlScriptsToDisable != null)
        {
            for (int i = 0; i < controlScriptsToDisable.Length; i++)
            {
                if (controlScriptsToDisable[i] != null)
                    controlScriptsToDisable[i].enabled = true;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ForceDoorClosedState()
    {
        if (doorAnimation == null)
            return;

        AnimationClip closeClip = doorAnimation.GetClip(closedClipName);
        if (closeClip == null)
        {
            Debug.LogWarning($"닫힘 애니메이션 '{closedClipName}' 을 찾을 수 없습니다.");
            return;
        }

        doorAnimation.Stop();
        doorAnimation.Play(closedClipName);
        doorAnimation[closedClipName].time = closeClip.length;
        doorAnimation.Sample();
        doorAnimation.Stop();
    }

    private void PlayOpenDoorAnimation()
    {
        if (doorAnimation == null)
        {
            Debug.LogWarning("Animation 컴포넌트가 연결되지 않았습니다.");
            return;
        }

        AnimationClip openClip = doorAnimation.GetClip(openClipName);
        if (openClip == null)
        {
            Debug.LogWarning($"열림 애니메이션 '{openClipName}' 을 찾을 수 없습니다.");
            return;
        }

        doorAnimation.Stop();
        doorAnimation.Play(openClipName);
    }

    private float GetClipLength(string clipName)
    {
        if (doorAnimation == null)
            return 0f;

        AnimationClip clip = doorAnimation.GetClip(clipName);
        if (clip == null)
            return 0f;

        return clip.length;
    }

#if UNITY_EDITOR
    [ContextMenu("닫힌 상태 강제 적용")]
    private void Editor_ForceClosed()
    {
        ForceDoorClosedState();
    }

    [ContextMenu("문 열기 테스트")]
    private void Editor_TestOpen()
    {
        PlayOpenDoorAnimation();
    }

    [ContextMenu("인트로 시퀀스 테스트")]
    private void Editor_TestIntro()
    {
        StartIntro();
    }
#endif
}