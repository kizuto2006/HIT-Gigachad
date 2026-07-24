using UnityEngine;

/// <summary>
/// Vũ khí bắn đạn — spawn projectile prefab từ WeaponData.
/// Tự động target enemy gần nhất. Hỗ trợ multiple projectiles (spread pattern).
/// </summary>
public class ProjectileWeapon : WeaponBehaviour
{
    [Header("── Projectile Settings ──")]
    [Tooltip("Tầm tìm target tự động.")]
    public float targetRange = 20f;

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

        // Tìm target gần nhất
        Transform target = FindClosestEnemy(targetRange);
        Vector3 baseDir;

        if (target != null)
        {
            baseDir = (target.position - playerTransform.position).normalized;
        }
        else
        {
            // Không có target → bắn theo hướng player đang nhìn
            baseDir = playerTransform.forward;
        }

        // Spawn position
        Vector3 spawnPos = playerTransform.position
            + playerTransform.right * spawnOffset.x
            + playerTransform.up * spawnOffset.y
            + playerTransform.forward * spawnOffset.z;

        // Spread pattern: phân bố đều quanh baseDir
        float totalSpread = (count - 1) * spreadAngle;
        float startAngle = -totalSpread * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * spreadAngle;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 dir = rot * baseDir;

            GameObject projGO = Instantiate(data.projectilePrefab, spawnPos, Quaternion.LookRotation(dir));

            // Scale projectile theo weapon size
            projGO.transform.localScale *= GetFinalSize();

            // Setup Projectile component
            Projectile proj = projGO.GetComponent<Projectile>();
            if (proj == null) proj = projGO.AddComponent<Projectile>();

            proj.Setup(RollCritDamage(damage), speed, GetFinalPierce(), GetFinalKnockback(), enemyLayer, playerTransform);
        }

        // Play SFX
        if (data.attackSound != null)
        {
            AudioSource.PlayClipAtPoint(data.attackSound, spawnPos);
        }
    }
}
