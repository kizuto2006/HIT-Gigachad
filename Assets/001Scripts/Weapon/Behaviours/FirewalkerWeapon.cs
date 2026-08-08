using System.Collections.Generic;
using UnityEngine;

public sealed class FirewalkerWeapon : WeaponBehaviour
{
    private struct PositionSample
    {
        public float time;
        public Vector3 position;

        public PositionSample(float sampleTime, Vector3 samplePosition)
        {
            time = sampleTime;
            position = samplePosition;
        }
    }

    [SerializeField, Min(0.01f)] private float delayedPositionSeconds = 0.2f;
    [SerializeField, Min(0f)] private float movementThreshold = 0.01f;
    private readonly List<PositionSample> positionHistory = new List<PositionSample>(32);
    private bool movingThisFrame;

    protected override void Update()
    {
        RecordCurrentPosition();
        base.Update();
    }

    private void LateUpdate()
    {
        RecordCurrentPosition();
    }

public override void Attack()
    {
        if (playerTransform == null || data == null)
            return;

        Vector3 currentPosition = playerTransform.position;
        Vector3 spawnPosition = movingThisFrame
            ? GetDelayedPosition(Time.time - delayedPositionSeconds)
            : currentPosition;

        GameObject patchObject = new GameObject();
        FirewalkerFirePatch patch = patchObject.AddComponent<FirewalkerFirePatch>();
        patch.Initialize(
            spawnPosition,
            GetFinalDamage(),
            GetFinalSize(),
            GetFinalDuration(),
            data.hitInterval,
            GetFinalKnockback(),
            GetFinalCritChance(),
            playerStats != null ? playerStats.FinalCriticalDamageMultiplier : 2f,
            data.attackEffectPrefab,
            playerTransform);

        PlayWeaponAttackSound(spawnPosition);
    }

    private void RecordCurrentPosition()
    {
        if (playerTransform == null)
            return;

        Vector3 currentPosition = playerTransform.position;
        if (positionHistory.Count > 0)
        {
            Vector3 delta = currentPosition - positionHistory[positionHistory.Count - 1].position;
            delta.y = 0f;
            movingThisFrame = delta.sqrMagnitude > movementThreshold * movementThreshold;
        }
        else
        {
            movingThisFrame = false;
        }

        float sampleTime = Time.time;
        if (positionHistory.Count > 0 &&
            Mathf.Approximately(positionHistory[positionHistory.Count - 1].time, sampleTime))
        {
            PositionSample latest = positionHistory[positionHistory.Count - 1];
            latest.position = currentPosition;
            positionHistory[positionHistory.Count - 1] = latest;
        }
        else
        {
            positionHistory.Add(new PositionSample(sampleTime, currentPosition));
        }

        float oldestAllowedTime = sampleTime - delayedPositionSeconds - 0.35f;
        while (positionHistory.Count > 2 && positionHistory[0].time < oldestAllowedTime)
            positionHistory.RemoveAt(0);
    }

    private Vector3 GetDelayedPosition(float targetTime)
    {
        if (positionHistory.Count == 0 || playerTransform == null)
            return playerTransform != null ? playerTransform.position : transform.position;

        for (int i = positionHistory.Count - 1; i >= 0; i--)
        {
            if (positionHistory[i].time <= targetTime)
                return positionHistory[i].position;
        }

        return positionHistory[0].position;
    }
}

public sealed class FirewalkerFirePatch : MonoBehaviour
{
    private const float HoldBeforeShrinkSeconds = 0.25f;
    private const float DefaultRotationSpeed = 42f;

    private sealed class LineLayer
    {
        public LineRenderer renderer;
        public Color startColor;
        public Color endColor;
    }

    private sealed class FillLayer
    {
        public Material material;
        public Color color;
        public bool usesFireShader;
    }

    private static readonly int FireTintPropertyId = Shader.PropertyToID(
        new string(new[] { '_', 'T', 'i', 'n', 't' }));
    private static readonly int FireIntensityPropertyId = Shader.PropertyToID(
        new string(new[] { '_', 'I', 'n', 't', 'e', 'n', 's', 'i', 't', 'y' }));
    private static readonly int FireShapePropertyId = Shader.PropertyToID(
        new string(new[] { '_', 'S', 'h', 'a', 'p', 'e' }));
    private static readonly int FireSoftnessPropertyId = Shader.PropertyToID(
        new string(new[] { '_', 'S', 'o', 'f', 't', 'n', 'e', 's', 's' }));

    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly List<LineLayer> lineLayers = new List<LineLayer>(24);
    private readonly List<LineRenderer> flameStreaks = new List<LineRenderer>(12);
    private readonly List<FillLayer> fillLayers = new List<FillLayer>(4);
    private readonly List<Mesh> runtimeFillMeshes = new List<Mesh>(4);
    private readonly List<Material> runtimeFillMaterials = new List<Material>(4);

