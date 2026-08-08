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
    private float rarityBaseMultiplier = 1f;
    private float rarityDamageBonus;
    private float rarityCriticalChanceBonus;
    private float rarityCooldownReduction;
    private float raritySizeBonus;
    private float rarityProjectileSpeedBonus;
    private float rarityKnockbackBonus;
    private int rarityProjectileCountBonus;

    public int CurrentLevel => currentLevel;

    protected float cooldownTimer;
    private bool attackInProgress;
    private bool cooldownDeferred;
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
        attackInProgress = false;
        cooldownDeferred = false;
    }

    // ═══════════════════════════════════════════
    //  COMPUTED STATS (base + level scaling + player bonuses)
    // ═══════════════════════════════════════════

    /// <summary>
    /// Damage cuối cùng: (weapon.atk + level * damagePerLevel) * (1 + playerBonusAtkPct)
    /// </summary>
    public float GetFinalDamage()
    {
        float damage = GetCurrentStatsSnapshot().damage;
        return damage * PlayerPowerupController.GetDamageMultiplierFor(playerTransform);
    }

    /// <summary>
    /// Cooldown cuối cùng: weapon.cooldown - (level-1) * cooldownReductionPerLevel.
    /// Tối thiểu 0.05s.
    /// </summary>
public float GetFinalCooldown()
    {
        float cooldown = GetCurrentStatsSnapshot().cooldown;
        float attackSpeedMultiplier = PlayerPowerupController.GetAttackSpeedMultiplierFor(playerTransform);
        return Mathf.Max(0.05f, cooldown / Mathf.Max(0.01f, attackSpeedMultiplier));
    }

    /// <summary>
    /// Size cuối cùng: weapon.size + (level-1) * sizePerLevel.
    /// </summary>
    public float GetFinalSize()
    {
        return GetCurrentStatsSnapshot().size;
    }

    /// <summary>
    /// Số projectile cuối cùng: weapon.projectileCount + (level-1) * projCountPerLevel + playerBonus.
    /// </summary>
    public int GetFinalProjCount()
    {
        return GetCurrentStatsSnapshot().projectileCount;
    }

    public float GetFinalProjectileSpeed()
    {
        return GetCurrentStatsSnapshot().projectileSpeed;
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
        return GetCurrentStatsSnapshot().knockback;
    }

    public float GetFinalCritChance()
    {
        return GetCurrentStatsSnapshot().crit;
    }

    public void SetInitialRarityMultiplier(float multiplier)
    {
        rarityBaseMultiplier = Mathf.Max(1f, multiplier);
    }


    public WeaponStatsSnapshot GetStatsSnapshotAtLevel(int level)
    {
        if (data == null)
            return WeaponStatsSnapshot.Empty;

        WeaponStatsSnapshot stats = data.GetStatsAtLevel(level, playerStats);
        float safeMultiplier = Mathf.Max(1f, rarityBaseMultiplier);
        stats.damage *= safeMultiplier;
        stats.crit = Mathf.Clamp01(stats.crit * safeMultiplier);
        stats.cooldown = Mathf.Max(0.05f, stats.cooldown / safeMultiplier);
        stats.size *= safeMultiplier;
        stats.projectileSpeed *= safeMultiplier;
        stats.projectileCount = Mathf.Max(
            1,
            Mathf.RoundToInt(stats.projectileCount * safeMultiplier));
        stats.knockback *= safeMultiplier;

        stats.damage += rarityDamageBonus;
        stats.crit = Mathf.Clamp01(stats.crit + rarityCriticalChanceBonus);
        stats.cooldown = Mathf.Max(0.05f, stats.cooldown - rarityCooldownReduction);
        stats.size += raritySizeBonus;
        stats.projectileSpeed += rarityProjectileSpeedBonus;
        stats.projectileCount += rarityProjectileCountBonus;
        stats.knockback += rarityKnockbackBonus;
        return stats;
    }


    public WeaponStatsSnapshot GetCurrentStatsSnapshot()
    {
        return GetStatsSnapshotAtLevel(currentLevel);
    }


    // ═══════════════════════════════════════════
    //  AUTO-ATTACK LOOP
    // ═══════════════════════════════════════════

    protected virtual void Update()
    {
        if (data == null || attackInProgress)
            return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f)
            return;

        attackInProgress = true;
        cooldownDeferred = false;
        Attack();

        if (!cooldownDeferred)
            CompleteAttackCycle();
    }

    /// <summary>
    /// Call before starting a multi-hit coroutine. The cooldown will remain
    /// stopped until CompleteAttackCycle is called after the final hit.
    /// </summary>
    protected void DeferCooldownUntilAttackCompletes()
    {
        cooldownDeferred = true;
    }

    /// <summary>
    /// Finishes the current attack and starts its cooldown from this moment.
    /// </summary>
    protected void CompleteAttackCycle()
    {
        attackInProgress = false;
        cooldownDeferred = false;
        cooldownTimer = GetFinalCooldown();
    }


    /// <summary>
    /// Logic tấn công cụ thể — override trong từng weapon type.
    /// </summary>
    public abstract void Attack();

    /// <summary>
    /// Level up vũ khí. Trả về false nếu đã max level.
    /// </summary>
    public virtual bool LevelUp(float rarityMultiplier = 1f)
    {
        if (currentLevel >= data.maxLevel)
            return false;

        WeaponStatsSnapshot before = GetCurrentStatsSnapshot();
        currentLevel++;
        WeaponStatsSnapshot after = GetCurrentStatsSnapshot();
        float extraMultiplier = Mathf.Max(0f, rarityMultiplier - 1f);

        rarityDamageBonus += Mathf.Max(0f, after.damage - before.damage) * extraMultiplier;
        rarityCriticalChanceBonus += Mathf.Max(0f, after.crit - before.crit) * extraMultiplier;
        rarityCooldownReduction += Mathf.Max(0f, before.cooldown - after.cooldown) * extraMultiplier;
        raritySizeBonus += Mathf.Max(0f, after.size - before.size) * extraMultiplier;
        rarityProjectileSpeedBonus += Mathf.Max(0f, after.projectileSpeed - before.projectileSpeed) * extraMultiplier;
        rarityKnockbackBonus += Mathf.Max(0f, after.knockback - before.knockback) * extraMultiplier;

        int projectileDelta = Mathf.Max(0, after.projectileCount - before.projectileCount);
        rarityProjectileCountBonus += Mathf.Max(
            0,
            Mathf.RoundToInt(projectileDelta * extraMultiplier));

        OnLevelUp();
        return true;
    }

    /// <summary>
    /// Hook cho subclass xử lý thêm khi level up (thay đổi visual, v.v.)
    /// </summary>
    protected virtual void OnLevelUp() { }

    protected void PlayWeaponAttackSound(Vector3 position)
    {
        if (data == null || data.attackSound == null)
            return;

        if (SoundEffectsAudioManager.Instance != null)
        {
            SoundEffectsAudioManager.Instance.PlayWeaponSound(
                data.attackSound,
                position);
            return;
        }

        AudioSource.PlayClipAtPoint(data.attackSound, position);
    }

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
protected Transform FindClosestEnemy(
        float range,
        float incomingProjectileDamage = 0f)
    {
        Collider[] hits = Physics.OverlapSphere(
            playerTransform.position,
            range,
            enemyLayer,
            QueryTriggerInteraction.Collide);
        if (hits.Length == 0)
            return null;

        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in hits)
        {
            EnemyHealth enemy = col.GetComponent<EnemyHealth>();
            if (enemy == null)
                enemy = col.GetComponentInParent<EnemyHealth>();

            if (enemy == null || !enemy.CanBeTargeted)
                continue;

            if (incomingProjectileDamage > 0f &&
                enemy.GetExpectedDamage(incomingProjectileDamage) <= 0f)
            {
                continue;
            }

            if (incomingProjectileDamage > 0f &&
                Projectile.HasEnoughIncomingDamageToKill(enemy))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(
                col.bounds.center - playerTransform.position);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = col.transform;
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
public static class WeaponHitParticles
{
    private static ParticleSystem particles;
    private static Material sharedMaterial;

    public static void PlaySwordHit(EnemyHealth enemy, Vector3 attackDirection)
    {
        Play(enemy, attackDirection, 8, 2.8f, 4.8f, 0.12f, 0.25f, 0.22f, 0.42f);
    }

    public static void PlayAuraHit(EnemyHealth enemy, Vector3 radialDirection)
    {
        Play(enemy, radialDirection, 5, 1.5f, 3f, 0.1f, 0.2f, 0.28f, 0.5f);
    }

    private static void Play(
        EnemyHealth enemy,
        Vector3 impactDirection,
        int particleCount,
        float minimumSpeed,
        float maximumSpeed,
        float minimumSize,
        float maximumSize,
        float minimumLifetime,
        float maximumLifetime)
    {
        if (enemy == null)
            return;

        EnsureParticleSystem();
        if (particles == null)
            return;

        Collider enemyCollider = enemy.GetComponent<Collider>();
        Vector3 hitPosition = enemyCollider != null
            ? enemyCollider.bounds.center
            : enemy.transform.position + Vector3.up * 0.75f;
        impactDirection.y = 0f;
        impactDirection = impactDirection.sqrMagnitude > 0.001f
            ? impactDirection.normalized
            : Vector3.up;

        for (int i = 0; i < particleCount; i++)
        {
            Vector3 scatter = Random.onUnitSphere;
            scatter.y = Mathf.Abs(scatter.y) * 0.8f;
            Vector3 velocityDirection = (scatter + impactDirection * 0.35f).normalized;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = hitPosition + Random.insideUnitSphere * 0.08f,
                velocity = velocityDirection * Random.Range(minimumSpeed, maximumSpeed),
                startLifetime = Random.Range(minimumLifetime, maximumLifetime),
                startSize = Random.Range(minimumSize, maximumSize),
                startColor = Color.white
            };
            particles.Emit(emit, 1);
        }
    }

    private static void EnsureParticleSystem()
    {
        if (particles != null)
            return;

        GameObject particleObject = new GameObject("Weapon Hit Particles");
        Object.DontDestroyOnLoad(particleObject);
        particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;
        main.cullingMode = ParticleSystemCullingMode.Automatic;
        main.gravityModifier = 0.15f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fade;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.allowOcclusionWhenDynamic = false;
        Material material = GetOrCreateMaterial();
        if (material != null)
            particleRenderer.sharedMaterial = material;
    }

    private static Material GetOrCreateMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/GoldenSandParticle");
        if (shader == null)
            shader = Shader.Find("Custom/Gigachad/Golden Sand Particle");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            return null;

        sharedMaterial = new Material(shader)
        {
            name = "Shared Runtime White Weapon Hit Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (sharedMaterial.HasProperty("_Softness"))
            sharedMaterial.SetFloat("_Softness", 0.18f);
        return sharedMaterial;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        particles = null;
        sharedMaterial = null;
    }
}
