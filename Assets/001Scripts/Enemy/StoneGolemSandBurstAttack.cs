using System.Collections;
using UnityEngine;

/// <summary>
/// Recreates the reference boss attack: several overlapping warning circles are
/// placed around the player, then erupt one after another.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneGolemSandBurstAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Transform target;

    [Header("Attack Timing")]
    [SerializeField, Min(0.1f)] private float cooldown = 4.5f;
    [SerializeField, Min(0f)] private float initialDelay = 1.25f;
    [SerializeField, Min(0.1f)] private float telegraphDuration = 0.9f;
    [SerializeField, Min(0f)] private float burstInterval = 0.14f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.55f;

    [Header("Attack Shape")]
    [SerializeField, Min(1)] private int burstCount = 3;
    [SerializeField, Min(0.25f)] private float burstRadius = 1.65f;
    [SerializeField, Min(0f)] private float clusterSpread = 2.15f;
    [SerializeField, Min(0.5f)] private float activationRange = 36f;

    [Header("Prediction")]
    [SerializeField, Range(0f, 1.5f)] private float predictionStrength = 0.9f;
    [SerializeField, Min(0f)] private float predictionLookAhead = 0.75f;
    [SerializeField, Min(0f)] private float maxPredictionDistance = 7f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0f)] private float knockbackForce = 10f;

    private bool isAttacking;
    private float nextAttackTime;
    private int attackSequence;
    private CharacterController targetController;
    private StoneGolemBossAttackLock attackLock;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        isAttacking = false;
        nextAttackTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (isAttacking || Time.time < nextAttackTime)
        {
            return;
        }

        ResolveReferences();
        if (target == null || enemyHealth == null || enemyHealth.currentHp <= 0f)
        {
            return;
        }

        Vector3 difference = target.position - transform.position;
        difference.y = 0f;
        if (difference.sqrMagnitude > activationRange * activationRange)
        {
            return;
        }

        if (attackLock != null && !attackLock.TryAcquire(this))
        {
            return;
        }

        StartCoroutine(AttackRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        bool wasAttacking = isAttacking;
        isAttacking = false;

        if (wasAttacking && enemyAI != null)
        {
            enemyAI.SetMovementLocked(false);
        }

        attackLock?.Release(this);
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        enemyAI?.SetMovementLocked(true);

        Vector3 targetVelocity = GetTargetVelocity();
        Vector3 targetPosition = PredictTargetPosition(
            target.position,
            targetVelocity,
            predictionLookAhead,
            maxPredictionDistance,
            predictionStrength);
        FaceTarget(targetPosition);
        PlayWindupVfx(telegraphDuration + burstInterval * burstCount);

        float baseAngle = attackSequence * 71f;
        Vector3[] offsets = BuildBurstOffsets(burstCount, clusterSpread, baseAngle);
        float damage = enemyHealth.data != null
            ? enemyHealth.AttackDamage * damageMultiplier
            : 20f * damageMultiplier;

        for (int i = 0; i < offsets.Length; i++)
        {
            float lookAhead = predictionLookAhead + burstInterval * i;
            targetVelocity = GetTargetVelocity();
            targetPosition = PredictTargetPosition(
                target.position,
                targetVelocity,
                lookAhead,
                maxPredictionDistance,
                predictionStrength);
            Vector3 burstPosition = FindGroundPosition(targetPosition + offsets[i]);

            GameObject zoneObject = new GameObject($"Sand Burst Warning {i + 1}");
            zoneObject.transform.position = burstPosition;
            StoneGolemSandBurstZone zone = zoneObject.AddComponent<StoneGolemSandBurstZone>();
            zone.Initialize(
                burstRadius,
                telegraphDuration,
                damage,
                knockbackForce,
                transform);

            if (burstInterval > 0f && i < offsets.Length - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        yield return new WaitForSeconds(telegraphDuration + recoveryDuration);

        attackSequence++;
        enemyAI?.SetMovementLocked(false);
        attackLock?.Release(this);
        nextAttackTime = Time.time + cooldown;
        isAttacking = false;
    }

    private void ResolveReferences()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (attackLock == null)
        {
            attackLock = GetComponent<StoneGolemBossAttackLock>();
        }

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                targetController = player.GetComponent<CharacterController>();
            }
        }
        else if (targetController == null)
        {
            targetController = target.GetComponent<CharacterController>();
        }
    }

    private void FaceTarget(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private static Vector3 FindGroundPosition(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * 8f,
            Vector3.down,
            20f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Vector3 result = position;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.GetComponentInParent<PlayerHealth>() != null ||
                hitCollider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            if (hits[i].normal.y >= 0.35f && hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                result = hits[i].point + Vector3.up * 0.035f;
            }
        }

        return result;
    }

    private void PlayWindupVfx(float lifetime)
    {
        GameObject windupObject = new GameObject("Sand Burst Windup");
        windupObject.transform.SetParent(transform, false);
        windupObject.transform.localPosition = Vector3.up * 1.4f;

        ParticleSystem particles = windupObject.AddComponent<ParticleSystem>();
        // A ParticleSystem added to an active GameObject starts automatically.
        // Stop it before changing duration or Unity logs an error every attack.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.72f, 0.12f, 0.95f),
            new Color(0.74f, 0.42f, 0.08f, 0.8f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 42f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.25f;
        shape.radiusThickness = 0.2f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Resources.Load<Shader>("Shaders/GoldenSandParticle");
        if (shader == null)
            shader = Shader.Find("Custom/Gigachad/Golden Sand Particle");
        if (shader != null)
        {
            Material material = new Material(shader)
            {
                name = "Runtime Sand Burst Windup Material"
            };
            material.SetFloat("_Softness", 0.28f);
            renderer.sharedMaterial = material;
            Destroy(material, lifetime + 0.3f);
        }

        particles.Play();
        Destroy(windupObject, lifetime + 0.25f);
    }

    /// <summary>Deterministic cluster pattern used by the attack and EditMode tests.</summary>
    public static Vector3[] BuildBurstOffsets(int count, float spread, float baseAngleDegrees)
    {
        int safeCount = Mathf.Max(1, count);
        Vector3[] offsets = new Vector3[safeCount];
        float baseAngle = baseAngleDegrees * Mathf.Deg2Rad;
        const float GoldenAngle = 2.39996323f;

        for (int i = 0; i < safeCount; i++)
        {
            float normalizedRadius = i == 0
                ? 0.18f
                : Mathf.Sqrt(i / (float)(safeCount - 1));
            float angle = baseAngle + i * GoldenAngle;
            offsets[i] = new Vector3(
                Mathf.Cos(angle) * spread * normalizedRadius,
                0f,
                Mathf.Sin(angle) * spread * normalizedRadius);
        }

        return offsets;
    }

    public static Vector3 PredictTargetPosition(
        Vector3 position,
        Vector3 velocity,
        float lookAhead,
        float maximumLeadDistance,
        float strength)
    {
        velocity.y = 0f;
        Vector3 lead = velocity * Mathf.Max(0f, lookAhead) * Mathf.Max(0f, strength);
        lead = Vector3.ClampMagnitude(lead, Mathf.Max(0f, maximumLeadDistance));
        return position + lead;
    }

    private Vector3 GetTargetVelocity()
    {
        if (targetController == null && target != null)
            targetController = target.GetComponent<CharacterController>();

        if (targetController == null)
            return Vector3.zero;

        Vector3 velocity = targetController.velocity;
        velocity.y = 0f;
        return velocity;
    }
}