    private Transform sourcePlayer;
    private Material sharedMaterial;
    private Material runtimeLineMaterial;
    private float damage;
    private float radius;
    private float lifetime;
    private float knockback;
    private float critChance;
    private float critDamageMultiplier;
    private float hitInterval;
    private readonly Dictionary<EnemyHealth, float> nextHitTimes = new Dictionary<EnemyHealth, float>();

    private float elapsed;
    private float rotationSpeed = DefaultRotationSpeed;
    private bool initialized;

    public void Initialize(
        Vector3 requestedPosition,
        float patchDamage,
        float patchRadius,
        float patchLifetime,
        float patchHitInterval,
        float patchKnockback,
        float patchCritChance,
        float patchCritDamageMultiplier,
        GameObject flamePrefab,
        Transform player)
    {
        sourcePlayer = player;
        elapsed = 0f;
        nextHitTimes.Clear();
        damage = Mathf.Max(0f, patchDamage);
        radius = Mathf.Max(0.35f, patchRadius);
        lifetime = Mathf.Max(HoldBeforeShrinkSeconds + 0.01f, patchLifetime);
        hitInterval = Mathf.Max(0.01f, patchHitInterval);
        knockback = Mathf.Max(0f, patchKnockback);
        critChance = Mathf.Clamp01(patchCritChance);
        critDamageMultiplier = Mathf.Max(1f, patchCritDamageMultiplier);

        SnapToGround(requestedPosition);
        CreateVisuals(flamePrefab);
        initialized = true;
        DealDamageOnContact();
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

        float shrinkDuration = Mathf.Max(
            0.01f,
            lifetime - HoldBeforeShrinkSeconds);
        float shrinkProgress = Mathf.Clamp01(
            (elapsed - HoldBeforeShrinkSeconds) / shrinkDuration);
        shrinkProgress = Mathf.SmoothStep(0f, 1f, shrinkProgress);
        float scale = Mathf.Lerp(1f, 0f, shrinkProgress);

        transform.localScale = Vector3.one * scale;
        float fadeStart = HoldBeforeShrinkSeconds + shrinkDuration * 0.45f;
        float alpha = Mathf.Lerp(
            1f,
            0f,
            Mathf.InverseLerp(fadeStart, lifetime, elapsed));
        UpdateFillAlpha(alpha);
        UpdateLineAlpha(alpha);

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        DealDamageOnContact();
    }

    private void CreateVisuals(GameObject flamePrefab)
    {
        if (flamePrefab != null)
        {
            GameObject flameObject = Instantiate(flamePrefab, transform);
            flameObject.transform.localPosition = Vector3.zero;
            flameObject.transform.localRotation = Quaternion.identity;
            flameObject.transform.localScale = Vector3.one * Mathf.Clamp(radius, 0.75f, 1.6f);

            Renderer sourceRenderer = flameObject.GetComponentInChildren<Renderer>(true);
            if (sourceRenderer != null)
                sharedMaterial = sourceRenderer.sharedMaterial;
        }

        runtimeLineMaterial = CreateTransparentMaterial();
        if (runtimeLineMaterial != null)
            sharedMaterial = runtimeLineMaterial;

        CreateDisc(
            radius * 1.14f,
            64,
            new Color(0.82f, 0.015f, 0f, 0.26f));
        CreateDisc(
            radius * 0.98f,
            64,
            new Color(1f, 0.055f, 0f, 0.22f));
        CreateDisc(
            radius * 0.77f,
            56,
            new Color(1f, 0.2f, 0f, 0.18f));
        CreateDisc(
            radius * 0.56f,
            48,
            new Color(1f, 0.52f, 0.01f, 0.15f));

        CreateRing(
            radius * 1.1f,
            52,
            0.095f,
            new Color(1f, 0.06f, 0.002f, 0.9f),
            new Color(1f, 0.34f, 0.005f, 0.48f));
        CreateRing(
            radius * 0.82f,
            46,
            0.06f,
            new Color(1f, 0.63f, 0.015f, 0.9f),
            new Color(1f, 0.18f, 0.002f, 0.62f));
        CreateRing(
            radius * 0.57f,
            40,
            0.045f,
            new Color(1f, 0.92f, 0.08f, 0.78f),
            new Color(1f, 0.28f, 0.002f, 0.4f));

        const int outerFlameCount = 16;
        for (int i = 0; i < outerFlameCount; i++)
            CreateFlameStreak(i, outerFlameCount);

        const int innerFlameCount = 14;
        for (int i = 0; i < innerFlameCount; i++)
            CreateSmallFlameTrail(i, innerFlameCount);
    }

