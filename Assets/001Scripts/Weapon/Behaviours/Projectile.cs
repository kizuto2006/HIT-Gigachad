
using System;
using UnityEngine;

/// <summary>
/// Runtime behaviour cho projectile đã được spawn.
/// Bay thẳng theo hướng forward, gây damage khi chạm enemy, hỗ trợ pierce.
/// </summary>
public class Projectile : MonoBehaviour
{
    private static readonly System.Collections.Generic.Dictionary<EnemyHealth, float>
        IncomingDamageByEnemy =
            new System.Collections.Generic.Dictionary<EnemyHealth, float>();

    [Header("── Stats (set bởi weapon) ──")]
    public float damage;
    public float speed;
    public int maxPierce;
    public float knockback;

    [Tooltip("Quãng đường tối đa. 0 nghĩa là chỉ dùng lifetime.")]
    [Min(0f)] public float maxTravelDistance;
    public float lifetime = 5f;

    [Tooltip("Các layer làm projectile phát nổ, gồm mặt đất và decor.")]
    public LayerMask impactLayer = ~0;
    public LayerMask enemyLayer;

    private int pierceCount;

    private float traveledDistance;
    private float timer;
    private Transform sourcePlayer;

    private EnemyHealth homingTargetHealth;
    private bool tracksHomingTarget;
    private bool isReleased;
    private Action<Projectile> releaseToPool;
    private Vector3 baseLocalScale;

    private EnemyHealth reservedTarget;
    private float reservedDamage;
    private ParticleSystem[] particleSystems;

    private Vector3 homingTargetOffset;
    private Transform homingTarget;
    private ProjectileTrailVFX trailVfx;

    [Header("── Homing ──")]
    [Tooltip("Tốc độ xoay theo mục tiêu, tính bằng độ/giây.")]
    [Min(0f)] public float homingTurnSpeed = 720f;

    [Header("── Hit Explosion ──")]
    public GameObject hitEffectPrefab;
    [Min(0f)] public float explosionRadius = 2f;
    [Range(0f, 1f)] public float explosionDamageMultiplier = 0.5f;
    [Min(0.1f)] public float explosionEffectLifetime = 1.5f;


    private readonly System.Collections.Generic.HashSet<int> hitEnemyIds =
        new System.Collections.Generic.HashSet<int>();
    private SphereCollider triggerCollider;



    /// <summary>
    /// Khởi tạo stats cho projectile. Gọi từ ProjectileWeapon.Attack().
    /// </summary>
public void Setup(
        float dmg,
        float spd,
        int pierce,
        float kb,
        LayerMask layer,
        Transform player,
        Transform target = null,
        float turnSpeed = 720f,
        float sizeMultiplier = 1f,
        Vector3 targetOffset = default)
    {
        ReleaseReservedDamage();

        damage = dmg;
        speed = spd;
        maxPierce = pierce;
        knockback = kb;
        enemyLayer = layer;
        sourcePlayer = player;
        homingTarget = target;
        homingTargetOffset = targetOffset;
        tracksHomingTarget = target != null;
        homingTargetHealth = target != null
            ? target.GetComponentInParent<EnemyHealth>()
            : null;
        homingTurnSpeed = Mathf.Max(0f, turnSpeed);
        triggerCollider = GetComponent<SphereCollider>();
        transform.localScale = baseLocalScale * Mathf.Max(0.01f, sizeMultiplier);
        hitEnemyIds.Clear();
        pierceCount = 0;

        traveledDistance = 0f;
        timer = 0f;

        ReserveTargetDamage();
        RestartParticles();
        if (trailVfx != null)
            trailVfx.PrepareForSpawn();
    }

private void Update()
    {
        if (isReleased)
            return;

        if (HasLostHomingTarget())
            StopTrackingTarget();

        if (homingTarget != null && homingTurnSpeed > 0f)
        {
            Vector3 targetPosition = homingTarget.position;
            Collider targetCollider = homingTarget.GetComponent<Collider>();
            if (targetCollider == null && homingTargetHealth != null)
            {
                targetCollider =
                    homingTargetHealth.GetComponentInChildren<Collider>();
            }
            if (targetCollider != null)
                targetPosition = targetCollider.bounds.center;

            targetPosition += homingTargetOffset;
            Vector3 direction = targetPosition - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    homingTurnSpeed * Time.deltaTime);
            }
        }

        float moveDistance = speed * Time.deltaTime;
        if (maxTravelDistance > 0f)
        {
            float remainingDistance = maxTravelDistance - traveledDistance;
            if (remainingDistance <= 0f)
            {
                Release();
                return;
            }

            moveDistance = Mathf.Min(moveDistance, remainingDistance);
        }

        if (moveDistance > 0f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                GetCollisionOrigin(),
                GetCollisionRadius(),
                transform.forward,
                moveDistance,
                impactLayer,
                QueryTriggerInteraction.Collide);

            System.Array.Sort(
                hits,
                (first, second) => first.distance.CompareTo(second.distance));

            foreach (RaycastHit hit in hits)
            {
                if (TryImpact(hit.collider, hit.point))
                    return;
            }

            transform.position += transform.forward * moveDistance;
            traveledDistance += moveDistance;
        }

        if (maxTravelDistance > 0f &&
            traveledDistance >= maxTravelDistance)
        {
            Release();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= lifetime)
            Release();
    }

