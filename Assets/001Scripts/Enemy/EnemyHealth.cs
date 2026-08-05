using UnityEngine;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour
{
    public static readonly List<EnemyHealth> ActiveEnemies = new List<EnemyHealth>(5000);
    private static readonly List<EnemyHealth> FlashingEnemies = new List<EnemyHealth>(256);
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static HitFlashUpdater hitFlashUpdater;

    [Header("── Data Reference ──")]
    public EnemyData data;

    [Header("── XP Drop ──")]
    [Tooltip("Prefab XP gem sẽ drop khi enemy chết. Để null nếu không drop.")]
    public GameObject xpGemPrefab;

    [Header("Hit Flash")]
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.1f;
    [SerializeField] private Color hitFlashColor = Color.white;

    // Runtime
    [HideInInspector] public float currentHp;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock[] originalPropertyBlocks;
    private MaterialPropertyBlock[] flashPropertyBlocks;
    private float hitFlashTimeRemaining;
    private bool isFlashing;
    private bool isRegisteredForFlash;
    private bool isDying;

    private EnemySpawn ownerSpawner;
    private Transform scaleTarget;
    private Vector3 baseScale;
    private Collider rootCollider;
    private Vector3 baseColliderCenter;
    private Vector3 baseBoxSize;
    private float baseColliderRadius;
    private float baseCapsuleHeight;
    private float runtimeScaleMultiplier = 1f;
    private float runtimeHpMultiplier = 1f;
    private float runtimeAttackMultiplier = 1f;
    private float runtimeSpeedMultiplier = 1f;

    private bool isSpawnProtected;

    public float AttackDamage => data != null
        ? Mathf.Max(0f, data.atk * runtimeAttackMultiplier)
        : 0f;
    public float MovementSpeed => data != null
        ? Mathf.Max(0f, data.speed * runtimeSpeedMultiplier)
        : 0f;
    public bool IsMiniBoss => runtimeHpMultiplier > 1f;

    public bool CanBeTargeted =>
        isActiveAndEnabled &&
        !isDying &&
        !isDead &&
        !isSpawnProtected &&
        data != null &&
        currentHp > 0f;
    private bool isDead;

    private void Awake()
    {
        CacheRenderers();
        CacheSizeTargets();

        if (data == null)
        {
            Debug.LogError($"[EnemyHealth] EnemyData chưa được gán trên {gameObject.name}!");
        }
    }

    public void SetSpawner(EnemySpawn spawner)
    {
        ownerSpawner = spawner;
    }

    public void ConfigureRuntimeVariant(
        float scaleMultiplier,
        float hpMultiplier,
        float attackMultiplier,
        float speedMultiplier)
    {
        runtimeScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        runtimeHpMultiplier = Mathf.Max(0.01f, hpMultiplier);
        runtimeAttackMultiplier = Mathf.Max(0f, attackMultiplier);
        runtimeSpeedMultiplier = Mathf.Max(0f, speedMultiplier);

        // Apply immediately even while this pooled enemy is inactive. Emergence
        // disables EnemyHealth until the enemy reaches the ground, so waiting for
        // OnEnable would make the visual grow after the crown was positioned.
        if (data != null && scaleTarget != null)
        {
            ApplySizeMultiplier();
        }
    }

internal void SetSpawnProtection(bool isProtected)
    {
        isSpawnProtected = isProtected;
    }


    /// <summary>Reset trạng thái sống và scale mỗi lần object được lấy từ pool.</summary>
    public void ResetForSpawn()
    {

        isSpawnProtected = false;
        isDead = false;

        if (data == null || scaleTarget == null)
        {
            currentHp = 0f;
            return;
        }

        ApplySizeMultiplier();
    }

    private void OnEnable()
    {
        isDying = false;
        ResetForSpawn();

        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        StopHitFlash();

        int index = ActiveEnemies.IndexOf(this);
        if (index < 0) return;

        int lastIndex = ActiveEnemies.Count - 1;
        ActiveEnemies[index] = ActiveEnemies[lastIndex];
        ActiveEnemies.RemoveAt(lastIndex);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveEnemies()
    {
        ActiveEnemies.Clear();
        FlashingEnemies.Clear();
        hitFlashUpdater = null;
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalPropertyBlocks = new MaterialPropertyBlock[cachedRenderers.Length];
        flashPropertyBlocks = new MaterialPropertyBlock[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            originalPropertyBlocks[i] = new MaterialPropertyBlock();
            flashPropertyBlocks[i] = new MaterialPropertyBlock();
        }
    }

    private void CacheSizeTargets()
    {
        // Scale the imported visual/model child while keeping the pooled root stable.
        scaleTarget = transform.childCount > 0 ? transform.GetChild(0) : transform;
        baseScale = scaleTarget.localScale;

        rootCollider = GetComponent<Collider>();
        if (rootCollider is BoxCollider box)
        {
            baseColliderCenter = box.center;
            baseBoxSize = box.size;
        }
        else if (rootCollider is SphereCollider sphere)
        {
            baseColliderCenter = sphere.center;
            baseColliderRadius = sphere.radius;
        }
        else if (rootCollider is CapsuleCollider capsule)
        {
            baseColliderCenter = capsule.center;
            baseColliderRadius = capsule.radius;
            baseCapsuleHeight = capsule.height;
        }
    }

    private void ApplyRootColliderScale(float scaleMultiplier)
    {
        if (rootCollider is BoxCollider box)
        {
            box.center = baseColliderCenter * scaleMultiplier;
            box.size = baseBoxSize * scaleMultiplier;
        }
        else if (rootCollider is SphereCollider sphere)
        {
            sphere.center = baseColliderCenter * scaleMultiplier;
            sphere.radius = baseColliderRadius * scaleMultiplier;
        }
        else if (rootCollider is CapsuleCollider capsule)
        {
            capsule.center = baseColliderCenter * scaleMultiplier;
            capsule.radius = baseColliderRadius * scaleMultiplier;
            capsule.height = baseCapsuleHeight * scaleMultiplier;
        }
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

        float finalScaleMultiplier = scaleMul * runtimeScaleMultiplier;
        scaleTarget.localScale = baseScale * finalScaleMultiplier;
        ApplyRootColliderScale(finalScaleMultiplier);
        currentHp = data.hp * hpMul * runtimeHpMultiplier;
    }

    private float GetHpMultiplier()
    {
        switch (data.size)
        {
            case EnemySize.Small:
                return 0.6f * runtimeHpMultiplier;
            case EnemySize.Large:
                return 2f * runtimeHpMultiplier;
            case EnemySize.Medium:
            default:
                return runtimeHpMultiplier;
        }
    }

    /// <summary>
    /// Nhận damage sau khi trừ armor. isEliteDmg để dành cho hệ thống sau.
    /// Công thức damage đầu vào (tính ở nơi gọi): raw = weaponBaseDmg * (1 + bonusAtkPct)
    /// </summary>
public void TakeDamage(float raw, bool isEliteDmg = false)
    {
        if (!CanBeTargeted)
            return;

        float finalDmg = GetExpectedDamage(raw);
        if (finalDmg <= 0f)
            return;

        currentHp -= finalDmg;
        AudioManager.Instance?.PlayEnemyHit();
        StartHitFlash();
        DamageNumberPopup.Show(finalDmg, GetDamageNumberPosition());

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            isDying = true;
        }
    }

    private Vector3 GetDamageNumberPosition()
    {
        if (rootCollider != null)
        {
            Bounds bounds = rootCollider.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.2f);
        }

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer targetRenderer = cachedRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                Bounds bounds = targetRenderer.bounds;
                return bounds.center + Vector3.up * (bounds.extents.y + 0.2f);
            }
        }

        return transform.position + Vector3.up;
    }

    private void StartHitFlash()
    {
        if (cachedRenderers == null)
            CacheRenderers();

        hitFlashTimeRemaining = hitFlashDuration;

        if (!isFlashing)
        {
            isFlashing = true;
            ApplyHitFlash();
        }

        if (!isRegisteredForFlash)
        {
            isRegisteredForFlash = true;
            FlashingEnemies.Add(this);
            EnsureHitFlashUpdater();
        }
    }

    private void ApplyHitFlash()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer targetRenderer = cachedRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(originalPropertyBlocks[i]);
            targetRenderer.GetPropertyBlock(flashPropertyBlocks[i]);
            flashPropertyBlocks[i].SetTexture(BaseMapId, Texture2D.whiteTexture);
            flashPropertyBlocks[i].SetTexture(MainTexId, Texture2D.whiteTexture);
            flashPropertyBlocks[i].SetColor(BaseColorId, hitFlashColor);
            flashPropertyBlocks[i].SetColor(ColorId, hitFlashColor);
            targetRenderer.SetPropertyBlock(flashPropertyBlocks[i]);
        }
    }

    private void RestoreRendererColors()
    {
        if (!isFlashing || cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].SetPropertyBlock(originalPropertyBlocks[i]);
        }

        isFlashing = false;
        hitFlashTimeRemaining = 0f;
    }

    private void StopHitFlash()
    {
        RestoreRendererColors();

        if (!isRegisteredForFlash)
            return;

        int index = FlashingEnemies.IndexOf(this);
        if (index >= 0)
        {
            int lastIndex = FlashingEnemies.Count - 1;
            FlashingEnemies[index] = FlashingEnemies[lastIndex];
            FlashingEnemies.RemoveAt(lastIndex);
        }

        isRegisteredForFlash = false;
    }

    private static void EnsureHitFlashUpdater()
    {
        if (hitFlashUpdater != null)
            return;

        GameObject updaterObject = new GameObject("Enemy Hit Flash Updater");
        DontDestroyOnLoad(updaterObject);
        hitFlashUpdater = updaterObject.AddComponent<HitFlashUpdater>();
    }

    private static void UpdateHitFlashes(float deltaTime)
    {
        for (int i = FlashingEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = FlashingEnemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                int lastIndex = FlashingEnemies.Count - 1;
                FlashingEnemies[i] = FlashingEnemies[lastIndex];
                FlashingEnemies.RemoveAt(lastIndex);
                continue;
            }

            enemy.hitFlashTimeRemaining -= deltaTime;
            if (enemy.hitFlashTimeRemaining > 0f)
                continue;

            enemy.RestoreRendererColors();
            enemy.isRegisteredForFlash = false;
            int last = FlashingEnemies.Count - 1;
            FlashingEnemies[i] = FlashingEnemies[last];
            FlashingEnemies.RemoveAt(last);

            // Cú đánh kết liễu vẫn hiển thị trọn một lần nháy trắng.
            // Chỉ sau khi renderer trở về màu gốc mới xử lý chết và trả về pool.
            if (enemy.isDying)
            {
                enemy.Die();
            }
        }
    }

    private sealed class HitFlashUpdater : MonoBehaviour
    {
        private void Update()
        {
            UpdateHitFlashes(Time.deltaTime);
        }
    }

private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log($"{gameObject.name} died");

        if (xpGemPrefab != null && data != null)
        {
            XPGem gem = XPGemPool.Spawn(xpGemPrefab, transform.position, Quaternion.identity);
            if (gem != null)
            {
                gem.xpAmount = Mathf.Max(1, data.xpReward);
            }
        }

        if (ownerSpawner != null)
        {
            ownerSpawner.ReturnEnemyToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


public float GetExpectedDamage(float rawDamage)
    {
        if (data == null)
            return 0f;

        return Mathf.Max(0f, rawDamage - data.armor);
    }
}
