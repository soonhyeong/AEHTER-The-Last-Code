using UnityEngine;
using UnityEngine.UI;

public class PlayerStatus : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("Thirst")]
    public float maxThirst = 100f;
    public float currentThirst = 100f;
    [SerializeField] private bool drainThirstOverTime = true;
    [SerializeField, Min(0f)] private float thirstDrainPerSecond = 0.2f;

    [Header("Shield")]
    public bool useShield = false;          // 윤서만 true
    public float maxShield = 100f;
    public float currentShield = 100f;

    [Header("HP UI")]
    public Slider hpSlider;
    public Image hpFillImage;

    [Header("Shield UI")]
    public Slider shieldSlider;
    public Image shieldFillImage;

    private void Start()
    {
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        currentThirst = Mathf.Clamp(currentThirst, 0f, maxThirst);

        if (useShield)
            currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        else
            currentShield = 0f;

        UpdateUI();
    }

    private void Update()
    {
        if (!drainThirstOverTime || thirstDrainPerSecond <= 0f || currentThirst <= 0f)
            return;

        currentThirst = Mathf.Max(0f, currentThirst - thirstDrainPerSecond * Time.deltaTime);
    }

    public bool IsDead => currentHP <= 0f;

    public void ChangeHP(float amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        UpdateUI();
    }

    public void ChangeShield(float amount)
    {
        if (!useShield) return;

        currentShield += amount;
        currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        UpdateUI();
    }

    public void ChangeThirst(float amount)
    {
        currentThirst = Mathf.Clamp(currentThirst + amount, 0f, maxThirst);
        UpdateUI();
    }

    public void SetThirst(float value)
    {
        currentThirst = Mathf.Clamp(value, 0f, maxThirst);
        UpdateUI();
    }

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f) return;

        if (useShield && currentShield > 0f)
        {
            if (currentShield >= damage)
            {
                currentShield -= damage;
                damage = 0f;
            }
            else
            {
                damage -= currentShield;
                currentShield = 0f;
            }
        }

        if (damage > 0f)
        {
            currentHP -= damage;
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        }

        UpdateUI();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        ApplyDamage(damageInfo.amount);
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(damage);
    }

    public void SetHP(float value)
    {
        currentHP = Mathf.Clamp(value, 0f, maxHP);
        UpdateUI();
    }

    public void SetShield(float value)
    {
        if (!useShield) return;

        currentShield = Mathf.Clamp(value, 0f, maxShield);
        UpdateUI();
    }

    public bool RestoreShield(float amount)
    {
        if (!useShield) return false;
        if (amount <= 0f) return false;
        if (currentShield >= maxShield) return false;

        currentShield += amount;
        currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool TryBuyShieldCharge(out string message)
    {
        message = "";

        if (!useShield)
        {
            message = "쉴드를 사용할 수 없습니다.";
            return false;
        }

        if (currentShield >= 100f)
        {
            message = "쉴드가 가득 찼습니다.";
            return false;
        }

        currentShield = Mathf.Clamp(100f, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool UseShieldPack(out string message)
    {
        message = "";

        if (!useShield)
        {
            message = "쉴드를 사용할 수 없습니다.";
            return false;
        }

        currentShield = Mathf.Clamp(100f, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool ApplyItem(ItemData item, out string message)
    {
        message = "";

        if (item == null || item.effects == null || item.effects.Length == 0)
        {
            message = "사용할 수 없는 아이템입니다.";
            return false;
        }

        foreach (var effect in item.effects)
        {
            if (effect == null)
            {
                message = "아이템 효과 데이터가 올바르지 않습니다.";
                return false;
            }
        }

        bool applied = false;

        foreach (var effect in item.effects)
        {
            switch (effect.type)
            {
                case EffectType.Health:
                    ChangeHP(effect.value);
                    applied = true;
                    break;

                case EffectType.Thirst:
                    ChangeThirst(effect.value);
                    applied = true;
                    break;

                case EffectType.CustomText:
                    // 필요 시 customText 출력 처리
                    applied = true;
                    break;

                case EffectType.ShieldPack:
                {
                    string shieldMessage;
                    if (UseShieldPack(out shieldMessage))
                    {
                        applied = true;
                    }
                    else if (string.IsNullOrEmpty(message))
                    {
                        message = shieldMessage;
                    }
                    break;
                }

                case EffectType.EnergyPack:
                    // EnergyPack은 Hankyul_Fire에서 처리
                    break;
            }
        }

        if (!applied && string.IsNullOrEmpty(message))
            message = "아이템을 사용할 수 없습니다.";

        return applied;
    }

    public void ApplyItem(ItemData item)
    {
        string _;
        ApplyItem(item, out _);
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
            float hpRatio = maxHP > 0f ? currentHP / maxHP : 0f;
            hpFillImage.fillAmount = hpRatio;
        }

        if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(useShield);
            shieldSlider.maxValue = maxShield;
            shieldSlider.value = currentShield;
        }

        if (shieldFillImage != null)
        {
            shieldFillImage.gameObject.SetActive(useShield);
            float shieldRatio = maxShield > 0f ? currentShield / maxShield : 0f;
            shieldFillImage.fillAmount = shieldRatio;
        }
    }
}
