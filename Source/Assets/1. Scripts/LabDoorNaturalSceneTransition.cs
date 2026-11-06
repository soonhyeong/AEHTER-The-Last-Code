using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LabDoorNaturalSceneTransition : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string targetSceneName = "06.연구소 메인 장소";
    [SerializeField] private string playerTag = "Player";

    [Header("Lab Door")]
    [SerializeField] private Lab_Door labDoor;
    [SerializeField] private Lab_Door[] additionalLabDoors;
    [SerializeField] private bool requireDoorDisabled = true;
    [SerializeField] private bool preloadWhenDoorDisabled = true;

    [Header("Timing")]
    [SerializeField] private float entryHoldTime = 0.45f;
    [SerializeField] private float fadeOutDuration = 1.35f;
    [SerializeField] private float fadeInDuration = 1.2f;
    [SerializeField] private float fullFadeHoldTime = 0.2f;

    [Header("Atmosphere")]
    [SerializeField] private ParticleSystem[] entryDustEffects;
    [SerializeField] private AudioSource cityAmbience;
    [SerializeField] private AudioSource labAmbience;
    [SerializeField] private float audioCrossfadeDuration = 1.2f;
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private float shakeDuration = 0.28f;
    [SerializeField] private float shakeAmplitude = 0.13f;
    [SerializeField] private float shakeFrequency = 28f;

    [Header("Fade Overlay")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private Color fadeColor = Color.black;

    private AsyncOperation preloadOperation;
    private bool doorDisabled;
    private bool triggered;
    private float cityStartVolume;
    private float labTargetVolume = 1f;
    private GameObject runtimeFadeCanvas;
    private bool waitingForLoadedScene;

    private void Awake()
    {
        if (labDoor == null)
            labDoor = GetComponentInParent<Lab_Door>();

        if (cameraShake == null)
            cameraShake = CameraShake.Instance;

        CacheAudioVolumes();
        PrepareFadeImage();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (labDoor != null)
            labDoor.OnDoorDisabled.AddListener(HandleDoorDisabled);

        if (additionalLabDoors == null)
            return;

        for (int i = 0; i < additionalLabDoors.Length; i++)
        {
            if (additionalLabDoors[i] != null)
                additionalLabDoors[i].OnDoorDisabled.AddListener(HandleDoorDisabled);
        }
    }

    private void Start()
    {
        if (IsAnyDoorDisabled())
            HandleDoorDisabled();
        else if (!requireDoorDisabled && preloadWhenDoorDisabled)
            BeginPreload();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (labDoor != null)
            labDoor.OnDoorDisabled.RemoveListener(HandleDoorDisabled);

        if (additionalLabDoors == null)
            return;

        for (int i = 0; i < additionalLabDoors.Length; i++)
        {
            if (additionalLabDoors[i] != null)
                additionalLabDoors[i].OnDoorDisabled.RemoveListener(HandleDoorDisabled);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (requireDoorDisabled && !doorDisabled)
            return;

        triggered = true;
        StartCoroutine(TransitionRoutine());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!waitingForLoadedScene)
            return;

        waitingForLoadedScene = false;
        StartCoroutine(FadeInAndCleanupRoutine());
    }

    private bool IsAnyDoorDisabled()
    {
        if (labDoor != null && labDoor.IsDisabled)
            return true;

        if (additionalLabDoors == null)
            return false;

        for (int i = 0; i < additionalLabDoors.Length; i++)
        {
            if (additionalLabDoors[i] != null && additionalLabDoors[i].IsDisabled)
                return true;
        }

        return false;
    }

    private void HandleDoorDisabled()
    {
        if (doorDisabled)
            return;

        doorDisabled = true;

        PlayEntryAtmosphere();

        if (preloadWhenDoorDisabled)
            BeginPreload();
    }

    private void BeginPreload()
    {
        if (preloadOperation != null)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("[LabDoorNaturalSceneTransition] targetSceneName is empty.", this);
            return;
        }

        preloadOperation = SceneManager.LoadSceneAsync(targetSceneName);
        if (preloadOperation != null)
            preloadOperation.allowSceneActivation = false;
    }

    private IEnumerator TransitionRoutine()
    {
        if (preloadOperation == null)
            BeginPreload();

        PlayEntryAtmosphere();

        if (entryHoldTime > 0f)
            yield return new WaitForSeconds(entryHoldTime);

        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeOutDuration));

        if (preloadOperation != null)
        {
            while (preloadOperation.progress < 0.9f)
                yield return null;
        }

        if (fullFadeHoldTime > 0f)
            yield return new WaitForSecondsRealtime(fullFadeHoldTime);

        waitingForLoadedScene = true;
        DontDestroyOnLoad(gameObject);

        if (preloadOperation != null)
            preloadOperation.allowSceneActivation = true;
        else
            SceneManager.LoadScene(targetSceneName);
    }

    private IEnumerator FadeInAndCleanupRoutine()
    {
        yield return null;
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeInDuration));

        if (runtimeFadeCanvas != null)
            Destroy(runtimeFadeCanvas);

        Destroy(gameObject);
    }

    private void PlayEntryAtmosphere()
    {
        if (cameraShake == null)
            cameraShake = CameraShake.Instance;

        if (cameraShake != null)
            cameraShake.Shake(shakeDuration, shakeAmplitude, shakeFrequency);

        if (entryDustEffects != null)
        {
            for (int i = 0; i < entryDustEffects.Length; i++)
            {
                if (entryDustEffects[i] != null)
                    entryDustEffects[i].Play();
            }
        }

        if (labAmbience != null && !labAmbience.isPlaying)
            labAmbience.Play();

        if (cityAmbience != null || labAmbience != null)
            StartCoroutine(CrossfadeAudioRoutine());
    }

    private IEnumerator CrossfadeAudioRoutine()
    {
        float elapsed = 0f;

        while (elapsed < audioCrossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = audioCrossfadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / audioCrossfadeDuration);

            if (cityAmbience != null)
                cityAmbience.volume = Mathf.Lerp(cityStartVolume, 0f, t);

            if (labAmbience != null)
                labAmbience.volume = Mathf.Lerp(0f, labTargetVolume, t);

            yield return null;
        }

        if (cityAmbience != null)
            cityAmbience.volume = 0f;

        if (labAmbience != null)
            labAmbience.volume = labTargetVolume;
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
    {
        PrepareFadeImage();

        if (fadeImage == null)
            yield break;

        float elapsed = 0f;
        SetFadeAlpha(fromAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetFadeAlpha(toAlpha);
    }

    private void CacheAudioVolumes()
    {
        if (cityAmbience != null)
            cityStartVolume = cityAmbience.volume;

        if (labAmbience != null)
        {
            labTargetVolume = labAmbience.volume;
            labAmbience.volume = 0f;
        }
    }

    private void PrepareFadeImage()
    {
        if (fadeImage == null)
            fadeImage = CreateRuntimeFadeImage();

        if (fadeImage == null)
            return;

        fadeImage.raycastTarget = false;
        SetFadeAlpha(0f);
    }

    private Image CreateRuntimeFadeImage()
    {
        GameObject canvasObject = new GameObject("Runtime Scene Fade Canvas");
        runtimeFadeCanvas = canvasObject;
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Fade Image");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = fadeColor;
        return image;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }
}
