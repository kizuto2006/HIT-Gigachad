using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logic chiến đấu riêng của Aura: tạo một xung lực quanh Player,
/// gây damage một lần cho mỗi enemy trong bán kính và hiển thị AuraVFX.
/// </summary>
public sealed class AuraWeapon : WeaponBehaviour
{
    public override void Attack()
    {
        float radius = GetFinalSize();
        Vector3 spawnPosition = playerTransform.position;

        GameObject pulseObject;
        if (data.attackEffectPrefab != null)
        {
            pulseObject = Instantiate(data.attackEffectPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            pulseObject = new GameObject($"Aura_{data.weaponName}");
            pulseObject.transform.position = spawnPosition;
        }

        pulseObject.transform.localScale = Vector3.one * radius * 2f;

        AuraDamagePulse pulse = pulseObject.AddComponent<AuraDamagePulse>();
        pulse.Initialize(
            GetFinalDamage(),
            radius,
            GetFinalDuration(),
            GetFinalKnockback(),
            GetFinalCritChance(),
            playerStats != null ? playerStats.FinalCriticalDamageMultiplier : 2f,
            playerTransform);

        if (data.attackSound != null)
            AudioSource.PlayClipAtPoint(data.attackSound, spawnPosition);
    }
}

/// <summary>
/// Một đợt damage của Aura. Component này chỉ thuộc cơ chế Aura và tự hủy
/// cùng VFX sau khi hoàn thành.
/// </summary>
public sealed class AuraDamagePulse : MonoBehaviour
{
    private float damage;
    private float radius;
    private float lifetime;
    private float knockback;
    private float critChance;
    private float critDamageMultiplier;
    private Transform player;
    private float elapsedTime;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    public void Initialize(
        float attackDamage,
        float attackRadius,
        float duration,
        float knockbackForce,
        float criticalChance,
        float criticalDamage,
        Transform sourcePlayer)
    {
        damage = attackDamage;
        radius = attackRadius;
        lifetime = Mathf.Max(0.05f, duration);
        knockback = knockbackForce;
        critChance = criticalChance;
        critDamageMultiplier = criticalDamage;
        player = sourcePlayer;

        FollowPlayerOnGround();
        DealDamageOnce();
    }

    private void Update()
    {
        if (player != null)
            FollowPlayerOnGround();

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= lifetime)
            Destroy(gameObject);
    }

    private void DealDamageOnce()
    {
        float radiusSquared = radius * radius;
        List<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            Vector3 offset = enemy.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSquared)
                continue;

            float finalDamage = Random.value < critChance ? damage * critDamageMultiplier : damage;
            enemy.TakeDamage(finalDamage, false);
            ApplyAuraKnockback(enemy);
        }
    }

    private void ApplyAuraKnockback(EnemyHealth enemy)
    {
        if (knockback <= 0f || !enemy.isActiveAndEnabled)
            return;

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            Vector3 direction = enemy.transform.position - transform.position;
            enemyAI.ApplyKnockback(direction, knockback);
        }
    }

    private void FollowPlayerOnGround()
    {
        Vector3 playerPosition = player.position;
        Vector3 rayOrigin = playerPosition + Vector3.up * 2f;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            20f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        float groundY = playerPosition.y;
        Vector3 groundNormal = Vector3.up;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == player || hitTransform.IsChildOf(player))
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            groundY = hit.point.y;
            groundNormal = hit.normal;
            foundGround = true;
        }

        if (!foundGround)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            groundY = controller != null ? controller.bounds.min.y : playerPosition.y;
        }

        transform.position = new Vector3(playerPosition.x, groundY, playerPosition.z);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
    }
}
