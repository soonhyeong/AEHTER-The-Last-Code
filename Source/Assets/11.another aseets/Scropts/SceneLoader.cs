using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("Next scene name (must be in Build Profiles)")]
    public string nextSceneName = "GameScene";

    [Header("Minimum loading screen time (seconds)")]
    public float minLoadingTime = 3f;

    void Start()
    {
        StartCoroutine(LoadGameScene());
    }

    IEnumerator LoadGameScene()
    {
        UnityEngine.AsyncOperation op =
            SceneManager.LoadSceneAsync(nextSceneName);

        if (op == null)
        {
            UnityEngine.Debug.LogError(
                $"SceneLoader: '{nextSceneName}' 씬을 로드할 수 없습니다. " +
                "Build Profiles에 씬이 추가되어 있는지 확인하세요."
            );
            yield break;
        }

        op.allowSceneActivation = false;

        float t = 0f;
        while (t < minLoadingTime)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
    }
}
