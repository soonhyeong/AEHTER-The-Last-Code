using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    // Compatibility access only: GlobalManager remains the lifecycle owner.
    public static AudioManager Instance
    {
        get
        {
            var globalManager = GlobalManager.Instance;
            return globalManager != null ? globalManager.audioManager : null;
        }
    }

    [Header("오디오 소스")]
    public AudioSource bgmSource;    // 배경음
    public AudioSource sfxSource;    // 효과음

    [Header("사운드 목록 (키 = clip)")]
    public List<SoundData> soundList = new List<SoundData>();
    private Dictionary<string, AudioClip> clipDict;

    [Header("메인 메뉴 씬 이름")]
    public string mainMenuSceneName = "MainMenu"; // 프로젝트에 맞게 변경

    private void Awake()
    {
        BuildSoundDictionary();
    }

    private void BuildSoundDictionary()
    {
        clipDict = new Dictionary<string, AudioClip>();
        if (soundList == null)
            return;

        // 리스트 → 딕셔너리로 변환
        foreach (var s in soundList)
        {
            if (s == null || string.IsNullOrEmpty(s.key) || s.clip == null)
                continue;

            if (!clipDict.ContainsKey(s.key))
                clipDict.Add(s.key, s.clip);
        }
    }

    private bool TryGetClip(string key, out AudioClip clip)
    {
        clip = null;
        if (clipDict == null)
            BuildSoundDictionary();

        return !string.IsNullOrEmpty(key)
            && clipDict.TryGetValue(key, out clip)
            && clip != null;
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    // 씬이 바뀔 때 호출됨
    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // 새로 활성화된 씬이 메인 메뉴가 아니라면 BGM 정지
        if (newScene.name != mainMenuSceneName)
        {
            if (bgmSource != null && bgmSource.isPlaying)
                bgmSource.Stop();
        }
    }

    // ----------------------------------------------------
    // BGM 재생
    // ----------------------------------------------------
    public void PlayBGM(string key)
    {
        PlayBGM(key, 1f);
    }

    public void PlayBGM(string key, float volume = 1f)
    {
        if (!TryGetClip(key, out AudioClip clip))
        {
            Debug.LogWarning("BGM key not found: " + key);
            return;
        }

        if (bgmSource == null)
            return;

        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    // ----------------------------------------------------
    // SFX 재생 (키 기반)
    // ----------------------------------------------------
    public void PlaySFX(string key)
    {
        PlaySFX(key, 1f);
    }

    public void PlaySFX(string key, float volume = 1f)
    {
        if (!TryGetClip(key, out AudioClip clip))
        {
            Debug.LogWarning("SFX key not found: " + key);
            return;
        }

        PlaySFX(clip, volume);
    }

    // ----------------------------------------------------
    // SFX 재생 (직접 클립 전달)
    // ----------------------------------------------------
    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip, volume);
    }
}

[System.Serializable]
public class SoundData
{
    public string key;
    public AudioClip clip;
}
