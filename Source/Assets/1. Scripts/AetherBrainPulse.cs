using UnityEngine;

public class AetherBrainPulse : MonoBehaviour
{
    [Header("크기 맥동")]
    public Vector3 baseScale = Vector3.one;
    public float scaleAmplitude = 0.12f;
    public float pulseSpeed = 2f;

    [Header("말하는 리듬")]
    public bool playPulse = false;
    public bool useFakeSpeechPulse = true;

    public float sentenceSpeed = 0.65f;
    public float syllableSpeed = 10f;
    public float pauseThreshold = 0.38f;
    public float idlePulse = 0.2f;
    public float speechPower = 1.25f;

    [Header("빛 맥동")]
    public Renderer targetRenderer;
    public Color baseEmissionColor = new Color(0.2f, 0.8f, 1f);
    public float minEmissionIntensity = 1.5f;
    public float maxEmissionIntensity = 4f;

    [Header("보조 조명")]
    public Light pointLight;
    public float minLightIntensity = 1f;
    public float maxLightIntensity = 3f;

    [Header("사운드")]
    public AudioSource heartbeatAudio;
    public float minVolume = 0.15f;
    public float maxVolume = 0.6f;
    public float minPitch = 0.9f;
    public float maxPitch = 1.15f;

    private Material runtimeMaterial;
    private float smoothPulse;
    private bool initialized;

    void Start()
    {
        Initialize();

        if (playPulse)
            StartPulse();
        else
            StopPulse();
    }

    private void Initialize()
    {
        // Timeline signals can arrive before Start. Capture the base state once.
        if (initialized)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        baseScale = transform.localScale;

        if (targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.material;
            runtimeMaterial.EnableKeyword("_EMISSION");
        }

        if (heartbeatAudio != null)
        {
            heartbeatAudio.loop = true;
            heartbeatAudio.playOnAwake = false;
        }

        initialized = true;
    }

    void Update()
    {
        if (!playPulse)
        {
            smoothPulse = 0f;
            ApplyIdleState();
            return;
        }

        // Preserve the current sine waveform; smooth only the Cinema speech mode.
        smoothPulse = useFakeSpeechPulse
            ? Mathf.Lerp(smoothPulse, GetFakeSpeechPulse(), Time.deltaTime * 8f)
            : (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        ApplyPulse(smoothPulse);
    }

    private float GetFakeSpeechPulse()
    {
        float t = Time.time;
        float sentence = Mathf.PerlinNoise(t * sentenceSpeed, 0f);
        float syllable = Mathf.Abs(Mathf.Sin(t * syllableSpeed));
        float pause = sentence > pauseThreshold ? 1f : 0.15f;
        float speech = sentence * syllable * pause;

        speech = Mathf.Lerp(idlePulse, 1f, speech);
        return Mathf.Clamp01(speech * speechPower);
    }

    private void ApplyPulse(float pulse)
    {
        float scaleOffset = Mathf.Lerp(-scaleAmplitude, scaleAmplitude, pulse);
        transform.localScale = baseScale + Vector3.one * scaleOffset;

        if (runtimeMaterial != null)
        {
            float emissionIntensity = Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, pulse);
            Color finalEmission = baseEmissionColor * emissionIntensity;
            runtimeMaterial.SetColor("_EmissionColor", finalEmission);
        }

        if (pointLight != null)
        {
            pointLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, pulse);
        }

        if (heartbeatAudio != null)
        {
            heartbeatAudio.volume = Mathf.Lerp(minVolume, maxVolume, pulse);
            heartbeatAudio.pitch = Mathf.Lerp(minPitch, maxPitch, pulse);
        }
    }

    public void StartPulse()
    {
        Initialize();
        playPulse = true;

        if (heartbeatAudio != null && !heartbeatAudio.isPlaying)
            heartbeatAudio.Play();
    }

    public void StopPulse()
    {
        Initialize();
        playPulse = false;
        smoothPulse = 0f;

        if (heartbeatAudio != null)
            heartbeatAudio.Stop();

        ApplyIdleState();
    }

    private void ApplyIdleState()
    {
        transform.localScale = baseScale;

        if (runtimeMaterial != null)
            runtimeMaterial.SetColor("_EmissionColor", baseEmissionColor * minEmissionIntensity);

        if (pointLight != null)
            pointLight.intensity = minLightIntensity;

        if (heartbeatAudio != null)
        {
            if (heartbeatAudio.isPlaying)
                heartbeatAudio.Stop();

            heartbeatAudio.volume = minVolume;
            heartbeatAudio.pitch = minPitch;
        }
    }
}
