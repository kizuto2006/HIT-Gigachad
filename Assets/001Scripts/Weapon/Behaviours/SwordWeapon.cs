using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logic chiến đấu riêng của Sword: chém một vùng ở phía trước Player,
/// gây damage một lần và phát SlashVFX theo hướng mặt của Player.
/// </summary>
public sealed class SwordWeapon : WeaponBehaviour
{
    private const float ForwardOffsetRatio = 0.45f;

    public override void Attack()
    {
        float radius = GetFinalSize();
        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        Vector3 attackCenter = playerTransform.position + forward * (radius * ForwardOffsetRatio);
        DamageEnemiesInSlash(attackCenter, forward, radius);
        SpawnSlashVFX(attackCenter, radius);
        PlayAttackSound(attackCenter);
    }

    private void DamageEnemiesInSlash(Vector3 attackCenter, Vector3 attackForward, float radius)
    {
        float radiusSquared = radius * radius;
        float baseDamage = GetFinalDamage();
        List<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            Vector3 directionFromPlayer = enemy.transform.position - playerTransform.position;
            directionFromPlayer.y = 0f;
            if (Vector3.Dot(attackForward, directionFromPlayer) <= 0f)
                continue;

            Vector3 offset = enemy.transform.position - attackCenter;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSquared)
                continue;

            enemy.TakeDamage(RollCritDamage(baseDamage), false);
            ApplySwordKnockback(enemy);
        }
    }

    private void ApplySwordKnockback(EnemyHealth enemy)
    {
        float knockback = GetFinalKnockback();
        if (knockback <= 0f || !enemy.isActiveAndEnabled)
            return;

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            Vector3 direction = enemy.transform.position - playerTransform.position;
            enemyAI.ApplyKnockback(direction, knockback);
        }
    }

    private void SpawnSlashVFX(Vector3 position, float radius)
    {
        if (data.attackEffectPrefab == null)
            return;

        GameObject vfx = Instantiate(data.attackEffectPrefab, position, playerTransform.rotation);
        vfx.transform.localScale *= radius;

        SlashVFX slashVFX = vfx.GetComponent<SlashVFX>();
        if (slashVFX != null)
            slashVFX.SetFacingTarget(playerTransform);

        Destroy(vfx, Mathf.Max(1f, data.duration));
    }

    private void PlayAttackSound(Vector3 position)
    {
        if (data.attackSound != null)
            AudioSource.PlayClipAtPoint(data.attackSound, position);
    }
}
