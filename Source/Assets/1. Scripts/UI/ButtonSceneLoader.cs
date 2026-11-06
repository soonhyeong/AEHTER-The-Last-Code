using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[ButtonSceneLoader] sceneName is empty.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[ButtonSceneLoader] targetSceneName is empty.", this);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}
