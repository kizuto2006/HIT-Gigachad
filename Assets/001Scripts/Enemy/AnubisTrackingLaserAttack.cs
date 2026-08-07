using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemBossAttackLock))]
public sealed class AnubisTrackingLaserAttack : MonoBehaviour
{
    public enum LaserPhase
    {
        Idle,
        Telegraph,
        Active
    }

    [Header("References")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private LineRenderer beamRenderer;
    [SerializeField] private EnemyAI enemyAI;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float telegraphDuration = 1f;
    [SerializeField, Min(0.1f)] private float activeDuration = 2.5f;
    [SerializeField, Min(0f)] private float cooldown = 6f;

    [Header("Tracking")]
    [SerializeField, Min(0f)] private float trackingDegreesPerSecond = 14.5f;
    [SerializeField, Min(0.5f)] private float range = 35f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 8f;
    [SerializeField, Min(0.02f)] private float damageInterval = 0.2f;

    [Header("Visual")]
    [SerializeField, Min(0.001f)] private float telegraphWidth = 0.035f;
    [SerializeField, Min(0.001f)] private float activeWidth = 0.12f;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.05f, 0.02f, 0.28f);
    [SerializeField] private Color activeColor = new Color(1f, 0.015f, 0.005f, 1f);

    private static Material sharedBeamMaterial;

    private StoneGolemBossAttackLock attackLock;
    private Transform target;
    private PlayerHealth targetHealth;
    private Collider targetCollider;
    private Coroutine attackRoutine;
    private Vector3 currentDirection = Vector3.forward;
    private float currentBeamLength;
    private float nextReadyTime;
    private float nextDamageTime;
    private bool movementLocked;

    public bool IsAttacking => Phase != LaserPhase.Idle;
    public bool IsTelegraphing => Phase == LaserPhase.Telegraph;
    public bool IsActive => Phase == LaserPhase.Active;
    public bool CanAttack => isActiveAndEnabled && !IsAttacking && Time.time >= nextReadyTime;
    public LaserPhase Phase { get; private set; }
    public Vector3 CurrentDirection => currentDirection;
    public float CurrentBeamLength => currentBeamLength;

    private void Awake()
    {
        attackLock = GetComponent<StoneGolemBossAttackLock>();
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();

        PrepareBeamRenderer();
        SetBeamVisible(false);
    }

    public bool TryStartAttack()
    {
        if (!CanAttack || !ResolveTarget())
            return false;

        if (attackLock != null && !attackLock.TryAcquire(this))
            return false;

        attackRoutine = StartCoroutine(AttackRoutine());
        return true;
    }

    private IEnumerator AttackRoutine()
    {
        Phase = LaserPhase.Telegraph;
        LockMovement();

        Vector3 desiredDirection = GetDesiredDirection();
        if (desiredDirection.sqrMagnitude > 0.0001f)
            currentDirection = desiredDirection.normalized;

        ConfigureBeam(telegraphWidth, telegraphColor);
        SetBeamVisible(true);

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            if (target == null)
            {
                FinishAttack();
                yield break;
            }

            TrackTarget();
            UpdateBeam(false);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Phase = LaserPhase.Active;
        nextDamageTime = Time.time;
        ConfigureBeam(activeWidth, activeColor);

        elapsed = 0f;
        while (elapsed < activeDuration)
        {
            if (target == null)
            {
                FinishAttack();
                yield break;
            }

            TrackTarget();
            UpdateBeam(true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishAttack();
    }

    private void TrackTarget()
    {
        Vector3 desiredDirection = GetDesiredDirection();
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion currentRotation = Quaternion.LookRotation(currentDirection, Vector3.up);
        Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        Quaternion limitedRotation = Quaternion.RotateTowards(
            currentRotation,
            desiredRotation,
            trackingDegreesPerSecond * Time.deltaTime);
        currentDirection = (limitedRotation * Vector3.forward).normalized;
    }

    private void UpdateBeam(bool allowDamage)
    {
        Vector3 origin = GetOriginPosition();
        Vector3 endpoint = origin + currentDirection * range;
        currentBeamLength = range;

        if (Physics.Raycast(
                origin,
                currentDirection,
                out RaycastHit hit,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore))
        {
            endpoint = hit.point;
            currentBeamLength = hit.distance;

            if (allowDamage && Time.time >= nextDamageTime)
            {
                PlayerHealth hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
                if (hitPlayer != null && hitPlayer == targetHealth)
                {
                    nextDamageTime = Time.time + damageInterval;
                    hitPlayer.TakeDamage(damage);
                }
            }
        }

        beamRenderer.SetPosition(0, origin);
        beamRenderer.SetPosition(1, endpoint);
    }

    private Vector3 GetDesiredDirection()
    {
        return target == null
            ? currentDirection
            : GetTargetPoint() - GetOriginPosition();
    }

    private Vector3 GetTargetPoint()
    {
        return targetCollider != null ? targetCollider.bounds.center : target.position + Vector3.up;
    }

    private Vector3 GetOriginPosition()
    {
        return laserOrigin != null ? laserOrigin.position : transform.position + Vector3.up;
    }

    private bool ResolveTarget()
    {
        if (targetHealth != null && target != null)
            return true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        targetHealth = player.GetComponent<PlayerHealth>();
        if (targetHealth == null)
            targetHealth = player.GetComponentInParent<PlayerHealth>();
        if (targetHealth == null)
            return false;

        target = targetHealth.transform;
        targetCollider = player.GetComponent<Collider>();
        if (targetCollider == null)
            targetCollider = player.GetComponentInChildren<Collider>();
        return true;
    }

    private void PrepareBeamRenderer()
    {
        if (beamRenderer == null)
        {
            GameObject beamObject = new GameObject("Red Tracking Beam");
            beamObject.transform.SetParent(transform, false);
            beamRenderer = beamObject.AddComponent<LineRenderer>();
        }

        beamRenderer.useWorldSpace = true;
        beamRenderer.positionCount = 2;
        beamRenderer.alignment = LineAlignment.View;
        beamRenderer.numCapVertices = 2;
        beamRenderer.numCornerVertices = 2;
        beamRenderer.textureMode = LineTextureMode.Stretch;
        beamRenderer.sharedMaterial = GetBeamMaterial();
    }

    private static Material GetBeamMaterial()
    {
        if (sharedBeamMaterial != null)
            return sharedBeamMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        sharedBeamMaterial = new Material(shader)
        {
            name = "Shared Anubis Red Laser Material",
            color = Color.white,
            renderQueue = 3000,
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedBeamMaterial;
    }

    private void ConfigureBeam(float width, Color color)
    {
        beamRenderer.startWidth = width;
        beamRenderer.endWidth = width;
        beamRenderer.startColor = color;
        beamRenderer.endColor = color;
    }

    private void SetBeamVisible(bool visible)
    {
        if (beamRenderer != null)
            beamRenderer.enabled = visible;
    }

    private void LockMovement()
    {
        if (enemyAI == null)
            return;

        enemyAI.SetMovementLocked(true);
        movementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!movementLocked)
            return;

        enemyAI?.SetMovementLocked(false);
        movementLocked = false;
    }

    private void FinishAttack()
    {
        SetBeamVisible(false);
        UnlockMovement();
        attackLock?.Release(this);
        Phase = LaserPhase.Idle;
        nextDamageTime = 0f;
        nextReadyTime = Time.time + cooldown;
        attackRoutine = null;
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        if (IsAttacking)
            FinishAttack();
        else
            SetBeamVisible(false);
    }
}
