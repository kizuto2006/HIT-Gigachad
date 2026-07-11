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

    private PlayerHealth playerHealth;
    private float lastDamageTime = -999f;

    void Start()
    {
        // Tìm PlayerHealth qua tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning($"[EnemyContactDamage] Không tìm thấy PlayerHealth trên object có tag 'Player'! ({gameObject.name})");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (playerHealth == null) return;
        if (!other.CompareTag("Player")) return;
        if (data == null) return;

        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;
            playerHealth.TakeDamage(data.atk);
            Debug.Log($"[Contact] {gameObject.name} dealt {data.atk:F1} damage to Player");
        }
    }

    /// <summary>
    /// Fallback: reset lastDamageTime khi enemy vừa chạm player lần đầu.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (data == null || playerHealth == null) return;

        // Gây damage ngay lập tức lần đầu chạm
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;
            playerHealth.TakeDamage(data.atk);
            Debug.Log($"[Contact] {gameObject.name} dealt {data.atk:F1} damage to Player (first hit)");
        }
    }
}