private void OnTriggerEnter(Collider other)
    {
        if (isReleased || other == null)
            return;

        TryImpact(other, other.ClosestPoint(transform.position));
    }


private float GetCollisionRadius()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
            return 0.05f;

        Vector3 scale = transform.lossyScale;
        float largestScale = Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));
        return Mathf.Max(0.01f, triggerCollider.radius * largestScale);
}

private Vector3 GetCollisionOrigin()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        return triggerCollider != null
            ? transform.TransformPoint(triggerCollider.center)
            : transform.position;
    }


private bool TryHit(Collider other, Vector3 hitPosition)
    {
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.CanBeTargeted)
            return false;

        int enemyId = enemyHealth.GetInstanceID();
        if (!hitEnemyIds.Add(enemyId))
            return false;

        bool hitLockedTarget = enemyHealth == homingTargetHealth;
        if (enemyHealth == reservedTarget)
            ReleaseReservedDamage();

        enemyHealth.TakeDamage(damage, false);

        if (knockback > 0f && sourcePlayer != null)
        {
            Rigidbody body = enemyHealth.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 direction =
                    (enemyHealth.transform.position - sourcePlayer.position).normalized;
                body.AddForce(direction * knockback, ForceMode.Impulse);
            }
        }

        if (hitLockedTarget)
            StopTrackingTarget();

        TriggerExplosion(hitPosition, enemyId);

        pierceCount++;
        if (pierceCount <= maxPierce)
            return false;

        Release();
        return true;
    }


private void TriggerExplosion(Vector3 position, int primaryEnemyId)
    {
        if (hitEffectPrefab != null)
        {
            ProjectilePool.SpawnEffect(
                hitEffectPrefab,
                position,
                Quaternion.identity,
                explosionEffectLifetime);
        }

        if (explosionRadius <= 0f || explosionDamageMultiplier <= 0f)
            return;

        float splashDamage = damage * explosionDamageMultiplier;
        Collider[] nearbyColliders = Physics.OverlapSphere(
            position,
            explosionRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide);
        System.Collections.Generic.HashSet<int> splashEnemyIds =
            new System.Collections.Generic.HashSet<int>();

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            EnemyHealth nearbyEnemy = nearbyCollider.GetComponent<EnemyHealth>();
            if (nearbyEnemy == null)
                nearbyEnemy = nearbyCollider.GetComponentInParent<EnemyHealth>();
            if (nearbyEnemy == null || !nearbyEnemy.CanBeTargeted)
                continue;

            int nearbyEnemyId = nearbyEnemy.GetInstanceID();
            if (nearbyEnemyId == primaryEnemyId ||
                !splashEnemyIds.Add(nearbyEnemyId))
            {
                continue;
            }

            nearbyEnemy.TakeDamage(splashDamage, false);

            if (knockback > 0f)
            {
                Rigidbody body = nearbyEnemy.GetComponent<Rigidbody>();
                if (body != null)
                {
                    Vector3 direction =
                        (nearbyEnemy.transform.position - position).normalized;
                    body.AddForce(
                        direction * knockback * 0.5f,
                        ForceMode.Impulse);
                }
            }
        }
    }


private void StopParticles()
    {
        if (particleSystems == null)
            return;

        foreach (ParticleSystem particle in particleSystems)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }


private void RestartParticles()
    {
        if (particleSystems == null)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particleSystems)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }


