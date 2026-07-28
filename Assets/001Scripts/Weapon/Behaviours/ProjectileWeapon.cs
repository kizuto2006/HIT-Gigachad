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
    [Min(0f)] public float spreadAngle = 4f;

    [Tooltip("Khoảng cách tối thiểu giữa các mũi tên tại điểm bắn.")]
    [Min(0.05f)] public float minimumArrowSpacing = 0.16f;

    [Tooltip("Phần chiều rộng collider mục tiêu được dùng cho đội hình tên.")]
    [Range(0.25f, 0.9f)] public float targetWidthUsage = 0.65f;

    [Tooltip("Độ cong theo chiều cao của đội hình nhiều mũi tên.")]
    [Min(0f)] public float formationArcHeight = 0.1f;

    [Tooltip("Giới hạn nửa chiều rộng để đội hình không xòe quá lớn.")]
    [Min(0.1f)] public float maxFormationHalfWidth = 0.55f;

    [Tooltip("Offset từ bone hông tới điểm xuất phát projectile/VFX.")]
    public Vector3 spawnOffset = new Vector3(0f, 0f, 0.45f);

    [Tooltip("Độ cao fallback tính từ Player root nếu model không có bone Hips.")]
    [Min(0f)] public float fallbackHipHeight = 0.25f;

    private Transform spawnAnchor;

    public override void Attack()
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning(
                $"[ProjectileWeapon] {data.weaponName} thiếu projectilePrefab!");
            return;
        }

        int count = Mathf.Max(1, GetFinalProjCount());
        float damage = GetFinalDamage();
        float speed = GetFinalProjectileSpeed();
        float firstRolledDamage = RollCritDamage(damage);
        Transform target = FindClosestEnemy(targetRange, firstRolledDamage);
        if (target == null)
            return;

        Vector3 spawnPos = GetSpawnPosition();

        Vector3 targetCenter = GetTargetCenter(target, out Collider targetCollider);
        Vector3 baseDirection = targetCenter - spawnPos;
        if (baseDirection.sqrMagnitude <= 0.0001f)
            baseDirection = playerTransform.forward;
        baseDirection.Normalize();

        Vector3 formationRight = Vector3.Cross(Vector3.up, baseDirection);
        if (formationRight.sqrMagnitude <= 0.0001f)
            formationRight = playerTransform.right;
        formationRight.Normalize();

        float targetDistance = Vector3.Distance(spawnPos, targetCenter);
        float targetHalfWidth = GetFormationHalfWidth(
            targetCollider,
            formationRight,
            targetDistance,
            count);
        float minimumSpawnHalfWidth =
            minimumArrowSpacing * (count - 1) * 0.5f;
        float spawnHalfWidth = Mathf.Min(
            maxFormationHalfWidth,
            Mathf.Max(targetHalfWidth, minimumSpawnHalfWidth));

        bool firedAnyProjectile = false;
        for (int i = 0; i < count; i++)
        {
            float slot = count == 1
                ? 0f
                : Mathf.Lerp(-1f, 1f, i / (float)(count - 1));
            float arc = count > 2
                ? formationArcHeight * (1f - slot * slot)
                : 0f;

            Vector3 spawnFormationOffset =
                formationRight * (slot * spawnHalfWidth) +
                Vector3.up * arc;
            Vector3 targetFormationOffset =
                formationRight * (slot * targetHalfWidth) +
                Vector3.up * arc;
            Vector3 projectileSpawnPos = spawnPos + spawnFormationOffset;
            Vector3 direction =
                targetCenter + targetFormationOffset - projectileSpawnPos;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = baseDirection;
            direction.Normalize();

            Projectile projectile = ProjectilePool.Spawn(
                data.projectilePrefab,
                projectileSpawnPos,
                Quaternion.LookRotation(direction));
            if (projectile == null)
                continue;

            float rolledDamage = i == 0
                ? firstRolledDamage
                : RollCritDamage(damage);
            projectile.Setup(
                rolledDamage,
                speed,
                GetFinalPierce(),
                GetFinalKnockback(),
                enemyLayer,
                playerTransform,
                target,
                720f,
                GetFinalSize(),
                targetFormationOffset);
            firedAnyProjectile = true;
        }

        if (!firedAnyProjectile)
            return;

        if (data.attackEffectPrefab != null)
        {
            ProjectilePool.SpawnEffect(
                data.attackEffectPrefab,
                spawnPos,
                Quaternion.LookRotation(baseDirection),
                0.4f);
        }

        if (data.attackSound != null)
            AudioSource.PlayClipAtPoint(data.attackSound, spawnPos);
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 origin = spawnAnchor != null
            ? spawnAnchor.position
            : playerTransform.position + playerTransform.up * fallbackHipHeight;

        return origin
            + playerTransform.right * spawnOffset.x
            + playerTransform.up * spawnOffset.y
            + playerTransform.forward * spawnOffset.z;
    }

    private static Transform FindHipAnchor(Transform player)
    {
        if (player == null)
            return null;

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform humanoidHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (humanoidHips != null)
                return humanoidHips;
        }

        Transform[] descendants = player.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            string childName = descendants[i].name;
            if (childName == "Hips" || childName.EndsWith(":Hips"))
                return descendants[i];
        }

        return null;
    }

    private static Vector3 GetTargetCenter(
        Transform target,
        out Collider targetCollider)
    {
        targetCollider = target.GetComponent<Collider>();
        if (targetCollider == null)
        {
            EnemyHealth targetHealth =
                target.GetComponentInParent<EnemyHealth>();
            if (targetHealth != null)
            {
                targetCollider =
                    targetHealth.GetComponentInChildren<Collider>();
            }
        }

        return targetCollider != null
            ? targetCollider.bounds.center
            : target.position;
    }

    private float GetFormationHalfWidth(
        Collider targetCollider,
        Vector3 formationRight,
        float targetDistance,
        int count)
    {
        if (count <= 1)
            return 0f;

        float totalHalfAngle = Mathf.Min(
            12f,
            spreadAngle * (count - 1) * 0.5f);
        float desiredHalfWidth = Mathf.Tan(
            totalHalfAngle * Mathf.Deg2Rad) * targetDistance;
        desiredHalfWidth = Mathf.Min(
            desiredHalfWidth,
            maxFormationHalfWidth);

        float projectileRadius = GetProjectileRadius();
        if (targetCollider == null)
        {
            return Mathf.Min(
                desiredHalfWidth,
                projectileRadius * Mathf.Max(1f, count * 0.75f));
        }

        Vector3 extents = targetCollider.bounds.extents;
        float projectedTargetRadius =
            Mathf.Abs(formationRight.x) * extents.x +
            Mathf.Abs(formationRight.y) * extents.y +
            Mathf.Abs(formationRight.z) * extents.z;
        float safeHalfWidth =
            projectedTargetRadius * targetWidthUsage +
            projectileRadius * 0.75f;

        return Mathf.Min(desiredHalfWidth, safeHalfWidth);
    }

    private float GetProjectileRadius()
    {
        SphereCollider projectileCollider =
            data.projectilePrefab.GetComponent<SphereCollider>();
        if (projectileCollider == null)
            return 0.08f * GetFinalSize();

        Vector3 prefabScale = data.projectilePrefab.transform.localScale;
        float largestScale = Mathf.Max(
            Mathf.Abs(prefabScale.x),
            Mathf.Abs(prefabScale.y),
            Mathf.Abs(prefabScale.z));
        return Mathf.Max(
            0.02f,
            projectileCollider.radius * largestScale * GetFinalSize());
    }

    public override void Initialize(
        PlayerBaseStats stats,
        LayerMask enemyMask,
        Transform player)
    {
        base.Initialize(stats, enemyMask, player);
        spawnAnchor = FindHipAnchor(player);
    }
}
