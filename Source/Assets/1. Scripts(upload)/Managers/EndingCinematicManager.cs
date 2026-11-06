using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// EndingType별 엔딩 시네마틱 재생 담당.
///
/// 두 가지 재생 방식을 모두 지원한다:
/// 1) Timeline  : entry.director(PlayableDirector)가 연결되어 있으면 그것을 재생한다. (우선)
/// 2) 씬 로드   : director가 없고 entry.sceneName이 있으면 해당 씬을 로드한다.
///                (기존 Ending_O.unity + VideoPlayer 방식과 호환)
///
/// 재생 전 처리: 플레이어 조작 잠금 → UI 숨김 → BGM 정리 → 오브젝트 활성화 → 재생
/// </summary>
public class EndingCinematicManager : MonoBehaviour
{
    [Serializable]
    public class EndingCinematicEntry
    {
        public EndingType endingType;

        [Header("재생 방식 1 - Timeline (우선)")]
        [Tooltip("연결되면 이 Timeline을 재생한다. 완료 이벤트를 받으려면 Timeline의 Wrap Mode를 None으로 둘 것.")]
        public PlayableDirector director;

        [Header("재생 방식 2 - 씬 로드 (director가 없을 때)")]
        [Tooltip("로드할 씬 이름. Build Settings > Scenes In Build에 등록되어 있어야 한다.")]
        public string sceneName;

        [Header("사운드 (AudioManager 문자열 key)")]
        public bool stopBgmBeforePlay = true;
        [Tooltip("재생 시 바꿀 BGM key. 비우면 BGM을 바꾸지 않는다.")]
        public string bgmKey = "";

        [Header("연출용 오브젝트 (선택)")]
        public GameObject[] objectsToActivate;
        public GameObject[] objectsToDeactivate;
    }

    [Header("EndingType별 시네마틱 (4종)")]
    public List<EndingCinematicEntry> entries = new List<EndingCinematicEntry>();

    [Header("공통 처리")]
    [Tooltip("재생 시 숨길 UI (HUD, 크로스헤어, 선택 캔버스 등)")]
    public GameObject[] uiToHide;

    [Tooltip("조작을 잠글 플레이어 오브젝트. 비우면 \"Player\" 태그로 탐색한다.")]
    public GameObject playerRoot;

    public bool lockPlayerControl = true;
    public bool controlCursor = true;

    public bool IsPlaying { get; private set; }
    public bool HasStarted { get; private set; }
    public EndingType CurrentEndingType { get; private set; }

    public event Action<EndingType> OnCinematicStarted;
    public event Action<EndingType> OnCinematicFinished;

    private readonly PlayerControlLock controlLock = new PlayerControlLock();
    private PlayableDirector activeDirector;

    private void OnDestroy()
    {
        UnsubscribeDirector();
    }

    /// <summary>
    /// 결정된 EndingType의 시네마틱을 재생한다.
    /// EndingDecisionManager가 호출한다.
    /// </summary>
    public void PlayEnding(EndingType type)
    {
        if (IsPlaying)
            return;

        EndingCinematicEntry entry = FindEntry(type);
        if (entry == null)
        {
            Debug.LogError($"[EndingCinematicManager] {type}에 대한 항목이 entries에 없습니다.", this);
            return;
        }

        CurrentEndingType = type;
        IsPlaying = true;
        HasStarted = true;

        if (lockPlayerControl)
            controlLock.Lock(playerRoot);

        if (controlCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HideUI();
        ApplyAudio(entry);
        ApplyObjects(entry);

        if (OnCinematicStarted != null)
            OnCinematicStarted(type);

        if (entry.director != null)
        {
            PlayDirector(entry.director);
            return;
        }

        if (!string.IsNullOrEmpty(entry.sceneName))
        {
            LoadEndingScene(entry.sceneName);
            return;
        }

        IsPlaying = false;
        Debug.LogError(
            $"[EndingCinematicManager] {type} 항목에 director와 sceneName이 모두 비어 있습니다.", this);
    }

    public EndingCinematicEntry FindEntry(EndingType type)
    {
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].endingType == type)
                return entries[i];
        }

        return null;
    }

    private void PlayDirector(PlayableDirector director)
    {
        UnsubscribeDirector();

        activeDirector = director;
        activeDirector.stopped += HandleDirectorStopped;

        if (!activeDirector.gameObject.activeSelf)
            activeDirector.gameObject.SetActive(true);

        activeDirector.time = 0.0;
        activeDirector.Play();
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (director != activeDirector)
            return;

        UnsubscribeDirector();
        IsPlaying = false;

        if (OnCinematicFinished != null)
            OnCinematicFinished(CurrentEndingType);
    }

    private void UnsubscribeDirector()
    {
        if (activeDirector == null)
            return;

        activeDirector.stopped -= HandleDirectorStopped;
        activeDirector = null;
    }

    private void LoadEndingScene(string sceneName)
    {
        // Build Settings에 등록되지 않은 씬은 로드할 수 없다.
        // 코드로 강제 등록하지 않고, 명확한 에러 로그만 남긴다.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            IsPlaying = false;
            Debug.LogError(
                $"[EndingCinematicManager] 씬 '{sceneName}'을 로드할 수 없습니다. " +
                "File > Build Profiles(Build Settings) > Scene List에 등록되어 있는지 확인하세요.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void HideUI()
    {
        if (uiToHide == null)
            return;

        for (int i = 0; i < uiToHide.Length; i++)
        {
            if (uiToHide[i] != null)
                uiToHide[i].SetActive(false);
        }
    }

    private void ApplyObjects(EndingCinematicEntry entry)
    {
        if (entry.objectsToDeactivate != null)
        {
            for (int i = 0; i < entry.objectsToDeactivate.Length; i++)
            {
                if (entry.objectsToDeactivate[i] != null)
                    entry.objectsToDeactivate[i].SetActive(false);
            }
        }

        if (entry.objectsToActivate != null)
        {
            for (int i = 0; i < entry.objectsToActivate.Length; i++)
            {
                if (entry.objectsToActivate[i] != null)
                    entry.objectsToActivate[i].SetActive(true);
            }
        }
    }

    // GlobalManager.Audio는 Instance가 null이면 NRE가 나므로 Instance까지 확인한다.
    private static void ApplyAudio(EndingCinematicEntry entry)
    {
        if (GlobalManager.Instance == null || GlobalManager.Audio == null)
            return;

        if (entry.stopBgmBeforePlay)
            GlobalManager.Audio.StopBGM();

        if (!string.IsNullOrEmpty(entry.bgmKey))
            GlobalManager.Audio.PlayBGM(entry.bgmKey);
    }
}
