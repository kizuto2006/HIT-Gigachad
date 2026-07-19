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

    /// <summary>Reset trạng thái sống và scale mỗi lần object được lấy từ pool.</summary>
    public void ResetForSpawn()
    {
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

        scaleTarget.localScale = baseScale * scaleMul;
        ApplyRootColliderScale(scaleMul);
        currentHp = data.hp * hpMul;
    }

    private float GetHpMultiplier()
    {
        switch (data.size)
        {
            case EnemySize.Small:
                return 0.6f;
            case EnemySize.Large:
                return 2f;
            case EnemySize.Medium:
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Nhận damage sau khi trừ armor. isEliteDmg để dành cho hệ thống sau.
    /// Công thức damage đầu vào (tính ở nơi gọi): raw = weaponBaseDmg * (1 + bonusAtkPct)
    /// </summary>
    public void TakeDamage(float raw, bool isEliteDmg = false)
    {
        if (isDying)
            return;

        float finalDmg = Mathf.Max(0f, raw - data.armor);
        if (finalDmg <= 0f)
            return;

        currentHp -= finalDmg;
        StartHitFlash();

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            isDying = true;
        }
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
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} died");

        // Drop XP gem
        if (xpGemPrefab != null && data != null)
        {
            Instantiate(xpGemPrefab, transform.position, Quaternion.identity);
        }

        // Tìm EnemySpawn và return về pool thay vì SetActive(false) trực tiếp
        EnemySpawn spawner = FindAnyObjectByType<EnemySpawn>();
        if (spawner != null)
            spawner.ReturnEnemyToPool(gameObject);
        else
            gameObject.SetActive(false);
    }
}
