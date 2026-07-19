using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("── Stats Reference ──")]
    public PlayerBaseStats stats;

    [Header("── Shield Regen ──")]
    [Tooltip("Thời gian chờ sau khi nhận damage để shield bắt đầu hồi phục")]
    public float shieldRegenDelay = 5f;
    [Tooltip("Tốc độ hồi shield mỗi giây (0 = hồi tức thì)")]
    public float shieldRegenRate = 0f;

    // Runtime values
    [HideInInspector] public float currentHp;
    [HideInInspector] public float currentShield;

    private Coroutine shieldRegenCoroutine;

    void Start()
    {
        currentHp = stats.FinalHp;
        currentShield = stats.FinalShield;
    }

    /// <summary>
    /// Nhận damage: shield hấp thụ trước, phần còn lại trừ vào HP.
    /// </summary>
    public void TakeDamage(float raw)
    {
        if (raw <= 0f) return;

        if (stats != null)
            raw *= 1f - stats.FinalArmorReduction;

        // Hủy coroutine hồi shield cũ nếu đang chạy
        if (shieldRegenCoroutine != null)
        {
            StopCoroutine(shieldRegenCoroutine);
            shieldRegenCoroutine = null;
        }

        // Shield hấp thụ trước
        if (currentShield > 0f)
        {
            float absorbed = Mathf.Min(currentShield, raw);
            currentShield -= absorbed;
            raw -= absorbed;
        }

        // Phần dư trừ vào HP
        if (raw > 0f)
        {
            currentHp -= raw;
        }

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            Die();
            return;
        }

        // Bắt đầu hồi shield sau delay
        shieldRegenCoroutine = StartCoroutine(ShieldRegenCoroutine());
    }

    /// <summary>
    /// Chờ shieldRegenDelay giây rồi hồi shield về max.
    /// Nếu shieldRegenRate > 0 thì hồi dần, ngược lại hồi tức thì.
    /// </summary>
    private IEnumerator ShieldRegenCoroutine()
    {
        yield return new WaitForSeconds(shieldRegenDelay);

        float maxShield = stats.FinalShield;

        if (shieldRegenRate <= 0f)
        {
            // Hồi tức thì
            currentShield = maxShield;
        }
        else
        {
            // Hồi dần
            while (currentShield < maxShield)
            {
                currentShield += shieldRegenRate * Time.deltaTime;
                currentShield = Mathf.Min(currentShield, maxShield);
                yield return null;
            }
        }

        shieldRegenCoroutine = null;
    }

    private void Die()
    {
        Debug.Log("Player died");
    }
}
