using UnityEngine;

/// <summary>
/// Draws a short-lived, pixel-stepped shockwave on the XZ ground plane.
/// AoEWeapon scales this object to the attack diameter, so an outer radius of
/// 0.5 always matches WeaponData.size.
/// </summary>
[ExecuteAlways]
public sealed class RadialPulseVFX : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField, Min(0.1f)] private float duration = 0.65f;
    [SerializeField, Range(0.02f, 0.5f)] private float drawDuration = 0.18f;
    [SerializeField] private float outerRotationSpeed = 42f;
    [SerializeField] private float innerRotationSpeed = -58f;
    [SerializeField, Range(0.5f, 1f)] private float startScale = 0.78f;

    [Header("Pixel Rings")]
    [SerializeField, Range(32, 192)] private int segments = 112;
    [SerializeField, Range(0.1f, 0.48f)] private float innerRadius = 0.32f;
    [SerializeField, Range(0.15f, 0.5f)] private float outerRadius = 0.5f;
    [Tooltip("Grid size used to turn the circles into visible pixel steps.")]
    [SerializeField, Range(0.005f, 0.08f)] private float pixelStep = 0.016f;
    [Tooltip("Small fixed dents make the rotating rings feel hand-drawn and unstable.")]
    [SerializeField, Range(0f, 0.08f)] private float notchSize = 0.018f;

    [Header("Outer Burst")]
    [SerializeField, Range(16, 96)] private int rayCount = 64;
    [SerializeField, Range(0f, 0.08f)] private float rayStartJitter = 0.018f;
    [SerializeField, Min(0f)] private float rayMinLength = 0.035f;
    [SerializeField, Min(0f)] private float rayMaxLength = 0.13f;
    [SerializeField, Min(0.001f)] private float rayWidth = 0.007f;

    [Header("Appearance")]
    [SerializeField] private Color coreColor = new Color(0.96f, 0.94f, 1f, 1f);
    [SerializeField] private Color glowColor = new Color(0.72f, 0.64f, 1f, 0.48f);
    [SerializeField, Min(0.001f)] private float coreWidth = 0.072f;
    [SerializeField, Min(0.001f)] private float glowWidth = 0.17f;
    [SerializeField, Min(0f)] private float groundOffset = 0.002f;

    private PixelRing[] rings;
    private BurstLayer burstGlow;
    private BurstLayer burstCore;
    private Material sharedMaterial;
    private float elapsed;
    private bool initialized;
    private bool rebuildRequested;

    private sealed class PixelRing
    {
        public Transform root;
        public LineRenderer glow;
        public LineRenderer core;
        public float radius;
        public float rotationSpeed;
        public int patternOffset;
    }

    private sealed class BurstLayer
    {
        public Transform root;
        public Mesh mesh;
        public Vector3[] vertices;
        public Color[] colors;
        public int[] triangles;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        elapsed = 0f;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
        }
