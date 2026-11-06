using UnityEngine;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance;

    [Header("Manager References")]
    public GameManager gameManager;
    public AudioManager audioManager;
    public CurrencyManager currencyManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (gameManager == null)
                gameManager = GetComponent<GameManager>();

            if (audioManager == null)
                audioManager = GetComponent<AudioManager>();

            if (currencyManager == null)
                currencyManager = GetComponent<CurrencyManager>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 전역 접근 프로퍼티
    public static GameManager Game => Instance.gameManager;
    public static AudioManager Audio => Instance.audioManager;
    public static CurrencyManager Currency => Instance.currencyManager;
}