    private void CreateDisc(
        float discRadius,
        int segmentCount,
        Color color)
    {
        GameObject discObject = new GameObject();
        discObject.transform.SetParent(transform, false);
        discObject.transform.localPosition = Vector3.up * 0.012f;

        MeshFilter meshFilter = discObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = discObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = 4;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segmentCount + 1];
        Vector2[] uvs = new Vector2[segmentCount + 1];
        Color[] colors = new Color[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = Color.white;

        for (int i = 0; i < segmentCount; i++)
        {
            float normalized = i / (float)segmentCount;
            float angle = normalized * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * discRadius,
                0f,
                Mathf.Sin(angle) * discRadius);
            uvs[i + 1] = new Vector2(
                0.5f + Mathf.Cos(angle) * 0.5f,
                0.5f + Mathf.Sin(angle) * 0.5f);
            colors[i + 1] = Color.white;

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segmentCount - 1 ? 1 : i + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        runtimeFillMeshes.Add(mesh);

        bool usesFireShader;
        Material material = CreateFireMaterial(color, out usesFireShader);
        if (material == null)
            return;

        meshRenderer.sharedMaterial = material;
        runtimeFillMaterials.Add(material);
        fillLayers.Add(new FillLayer
        {
            material = material,
            color = color,
            usesFireShader = usesFireShader
        });
    }

    private static Material CreateFireMaterial(Color color, out bool usesFireShader)
    {
        Shader shader = Shader.Find(
            new string(
                new[]
                {
                    'G', 'i', 'g', 'a', 'c', 'h', 'a', 'd', '/',
                    'F', 'i', 'r', 'e', 'b', 'a', 'l', 'l', 'B', 'i', 'l', 'l', 'b', 'o', 'a', 'r', 'd'
                }));
        usesFireShader = shader != null;
        Material material = usesFireShader
            ? new Material(shader)
            : CreateTransparentMaterial();
        if (material == null)
            return null;

        if (usesFireShader)
        {
            material.SetColor(FireTintPropertyId, color);
            material.SetFloat(FireIntensityPropertyId, 1.55f);
            material.SetFloat(FireShapePropertyId, 0f);
            material.SetFloat(FireSoftnessPropertyId, 0.13f);
        }
        else
        {
            material.color = color;
        }

        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find(
            new string(
                new[]
                {
                    'S', 'p', 'r', 'i', 't', 'e', 's', '/',
                    'D', 'e', 'f', 'a', 'u', 'l', 't'
                }));
        if (shader == null)
        {
            shader = Shader.Find(
                new string(
                    new[]
                    {
                        'U', 'n', 'l', 'i', 't', '/',
                        'C', 'o', 'l', 'o', 'r'
                    }));
        }

        if (shader == null)
            return null;

        return new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void CreateRing(
        float ringRadius,
        int segmentCount,
        float width,
        Color startColor,
        Color endColor)
    {
        LineLayer layer = CreateLineLayer(
            segmentCount,
            width,
            true,
            startColor,
            endColor);

        for (int i = 0; i < segmentCount; i++)
        {
            float normalized = i / (float)segmentCount;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                Mathf.Cos(angle) * ringRadius,
                0.018f,
                Mathf.Sin(angle) * ringRadius);
            layer.renderer.SetPosition(i, point);
        }
    }

