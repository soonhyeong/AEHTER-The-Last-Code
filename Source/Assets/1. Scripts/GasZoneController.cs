using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GasZoneController : MonoBehaviour
{
    [Header("가스 비주얼")]
    [SerializeField] private ParticleSystem gasParticle;

    [Header("가스 활성화")]
    [SerializeField] private bool gasActive = true;

    [Header("초당 데미지")]
    [SerializeField] private float damagePerSecond = 5f;

    [Header("테스트용 토글 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.G;

    [Header("위험 경고 사운드")]
    [SerializeField] private AudioSource warningAudioSource;

    [Tooltip("가스가 켜질 때 경고음을 한 번 재생할지")]
    [SerializeField] private bool playWarningOnceOnActivate = true;

    [Tooltip("가스가 켜져 있는 동안 경고음을 루프로 재생할지")]
    [SerializeField] private bool loopWarningWhileActive = false;

    [Header("기침 사운드")]
    [SerializeField] private AudioSource coughAudioSource;

    [Tooltip("플레이어가 가스 안에 있을 때 기침 재생 간격")]
    [SerializeField] private float coughInterval = 2.5f;

    private Collider gasTrigger;
    private float nextCoughTime = 0f;

    private void Awake()
    {
        gasTrigger = GetComponent<Collider>();

        if (gasTrigger != null)
            gasTrigger.isTrigger = true;
    }

    private void Start()
    {
        ApplyGasVisualState();
        ApplyWarningAudioState();
    }

    private void Update()
    {
        // 테스트용: G 키로 가스 ON/OFF
        if (Input.GetKeyDown(toggleKey))
        {
            SetGasActive(!gasActive);
        }
    }

    public void SetGasActive(bool active)
    {
        bool wasActive = gasActive;
        gasActive = active;

        ApplyGasVisualState();
        ApplyWarningAudioState(wasActive);
    }

    private void ApplyGasVisualState()
    {
        if (gasParticle == null) return;

        if (gasActive)
        {
            if (!gasParticle.isPlaying)
                gasParticle.Play();
        }
        else
        {
            if (gasParticle.isPlaying)
                gasParticle.Stop();
        }
    }

    private void ApplyWarningAudioState(bool previousState = false)
    {
        if (warningAudioSource == null) return;

        warningAudioSource.loop = loopWarningWhileActive;

        if (gasActive)
        {
            // 꺼져 있다가 켜진 순간
            if (!previousState)
            {
                if (playWarningOnceOnActivate)
                {
                    warningAudioSource.Play();
                }
                else if (loopWarningWhileActive && !warningAudioSource.isPlaying)
                {
                    warningAudioSource.Play();
                }
            }
            else
            {
                if (loopWarningWhileActive && !warningAudioSource.isPlaying)
                {
                    warningAudioSource.Play();
                }
            }
        }
        else
        {
            if (warningAudioSource.isPlaying)
                warningAudioSource.Stop();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!gasActive) return;
        if (!other.CompareTag("Player")) return;

        PlayerStatus playerStatus = other.GetComponent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = other.GetComponentInParent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = other.GetComponentInChildren<PlayerStatus>();

        if (playerStatus != null && !playerStatus.IsDead)
        {
            // 데미지
            playerStatus.TakeDamage(damagePerSecond * Time.deltaTime);

            // 기침 사운드
            TryPlayCoughSound();
        }
    }

    private void TryPlayCoughSound()
    {
        if (coughAudioSource == null) return;
        if (Time.time < nextCoughTime) return;

        coughAudioSource.Play();
        nextCoughTime = Time.time + coughInterval;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 가스 구역을 벗어나면 다음에 다시 들어왔을 때 바로 기침할 수 있게 초기화
        nextCoughTime = 0f;
    }
}