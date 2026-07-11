using UnityEngine;

/// <summary>
/// Gắn lên GameObject con của Player (đặt tên "MeleeHitbox").
/// Nhấn chuột trái để tấn công melee tạm thời — dùng OverlapSphere tìm enemy trong range.
/// </summary>
public class PlayerMeleeHitbox : MonoBehaviour
{
    [Header("── References ──")]
    public PlayerBaseStats stats;

    [Header("── Hitbox Settings ──")]
    [Tooltip("Bán kính vùng tấn công melee")]
    public float hitboxRadius = 1.2f;

    [Tooltip("Phím tấn công (mặc định chuột trái)")]
    public KeyCode attackKey = KeyCode.Mouse0;

    [Header("── Cooldown ──")]
    [Tooltip("Thời gian chờ giữa các lần tấn công (giây)")]
    public float attackCooldown = 0.5f;

    [Header("── Layer Filter ──")]
    [Tooltip("Layer của Enemy — chỉ detect collider trên layer này")]
    public LayerMask enemyLayer = ~0; // mặc định tất cả layer

    private float lastAttackTime = -999f;

    void Update()
    {
        if (Input.GetKeyDown(attackKey) && Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
        }
    }

    private void Attack()
    {
        lastAttackTime = Time.time;

        float damage = stats.FinalAtk;
        Collider[] hits = Physics.OverlapSphere(transform.position, hitboxRadius, enemyLayer);

        int hitCount = 0;
        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                // Thử tìm trên parent (nếu collider nằm trên child)
                enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, false);
                hitCount++;
            }
        }

        if (hitCount > 0)
        {
            Debug.Log($"[Melee] Hit {hitCount} enemy(ies) for {damage:F1} damage");
        }
    }

    /// <summary>
    /// Vẽ Gizmos hình cầu đỏ bán trong suốt trong Editor để thấy range.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, hitboxRadius);

        // Viền wireframe rõ hơn
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, hitboxRadius);
    }
}
