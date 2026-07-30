using System.Collections;
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

    [Tooltip("Offset from the chest anchor toward the current target.")]
    public Vector3 spawnOffset = new Vector3(0f, 0.03f, 0.35f);

    [Tooltip("Fallback chest height above the Player root when no chest bone exists.")]
    [Min(0f)] public float fallbackChestHeight = 1.25f;

    private Transform spawnAnchor;

    public override void Attack()
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning(
                $"[ProjectileWeapon] {data.weaponName} thiếu projectilePrefab!");
            return;
        }

        DeferCooldownUntilAttackCompletes();
        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        int count = Mathf.Max(1, GetFinalProjCount());
        float damage = GetFinalDamage();
        float speed = GetFinalProjectileSpeed();
        float firstRolledDamage = RollCritDamage(damage);
        Transform target = FindClosestEnemy(targetRange, firstRolledDamage);

        if (target == null)
        {
            CompleteAttackCycle();
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                target = FindClosestEnemy(targetRange, damage);

            if (target == null)
                break;

            Vector3 targetCenter =
                GetTargetCenter(target, out Collider targetCollider);
            Vector3 spawnPos = GetSpawnPosition(targetCenter);
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
            if (projectile != null)
            {
                float damageMultiplier = i == 0
                    ? 1f
                    : data.additionalProjectileDamageMultiplier;
                float rolledDamage = i == 0
                    ? firstRolledDamage
                    : RollCritDamage(damage * damageMultiplier);
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

                if (data.attackEffectPrefab != null)
                {
                    ProjectilePool.SpawnEffect(
                        data.attackEffectPrefab,
                        projectileSpawnPos,
                        Quaternion.LookRotation(direction),
                        0.4f);
                }

                if (data.attackSound != null)
                    AudioSource.PlayClipAtPoint(
                        data.attackSound,
                        projectileSpawnPos);
            }

            if (i < count - 1 && data.projectileBurstInterval > 0f)
                yield return new WaitForSeconds(data.projectileBurstInterval);
        }

        CompleteAttackCycle();
    }


private Vector3 GetSpawnPosition(Vector3 targetPosition)
    {
        Vector3 origin = spawnAnchor != null
            ? spawnAnchor.position
            : playerTransform.position +
              playerTransform.up * fallbackChestHeight;

        Vector3 aimForward = targetPosition - origin;
        aimForward.y = 0f;
        if (aimForward.sqrMagnitude <= 0.0001f)
        {
            aimForward = playerTransform.forward;
            aimForward.y = 0f;
        }
        aimForward.Normalize();

        Vector3 aimRight = Vector3.Cross(Vector3.up, aimForward);
        if (aimRight.sqrMagnitude <= 0.0001f)
            aimRight = playerTransform.right;
        aimRight.Normalize();

        return origin
            + aimRight * spawnOffset.x
            + playerTransform.up * spawnOffset.y
            + aimForward * spawnOffset.z;
    }

    private static Transform FindChestAnchor(Transform player)
    {
        if (player == null)
            return null;

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform upperChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperChest != null)
                return upperChest;

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
                return chest;

            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
                return spine;
        }

        Transform[] descendants =
            player.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            string childName = descendants[i].name;
            if (childName == "UpperChest" ||
                childName.EndsWith(":UpperChest") ||
                childName == "Chest" ||
                childName.EndsWith(":Chest") ||
                childName == "Spine2" ||
                childName.EndsWith(":Spine2"))
            {
                return descendants[i];
            }
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
        spawnAnchor = FindChestAnchor(player);
    }
}
