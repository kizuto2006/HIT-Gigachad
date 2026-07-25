using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Vũ khí vùng — tạo damage zone tại vị trí ngẫu nhiên gần player hoặc quanh player.
/// Tick damage theo hitInterval, tồn tại trong duration giây.
/// Ví dụ: Aura, Black Hole, Tornado.
/// </summary>
public class AoEWeapon : WeaponBehaviour
{
    [Header("── AoE Settings ──")]
    [Tooltip("Spawn vùng AoE ngay vị trí player (true) hoặc random offset (false).")]
    public bool centerOnPlayer = true;

    [Tooltip("Khoảng cách random offset max khi không center on player.")]
    public float randomOffsetRange = 5f;

    [Tooltip("Mỗi lần hiệu ứng Aura xuất hiện chỉ gây damage đúng một lần.")]
    public bool hitOncePerAttack = true;

    public override void Attack()
    {
        // Spawn AoE zone
        Vector3 spawnPos;
        if (centerOnPlayer)
        {
            spawnPos = playerTransform.position;
        }
        else
        {
            // Random vị trí gần player
            Vector2 offset = Random.insideUnitCircle * randomOffsetRange;
            spawnPos = playerTransform.position + new Vector3(offset.x, 0f, offset.y);
        }

        // Tạo zone object
        GameObject zoneGO;
        if (data.attackEffectPrefab != null)
        {
            zoneGO = Instantiate(data.attackEffectPrefab, spawnPos, playerTransform.rotation);
        }
        else
        {
            // Nếu chưa có prefab VFX → tạo empty với sphere collider
            zoneGO = new GameObject($"AoE_{data.weaponName}");
            zoneGO.transform.position = spawnPos;
        }

        float radius = GetFinalSize();
        zoneGO.transform.localScale = Vector3.one * radius * 2f;

        // Thêm AoEZone component
        AoEZone zone = zoneGO.AddComponent<AoEZone>();
        zone.Initialize(
            GetFinalDamage(),
            radius,
            data.duration,
            data.hitInterval,
            data.knockback,
            enemyLayer,
            playerTransform,
            data.crit,
            centerOnPlayer ? playerTransform : null,
            hitOncePerAttack
        );

        // Play SFX
        if (data.attackSound != null)
        {
            AudioSource.PlayClipAtPoint(data.attackSound, spawnPos);
        }
    }
}

/// <summary>
/// Runtime behaviour cho AoE zone — tick damage theo interval rồi tự hủy.
/// </summary>
public class AoEZone : MonoBehaviour
{
    private float damage;
    private float radius;
    private float duration;
    private float hitInterval;
    private float knockback;
    private LayerMask enemyLayer;
    private Transform sourcePlayer;
    private float critChance;
    private bool hitOncePerAttack;
    private Transform followTarget; // Nếu cần theo player

    private float timer;
    private float hitTimer;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    public void Initialize(float dmg, float rad, float dur, float interval,
                           float kb, LayerMask layer, Transform player, float crit,
                           Transform follow = null, bool singleHit = true)
    {
        damage = dmg;
        radius = rad;
        duration = dur;
        hitInterval = interval;
        knockback = kb;
        enemyLayer = layer;
        sourcePlayer = player;
        critChance = crit;
        hitOncePerAttack = singleHit;
        followTarget = follow;
        timer = 0f;
        hitTimer = 0f;

        if (followTarget != null)
        {
            SnapToGroundBelowTarget();
        }

        if (hitOncePerAttack)
        {
            TickDamage();
        }
    }

    void Update()
    {
        // Follow player nếu cần (aura)
        if (followTarget != null)
        {
            SnapToGroundBelowTarget();
        }

        timer += Time.deltaTime;
        if (timer >= duration)
        {
            Destroy(gameObject);
            return;
        }

        // Tick damage
        if (!hitOncePerAttack)
        {
            hitTimer += Time.deltaTime;
            if (hitTimer >= hitInterval)
            {
                hitTimer = 0f;
                TickDamage();
            }
        }
    }

    /// <summary>
    /// Follow the player's X/Z position while keeping the aura on the walkable
    /// surface below their feet. The player and enemies are ignored so their
    /// colliders cannot lift the visual back up to body height.
    /// </summary>
    private void SnapToGroundBelowTarget()
    {
        Vector3 targetPosition = followTarget.position;
        Vector3 origin = targetPosition + Vector3.up * 2f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            groundHits,
            20f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        float groundY = transform.position.y;
        Vector3 groundNormal = Vector3.up;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == followTarget || hitTransform.IsChildOf(followTarget))
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundY = hit.point.y;
                groundNormal = hit.normal;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            CharacterController controller = followTarget.GetComponent<CharacterController>();
            groundY = controller != null ? controller.bounds.min.y : targetPosition.y;
        }

        transform.position = new Vector3(targetPosition.x, groundY, targetPosition.z);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
    }

    private void TickDamage()
    {
        float radiusSquared = radius * radius;
        List<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemyHealth = enemies[i];
            if (enemyHealth == null || !enemyHealth.isActiveAndEnabled)
                continue;

            Vector3 offset = enemyHealth.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSquared)
                continue;

            float finalDmg = damage;
            if (Random.value < critChance) finalDmg *= 2f;

            enemyHealth.TakeDamage(finalDmg, false);

            if (knockback > 0f && sourcePlayer != null && enemyHealth.isActiveAndEnabled)
            {
                EnemyAI enemyAI = enemyHealth.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    Vector3 direction = enemyHealth.transform.position - transform.position;
                    enemyAI.ApplyKnockback(direction, knockback);
                }
            }
        }
    }
}