#endif
    }

    private void OnValidate()
    {
        rayMaxLength = Mathf.Max(rayMinLength, rayMaxLength);
        rebuildRequested = true;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;

        RemoveStaleGeneratedChildren();
        sharedMaterial = CreateTransparentMaterial();
        rings = new[]
        {
            CreateRing("Outer_Pixel_Ring", outerRadius, outerRotationSpeed, 0, 2),
            CreateRing("Inner_Pixel_Ring", innerRadius, innerRotationSpeed, 7, 4)
        };
        burstGlow = CreateBurstLayer("Outer_Burst_Glow", 0);
        burstCore = CreateBurstLayer("Outer_Burst_Core", 1);
        initialized = true;
    }

    private void RemoveStaleGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "Outer_Pixel_Ring" &&
                child.name != "Inner_Pixel_Ring" &&
                child.name != "Outer_Burst_Glow" &&
                child.name != "Outer_Burst_Core")
                continue;

            child.gameObject.SetActive(false);
            DestroyRuntimeObject(child.gameObject);
        }
    }

    private PixelRing CreateRing(
        string ringName,
        float radius,
        float rotationSpeed,
        int patternOffset,
        int sortingOffset)
    {
        GameObject ringObject = CreateRuntimeObject(ringName, transform);
        return new PixelRing
        {
            root = ringObject.transform,
            glow = CreateLine(ringObject.transform, "Glow", glowWidth, sortingOffset),
            core = CreateLine(ringObject.transform, "Core", coreWidth, sortingOffset + 1),
            radius = radius,
            rotationSpeed = rotationSpeed,
            patternOffset = patternOffset
        };
    }

    private LineRenderer CreateLine(Transform parent, string objectName, float width, int order)
    {
        GameObject lineObject = CreateRuntimeObject(objectName, parent);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 0;
        line.widthMultiplier = width;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = sharedMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = order;
        return line;
    }

    private BurstLayer CreateBurstLayer(string objectName, int sortingOrder)
    {
        GameObject burstObject = CreateRuntimeObject(objectName, transform);
        MeshFilter filter = burstObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = burstObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh { name = objectName + "_Mesh" };
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = sharedMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = sortingOrder;

        var layer = new BurstLayer
        {
            root = burstObject.transform,
            mesh = mesh,
            vertices = new Vector3[rayCount * 4],
            colors = new Color[rayCount * 4],
            triangles = new int[rayCount * 6]
        };

        for (int i = 0; i < rayCount; i++)
        {
            int vertex = i * 4;
            int triangle = i * 6;
            layer.triangles[triangle] = vertex;
            layer.triangles[triangle + 1] = vertex + 1;
            layer.triangles[triangle + 2] = vertex + 2;
            layer.triangles[triangle + 3] = vertex + 1;
            layer.triangles[triangle + 4] = vertex + 3;
            layer.triangles[triangle + 5] = vertex + 2;
        }

        mesh.vertices = layer.vertices;
        mesh.colors = layer.colors;
        mesh.triangles = layer.triangles;
        return layer;
    }

    private GameObject CreateRuntimeObject(string objectName, Transform parent)
    {
        var child = new GameObject(objectName);
        if (!Application.isPlaying) child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(parent, false);
        return child;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        elapsed += Time.deltaTime;
        AnimateEffect();
    }

    private void AnimateEffect()
    {
        if (!HasValidRuntimeObjects())
        {
            DisposeRuntimeObjects();
            EnsureInitialized();
        }

        float previewScale = Application.isPlaying ? 1f : 6f;
        float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        float drawProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, drawDuration, elapsed));
        int visiblePoints = Mathf.Clamp(Mathf.CeilToInt(segments * drawProgress), 2, segments);
        float pulseScale = Mathf.Lerp(startScale, 1f, EaseOutBack(drawProgress));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, normalizedTime));

        Color core = coreColor;
        core.a *= fade;
        Color glow = glowColor;
        glow.a *= fade;

        foreach (PixelRing ring in rings)
        {
            ring.root.localRotation = Quaternion.Euler(0f, elapsed * ring.rotationSpeed, 0f);
            ring.root.localScale = Vector3.one * pulseScale;
            ring.core.widthMultiplier = coreWidth * previewScale;
            ring.glow.widthMultiplier = glowWidth * previewScale;
            ring.core.startColor = ring.core.endColor = core;
            ring.glow.startColor = ring.glow.endColor = glow;
            UpdatePixelLine(ring.core, ring, visiblePoints, previewScale);
            UpdatePixelLine(ring.glow, ring, visiblePoints, previewScale);
        }

        float burstReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.22f, normalizedTime));
        float burstFlicker = 0.82f + Mathf.Sin(elapsed * 37f) * 0.18f;
        UpdateBurstMesh(burstGlow, previewScale, pulseScale, burstReveal, fade * 0.52f * burstFlicker, 3.2f, 1.18f);
        UpdateBurstMesh(burstCore, previewScale, pulseScale, burstReveal, fade * burstFlicker, 1f, 1f);
    }

    private void UpdatePixelLine(LineRenderer line, PixelRing ring, int visiblePoints, float previewScale)
    {
        line.positionCount = visiblePoints;
        line.loop = visiblePoints >= segments;

        float radius = ring.radius * previewScale;
        float grid = pixelStep * previewScale;
        float notch = notchSize * previewScale;

        for (int i = 0; i < visiblePoints; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            int pattern = (i + ring.patternOffset) % 28;
            float patternedRadius = radius;
            if (pattern == 3 || pattern == 4 || pattern == 17)
                patternedRadius -= notch;
            else if (pattern == 10 || pattern == 23)
                patternedRadius += notch * 0.7f;

            float x = Mathf.Round(Mathf.Cos(angle) * patternedRadius / grid) * grid;
            float z = Mathf.Round(Mathf.Sin(angle) * patternedRadius / grid) * grid;
            line.SetPosition(i, new Vector3(x, groundOffset * previewScale, z));
        }
    }

    private void UpdateBurstMesh(
        BurstLayer layer,
        float previewScale,
        float pulseScale,
        float reveal,
        float alpha,
        float widthMultiplier,
        float lengthMultiplier)
    {
        layer.root.localRotation = Quaternion.Euler(0f, elapsed * outerRotationSpeed * 0.22f, 0f);

        for (int i = 0; i < rayCount; i++)
        {
            float seedA = Hash01(i * 2 + 11);
            float seedB = Hash01(i * 2 + 37);
            float angle = (i + seedA * 0.72f) / rayCount * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-direction.z, 0f, direction.x);

            float startRadius = (outerRadius - rayStartJitter * seedB) * pulseScale * previewScale;
            float fullLength = Mathf.Lerp(rayMinLength, rayMaxLength, seedA * seedA) * lengthMultiplier * previewScale;
            float length = fullLength * reveal;
            float halfWidth = rayWidth * widthMultiplier * previewScale * Mathf.Lerp(0.7f, 1.25f, seedB);
            Vector3 start = direction * startRadius + Vector3.up * groundOffset * previewScale;
            Vector3 end = start + direction * length;
            Vector3 side = tangent * halfWidth;
            int vertex = i * 4;

            layer.vertices[vertex] = start - side;
            layer.vertices[vertex + 1] = start + side;
            layer.vertices[vertex + 2] = end - side * 0.15f;
            layer.vertices[vertex + 3] = end + side * 0.15f;

            Color startColor = Color.Lerp(glowColor, coreColor, widthMultiplier <= 1f ? 0.8f : 0f);
            startColor.a = alpha * Mathf.Lerp(0.55f, 1f, seedB);
            Color endColor = startColor;
            endColor.a = 0f;
            layer.colors[vertex] = startColor;
            layer.colors[vertex + 1] = startColor;
            layer.colors[vertex + 2] = endColor;
            layer.colors[vertex + 3] = endColor;
        }

        layer.mesh.vertices = layer.vertices;
        layer.mesh.colors = layer.colors;
        layer.mesh.RecalculateBounds();
    }

    private static float Hash01(int value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.35f;
        float t = value - 1f;
        return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
    }

    private bool HasValidRuntimeObjects()
    {
        if (!initialized || rings == null || rings.Length != 2 ||
            burstGlow == null || burstGlow.root == null || burstGlow.mesh == null ||
            burstCore == null || burstCore.root == null || burstCore.mesh == null)
            return false;

        foreach (PixelRing ring in rings)
        {
            if (ring == null || ring.root == null || ring.core == null || ring.glow == null)
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
        if (rebuildRequested)
        {
            DisposeRuntimeObjects();
            EnsureInitialized();
            rebuildRequested = false;
        }
        elapsed = (float)(UnityEditor.EditorApplication.timeSinceStartup % duration);
        AnimateEffect();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        return new Material(shader)
        {
            name = "AuraVFX_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
        DisposeRuntimeObjects();
    }

    private void DisposeRuntimeObjects()
    {
        DestroyRuntimeObject(burstGlow != null ? burstGlow.mesh : null);
        DestroyRuntimeObject(burstCore != null ? burstCore.mesh : null);
        if (rings != null)
        {
            foreach (PixelRing ring in rings)
                DestroyRuntimeObject(ring != null && ring.root != null ? ring.root.gameObject : null);
        }
        DestroyRuntimeObject(burstGlow != null && burstGlow.root != null ? burstGlow.root.gameObject : null);
        DestroyRuntimeObject(burstCore != null && burstCore.root != null ? burstCore.root.gameObject : null);
        DestroyRuntimeObject(sharedMaterial);

        rings = null;
        burstGlow = null;
        burstCore = null;
        sharedMaterial = null;
        initialized = false;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null) return;
        if (target is GameObject gameObject)
            gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
