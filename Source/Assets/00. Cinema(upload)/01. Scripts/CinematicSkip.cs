using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

public class CinematicSkip : MonoBehaviour
{
    public PlayableDirector director;

    public void SkipToNext()
    {
        if (director != null)
        {
            director.time = director.duration;
            director.Evaluate();
            director.Stop();
        }

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentIndex + 1);
    }
}