    private void CreateFlameStreak(int index, int count)
    {
        float angle = index / (float)count * Mathf.PI * 2f
            + Random.Range(-0.12f, 0.12f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        float startRadius = radius * Random.Range(0.72f, 0.86f);
        float endRadius = radius * Random.Range(0.86f, 1.02f);
        float height = Random.Range(0.08f, 0.2f);

        LineLayer layer = CreateLineLayer(
            3,
            Random.Range(0.025f, 0.05f),
            false,
            new Color(1f, 0.8f, 0.03f, 0.9f),
            new Color(1f, 0.04f, 0f, 0.1f));

        layer.renderer.SetPosition(
            0,
            direction * startRadius + Vector3.up * 0.025f);
        layer.renderer.SetPosition(
            1,
            direction * (startRadius + endRadius) * 0.5f
                + Vector3.up * height);
        layer.renderer.SetPosition(
            2,
            direction * endRadius + Vector3.up * (height * 0.45f));
        flameStreaks.Add(layer.renderer);
    }

    private void CreateSmallFlameTrail(int index, int count)
    {
        float angle = index / (float)count * Mathf.PI * 2f
            + Random.Range(-0.18f, 0.18f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        float startRadius = radius * Random.Range(0.18f, 0.48f);
        float endRadius = startRadius + radius * Random.Range(0.12f, 0.27f);
        float height = Random.Range(0.045f, 0.11f);

        LineLayer layer = CreateLineLayer(
            3,
            Random.Range(0.014f, 0.026f),
            false,
            new Color(1f, 0.95f, 0.16f, 0.8f),
            new Color(1f, 0.12f, 0f, 0.08f));

        layer.renderer.SetPosition(
            0,
            direction * startRadius + Vector3.up * 0.03f);
        layer.renderer.SetPosition(
            1,
            direction * (startRadius + endRadius) * 0.5f
                + Vector3.up * height);
        layer.renderer.SetPosition(
            2,
            direction * endRadius + Vector3.up * (height * 0.35f));
        flameStreaks.Add(layer.renderer);
    }

    private LineLayer CreateLineLayer(
        int positionCount,
        float width,
        bool loop,
        Color startColor,
        Color endColor)
    {
        GameObject lineObject = new GameObject();
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = positionCount;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 5;
        if (sharedMaterial != null)
            line.sharedMaterial = sharedMaterial;

        LineLayer layer = new LineLayer
        {
            renderer = line,
            startColor = startColor,
            endColor = endColor
        };
        lineLayers.Add(layer);
        return layer;
    }

    private void UpdateLineAlpha(float alpha)
    {
        for (int i = 0; i < lineLayers.Count; i++)
        {
            LineLayer layer = lineLayers[i];
            layer.renderer.startColor = WithAlpha(layer.startColor, alpha);
            layer.renderer.endColor = WithAlpha(layer.endColor, alpha);
        }
    }

    private void UpdateFillAlpha(float alpha)
    {
        for (int i = 0; i < fillLayers.Count; i++)
        {
            FillLayer layer = fillLayers[i];
            if (layer.material != null)
            {
                Color fadedColor = WithAlpha(layer.color, alpha);
                if (layer.usesFireShader)
                    layer.material.SetColor(FireTintPropertyId, fadedColor);
                else
                    layer.material.color = fadedColor;
            }
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

    private void DealDamageOnContact()
    {
        float currentRadius = radius * Mathf.Clamp01(Mathf.Abs(transform.localScale.x));
        float radiusSquared = currentRadius * currentRadius;
        List<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.CanBeTargeted)
                continue;

            Vector3 offset = enemy.transform.position - transform.position;
            offset.y = 0f;

            Collider enemyCollider = enemy.GetComponent<Collider>();
            if (enemyCollider == null)
                enemyCollider = enemy.GetComponentInChildren<Collider>(true);

            if (enemyCollider != null)
            {
                Vector3 closestPoint = enemyCollider.bounds.ClosestPoint(transform.position);
                Vector3 horizontalOffset = closestPoint - transform.position;
                horizontalOffset.y = 0f;
                if (horizontalOffset.sqrMagnitude > radiusSquared)
                    continue;
            }
            else if (offset.sqrMagnitude > radiusSquared)
            {
                continue;
            }

            float nextHitTime;
            if (nextHitTimes.TryGetValue(enemy, out nextHitTime) &&
                Time.time < nextHitTime)
            {
                continue;
            }

            float finalDamage = Random.value < critChance
                ? damage * critDamageMultiplier
                : damage;
            if (enemy.GetExpectedDamage(finalDamage) <= 0f)
                continue;

            enemy.TakeDamage(finalDamage, false);
            WeaponHitParticles.PlayAuraHit(enemy, offset);
            ApplyKnockback(enemy, offset);
            nextHitTimes[enemy] = Time.time + hitInterval;
        }
    }

    private void ApplyKnockback(EnemyHealth enemy, Vector3 offset)
    {
        if (knockback <= 0f || enemy == null || !enemy.isActiveAndEnabled)
            return;

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
            enemyAI.ApplyKnockback(offset, knockback);
    }

    private void SnapToGround(Vector3 requestedPosition)
    {
        Vector3 rayOrigin = requestedPosition + Vector3.up * 2f;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            20f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        float groundY = requestedPosition.y;
        Vector3 groundNormal = Vector3.up;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (sourcePlayer != null &&
                (hitTransform == sourcePlayer || hitTransform.IsChildOf(sourcePlayer)))
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            groundY = hit.point.y;
            groundNormal = hit.normal.normalized;
            foundGround = true;
        }

        if (!foundGround && sourcePlayer != null)
        {
            CharacterController controller = sourcePlayer.GetComponent<CharacterController>();
            if (controller != null)
                groundY = controller.bounds.min.y;
        }

        transform.position = new Vector3(
            requestedPosition.x,
            groundY + 0.02f,
            requestedPosition.z);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
            Destroy(runtimeLineMaterial);

        for (int i = 0; i < runtimeFillMaterials.Count; i++)
        {
            if (runtimeFillMaterials[i] != null)
                Destroy(runtimeFillMaterials[i]);
        }

        for (int i = 0; i < runtimeFillMeshes.Count; i++)
        {
            if (runtimeFillMeshes[i] != null)
                Destroy(runtimeFillMeshes[i]);
        }
    }
}
