using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("── Data Reference ──")]
    public EnemyData data;

    [Tooltip("Transform được scale theo EnemySize. Mặc định tự tìm child 'Visual', fallback về root.")]
    [SerializeField] private Transform scaleTarget;

    // Runtime
    [HideInInspector] public float currentHp;

    private EnemySpawn ownerSpawner;
    private Vector3 baseScale;
    private Collider rootCollider;
    private Vector3 baseColliderCenter;
    private Vector3 baseBoxSize;
    private float baseColliderRadius;
    private float baseCapsuleHeight;
    private bool isDead;

    private void Awake()
    {
        if (scaleTarget == null)
        {
            Transform visual = transform.Find("Visual");
            scaleTarget = visual != null ? visual : transform;
        }

        baseScale = scaleTarget.localScale;
        CacheRootColliderGeometry();

        if (data == null)
        {
            Debug.LogError($"[EnemyHealth] EnemyData chưa được gán trên {gameObject.name}!");
        }
    }

    private void OnEnable()
    {
        ResetForSpawn();
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

    private void CacheRootColliderGeometry()
    {
        rootCollider = GetComponent<Collider>();
        if (rootCollider is CapsuleCollider capsule)
        {
            baseColliderCenter = capsule.center;
            baseColliderRadius = capsule.radius;
            baseCapsuleHeight = capsule.height;
        }
        else if (rootCollider is BoxCollider box)
        {
            baseColliderCenter = box.center;
            baseBoxSize = box.size;
        }
        else if (rootCollider is SphereCollider sphere)
        {
            baseColliderCenter = sphere.center;
            baseColliderRadius = sphere.radius;
        }
    }

    private void ApplyRootColliderScale(float scaleMultiplier)
    {
        // A collider on a scaled root already inherits scale through Transform.
        if (rootCollider == null || scaleTarget == transform) return;

        if (rootCollider is CapsuleCollider capsule)
        {
            capsule.center = baseColliderCenter * scaleMultiplier;
            capsule.radius = baseColliderRadius * scaleMultiplier;
            capsule.height = baseCapsuleHeight * scaleMultiplier;
        }
        else if (rootCollider is BoxCollider box)
        {
            box.center = baseColliderCenter * scaleMultiplier;
            box.size = baseBoxSize * scaleMultiplier;
        }
        else if (rootCollider is SphereCollider sphere)
        {
            sphere.center = baseColliderCenter * scaleMultiplier;
            sphere.radius = baseColliderRadius * scaleMultiplier;
        }
    }

    /// <summary>
    /// Nhận damage sau khi trừ armor. isEliteDmg để dành cho hệ thống sau.
    /// Công thức damage đầu vào (tính ở nơi gọi): raw = weaponBaseDmg * (1 + bonusAtkPct)
    /// </summary>
    public void TakeDamage(float raw, bool isEliteDmg = false)
    {
        if (isDead || data == null) return;

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
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} died");

        if (ownerSpawner != null)
            ownerSpawner.ReturnEnemyToPool(gameObject);
        else
            gameObject.SetActive(false);
    }
}
