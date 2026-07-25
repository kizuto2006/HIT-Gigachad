using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Short-lived stylized melee slash. The visual children and material are
/// generated at runtime so the prefab stays small and self-contained.
/// </summary>
[ExecuteAlways]
public sealed class SlashVFX : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField, Min(0.1f)] private float duration = 0.25f;
    [SerializeField, Range(0.02f, 0.3f)] private float drawDuration = 0.065f;
    [SerializeField, Min(0f)] private float forwardDrift = 0f;
    [SerializeField, Range(0f, 0.5f)] private float startScale = 0.72f;
    [SerializeField] private bool followPlayerFacing = true;
    [SerializeField] private bool followPlayerPosition = true;

    [Header("Slash Shape")]
    [SerializeField, Range(12, 64)] private int segments = 36;
    [SerializeField, Min(0.1f)] private float radius = 1.15f;
    [SerializeField, Range(0.1f, 1f)] private float verticalRatio = 0.62f;
    [SerializeField, Range(-360f, 360f)] private float startAngle = 200f;
    [SerializeField, Range(-360f, 360f)] private float endAngle = -20f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.11f, 0.25f);

    [Header("Appearance")]
    [SerializeField] private Color coreColor = new Color(0.96f, 0.99f, 1f, 1f);
    [SerializeField] private Color glowColor = new Color(0.12f, 0.72f, 1f, 0.62f);
    [SerializeField, Min(0.005f)] private float coreWidth = 0.075f;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.24f;
    [SerializeField, Range(0f, 1f)] private float trailRadius = 0.78f;

    [Header("Impact Streaks")]
    [SerializeField, Range(0, 12)] private int streakCount = 7;
    [SerializeField, Min(0f)] private float streakLength = 0.32f;
    [SerializeField, Min(0.001f)] private float streakWidth = 0.025f;

    private LineRenderer mainGlow;
    private LineRenderer mainCore;
    private LineRenderer trailGlow;
    private LineRenderer trailCore;
    private LineRenderer[] streaks;
    private MeshFilter bladeGlowFilter;
    private MeshFilter bladeCoreFilter;
    private Mesh bladeGlowMesh;
    private Mesh bladeCoreMesh;
    private Transform visualRoot;
    private Material sharedMaterial;
    private float elapsed;
    private bool initialized;
    private bool rebuildRequested;
    private Transform facingTarget;
    private Vector3 targetLocalPosition;

    public void SetFacingTarget(Transform target)
    {
        facingTarget = target;
        if (facingTarget != null)
            targetLocalPosition = facingTarget.InverseTransformPoint(transform.position);

        UpdateFollowTransform();
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
        duration = Mathf.Max(0.1f, duration);
        drawDuration = Mathf.Min(drawDuration, duration * 0.8f);
        rebuildRequested = true;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        elapsed += Time.deltaTime;
        Animate(elapsed);

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        UpdateFollowTransform();
    }

    private void UpdateFollowTransform()
    {
        if (facingTarget == null)
            return;

        if (followPlayerPosition)
            transform.position = facingTarget.TransformPoint(targetLocalPosition);

        UpdateFacingDirection();
    }

    private void UpdateFacingDirection()
    {
        if (!followPlayerFacing || facingTarget == null || visualRoot == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(facingTarget.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            visualRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        if (this == null || Application.isPlaying) return;

        if (rebuildRequested || !HasValidVisuals())
        {
            DisposeVisuals();
            EnsureInitialized();
            rebuildRequested = false;
        }

        // Keep the complete slash visible while inspecting the prefab.
        Animate(drawDuration * 1.35f);
    }
#endif

    private void EnsureInitialized()
    {
        if (initialized && HasValidVisuals()) return;

        DisposeVisuals();
        sharedMaterial = CreateTransparentMaterial();

        GameObject visualObject = new GameObject("Slash_Visuals")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        visualRoot = visualObject.transform;
        visualRoot.SetParent(transform, false);

        mainGlow = CreateLine("Main_Glow", glowWidth, 0);
        mainCore = CreateLine("Main_Core", coreWidth, 2);
        trailGlow = CreateLine("Trail_Glow", glowWidth * 0.42f, 1);
        trailCore = CreateLine("Trail_Core", coreWidth * 0.55f, 3);
        bladeGlowFilter = CreateBladeLayer("Blade_Glow", 0, out bladeGlowMesh);
        bladeCoreFilter = CreateBladeLayer("Blade_Core", 1, out bladeCoreMesh);

        streaks = new LineRenderer[streakCount];
        for (int i = 0; i < streakCount; i++)
            streaks[i] = CreateLine($"Impact_Streak_{i + 1:00}", streakWidth, 4);

        initialized = true;
        Animate(Application.isPlaying ? 0f : drawDuration * 1.35f);
    }

    private LineRenderer CreateLine(string objectName, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lineObject.transform.SetParent(visualRoot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 0;
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.76f, 0.82f),
            new Keyframe(1f, 0f));
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = sharedMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        return line;
    }

    private MeshFilter CreateBladeLayer(string objectName, int sortingOrder, out Mesh mesh)
    {
        GameObject bladeObject = new GameObject(objectName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        bladeObject.transform.SetParent(visualRoot, false);

        MeshFilter filter = bladeObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = bladeObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = objectName + "_Mesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = sharedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = sortingOrder;
        return filter;
    }

    private void Animate(float time)
    {
        if (!HasValidVisuals()) return;

        float normalizedTime = Mathf.Clamp01(time / duration);
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, drawDuration, time));
        float trailReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(drawDuration * 0.18f, drawDuration * 1.2f, time));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, normalizedTime));
        float scale = Mathf.Lerp(startScale, 1.06f, EaseOutCubic(reveal));

        Color core = coreColor;
        core.a *= fade;
        Color glow = glowColor;
        glow.a *= fade;

        visualRoot.localScale = Vector3.one * scale;
        Vector3 drift = Vector3.forward * (forwardDrift * normalizedTime);

        UpdateArc(mainGlow, reveal, radius, localOffset + drift, glow);
        UpdateArc(mainCore, reveal, radius, localOffset + drift, core);

        Color bladeGlow = glow;
        bladeGlow.a *= 0.82f;
        Color bladeCore = core;
        bladeCore.a *= 0.94f;
        UpdateBladeMesh(bladeGlowMesh, reveal, radius * 1.04f, radius * 0.78f, localOffset + drift, bladeGlow);
        UpdateBladeMesh(bladeCoreMesh, reveal, radius, radius * 0.88f, localOffset + new Vector3(0f, 0f, -0.004f) + drift, bladeCore);

        Color trailCoreColor = core;
        trailCoreColor.a *= 0.66f;
        Color trailGlowColor = glow;
        trailGlowColor.a *= 0.48f;
        Vector3 trailOffset = localOffset + new Vector3(-0.015f, -0.012f, 0.008f) + drift;
        UpdateArc(trailGlow, trailReveal, radius * trailRadius, trailOffset, trailGlowColor);
        UpdateArc(trailCore, trailReveal, radius * trailRadius, trailOffset, trailCoreColor);

        UpdateStreaks(reveal, fade, drift);
    }

    private void UpdateBladeMesh(
        Mesh mesh,
        float reveal,
        float outerRadius,
        float innerRadius,
        Vector3 offset,
        Color color)
    {
        int visiblePoints = Mathf.Clamp(Mathf.CeilToInt(segments * reveal), 2, segments);
        Vector3[] vertices = new Vector3[visiblePoints * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[(visiblePoints - 1) * 6];

        for (int i = 0; i < visiblePoints; i++)
        {
            float t = i / (float)(segments - 1);
            float shapeT = i / (float)(visiblePoints - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle) * verticalRatio;
            float height = Mathf.Sin(t * Mathf.PI) * 0.004f;

            // The inner edge meets the outer edge at both ends, creating two
            // sharp tips and a thick middle like a crescent moon.
            float crescentProfile = Mathf.Pow(Mathf.Sin(shapeT * Mathf.PI), 0.7f);
            float taperedInnerRadius = Mathf.Lerp(outerRadius, innerRadius, crescentProfile);

            vertices[i * 2] = offset + new Vector3(cos * taperedInnerRadius, height, sin * taperedInnerRadius);
            vertices[i * 2 + 1] = offset + new Vector3(cos * outerRadius, height, sin * outerRadius);

            float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, shapeT))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1f, shapeT)));
            Color vertexColor = color;
            vertexColor.a *= edgeFade;
            colors[i * 2] = vertexColor;
            colors[i * 2 + 1] = vertexColor;

            if (i >= visiblePoints - 1) continue;
            int vertex = i * 2;
            int triangle = i * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 3;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void UpdateArc(LineRenderer line, float reveal, float arcRadius, Vector3 offset, Color color)
    {
        int visiblePoints = Mathf.Clamp(Mathf.CeilToInt(segments * reveal), 2, segments);
        line.positionCount = visiblePoints;
        line.startColor = line.endColor = color;

        for (int i = 0; i < visiblePoints; i++)
        {
            float t = i / (float)(segments - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * arcRadius;
            float z = Mathf.Sin(angle) * arcRadius * verticalRatio;
            float height = Mathf.Sin(t * Mathf.PI) * 0.006f;
            line.SetPosition(i, offset + new Vector3(x, height, z));
        }
    }

    private void UpdateStreaks(float reveal, float fade, Vector3 drift)
    {
        float streakReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, reveal));
        Color streakColor = coreColor;
        streakColor.a *= fade * streakReveal * 0.88f;

        for (int i = 0; i < streaks.Length; i++)
        {
            LineRenderer streak = streaks[i];
            streak.startColor = streak.endColor = streakColor;
            streak.positionCount = streakReveal > 0.01f ? 2 : 0;
            if (streak.positionCount == 0) continue;

            float t = (i + 0.5f) / Mathf.Max(1f, streaks.Length);
            float angle = Mathf.Lerp(startAngle + 10f, endAngle - 6f, t) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * verticalRatio).normalized;
            Vector3 start = localOffset + drift + new Vector3(
                Mathf.Cos(angle) * radius * 0.9f,
                0.004f,
                Mathf.Sin(angle) * radius * verticalRatio * 0.9f);
            float alternatingLength = streakLength * (0.62f + (i % 3) * 0.2f);
            streak.SetPosition(0, start);
            streak.SetPosition(1, start + radial * alternatingLength * streakReveal);
        }
    }

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        return new Material(shader)
        {
            name = "SlashVFX_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3100
        };
    }

    private bool HasValidVisuals()
    {
        if (visualRoot == null || mainGlow == null || mainCore == null || trailGlow == null || trailCore == null ||
            bladeGlowFilter == null || bladeCoreFilter == null || bladeGlowMesh == null || bladeCoreMesh == null || streaks == null)
            return false;

        for (int i = 0; i < streaks.Length; i++)
        {
            if (streaks[i] == null) return false;
        }

        return true;
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
        DisposeVisuals();
    }

    private void DisposeVisuals()
    {
        DestroyLine(mainGlow);
        DestroyLine(mainCore);
        DestroyLine(trailGlow);
        DestroyLine(trailCore);

        if (streaks != null)
        {
            for (int i = 0; i < streaks.Length; i++)
                DestroyLine(streaks[i]);
        }

        DestroyRuntimeObject(bladeGlowMesh);
        DestroyRuntimeObject(bladeCoreMesh);
        DestroyRuntimeObject(visualRoot != null ? visualRoot.gameObject : null);
        DestroyRuntimeObject(sharedMaterial);
        mainGlow = null;
        mainCore = null;
        trailGlow = null;
        trailCore = null;
        streaks = null;
        bladeGlowFilter = null;
        bladeCoreFilter = null;
        bladeGlowMesh = null;
        bladeCoreMesh = null;
        visualRoot = null;
        sharedMaterial = null;
        initialized = false;
    }

    private static void DestroyLine(LineRenderer line)
    {
        if (line != null)
            DestroyRuntimeObject(line.gameObject);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null) return;
        if (target is GameObject gameObject) gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }
}
