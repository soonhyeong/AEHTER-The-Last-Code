using UnityEngine;

public class FlameVent : MonoBehaviour
{
    [Header("오브젝트 연결")]
    public GameObject warningZone;          // 필요 없으면 비워도 됨
    public Collider flameDamageTrigger;     // 데미지 판정
    public ParticleSystem flameParticle;    // 화염 파티클

    [Header("오디오")]
    public AudioSource audioSource;
    public AudioClip flameLoopClip;

    [Header("상태")]
    public bool isFlaming = true;

    private void Start()
    {
        StartFlame();
    }

    public void StartFlame()
    {
        isFlaming = true;

        if (warningZone != null)
            warningZone.SetActive(false);

        if (flameDamageTrigger != null)
            flameDamageTrigger.enabled = true;

        if (flameParticle != null)
        {
            if (!flameParticle.isPlaying)
                flameParticle.Play();
        }

        if (audioSource != null && flameLoopClip != null)
        {
            audioSource.clip = flameLoopClip;
            audioSource.loop = true;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }

    public void StopFlame()
    {
        isFlaming = false;

        if (flameDamageTrigger != null)
            flameDamageTrigger.enabled = false;

        if (flameParticle != null)
            flameParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}