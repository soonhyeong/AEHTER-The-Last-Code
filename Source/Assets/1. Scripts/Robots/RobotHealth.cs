using System;
using UnityEngine;
using UnityEngine.UI;

public class RobotHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP;
    [SerializeField] private float deathDespawnDelay = 2f;

    [Header("HP UI")]
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFillImage;

    private bool isDead;

    public event Action<RobotHealth> Died;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsDead => isDead;
    public float DeathDespawnDelay => deathDespawnDelay;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = maxHP;
        deathDespawnDelay = Mathf.Max(0f, deathDespawnDelay);
        isDead = false;

        SetHpBarVisible(hpBarRoot != null);
        UpdateUI();
    }

    public void Configure(int maxHpValue, float despawnDelay)
    {
        maxHP = Mathf.Max(1, maxHpValue);
        deathDespawnDelay = Mathf.Max(0f, despawnDelay);
        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 0, maxHP);
        UpdateUI();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (isDead) return;

        int damage = Mathf.Max(0, Mathf.RoundToInt(damageInfo.amount));
        if (damage <= 0) return;

        currentHP = Mathf.Max(0, currentHP - damage);
        UpdateUI();

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHP = 0;
        UpdateUI();
        SetHpBarVisible(false);

        EnemyReward reward = GetComponent<EnemyReward>();
        if (reward != null)
        {
            reward.GiveReward();
        }

        Died?.Invoke(this);
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }

        if (hpFillImage != null)
        {
            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            hpFillImage.fillAmount = ratio;
            hpFillImage.color = Color.Lerp(
                new Color(0.85f, 0.15f, 0.15f),
                new Color(0.2f, 0.9f, 0.2f),
                ratio
            );
        }
    }

    private void SetHpBarVisible(bool visible)
    {
        if (hpBarRoot != null)
            hpBarRoot.SetActive(visible);
    }
}