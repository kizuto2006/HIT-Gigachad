using System.Collections;
using UnityEngine;

/// <summary>
/// A jump-readable boss attack: telegraphs several final radii, then sends
/// expanding ground shockwaves. A player above the safe height avoids damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemBossAttackLock))]
public sealed class StoneGolemSeismicRingAttack : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float cooldown = 8f;
    [SerializeField, Min(0f)] private float initialDelay = 4f;
    [SerializeField, Min(0.1f)] private float telegraphDuration = 0.8f;
    [SerializeField, Min(0.05f)] private float pulseTravelDuration = 0.42f;
    [SerializeField, Min(0f)] private float pulseInterval = 0.22f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.65f;

    [Header("Shape")]
    [SerializeField, Min(1)] private int pulseCount = 3;
    [SerializeField, Min(1f)] private float firstRadius = 4f;
    [SerializeField, Min(0.5f)] private float radiusStep = 3f;
    [SerializeField, Min(0.1f)] private float ringWidth = 0.85f;
    [SerializeField, Min(0.5f)] private float activationRange = 36f;
    [SerializeField, Min(0f)] private float safeJumpHeight = 0.9f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damageMultiplier = 0.7f;
    [SerializeField, Min(0f)] private float knockbackForce = 8f;

    private EnemyAI enemyAI;
    private EnemyHealth enemyHealth;
    private Transform target;
    private StoneGolemBossAttackLock attackLock;
    private float nextAttackTime;
    private bool isAttacking;
    private Material telegraphMaterial;

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
        if (PlayerPowerupController.AreEnemyActionsFrozen)
            return;

        if (isAttacking || Time.time < nextAttackTime)
            return;

        ResolveReferences();
        if (target == null || enemyHealth == null || enemyHealth.currentHp <= 0f)
            return;

        Vector3 difference = target.position - transform.position;
        difference.y = 0f;
        if (difference.sqrMagnitude > activationRange * activationRange)
            return;

        if (attackLock != null && !attackLock.TryAcquire(this))
            return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        enemyAI?.SetMovementLocked(true);
        FaceTarget();

        GameObject telegraph = CreateTelegraph();
        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            if (PlayerPowerupController.AreEnemyActionsFrozen)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float pulse = 0.75f + Mathf.Sin(elapsed * 18f) * 0.25f;
            LineRenderer[] lines = telegraph.GetComponentsInChildren<LineRenderer>();
            for (int i = 0; i < lines.Length; i++)
                lines[i].widthMultiplier = 0.08f + pulse * 0.06f;
            yield return null;
        }

        Destroy(telegraph);

        float damage = enemyHealth.data != null
            ? enemyHealth.AttackDamage * damageMultiplier
            : 20f * damageMultiplier;

        for (int i = 0; i < pulseCount; i++)
        {
            float radius = firstRadius + radiusStep * i;
            yield return ExpandShockwave(radius, damage);
            if (i < pulseCount - 1 && pulseInterval > 0f)
                yield return new WaitForSeconds(pulseInterval);
        }

        yield return new WaitForSeconds(recoveryDuration);
        FinishAttack();
    }

    private IEnumerator ExpandShockwave(float finalRadius, float damage)
    {
        LineRenderer ring = CreateRing(
            $"Seismic Shockwave {finalRadius:0.0}",
            new Color(1f, 0.62f, 0.08f, 0.95f),
            ringWidth);

        float elapsed = 0f;
        float previousRadius = 0.15f;
        bool damagedPlayer = false;

        while (elapsed < pulseTravelDuration)
        {
            if (PlayerPowerupController.AreEnemyActionsFrozen)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / pulseTravelDuration);
            float currentRadius = Mathf.Lerp(0.15f, finalRadius, progress);
            SetRingRadius(ring, currentRadius);

            if (!damagedPlayer && target != null)
            {
                Vector3 difference = target.position - transform.position;
                float verticalHeight = difference.y;
                difference.y = 0f;
                float playerDistance = difference.magnitude;
                float halfWidth = ringWidth * 0.5f;

                bool crossedRing =
                    playerDistance >= previousRadius - halfWidth &&
                    playerDistance <= currentRadius + halfWidth;
                bool lowEnough = verticalHeight <= safeJumpHeight;
                if (crossedRing && lowEnough)
                {
                    ApplyDamage(damage, difference);
                    damagedPlayer = true;
                }
            }

            Color color = ring.startColor;
            color.a = Mathf.Lerp(1f, 0.25f, progress);
            ring.startColor = color;
            ring.endColor = color;
            previousRadius = currentRadius;
            yield return null;
        }

        Destroy(ring.gameObject);
    }

    private void ApplyDamage(float damage, Vector3 knockbackDirection)
    {
        if (PlayerPowerupController.AreEnemyActionsFrozen)
            return;

        if (target == null)
            return;

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = target.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        playerHealth.TakeDamage(damage);

        PlayerSimpleMovement movement = playerHealth.GetComponent<PlayerSimpleMovement>();
        if (movement != null && knockbackDirection.sqrMagnitude > 0.001f)
            movement.ApplyKnockback(knockbackDirection.normalized * knockbackForce);
    }

    private GameObject CreateTelegraph()
    {
        GameObject root = new GameObject("Seismic Rings Telegraph");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.up * 0.045f;

        for (int i = 0; i < pulseCount; i++)
        {
            float radius = firstRadius + radiusStep * i;
            LineRenderer ring = CreateRing(
                $"Warning Ring {i + 1}",
                new Color(1f, 0.08f, 0.015f, 0.72f),
                0.12f,
                root.transform);
            SetRingRadius(ring, radius);
        }

        return root;
    }

    private LineRenderer CreateRing(
        string objectName,
        Color color,
        float width,
        Transform parent = null)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(parent != null ? parent : transform, false);
        ringObject.transform.localPosition = Vector3.up * 0.055f;

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 96;
        ring.widthMultiplier = width;
        ring.numCornerVertices = 2;
        ring.numCapVertices = 2;
        ring.startColor = color;
        ring.endColor = color;
        ring.sortingOrder = 40;
        ring.sharedMaterial = CreateTelegraphMaterial();
        return ring;
    }

    private static void SetRingRadius(LineRenderer ring, float radius)
    {
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
        }
    }

    private Material CreateTelegraphMaterial()
    {
        if (telegraphMaterial != null)
            return telegraphMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/BossTelegraph");
        if (shader == null)
            shader = Shader.Find("Custom/Gigachad/Boss Telegraph");

        telegraphMaterial = new Material(shader)
        {
            name = "Runtime Boss Telegraph Material"
        };
        telegraphMaterial.SetColor("_Color", Color.white);
        return telegraphMaterial;
    }

    private void ResolveReferences()
    {
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
        if (attackLock == null)
            attackLock = GetComponent<StoneGolemBossAttackLock>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void FinishAttack()
    {
        enemyAI?.SetMovementLocked(false);
        attackLock?.Release(this);
        nextAttackTime = Time.time + cooldown;
        isAttacking = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (isAttacking)
            enemyAI?.SetMovementLocked(false);
        attackLock?.Release(this);
        isAttacking = false;
    }

    private void OnDestroy()
    {
        if (telegraphMaterial != null)
            Destroy(telegraphMaterial);
    }
}
