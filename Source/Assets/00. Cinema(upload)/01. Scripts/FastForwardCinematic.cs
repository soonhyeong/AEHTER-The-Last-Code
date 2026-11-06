using UnityEngine;
using UnityEngine.Playables;

public class FastForwardCinematic : MonoBehaviour
{
    public PlayableDirector director;
    public float fastSpeed = 3f;

    private bool isFastForward = false;

    void Start()
    {
        if (director == null)
        {
            director = FindAnyObjectByType<PlayableDirector>();
        }

        SetTimelineSpeed(1f);
    }

    public void ToggleFastForward()
    {
        isFastForward = !isFastForward;

        if (isFastForward)
        {
            SetTimelineSpeed(fastSpeed);
            Debug.Log("시네마틱 빨리감기");
        }
        else
        {
            SetTimelineSpeed(1f);
            Debug.Log("시네마틱 기본 속도");
        }
    }

    private void SetTimelineSpeed(float speed)
    {
        if (director == null)
        {
            Debug.LogWarning("PlayableDirector가 연결되지 않았습니다.");
            return;
        }

        if (director.playableGraph.IsValid())
        {
            director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }
}