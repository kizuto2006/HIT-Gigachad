using UnityEngine;

/// <summary>
/// Runtime behaviour cho projectile đã được spawn.
/// Bay thẳng theo hướng forward, gây damage khi chạm enemy, hỗ trợ pierce.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("── Stats (set bởi weapon) ──")]
    public float damage;
    public float speed;
    public int maxPierce;
    public float knockback;
    public float lifetime = 5f;
    public LayerMask enemyLayer;

    private int pierceCount;
    private float timer;
    private Transform sourcePlayer;

    /// <summary>
    /// Khởi tạo stats cho projectile. Gọi từ ProjectileWeapon.Attack().
    /// </summary>
    public void Setup(float dmg, float spd, int pierce, float kb, LayerMask layer, Transform player)
    {
        damage = dmg;
        speed = spd;
        maxPierce = pierce;
        knockback = kb;
        enemyLayer = layer;
        sourcePlayer = player;
        pierceCount = 0;
        timer = 0f;
    }

    void Update()
    {
        // Bay thẳng
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        // Lifetime check
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check layer
        if ((enemyLayer & (1 << other.gameObject.layer)) == 0) return;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage, false);

            // Knockback
            if (knockback > 0f && sourcePlayer != null)
            {
                Rigidbody rb = enemyHealth.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (enemyHealth.transform.position - sourcePlayer.position).normalized;
                    rb.AddForce(dir * knockback, ForceMode.Impulse);
                }
            }

            pierceCount++;
            if (pierceCount > maxPierce)
            {
                Destroy(gameObject);
            }
        }
    }
}