private void Release()
    {
        if (isReleased)
            return;

        isReleased = true;
        ReleaseReservedDamage();

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (trailVfx != null &&
            trailVfx.TryBeginRelease(CompleteRelease))
        {
            return;
        }

        CompleteRelease();
    }


private void CompleteRelease()
    {
        if (releaseToPool != null)
            releaseToPool(this);
        else
            Destroy(gameObject);
    }


private bool HasLostHomingTarget()
    {
        if (!tracksHomingTarget)
            return false;

        if (homingTarget == null || !homingTarget.gameObject.activeInHierarchy)
            return true;

        return homingTargetHealth != null &&
            (!homingTargetHealth.isActiveAndEnabled ||
             homingTargetHealth.currentHp <= 0f);
    }


internal void ReturnToPool()
    {
        ReleaseReservedDamage();
        StopParticles();
        if (trailVfx != null)
            trailVfx.ResetForPool();
        homingTarget = null;
        homingTargetHealth = null;
        homingTargetOffset = Vector3.zero;
        tracksHomingTarget = false;
        sourcePlayer = null;
        gameObject.SetActive(false);
    }


internal void PrepareForSpawn(Vector3 position, Quaternion rotation)
    {
        isReleased = false;
        transform.SetPositionAndRotation(position, rotation);
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider != null)
            triggerCollider.enabled = true;
        gameObject.SetActive(true);
    }


internal void SetPoolRelease(Action<Projectile> releaseAction)
    {
        releaseToPool = releaseAction;
    }


private void Awake()
    {
        baseLocalScale = transform.localScale;
        triggerCollider = GetComponent<SphereCollider>();
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        trailVfx = GetComponent<ProjectileTrailVFX>();
    }

private void OnDestroy()
    {
        ReleaseReservedDamage();
    }



private void StopTrackingTarget()
    {
        ReleaseReservedDamage();
        homingTarget = null;
        homingTargetHealth = null;
        homingTargetOffset = Vector3.zero;
        tracksHomingTarget = false;
    }


private void ReleaseReservedDamage()
    {
        if (reservedTarget == null || reservedDamage <= 0f)
        {
            reservedTarget = null;
            reservedDamage = 0f;
            return;
        }

        if (IncomingDamageByEnemy.TryGetValue(
            reservedTarget,
            out float existingDamage))
        {
            float remainingDamage = existingDamage - reservedDamage;
            if (remainingDamage > 0.001f)
                IncomingDamageByEnemy[reservedTarget] = remainingDamage;
            else
                IncomingDamageByEnemy.Remove(reservedTarget);
        }

        reservedTarget = null;
        reservedDamage = 0f;
    }


private void ReserveTargetDamage()
    {
        if (homingTargetHealth == null || !homingTargetHealth.CanBeTargeted)
            return;

        float expectedDamage = homingTargetHealth.GetExpectedDamage(damage);
        if (expectedDamage <= 0f)
            return;

        reservedTarget = homingTargetHealth;
        reservedDamage = expectedDamage;

        IncomingDamageByEnemy.TryGetValue(
            reservedTarget,
            out float existingDamage);
        IncomingDamageByEnemy[reservedTarget] =
            existingDamage + reservedDamage;
    }


private bool ShouldIgnoreCollider(Collider other)
    {
        if (other == null ||
            (impactLayer & (1 << other.gameObject.layer)) == 0)
        {
            return true;
        }

        Transform otherTransform = other.transform;
        if (sourcePlayer != null &&
            (otherTransform == sourcePlayer ||
             otherTransform.IsChildOf(sourcePlayer)))
        {
            return true;
        }

        Projectile otherProjectile =
            other.GetComponentInParent<Projectile>();
        return otherProjectile != null;
    }


private bool TryImpact(Collider other, Vector3 impactPosition)
    {
        if (ShouldIgnoreCollider(other))
            return false;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth != null)
        {
            if (!enemyHealth.CanBeTargeted)
                return false;

            return TryHit(other, impactPosition);
        }

        if (other.isTrigger)
            return false;

        TriggerExplosion(impactPosition, -1);
        Release();
        return true;
    }


public static bool HasEnoughIncomingDamageToKill(EnemyHealth enemy)
    {
        if (enemy == null || !enemy.CanBeTargeted)
            return true;

        return IncomingDamageByEnemy.TryGetValue(
                enemy,
                out float incomingDamage) &&
            incomingDamage >= enemy.currentHp;
    }


[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetIncomingDamage()
    {
        IncomingDamageByEnemy.Clear();
    }
}
