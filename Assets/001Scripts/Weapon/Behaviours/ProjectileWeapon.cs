using UnityEngine;

/// <summary>
/// Vũ khí bắn đạn — spawn projectile prefab từ WeaponData.
/// Tự động target enemy gần nhất. Hỗ trợ multiple projectiles (spread pattern).
/// </summary>
public class ProjectileWeapon : WeaponBehaviour
{
    [Header("── Projectile Settings ──")]
    [Tooltip("Tầm tìm target tự động.")]
    public float targetRange = 60f;

    [Tooltip("Góc spread giữa các projectile (độ) khi bắn nhiều viên.")]
    public float spreadAngle = 15f;

    [Tooltip("Vị trí spawn đạn offset so với player.")]
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0.5f);

public override void Attack()
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning($"[ProjectileWeapon] {data.weaponName} thiếu projectilePrefab!");
            return;
        }

        int count = GetFinalProjCount();
        float damage = GetFinalDamage();
        float speed = GetFinalProjectileSpeed();

        Vector3 spawnPos = playerTransform.position
            + playerTransform.right * spawnOffset.x
            + playerTransform.up * spawnOffset.y
            + playerTransform.forward * spawnOffset.z;

        float totalSpread = (count - 1) * spreadAngle;
        float startAngle = -totalSpread * 0.5f;
        bool firedAnyProjectile = false;

        for (int i = 0; i < count; i++)
        {
            float rolledDamage = RollCritDamage(damage);
            Transform target = FindClosestEnemy(targetRange, rolledDamage);
            if (target == null)
                break;

            Vector3 baseDirection =
                (target.position - playerTransform.position).normalized;
            float angle = startAngle + i * spreadAngle;
            Quaternion spreadRotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 direction = spreadRotation * baseDirection;

            Projectile projectile = ProjectilePool.Spawn(
                data.projectilePrefab,
                spawnPos,
                Quaternion.LookRotation(direction));
            if (projectile == null)
                continue;

            projectile.Setup(
                rolledDamage,
                speed,
                GetFinalPierce(),
                GetFinalKnockback(),
                enemyLayer,
                playerTransform,
                target,
                720f,
                GetFinalSize());
            firedAnyProjectile = true;
        }

        if (firedAnyProjectile && data.attackSound != null)
            AudioSource.PlayClipAtPoint(data.attackSound, spawnPos);
    }
}
