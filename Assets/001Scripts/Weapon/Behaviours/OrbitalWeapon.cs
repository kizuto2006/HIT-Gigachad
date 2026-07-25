using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Vũ khí quỹ đạo — object xoay quanh player theo vòng tròn.
/// Gây damage khi chạm enemy (trigger). Level up tăng số lượng orbitals.
/// </summary>
public class OrbitalWeapon : WeaponBehaviour
{
    [Header("── Orbital Settings ──")]
    [Tooltip("Prefab cho mỗi orbital object. Nếu null, dùng attackEffectPrefab.")]
    public GameObject orbitalPrefab;

    private readonly List<GameObject> orbitals = new List<GameObject>();
    private float currentAngle;

    public override void Initialize(PlayerBaseStats stats, LayerMask enemyMask, Transform player)
    {
        base.Initialize(stats, enemyMask, player);
        SpawnOrbitals();
    }

    /// <summary>
    /// Orbital weapon không dùng cooldown-based Attack() truyền thống.
    /// Thay vào đó, orbitals liên tục quay và gây damage on contact.
    /// Override Update() để quay orbitals.
    /// </summary>
    protected override void Update()
    {
        if (data == null || playerTransform == null) return;

        // Quay orbitals quanh player
        float orbitSpd = data.orbitSpeed;
        currentAngle += orbitSpd * Time.deltaTime;

        float radius = data.orbitRadius;
        int count = orbitals.Count;

        for (int i = 0; i < count; i++)
        {
            if (orbitals[i] == null) continue;

            float angleOffset = (360f / count) * i;
            float angle = (currentAngle + angleOffset) * Mathf.Deg2Rad;

            Vector3 pos = playerTransform.position + new Vector3(
                Mathf.Cos(angle) * radius,
                0.5f, // Nâng lên 1 chút khỏi mặt đất
                Mathf.Sin(angle) * radius
            );

            orbitals[i].transform.position = pos;
            orbitals[i].transform.localScale = Vector3.one * GetFinalSize();
        }
    }

    /// <summary>
    /// Attack() của orbital: respawn/refresh damage trên các orbitals.
    /// Được gọi theo cooldown nhưng orbitals vẫn luôn quay.
    /// </summary>
    public override void Attack()
    {
        // Refresh damage values trên tất cả orbital triggers
        float dmg = GetFinalDamage();
        foreach (GameObject orbital in orbitals)
        {
            if (orbital == null) continue;
            OrbitalTrigger trigger = orbital.GetComponent<OrbitalTrigger>();
            if (trigger != null)
            {
                trigger.RefreshDamage(dmg);
            }
        }
    }

    protected override void OnLevelUp()
    {
        // Khi level up → thêm orbital mới
        SpawnOrbitals();
    }

    /// <summary>
    /// Spawn/cập nhật số lượng orbitals dựa trên level.
    /// Số orbital = projectileCount + (level-1) * projCountPerLevel
    /// </summary>
    private void SpawnOrbitals()
    {
        int targetCount = GetFinalProjCount();

        // Thêm orbital mới nếu cần
        while (orbitals.Count < targetCount)
        {
            GameObject prefab = orbitalPrefab != null ? orbitalPrefab : data.attackEffectPrefab;
            GameObject orbital;

            if (prefab != null)
            {
                orbital = Instantiate(prefab, playerTransform.position, Quaternion.identity);
            }
            else
            {
                // Fallback: tạo sphere đơn giản
                orbital = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orbital.transform.position = playerTransform.position;
                orbital.transform.localScale = Vector3.one * 0.5f;

                // Đảm bảo collider là trigger
                Collider col = orbital.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                // Xóa collider mặc định nếu cần thêm lại
                Renderer rend = orbital.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(1f, 0.5f, 0f, 0.8f);
            }

            orbital.name = $"Orbital_{data.weaponName}_{orbitals.Count}";

            // Đảm bảo có trigger collider
            Collider orbitalCol = orbital.GetComponent<Collider>();
            if (orbitalCol != null) orbitalCol.isTrigger = true;

            // Thêm OrbitalTrigger component
            OrbitalTrigger trigger = orbital.GetComponent<OrbitalTrigger>();
            if (trigger == null) trigger = orbital.AddComponent<OrbitalTrigger>();
            trigger.Initialize(GetFinalDamage(), data.knockback, data.crit, enemyLayer, playerTransform);

            orbitals.Add(orbital);
        }
    }

    void OnDestroy()
    {
        foreach (GameObject orbital in orbitals)
        {
            if (orbital != null) Destroy(orbital);
        }
        orbitals.Clear();
    }
}

/// <summary>
/// Gắn trên mỗi orbital object. Gây damage khi chạm enemy qua OnTriggerEnter.
/// Có cooldown per-enemy để tránh hit liên tục.
/// </summary>
public class OrbitalTrigger : MonoBehaviour
{
    private float damage;
    private float knockback;
    private float critChance;
    private LayerMask enemyLayer;
    private Transform sourcePlayer;

    // Cooldown hit per enemy
    private readonly Dictionary<int, float> hitCooldowns = new Dictionary<int, float>();
    private const float HIT_COOLDOWN = 0.5f;

    public void Initialize(float dmg, float kb, float crit, LayerMask layer, Transform player)
    {
        damage = dmg;
        knockback = kb;
        critChance = crit;
        enemyLayer = layer;
        sourcePlayer = player;
    }

    public void RefreshDamage(float newDamage)
    {
        damage = newDamage;
    }

    void OnTriggerStay(Collider other)
    {
        if ((enemyLayer & (1 << other.gameObject.layer)) == 0) return;

        int id = other.gameObject.GetInstanceID();
        if (hitCooldowns.TryGetValue(id, out float lastHit) && Time.time - lastHit < HIT_COOLDOWN)
            return;

        hitCooldowns[id] = Time.time;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth != null)
        {
            float finalDmg = damage;
            if (Random.value < critChance) finalDmg *= 2f;

            enemyHealth.TakeDamage(finalDmg, false);

            if (knockback > 0f && sourcePlayer != null)
            {
                Rigidbody rb = enemyHealth.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (enemyHealth.transform.position - sourcePlayer.position).normalized;
                    rb.AddForce(dir * knockback, ForceMode.Impulse);
                }
            }
        }
    }

    // Cleanup stale entries periodically
    private float cleanupTimer;
    void Update()
    {
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= 2f)
        {
            cleanupTimer = 0f;
            List<int> stale = new List<int>();
            foreach (var kvp in hitCooldowns)
            {
                if (Time.time - kvp.Value > HIT_COOLDOWN * 2f) stale.Add(kvp.Key);
            }
            foreach (int key in stale) hitCooldowns.Remove(key);
        }
    }
}
