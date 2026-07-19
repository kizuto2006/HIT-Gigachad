using UnityEngine;

/// <summary>
/// Vũ khí cận chiến — dùng OverlapSphere quanh player để gây damage.
/// Spawn VFX slash effect khi tấn công. Hỗ trợ knockback.
/// </summary>
public class MeleeWeapon : WeaponBehaviour
{
    [Header("── Melee Settings ──")]
    [Tooltip("Offset vị trí hitbox so với player (hướng forward).")]
    public float hitboxForwardOffset = 0.5f;

    public override void Attack()
    {
        float damage = GetFinalDamage();
        float radius = GetFinalSize();
        Vector3 center = playerTransform.position + playerTransform.forward * hitboxForwardOffset;

        Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer);

        hitCache.Clear();

        foreach (Collider col in hits)
        {
            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = col.GetComponentInParent<EnemyHealth>();

            if (enemyHealth != null && !hitCache.Contains(enemyHealth))
            {
                hitCache.Add(enemyHealth);

                float finalDmg = RollCritDamage(damage);
                enemyHealth.TakeDamage(finalDmg, false);
                ApplyKnockback(enemyHealth.transform, data.knockback);
            }
        }

        // Spawn VFX
        if (data.attackEffectPrefab != null)
        {
            GameObject vfx = Instantiate(data.attackEffectPrefab, center, playerTransform.rotation);

            SlashVFX slashVFX = vfx.GetComponent<SlashVFX>();
            if (slashVFX != null)
                slashVFX.SetFacingTarget(playerTransform);

            vfx.transform.localScale *= radius;
            Destroy(vfx, 1f);
        }

        // Play SFX
        if (data.attackSound != null)
        {
            AudioSource.PlayClipAtPoint(data.attackSound, center);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        float radius = (data != null) ? GetFinalSize() : 1f;
        Vector3 center = playerTransform.position + playerTransform.forward * hitboxForwardOffset;

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawSphere(center, radius);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
        Gizmos.DrawWireSphere(center, radius);
    }
}
