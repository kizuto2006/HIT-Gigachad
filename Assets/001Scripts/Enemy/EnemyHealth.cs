using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("── Data Reference ──")]
    public EnemyData data;

    // Runtime
    [HideInInspector] public float currentHp;

    void Awake()
    {
        if (data == null)
        {
            Debug.LogError($"[EnemyHealth] EnemyData chưa được gán trên {gameObject.name}!");
            return;
        }

        ApplySizeMultiplier();
    }

    /// <summary>
    /// Áp dụng scale và HP multiplier dựa trên EnemySize.
    /// </summary>
    private void ApplySizeMultiplier()
    {
        float scaleMul;
        float hpMul;

        switch (data.size)
        {
            case EnemySize.Small:
                scaleMul = 0.7f;
                hpMul = 0.6f;
                break;
            case EnemySize.Large:
                scaleMul = 1.5f;
                hpMul = 2f;
                break;
            case EnemySize.Medium:
            default:
                scaleMul = 1f;
                hpMul = 1f;
                break;
        }

        transform.localScale *= scaleMul;
        currentHp = data.hp * hpMul;
    }

    /// <summary>
    /// Nhận damage sau khi trừ armor. isEliteDmg để dành cho hệ thống sau.
    /// Công thức damage đầu vào (tính ở nơi gọi): raw = weaponBaseDmg * (1 + bonusAtkPct)
    /// </summary>
    public void TakeDamage(float raw, bool isEliteDmg = false)
    {
        float finalDmg = Mathf.Max(0f, raw - data.armor);
        currentHp -= finalDmg;

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died");

        // Tìm EnemySpawn và return về pool thay vì SetActive(false) trực tiếp
        EnemySpawn spawner = FindAnyObjectByType<EnemySpawn>();
        if (spawner != null)
            spawner.ReturnEnemyToPool(gameObject);
        else
            gameObject.SetActive(false); // fallback nếu không tìm thấy EnemySpawn
    }
}
