using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Base class cho tất cả weapon behaviours trong hệ thống auto-attack.
/// Mỗi loại vũ khí (Melee, Projectile, AoE, Orbital) kế thừa class này
/// và override Attack() để xử lý logic riêng.
/// </summary>
public abstract class WeaponBehaviour : MonoBehaviour
{
    [Header("── Weapon Data ──")]
    [Tooltip("ScriptableObject chứa stats của vũ khí.")]
    public WeaponData data;

    [Header("── Runtime ──")]
    [SerializeField] private int currentLevel = 1;
    public int CurrentLevel => currentLevel;

    protected float cooldownTimer;
    protected PlayerBaseStats playerStats;
    protected LayerMask enemyLayer;
    protected Transform playerTransform;
    public PlayerBaseStats PlayerStats => playerStats;

    // Cache danh sách enemy đã hit trong 1 lần attack (dùng cho melee/AoE)
    protected static readonly List<EnemyHealth> hitCache = new List<EnemyHealth>(32);

    /// <summary>
    /// Khởi tạo weapon behaviour. Gọi từ WeaponInventory khi add weapon.
    /// </summary>
    public virtual void Initialize(PlayerBaseStats stats, LayerMask enemyMask, Transform player)
    {
        playerStats = stats;
        enemyLayer = enemyMask;
        playerTransform = player;
        cooldownTimer = 0f;
    }

    // ═══════════════════════════════════════════
    //  COMPUTED STATS (base + level scaling + player bonuses)
    // ═══════════════════════════════════════════

    /// <summary>
    /// Damage cuối cùng: (weapon.atk + level * damagePerLevel) * (1 + playerBonusAtkPct)
    /// </summary>
    public float GetFinalDamage()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).damage;
    }

    /// <summary>
    /// Cooldown cuối cùng: weapon.cooldown - (level-1) * cooldownReductionPerLevel.
    /// Tối thiểu 0.05s.
    /// </summary>
    public float GetFinalCooldown()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).cooldown;
    }

    /// <summary>
    /// Size cuối cùng: weapon.size + (level-1) * sizePerLevel.
    /// </summary>
    public float GetFinalSize()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).size;
    }

    /// <summary>
    /// Số projectile cuối cùng: weapon.projectileCount + (level-1) * projCountPerLevel + playerBonus.
    /// </summary>
    public int GetFinalProjCount()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).projectileCount;
    }

    public float GetFinalProjectileSpeed()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).projectileSpeed;
    }


    /// <summary>
    /// Pierce count: weapon.pierce (không scale theo level hiện tại).
    /// </summary>
    public int GetFinalPierce()
    {
        return data.pierce;
    }

    public float GetFinalDuration()
    {
        return data.duration * (playerStats != null ? playerStats.FinalDurationMultiplier : 1f);
    }

    public float GetFinalKnockback()
    {
        return data.GetStatsAtLevel(currentLevel, playerStats).knockback;
    }

    public float GetFinalCritChance()
    {
        float playerCrit = playerStats != null ? playerStats.FinalCriticalChance : 0f;
        return Mathf.Clamp01(data.crit + playerCrit);
    }

    // ═══════════════════════════════════════════
    //  AUTO-ATTACK LOOP
    // ═══════════════════════════════════════════

    protected virtual void Update()
    {
        if (data == null) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            Attack();
            cooldownTimer = GetFinalCooldown();
        }
    }

    /// <summary>
    /// Logic tấn công cụ thể — override trong từng weapon type.
    /// </summary>
    public abstract void Attack();

    /// <summary>
    /// Level up vũ khí. Trả về false nếu đã max level.
    /// </summary>
    public virtual bool LevelUp()
    {
        if (currentLevel >= data.maxLevel) return false;
        currentLevel++;
        OnLevelUp();
        return true;
    }

    /// <summary>
    /// Hook cho subclass xử lý thêm khi level up (thay đổi visual, v.v.)
    /// </summary>
    protected virtual void OnLevelUp() { }

    /// <summary>
    /// Check xem weapon đã max level chưa.
    /// </summary>
    public bool IsMaxLevel => currentLevel >= data.maxLevel;

    // ═══════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════

    /// <summary>
    /// Tìm enemy gần nhất trong range. Trả về null nếu không có.
    /// </summary>
    protected Transform FindClosestEnemy(float range)
    {
        Collider[] hits = Physics.OverlapSphere(playerTransform.position, range, enemyLayer);
        if (hits.Length == 0) return null;

        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            float dist = Vector3.SqrMagnitude(col.transform.position - playerTransform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = col.transform;
            }
        }

        return closest;
    }

    /// <summary>
    /// Áp dụng knockback lên enemy.
    /// </summary>
    protected void ApplyKnockback(Transform enemy, float force)
    {
        if (force <= 0f) return;

        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 dir = (enemy.position - playerTransform.position).normalized;
            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Check crit và trả về damage (đã nhân 2 nếu crit).
    /// </summary>
    protected float RollCritDamage(float baseDmg)
    {
        if (Random.value < GetFinalCritChance())
        {
            float multiplier = playerStats != null ? playerStats.FinalCriticalDamageMultiplier : 2f;
            return baseDmg * multiplier;
        }
        return baseDmg;
    }
}
