using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [Header("페이드 이미지")]
    [SerializeField] private Image fadeImage;

    [Header("기본 시간")]
    [SerializeField] private float defaultFadeOutTime = 1.0f;
    [SerializeField] private float defaultFadeInTime = 1.0f;

    private bool isFading = false;
    private bool localTransition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeImage == null)
        {
            Debug.LogError("[SceneFader] Fade Image가 연결되지 않았습니다.");
            return;
        }

        SetAlpha(0f);
    }

    public void FadeOutAndLoadScene(string sceneName)
    {
        FadeOutAndLoadScene(sceneName, defaultFadeOutTime, defaultFadeInTime);
    }

    public void FadeOutAndLoadScene(string sceneName, float fadeOutTime, float fadeInTime)
    {
        TryFadeOutAndLoadScene(sceneName, fadeOutTime, fadeInTime, null);
    }

    public bool TryFadeOutAndLoadScene(string sceneName, float fadeOutTime, float fadeInTime, Component owner)
    {
        if (isFading)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[SceneFader] sceneName이 비어 있습니다.");
            return false;
        }

        localTransition = PlayerStateSessionStore.IsLocalRoute(sceneName);
        if (localTransition && !PlayerStateSessionStore.TryReserveLocal(this, owner, sceneName))
        { localTransition = false; return false; }

        StartCoroutine(FadeOutLoadFadeInRoutine(sceneName, fadeOutTime, fadeInTime));
        return true;
    }

    private void OnDisable()
    {
        PlayerStateSessionStore.CancelLocal(this);
        StopAllCoroutines();
        localTransition = false;
        isFading = false;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        PlayerStateSessionStore.CancelLocal(this);
        if (Instance == this) Instance = null;
    }

    private IEnumerator FadeOutLoadFadeInRoutine(string sceneName, float fadeOutTime, float fadeInTime)
    {
        isFading = true;

        yield return StartCoroutine(Fade(0f, 1f, fadeOutTime));

        AsyncOperation loadOperation = null;
        if (localTransition)
        {
            if (!PlayerStateSessionStore.TryLoadLocal(this, null, target =>
                { loadOperation = SceneManager.LoadSceneAsync(target); if (loadOperation == null) throw new System.InvalidOperationException("Scene load did not start"); }))
            {
                PlayerStateSessionStore.CancelLocal(this);
                localTransition = false;
                yield return StartCoroutine(Fade(1f, 0f, fadeInTime));
                isFading = false;
                yield break;
            }
        }
        else
        {
            try { loadOperation = SceneManager.LoadSceneAsync(sceneName); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
        if (loadOperation == null)
        {
            PlayerStateSessionStore.CancelLocal(this);
            localTransition = false;
            yield return StartCoroutine(Fade(1f, 0f, fadeInTime));
            isFading = false;
            yield break;
        }
        float loadWait = 0f;
        while (!loadOperation.isDone)
        {
            loadWait += Time.unscaledDeltaTime;
            if (loadWait >= 120f)
            {
                // A committed async load cannot be cancelled. Keep its pending ticket intact,
                // but release the fade so the current scene is not permanently black.
                Debug.LogError("[SceneFader] Scene load did not complete within the wait limit.", this);
                SetAlpha(0f);
                isFading = false;
                localTransition = false;
                yield break;
            }
            yield return null;
        }

        yield return null;

        yield return StartCoroutine(Fade(1f, 0f, fadeInTime));

        isFading = false;
        localTransition = false;
    }

    public void FadeInOnly(float fadeInTime)
    {
        if (isFading)
            return;

        StartCoroutine(FadeInOnlyRoutine(fadeInTime));
    }

    private IEnumerator FadeInOnlyRoutine(float fadeInTime)
    {
        isFading = true;
        yield return StartCoroutine(Fade(1f, 0f, fadeInTime));
        isFading = false;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float time = 0f;

        SetAlpha(startAlpha);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(endAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}
