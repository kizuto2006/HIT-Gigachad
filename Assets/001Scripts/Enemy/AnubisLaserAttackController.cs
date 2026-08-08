using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemBossAttackLock))]
public sealed class AnubisLaserAttackController : MonoBehaviour
{
    public enum AbilityKind
    {
        CrimsonFanSweep,
        RotatingCrimsonRing,
        SkyfallLaserBarrage,
        SandBurstDetonation
    }

    public enum LaserPhase
    {
        Idle,
        Telegraph,
        Active,
        Recovery
    }

    private sealed class LaserSet
    {
        public readonly LineRenderer[] warning;
        public readonly LineRenderer[] glow;
        public readonly LineRenderer[] core;
        public readonly float[] lengths;

        public LaserSet(int count)
        {
            warning = new LineRenderer[count];
            glow = new LineRenderer[count];
            core = new LineRenderer[count];
            lengths = new float[count];
        }
    }

    [Header("References")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private EnemyAI enemyAI;

    [Header("Pattern")]
    [SerializeField, Range(4, 16)] private int ringBeamCount = 8;
    [SerializeField, Min(4f)] private float range = 35f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Attack Rhythm")]
    [SerializeField, Min(0f)] private float initialDelay = 1.25f;
    [SerializeField, Min(0.1f)] private float cooldownDelay = 2f;
    [SerializeField, Min(0.05f)] private float retryDelay = 0.25f;

    [Header("Crimson Fan Sweep")]
    [SerializeField, Range(3, 9)] private int fanBeamCount = 5;
    [SerializeField, Min(0.1f)] private float fanTelegraph = 1.15f;
    [SerializeField, Min(0.1f)] private float fanActive = 2.7f;
    [SerializeField, Range(10f, 180f)] private float fanSpreadAngle = 54f;
    [SerializeField, Min(0f)] private float fanSweepSpeed = 86f;
    [SerializeField, Min(0f)] private float fanDamage = 6f;
    [SerializeField, Min(0.02f)] private float fanDamageInterval = 0.25f;
    [SerializeField] private float fanHeightOffset = 0.55f;

    [Header("Rotating Crimson Ring")]
    [SerializeField, Min(0.1f)] private float ringTelegraph = 1.1f;
    [SerializeField, Min(0.1f)] private float ringActive = 3.6f;
    [SerializeField] private float ringRotationSpeed = 72f;
    [SerializeField, Min(0f)] private float ringDamage = 7f;
    [SerializeField, Min(0.02f)] private float ringDamageInterval = 0.3f;
    [SerializeField] private float ringHeightOffset = 0.3f;

    [Header("Skyfall Laser Barrage")]
    [SerializeField, Range(3, 20)] private int skyfallBeamCount = 15;
    [SerializeField, Min(0.1f)] private float skyfallWarningLead = 0.65f;
    [SerializeField, Min(0.01f)] private float skyfallShotInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float skyfallBeamLifetime = 0.18f;
    [SerializeField, Min(4f)] private float skyfallHeight = 18f;
    [SerializeField, Min(0f)] private float skyfallSpread = 5.5f;
    [SerializeField, Min(0f)] private float skyfallDamage = 9f;

    [Header("Crimson Sand Detonation")]
    [SerializeField, Min(1)] private int burstCount = 3;
    [SerializeField, Min(0.25f)] private float burstRadius = 1.65f;
    [SerializeField, Min(0f)] private float burstClusterSpread = 2.15f;
    [SerializeField, Min(0.1f)] private float burstTelegraph = 0.9f;
    [SerializeField, Min(0f)] private float burstInterval = 0.14f;
    [SerializeField, Min(0.1f)] private float burstRecovery = 1.8f;
    [SerializeField, Min(0f)] private float burstDamage = 12f;
    [SerializeField, Min(0f)] private float burstKnockback = 10f;

    [Header("Layered VFX")]
    [SerializeField, Min(0.001f)] private float warningWidth = 0.075f;
    [SerializeField, Min(0.001f)] private float glowWidth = 0.16f;
    [SerializeField, Min(0.001f)] private float coreWidth = 0.055f;
    [SerializeField] private Color warningRed = new Color(1f, 0.015f, 0.005f, 0.3f);
    [SerializeField] private Color warningAmber = new Color(1f, 0.22f, 0.01f, 0.48f);
    [SerializeField] private Color warningWhite = new Color(1f, 0.78f, 0.5f, 0.9f);
    [SerializeField] private Color activeOuterRed = new Color(0.62f, 0f, 0.008f, 0.2f);
    [SerializeField] private Color activeRed = new Color(1f, 0.01f, 0.005f, 0.7f);
    [SerializeField] private Color activeCore = new Color(1f, 0.75f, 0.48f, 1f);
    [SerializeField] private Color prismaticAccent = new Color(0.5f, 0.08f, 1f, 0.85f);

    private static Material sharedBeamMaterial;

    private StoneGolemBossAttackLock attackLock;
    private Transform target;
    private PlayerHealth targetHealth;
    private Collider targetCollider;
    private Transform vfxRoot;
    private LaserSet fanSet;
    private LaserSet ringSet;
    private LaserSet skyfallSet;
    private Coroutine attackRoutine;
    private float nextDamageTime;
    private float nextReadyTime;
    private bool movementLocked;
    private bool hasLastAbility;
    private AbilityKind lastAbility;

    public LaserPhase Phase { get; private set; }
    public AbilityKind LastAbility => lastAbility;
    public bool IsAttacking => Phase != LaserPhase.Idle;
    public bool IsTelegraphing => Phase == LaserPhase.Telegraph;
    public bool IsActive => Phase == LaserPhase.Active;

    private void Awake()
    {
        attackLock = GetComponent<StoneGolemBossAttackLock>();
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();
        if (laserOrigin == null)
            laserOrigin = transform;

        CreateLayeredVfx();
        SetAllVisible(false);
    }

    private void OnEnable()
    {
        if (attackRoutine == null)
            attackRoutine = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        SetAllVisible(false);
        UnlockMovement();
        if (attackLock != null)
            attackLock.Release(this);
        Phase = LaserPhase.Idle;
    }

    private IEnumerator AttackLoop()
    {
        Phase = LaserPhase.Idle;
        yield return new WaitForSeconds(initialDelay);

        while (isActiveAndEnabled)
        {
            if (!ResolveTarget())
            {
                yield return new WaitForSeconds(retryDelay);
                continue;
            }

            AbilityKind ability = ChooseNextAbility();
            if (attackLock != null && !attackLock.TryAcquire(this))
            {
                yield return new WaitForSeconds(retryDelay);
                continue;
            }

            switch (ability)
            {
                case AbilityKind.CrimsonFanSweep:
                    yield return CrimsonFanSweepRoutine();
                    break;
                case AbilityKind.RotatingCrimsonRing:
                    yield return RotatingRingRoutine();
                    break;
                case AbilityKind.SkyfallLaserBarrage:
                    yield return SkyfallLaserBarrageRoutine();
                    break;
                default:
                    yield return SandBurstDetonationRoutine();
                    break;
            }

            FinishAbility();
            Phase = LaserPhase.Recovery;
            yield return new WaitForSeconds(cooldownDelay);
            Phase = LaserPhase.Idle;
        }
    }

    private AbilityKind ChooseNextAbility()
    {
        int value = Random.Range(0, 4);
        if (hasLastAbility && value == (int)lastAbility)
            value = (value + Random.Range(1, 4)) % 4;

        lastAbility = (AbilityKind)value;
        hasLastAbility = true;
        return lastAbility;
    }

    private IEnumerator CrimsonFanSweepRoutine()
    {
        Phase = LaserPhase.Telegraph;
        LockMovement();

        float halfSpread = fanSpreadAngle * 0.5f;
        float baseAngle = GetTargetAngle() - halfSpread;
        float angleStep = fanBeamCount > 1 ? fanSpreadAngle / (fanBeamCount - 1) : 0f;
        float sweepSign = Random.value < 0.5f ? -1f : 1f;

        ConfigureTelegraph(fanSet);
        SetVisible(fanSet, true);

        float elapsed = 0f;
        while (elapsed < fanTelegraph)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(elapsed * 18f);
            ApplyTelegraphPulse(fanSet, pulse);
            UpdatePattern(
                fanSet,
                baseAngle,
                angleStep,
                GetOriginPosition(fanHeightOffset),
                false,
                fanDamage,
                fanDamageInterval,
                true);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Phase = LaserPhase.Active;
        ConfigureActive(fanSet, true);
        nextDamageTime = Time.time;
        elapsed = 0f;

        while (elapsed < fanActive)
        {
            baseAngle += sweepSign * fanSweepSpeed * Time.deltaTime;
            UpdatePattern(
                fanSet,
                baseAngle,
                angleStep,
                GetOriginPosition(fanHeightOffset),
                true,
                fanDamage,
                fanDamageInterval,
                true);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator RotatingRingRoutine()
    {
        Phase = LaserPhase.Telegraph;
        LockMovement();

        float baseAngle = GetTargetAngle();
        ConfigureTelegraph(ringSet);
        SetVisible(ringSet, true);

        float elapsed = 0f;
        while (elapsed < ringTelegraph)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(elapsed * 16f);
            ApplyTelegraphPulse(ringSet, pulse);
            UpdatePattern(ringSet, baseAngle, 360f / ringBeamCount, GetOriginPosition(ringHeightOffset), false, ringDamage, ringDamageInterval, false);
            baseAngle += 18f * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Phase = LaserPhase.Active;
        ConfigureActive(ringSet, false);
        nextDamageTime = Time.time;
        elapsed = 0f;
        while (elapsed < ringActive)
        {
            baseAngle += ringRotationSpeed * Time.deltaTime;
            UpdatePattern(ringSet, baseAngle, 360f / ringBeamCount, GetOriginPosition(ringHeightOffset), true, ringDamage, ringDamageInterval, false);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator SkyfallLaserBarrageRoutine()
    {
        Phase = LaserPhase.Telegraph;
        LockMovement();

        LaserSet set = skyfallSet;
        ConfigureActive(set, true);
        SetVisible(set, false);

        Vector3 center = FindGroundPosition(GetTargetPoint());
        Vector3[] offsets = StoneGolemSandBurstAttack.BuildBurstOffsets(
            skyfallBeamCount,
            skyfallSpread,
            transform.eulerAngles.y + Time.time * 37f);
        Vector3[] groundPositions = new Vector3[offsets.Length];

        for (int i = 0; i < groundPositions.Length; i++)
        {
            groundPositions[i] = FindGroundPosition(center + offsets[i]);
            SetSkyfallBeamPositions(set, i, groundPositions[i]);
            ConfigureSkyfallTelegraphBeam(set, i);
        }

        ShowSkyfallWarning(set, 0);
        yield return new WaitForSeconds(skyfallWarningLead);

        Phase = LaserPhase.Active;
        for (int i = 0; i < groundPositions.Length; i++)
        {
            if (i + 1 < groundPositions.Length)
            {
                ShowSkyfallWarning(set, i + 1);
                yield return new WaitForSeconds(skyfallShotInterval);
            }

            FireSkyfallBeam(set, i, groundPositions[i]);
        }

        yield return new WaitForSeconds(skyfallBeamLifetime);
    }


    private IEnumerator SandBurstDetonationRoutine()
    {
        Phase = LaserPhase.Telegraph;
        LockMovement();

        Vector3 burstCenter = FindGroundPosition(GetTargetPoint());
        Vector3[] offsets = StoneGolemSandBurstAttack.BuildBurstOffsets(
            burstCount,
            burstClusterSpread,
            transform.eulerAngles.y + Time.time * 23f);

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject zoneObject = new GameObject($"Anubis Crimson Burst Warning {i + 1}");
            zoneObject.transform.position = FindGroundPosition(burstCenter + offsets[i]);

            StoneGolemSandBurstZone zone = zoneObject.AddComponent<StoneGolemSandBurstZone>();
            zone.Initialize(
                burstRadius,
                burstTelegraph,
                burstDamage,
                burstKnockback,
                transform);

            if (burstInterval > 0f && i < offsets.Length - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        yield return new WaitForSeconds(burstTelegraph);
        Phase = LaserPhase.Active;
        yield return new WaitForSeconds(burstRecovery);
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
            if (hitCollider == null ||
                hitCollider.GetComponentInParent<PlayerHealth>() != null ||
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




    private void UpdatePattern(
        LaserSet set,
        float baseAngle,
        float angleStep,
        Vector3 origin,
        bool allowDamage,
        float damageAmount,
        float damageInterval,
        bool usePrismaticAccent)
    {
        bool damageTicked = false;
        for (int i = 0; i < set.core.Length; i++)
        {
            float angle = baseAngle + i * angleStep;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            direction.y = 0f;
            direction.Normalize();
            UpdateBeam(set, i, origin, direction, allowDamage && !damageTicked, damageAmount, damageInterval, usePrismaticAccent, i);
            if (allowDamage && Time.time < nextDamageTime)
                damageTicked = true;
        }
    }



    private void UpdateBeam(
        LaserSet set,
        int index,
        Vector3 origin,
        Vector3 direction,
        bool allowDamage,
        float damageAmount,
        float damageInterval,
        bool usePrismaticAccent,
        int colorIndex)
    {
        Vector3 endpoint = origin + direction * range;
        set.lengths[index] = range;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            endpoint = hit.point;
            set.lengths[index] = hit.distance;

            if (allowDamage && Time.time >= nextDamageTime)
            {
                PlayerHealth hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
                if (hitPlayer != null && hitPlayer == targetHealth)
                {
                    hitPlayer.TakeDamage(damageAmount);
                    nextDamageTime = Time.time + damageInterval;
                }
            }
        }

        SetBeamPositions(set, index, origin, endpoint);
        if (usePrismaticAccent && colorIndex % 2 == 1)
        {
            Color accent = prismaticAccent;
            accent.a = Phase == LaserPhase.Active ? 0.85f : 0.55f;
            SetLayerColor(set.glow[index], accent);
        }
    }



    private Vector3 GetTargetPoint()
    {
        if (targetCollider != null)
            return targetCollider.bounds.center;
        return target != null ? target.position + Vector3.up : transform.position + transform.forward;
    }

    private float GetTargetAngle()
    {
        Vector3 flat = GetTargetPoint() - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            flat = transform.forward;
        return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
    }

    private Vector3 GetOriginPosition(float heightOffset)
    {
        Vector3 origin = laserOrigin != null ? laserOrigin.position : transform.position;
        return origin + Vector3.up * heightOffset;
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

    private bool HasTarget()
    {
        return target != null && targetHealth != null;
    }

private void CreateLayeredVfx()
    {
        vfxRoot = new GameObject("Anubis Layered Laser VFX").transform;
        vfxRoot.SetParent(transform, false);
        fanSet = CreateLaserSet("Crimson Fan Sweep", fanBeamCount);
        ringSet = CreateLaserSet("Rotating Crimson Ring", ringBeamCount);
        skyfallSet = CreateLaserSet("Skyfall Laser Barrage", skyfallBeamCount);
    }

    private LaserSet CreateLaserSet(string setName, int count)
    {
        LaserSet set = new LaserSet(Mathf.Max(1, count));
        Transform setRoot = new GameObject(setName).transform;
        setRoot.SetParent(vfxRoot, false);

        for (int i = 0; i < set.core.Length; i++)
        {
            set.warning[i] = CreateLine(setRoot, setName + " Warning " + i, 10);
            set.glow[i] = CreateLine(setRoot, setName + " Glow " + i, 11);
            set.core[i] = CreateLine(setRoot, setName + " Core " + i, 12);
        }

        return set;
    }

    private static LineRenderer CreateLine(Transform parent, string name, int sortingOrder)
    {
        GameObject beamObject = new GameObject(name);
        beamObject.transform.SetParent(parent, false);
        LineRenderer line = beamObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = sortingOrder;
        line.sharedMaterial = GetBeamMaterial();
        line.enabled = false;
        return line;
    }

    private static Material GetBeamMaterial()
    {
        if (sharedBeamMaterial != null)
            return sharedBeamMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        sharedBeamMaterial = new Material(shader);
        sharedBeamMaterial.name = "Shared Anubis Layered Laser Material";
        sharedBeamMaterial.color = Color.white;
        sharedBeamMaterial.renderQueue = 3000;
        sharedBeamMaterial.hideFlags = HideFlags.HideAndDontSave;
        return sharedBeamMaterial;
    }

    private void ConfigureTelegraph(LaserSet set)
    {
        for (int i = 0; i < set.core.Length; i++)
        {
            set.warning[i].startWidth = warningWidth;
            set.warning[i].endWidth = warningWidth;
            set.glow[i].startWidth = glowWidth * 0.55f;
            set.glow[i].endWidth = glowWidth * 0.55f;
            set.core[i].startWidth = coreWidth * 0.7f;
            set.core[i].endWidth = coreWidth * 0.7f;
            SetLayerColor(set.warning[i], warningRed);
            SetLayerColor(set.glow[i], warningAmber);
            SetLayerColor(set.core[i], warningWhite);
        }
    }

    private void ConfigureActive(LaserSet set, bool usePrismaticCore)
    {
        for (int i = 0; i < set.core.Length; i++)
        {
            set.warning[i].startWidth = warningWidth * 2.25f;
            set.warning[i].endWidth = warningWidth * 2.25f;
            set.glow[i].startWidth = glowWidth;
            set.glow[i].endWidth = glowWidth;
            set.core[i].startWidth = coreWidth;
            set.core[i].endWidth = coreWidth;
            SetLayerColor(set.warning[i], activeOuterRed);
            SetLayerColor(set.glow[i], activeRed);
            SetLayerColor(set.core[i], usePrismaticCore && i % 2 == 1 ? new Color(0.8f, 0.35f, 1f, 1f) : activeCore);
        }
    }

    private void ConfigureSkyfallTelegraphBeam(LaserSet set, int index)
    {
        set.warning[index].startWidth = warningWidth * 1.35f;
        set.warning[index].endWidth = warningWidth * 1.35f;
        set.glow[index].startWidth = glowWidth * 0.6f;
        set.glow[index].endWidth = glowWidth * 0.6f;
        set.core[index].startWidth = coreWidth * 0.8f;
        set.core[index].endWidth = coreWidth * 0.8f;
        SetLayerColor(set.warning[index], warningRed);
        SetLayerColor(set.glow[index], warningAmber);
        SetLayerColor(set.core[index], warningWhite);
    }

    private void ConfigureSkyfallActiveBeam(LaserSet set, int index)
    {
        set.warning[index].startWidth = warningWidth * 2.25f;
        set.warning[index].endWidth = warningWidth * 2.25f;
        set.glow[index].startWidth = glowWidth;
        set.glow[index].endWidth = glowWidth;
        set.core[index].startWidth = coreWidth;
        set.core[index].endWidth = coreWidth;
        SetLayerColor(set.warning[index], activeOuterRed);
        SetLayerColor(set.glow[index], activeRed);
        SetLayerColor(
            set.core[index],
            index % 2 == 1 ? new Color(0.8f, 0.35f, 1f, 1f) : activeCore);
    }

    private void SetSkyfallBeamPositions(LaserSet set, int index, Vector3 groundPosition)
    {
        Vector3 origin = groundPosition + Vector3.up * skyfallHeight;
        Vector3 endpoint = groundPosition + Vector3.up * 0.04f;
        SetBeamPositions(set, index, origin, endpoint);
    }

    private void ShowSkyfallWarning(LaserSet set, int index)
    {
        ConfigureSkyfallTelegraphBeam(set, index);
        SetSkyfallBeamEnabled(set, index, true);
    }

    private void FireSkyfallBeam(LaserSet set, int index, Vector3 groundPosition)
    {
        ConfigureSkyfallActiveBeam(set, index);
        SetSkyfallBeamPositions(set, index, groundPosition);
        set.warning[index].enabled = false;
        set.glow[index].enabled = true;
        set.core[index].enabled = true;

        Vector3 origin = groundPosition + Vector3.up * skyfallHeight;
        float rayDistance = skyfallHeight + 1f;
        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                hitMask,
                QueryTriggerInteraction.Ignore))
        {
            PlayerHealth hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hitPlayer != null && hitPlayer == targetHealth)
                hitPlayer.TakeDamage(skyfallDamage);
        }

        StartCoroutine(HideSkyfallBeamAfter(set, index, skyfallBeamLifetime));
    }

    private static void SetSkyfallBeamEnabled(LaserSet set, int index, bool visible)
    {
        set.warning[index].enabled = visible;
        set.glow[index].enabled = visible;
        set.core[index].enabled = visible;
    }

    private IEnumerator HideSkyfallBeamAfter(LaserSet set, int index, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        if (set != null)
            SetSkyfallBeamEnabled(set, index, false);
    }








    private void ApplyTelegraphPulse(LaserSet set, float pulse)
    {
        for (int i = 0; i < set.core.Length; i++)
        {
            Color red = warningRed;
            red.a *= pulse;
            Color amber = warningAmber;
            amber.a *= pulse;
            Color white = warningWhite;
            white.a = Mathf.Lerp(0.4f, 1f, pulse);
            SetLayerColor(set.warning[i], red);
            SetLayerColor(set.glow[i], amber);
            SetLayerColor(set.core[i], white);
        }
    }

    private static void SetLayerColor(LineRenderer renderer, Color color)
    {
        renderer.startColor = color;
        renderer.endColor = color;
    }

    private static void SetBeamPositions(LaserSet set, int index, Vector3 origin, Vector3 endpoint)
    {
        set.warning[index].SetPosition(0, origin);
        set.warning[index].SetPosition(1, endpoint);
        set.glow[index].SetPosition(0, origin);
        set.glow[index].SetPosition(1, endpoint);
        set.core[index].SetPosition(0, origin);
        set.core[index].SetPosition(1, endpoint);
    }

private void SetAllVisible(bool visible)
    {
        SetVisible(fanSet, visible);
        SetVisible(ringSet, visible);
        SetVisible(skyfallSet, visible);
    }

    private static void SetVisible(LaserSet set, bool visible)
    {
        if (set == null)
            return;

        for (int i = 0; i < set.core.Length; i++)
        {
            set.warning[i].enabled = visible;
            set.glow[i].enabled = visible;
            set.core[i].enabled = visible;
        }
    }

    private void FinishAbility()
    {
        SetAllVisible(false);
        UnlockMovement();
        if (attackLock != null)
            attackLock.Release(this);
        nextDamageTime = 0f;
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

        if (enemyAI != null)
            enemyAI.SetMovementLocked(false);
        movementLocked = false;
    }
}
