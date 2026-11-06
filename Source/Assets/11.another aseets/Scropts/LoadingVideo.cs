using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class LoadingVideo : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("파일 설정")]
    [SerializeField] private string videoFileName = "loading.mp4";

    [Header("재생 설정")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = false;

    private string videoPath;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (videoPlayer == null)
        {
            Debug.LogError("[LoadingVideo] VideoPlayer가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        videoPath = Path.Combine(Application.streamingAssetsPath, videoFileName);

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoPath;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loop;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;

        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.errorReceived += OnErrorReceived;
        videoPlayer.loopPointReached += OnLoopPointReached;
    }

    private void Start()
    {
        Debug.Log("[LoadingVideo] StreamingAssets 경로: " + Application.streamingAssetsPath);
        Debug.Log("[LoadingVideo] 영상 경로: " + videoPath);

        if (!File.Exists(videoPath))
        {
            Debug.LogError("[LoadingVideo] 영상 파일이 없습니다: " + videoPath);
            return;
        }

        FileInfo fileInfo = new FileInfo(videoPath);
        Debug.Log("[LoadingVideo] 파일 크기(byte): " + fileInfo.Length);

        if (fileInfo.Length <= 0)
        {
            Debug.LogError("[LoadingVideo] 영상 파일이 비어 있습니다(0바이트): " + videoPath);
            return;
        }

        if (playOnStart)
        {
            videoPlayer.Prepare();
            Debug.Log("[LoadingVideo] Prepare 시작");
        }
    }

    private void OnPrepareCompleted(VideoPlayer vp)
    {
        Debug.Log("[LoadingVideo] Prepare 완료");
        vp.Play();
        Debug.Log("[LoadingVideo] 영상 재생 시작");
    }

    private void OnErrorReceived(VideoPlayer vp, string message)
    {
        Debug.LogError("[LoadingVideo] VideoPlayer 에러: " + message);
        Debug.LogError("[LoadingVideo] 현재 URL: " + vp.url);
    }

    private void OnLoopPointReached(VideoPlayer vp)
    {
        Debug.Log("[LoadingVideo] 영상 재생 종료");
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.errorReceived -= OnErrorReceived;
            videoPlayer.loopPointReached -= OnLoopPointReached;
        }
    }
}