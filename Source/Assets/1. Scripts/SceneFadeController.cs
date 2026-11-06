using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFadeController : MonoBehaviour
{
    public static SceneFadeController Instance;

    [Header("페이드 이미지")]
    [SerializeField] private Image fadeImage;

    [Header("씬 시작 시 페이드 인 시간")]
    [SerializeField] private float defaultFadeInDuration = 1.5f;

    private bool isTransitioning = false;
    private bool networkTransition;
    private bool localTransition;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    private void OnDestroy()
    {
        GameSceneNetworkTransition.Cancel(this);
        PlayerStateSessionStore.CancelLocal(this);
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        GameSceneNetworkTransition.Cancel(this);
        PlayerStateSessionStore.CancelLocal(this);
        localTransition = false;
        networkTransition = false;
        isTransitioning = false;
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        localTransition = false;
        networkTransition = false;
        // 새 씬이 열리면 하얀 화면에서 천천히 사라지게
        StopAllCoroutines();
        StartCoroutine(FadeInRoutine(defaultFadeInDuration));
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float waitAfterFullWhite)
    {
        TryFadeToScene(sceneName, fadeOutDuration, waitAfterFullWhite);
    }

    public bool TryFadeToScene(string sceneName, float fadeOutDuration, float waitAfterFullWhite)
        => TryFadeToScene(sceneName, fadeOutDuration, waitAfterFullWhite, null);

    public bool TryFadeToScene(string sceneName, float fadeOutDuration, float waitAfterFullWhite, Component owner)
    {
        if (isTransitioning)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[SceneFadeController] Scene cannot be loaded.", this);
            return false;
        }

        if (fadeImage == null)
        {
            Debug.LogError("[SceneFadeController] fadeImage is not assigned.");
            return false;
        }

        networkTransition = GameSceneNetworkTransition.IsNetworkRoute(sceneName);
        if (networkTransition && !GameSceneNetworkTransition.TryReserve(this, sceneName))
        {
            networkTransition = false;
            return false;
        }

        localTransition = PlayerStateSessionStore.IsLocalRoute(sceneName);
        if (localTransition && !PlayerStateSessionStore.TryReserveLocal(this, owner, sceneName))
        { localTransition = false; return false; }
        StartCoroutine(FadeOutAndLoadRoutine(sceneName, fadeOutDuration, waitAfterFullWhite));
        return true;
    }

    private IEnumerator FadeOutAndLoadRoutine(string sceneName, float fadeOutDuration, float waitAfterFullWhite)
    {
        if (fadeImage == null)
        {
            Debug.LogError("[SceneFadeController] fadeImage가 연결되지 않았습니다.");
            yield break;
        }

        isTransitioning = true;

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(t / fadeOutDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeImage.color = c;

        yield return new WaitForSecondsRealtime(waitAfterFullWhite);

        if (networkTransition)
        {
            if (!GameSceneNetworkTransition.TryLoad(this, sceneName))
            {
                networkTransition = false;
                StartCoroutine(FadeInRoutine(defaultFadeInDuration));
            }
        }
        else if (localTransition)
        {
            if (!PlayerStateSessionStore.TryLoadLocal(this))
            {
                localTransition = false;
                PlayerStateSessionStore.CancelLocal(this);
                StartCoroutine(FadeInRoutine(defaultFadeInDuration));
            }
        }
        else
        {
            try { SceneManager.LoadScene(sceneName); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                StartCoroutine(FadeInRoutine(defaultFadeInDuration));
            }
        }
    }

    private IEnumerator FadeInRoutine(float duration)
    {
        if (fadeImage == null)
        {
            isTransitioning = false;
            yield break;
        }

        Color c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;

        isTransitioning = false;
    }
}
