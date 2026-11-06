using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonMove : MonoBehaviour
{
    public void MoveToScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}