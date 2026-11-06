using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class LoadingSceneController : MonoBehaviour
{
    [Header("필수 참조")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Text loadingText;

    [Header("시간 설정")]
    [SerializeField] private float minimumLoadingTime = 10f;

    [Header("예외 처리")]
    [SerializeField] private string fallbackSceneName = "MainScene";

    private AsyncOperation asyncLoadOperation;
    private bool isVideoFinished = false;
    private bool isSceneReady = false;
    private float elapsedTime = 0f;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning("[LoadingSceneController] VideoPlayer가 없어 영상 없이 로드합니다.");
            isVideoFinished = true;
            return;
        }

        if (videoRawImage != null && renderTexture != null)
        {
            videoRawImage.texture = renderTexture;
        }

        if (videoPlayer.targetTexture != renderTexture)
        {
            videoPlayer.targetTexture = renderTexture;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
    }

    private IEnumerator Start()
    {
        string targetSceneName = LoadingSceneData.TargetSceneName;

        if (!CanLoad(targetSceneName))
        {
            Debug.LogWarning("[LoadingSceneController] 목표 씬을 로드할 수 없어 fallback을 확인합니다.");
            targetSceneName = fallbackSceneName;
        }

        if (!CanLoad(targetSceneName) || !TryBeginLoad(targetSceneName))
        {
            if (loadingText != null) loadingText.text = "LOAD FAILED";
            Debug.LogError("[LoadingSceneController] 유효한 목표 씬 로드를 시작하지 못했습니다.");
            yield break;
        }

        if (loadingText != null)
        {
            loadingText.text = "LOADING...";
        }

        asyncLoadOperation.allowSceneActivation = false;

        if (videoPlayer != null)
        {
            videoPlayer.Prepare();
            float prepareTime = 0f;
            while (!videoPlayer.isPrepared && !isVideoFinished && prepareTime < 30f)
            {
                prepareTime += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!isVideoFinished && videoPlayer.isPrepared) videoPlayer.Play();
            else if (!isVideoFinished)
            {
                Debug.LogWarning("[LoadingSceneController] 영상 준비 제한 시간을 초과해 영상을 생략합니다.");
                isVideoFinished = true;
            }
        }
        else isVideoFinished = true;

        float minimumWait = float.IsNaN(minimumLoadingTime) || float.IsInfinity(minimumLoadingTime)
            ? 0f : Mathf.Max(0f, minimumLoadingTime);
        double videoLength = videoPlayer != null ? videoPlayer.length : 0d;
        float videoWaitLimit = double.IsNaN(videoLength) || double.IsInfinity(videoLength)
            ? 120f : (float)System.Math.Min(float.MaxValue, System.Math.Max(120d, videoLength + 30d));

        while (!isVideoFinished || !isSceneReady || elapsedTime < minimumWait)
        {
            elapsedTime += Time.unscaledDeltaTime;
            if (asyncLoadOperation.progress >= 0.9f) isSceneReady = true;

            if (!isVideoFinished && elapsedTime >= videoWaitLimit)
            {
                isVideoFinished = true;
                Debug.LogWarning("[LoadingSceneController] 영상 종료를 기다리는 제한 시간을 초과했습니다.");
            }
            if (!isSceneReady && elapsedTime >= 120f)
            {
                // Unity async loads cannot be cancelled; never block its global load queue.
                asyncLoadOperation.allowSceneActivation = true;
                if (loadingText != null) loadingText.text = "LOAD FAILED";
                Debug.LogError("[LoadingSceneController] Scene 준비 제한 시간을 초과했습니다.");
                yield break;
            }

            if (asyncLoadOperation != null)
            {
                if (asyncLoadOperation.progress >= 0.9f)
                {
                    isSceneReady = true;
                }

                if (loadingText != null)
                {
                    float displayProgress = Mathf.Clamp01(asyncLoadOperation.progress / 0.9f);
                    int percent = Mathf.RoundToInt(displayProgress * 100f);

                    if (!isSceneReady)
                    {
                        loadingText.text = "LOADING... " + percent + "%";
                    }
                    else
                    {
                        loadingText.text = "LOADING COMPLETE";
                    }
                }
            }

            yield return null;
        }

        asyncLoadOperation.allowSceneActivation = true;
    }

    private bool CanLoad(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName)
            && sceneName != gameObject.scene.name
            && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private bool TryBeginLoad(string sceneName)
    {
        try { asyncLoadOperation = SceneManager.LoadSceneAsync(sceneName); }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
        return asyncLoadOperation != null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (asyncLoadOperation != null) asyncLoadOperation.allowSceneActivation = true;
        if (videoPlayer != null) videoPlayer.Stop();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        isVideoFinished = true;
        Debug.Log("[LoadingSceneController] 영상 재생 완료");
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("[LoadingSceneController] VideoPlayer 에러: " + message);
        isVideoFinished = true;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}
