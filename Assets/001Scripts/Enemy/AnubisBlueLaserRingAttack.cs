using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemBossAttackLock))]
public sealed class AnubisBlueLaserRingAttack : MonoBehaviour
{
    public enum LaserPhase
    {
        Idle,
        Telegraph,
        Active
    }

    [Header("References")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private Transform beamContainer;
    [SerializeField] private LineRenderer[] beamRenderers;
    [SerializeField] private EnemyAI enemyAI;

    [Header("Pattern")]
    [SerializeField, Range(1, 32)] private int laserCount = 8;
    [SerializeField] private bool clockwise = true;
    [SerializeField, Min(0f)] private float rotationSpeed = 45f;
    [SerializeField] private float beamHeightOffset = 0.25f;
    [SerializeField, Min(0.5f)] private float range = 35f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float telegraphDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float activeDuration = 4f;
    [SerializeField, Min(0f)] private float cooldown = 7f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 7f;
    [SerializeField, Min(0.02f)] private float damageInterval = 0.25f;

    [Header("Visual")]
    [SerializeField, Min(0.001f)] private float telegraphWidth = 0.035f;
    [SerializeField, Min(0.001f)] private float activeWidth = 0.1f;
    [SerializeField] private Color telegraphColor = new Color(0.05f, 0.65f, 1f, 0.28f);
    [SerializeField] private Color activeColor = new Color(0.02f, 0.75f, 1f, 1f);

    private static Material sharedBeamMaterial;

    private StoneGolemBossAttackLock attackLock;
    private PlayerHealth targetHealth;
    private Coroutine attackRoutine;
    private float[] beamLengths;
    private float currentBaseAngle;
    private float nextReadyTime;
    private float nextDamageTime;
    private bool clockwiseThisCast;
    private bool movementLocked;

    public bool IsAttacking => Phase != LaserPhase.Idle;
    public bool IsTelegraphing => Phase == LaserPhase.Telegraph;
    public bool IsActive => Phase == LaserPhase.Active;
    public bool CanAttack => isActiveAndEnabled && !IsAttacking && Time.time >= nextReadyTime;
    public LaserPhase Phase { get; private set; }
    public int BeamCount => beamRenderers != null ? beamRenderers.Length : 0;
    public float CurrentBaseAngle => currentBaseAngle;
    public float AngleStep => 360f / Mathf.Max(1, laserCount);

    private void Awake()
    {
        attackLock = GetComponent<StoneGolemBossAttackLock>();
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();

        PrepareBeamRenderers();
        SetBeamsVisible(false);
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

    public Vector3 GetBeamDirection(int index)
    {
        float angle = currentBaseAngle + index * AngleStep;
        Vector3 basis = Quaternion.Euler(0f, angle, 0f) * transform.forward;
        basis.y = 0f;
        return basis.normalized;
    }

    public float GetBeamLength(int index)
    {
        return beamLengths != null && index >= 0 && index < beamLengths.Length
            ? beamLengths[index]
            : 0f;
    }

    private IEnumerator AttackRoutine()
    {
        Phase = LaserPhase.Telegraph;
        currentBaseAngle = 0f;
        clockwiseThisCast = clockwise;
        LockMovement();
        ConfigureBeams(telegraphWidth, telegraphColor);
        SetBeamsVisible(true);

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            UpdateBeams(false);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Phase = LaserPhase.Active;
        nextDamageTime = Time.time;
        ConfigureBeams(activeWidth, activeColor);

        elapsed = 0f;
        float directionSign = clockwiseThisCast ? 1f : -1f;
        while (elapsed < activeDuration)
        {
            currentBaseAngle = Mathf.Repeat(
                currentBaseAngle + directionSign * rotationSpeed * Time.deltaTime,
                360f);
            UpdateBeams(true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishAttack();
    }

    private void UpdateBeams(bool allowDamage)
    {
        Vector3 origin = GetOriginPosition();
        bool mayDamage = allowDamage && Time.time >= nextDamageTime;
        bool damagedThisTick = false;

        for (int i = 0; i < beamRenderers.Length; i++)
        {
            Vector3 direction = GetBeamDirection(i);
            Vector3 endpoint = origin + direction * range;
            beamLengths[i] = range;

            if (Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    range,
                    hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                endpoint = hit.point;
                beamLengths[i] = hit.distance;

                if (mayDamage && !damagedThisTick)
                {
                    PlayerHealth hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
                    if (hitPlayer != null && hitPlayer == targetHealth)
                    {
                        hitPlayer.TakeDamage(damage);
                        damagedThisTick = true;
                        nextDamageTime = Time.time + damageInterval;
                    }
                }
            }

            LineRenderer beam = beamRenderers[i];
            beam.SetPosition(0, origin);
            beam.SetPosition(1, endpoint);
        }
    }

    private Vector3 GetOriginPosition()
    {
        Vector3 position = laserOrigin != null ? laserOrigin.position : transform.position;
        return position + Vector3.up * beamHeightOffset;
    }

    private bool ResolveTarget()
    {
        if (targetHealth != null)
            return true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        targetHealth = player.GetComponent<PlayerHealth>();
        if (targetHealth == null)
            targetHealth = player.GetComponentInParent<PlayerHealth>();
        return targetHealth != null;
    }

    private void PrepareBeamRenderers()
    {
        laserCount = Mathf.Max(1, laserCount);

        if (beamContainer == null)
        {
            GameObject containerObject = new GameObject("Blue Laser Ring");
            containerObject.transform.SetParent(transform, false);
            beamContainer = containerObject.transform;
        }

        LineRenderer[] existing = beamContainer.GetComponentsInChildren<LineRenderer>(true);
        beamRenderers = new LineRenderer[laserCount];

        for (int i = 0; i < laserCount; i++)
        {
            LineRenderer beam = i < existing.Length ? existing[i] : CreateBeam(i);
            ConfigureRenderer(beam);
            beamRenderers[i] = beam;
        }

        for (int i = laserCount; i < existing.Length; i++)
            existing[i].enabled = false;

        beamLengths = new float[laserCount];
    }

    private LineRenderer CreateBeam(int index)
    {
        GameObject beamObject = new GameObject($"Blue Beam {index + 1:00}");
        beamObject.transform.SetParent(beamContainer, false);
        return beamObject.AddComponent<LineRenderer>();
    }

    private static void ConfigureRenderer(LineRenderer beam)
    {
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.alignment = LineAlignment.View;
        beam.numCapVertices = 2;
        beam.numCornerVertices = 2;
        beam.textureMode = LineTextureMode.Stretch;
        beam.sharedMaterial = GetBeamMaterial();
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
            name = "Shared Anubis Blue Laser Material",
            color = Color.white,
            renderQueue = 3000,
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedBeamMaterial;
    }

    private void ConfigureBeams(float width, Color color)
    {
        for (int i = 0; i < beamRenderers.Length; i++)
        {
            LineRenderer beam = beamRenderers[i];
            beam.startWidth = width;
            beam.endWidth = width;
            beam.startColor = color;
            beam.endColor = color;
        }
    }

    private void SetBeamsVisible(bool visible)
    {
        if (beamRenderers == null)
            return;

        for (int i = 0; i < beamRenderers.Length; i++)
        {
            if (beamRenderers[i] != null)
                beamRenderers[i].enabled = visible;
        }
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
        SetBeamsVisible(false);
        UnlockMovement();
        attackLock?.Release(this);
        Phase = LaserPhase.Idle;
        currentBaseAngle = 0f;
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
            SetBeamsVisible(false);
    }
}
