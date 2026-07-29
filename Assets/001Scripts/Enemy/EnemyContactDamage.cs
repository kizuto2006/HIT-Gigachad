using UnityEngine;

/// <summary>
/// Gắn lên Enemy Prefab. Khi enemy chạm vào Player (OnTriggerStay), gây damage liên tục.
/// Yêu cầu: Collider trên Enemy phải bật Is Trigger, hoặc thêm một child Trigger collider.
/// Player phải có tag "Player" và component PlayerHealth.
/// </summary>
public class EnemyContactDamage : MonoBehaviour
{
    [Header("── Data Reference ──")]
    public EnemyData data;

    [Header("── Contact Settings ──")]
    [Tooltip("Cooldown giữa các lần gây damage khi chạm liên tục (giây)")]
    public float damageCooldown = 1f;
    [Tooltip("Tốc độ enemy đẩy player liên tục khi đang chạm (m/giây)")]
    [Min(0f)] public float contactPushSpeed = 3f;

    private PlayerHealth playerHealth;
    private PlayerSimpleMovement playerMovement;
    private EnemyHealth enemyHealth;
    private float lastDamageTime = -999f;

    private void OnEnable()
    {
        lastDamageTime = -999f;
    }

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            playerMovement = playerObj.GetComponent<PlayerSimpleMovement>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning($"[EnemyContactDamage] Không tìm thấy PlayerHealth trên object có tag 'Player'! ({gameObject.name})");
        }
    }

    void OnTriggerStay(Collider other)
    {
        HandleTriggerStay(other);
    }

    public void HandleTriggerStay(Collider other)
    {
        if (playerHealth == null) return;
        if (!other.CompareTag("Player")) return;
        if (data == null) return;

        PushPlayerContinuously();

        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;
            float damage = GetAttackDamage();
            playerHealth.TakeDamage(damage);
            
            Debug.Log($"[Contact] {gameObject.name} dealt {damage:F1} damage to Player");
        }
    }

    /// <summary>
    /// Fallback: reset lastDamageTime khi enemy vừa chạm player lần đầu.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (data == null || playerHealth == null) return;

        // Gây damage ngay lập tức lần đầu chạm
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;
            float damage = GetAttackDamage();
            playerHealth.TakeDamage(damage);
            
            Debug.Log($"[Contact] {gameObject.name} dealt {damage:F1} damage to Player (first hit)");
        }
    }

    private float GetAttackDamage()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        return enemyHealth != null ? enemyHealth.AttackDamage : data.atk;
    }

    private void PushPlayerContinuously()
    {
        if (playerMovement == null || contactPushSpeed <= 0f)
        {
            return;
        }

        Vector3 direction = playerHealth.transform.position - transform.position;
        direction.y = 0f;
        playerMovement.ApplyContactPush(direction, contactPushSpeed);
    }
}
