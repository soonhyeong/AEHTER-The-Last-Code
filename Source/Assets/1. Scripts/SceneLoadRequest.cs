using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadRequest : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string targetSceneName = "NextScene";

    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(loadingSceneName))
        {
            Debug.LogError("[SceneLoadRequest] 로딩 씬 이름이 비어 있습니다.");
            return;
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[SceneLoadRequest] 목표 씬 이름이 비어 있습니다.");
            return;
        }

        LoadingSceneData.TargetSceneName = targetSceneName;
        SceneManager.LoadScene(loadingSceneName);
    }
}