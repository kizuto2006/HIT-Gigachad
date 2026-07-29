using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only mini boss marker for pooled enemies. The crown mesh and material
/// are shared by every instance so an elite remains inexpensive to render.
/// </summary>
public sealed class EnemyMiniBoss : MonoBehaviour
{
    private const string CrownObjectName = "MiniBoss Crown";
    private const float CrownClearanceScaleRatio = 0.08f;
    private const float MinimumCrownClearance = 0.03f;
    private static readonly Color CrownGoldColor = new Color(1f, 0.76f, 0.035f, 1f);
    private static readonly Color CrownOutlineColor = new Color(0.07f, 0.018f, 0.006f, 1f);
    private static Mesh sharedCrownMesh;
    private static Material sharedCrownMaterial;

    private EnemyHealth enemyHealth;
    private GameObject crownObject;
    private Renderer crownRenderer;
    private Renderer[] bodyRenderers;
    private Collider[] bodyColliders;
    private bool isMiniBoss;
    private bool crownPositionPending;

    public bool IsMiniBoss => isMiniBoss;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        CreateCrown();
        crownObject.SetActive(false);
    }

    public void Configure(
        bool makeMiniBoss,
        float scaleMultiplier,
        float hpMultiplier,
        float damageMultiplier)
    {
        isMiniBoss = makeMiniBoss;

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (enemyHealth != null)
        {
            enemyHealth.ConfigureRuntimeVariant(
                makeMiniBoss ? scaleMultiplier : 1f,
                makeMiniBoss ? hpMultiplier : 1f,
                makeMiniBoss ? damageMultiplier : 1f);
        }

        if (crownObject == null)
        {
            CreateCrown();
        }

        crownObject.SetActive(makeMiniBoss);
        crownPositionPending = makeMiniBoss;
        gameObject.name = RemoveMiniBossSuffix(gameObject.name) +
            (makeMiniBoss ? " [Mini Boss]" : string.Empty);
    }

    private void OnEnable()
    {
        if (crownObject != null)
        {
            crownObject.SetActive(isMiniBoss);
            crownPositionPending = isMiniBoss;
        }
    }

    private void LateUpdate()
    {
        if (!isMiniBoss || crownObject == null || !crownPositionPending)
        {
            return;
        }

        // Place the crown once after the animated model has evaluated its first
        // frame. Keeping the resulting local pose prevents animation bounds
        // from making the crown bob above the enemy.
        PositionCrownAboveEnemy();
        crownPositionPending = false;
    }

    private void CreateCrown()
    {
        if (crownObject != null)
        {
            return;
        }

        crownObject = new GameObject(CrownObjectName);
        crownObject.transform.SetParent(transform, false);

        MeshFilter filter = crownObject.AddComponent<MeshFilter>();
        crownRenderer = crownObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = GetSharedCrownMesh();
        crownRenderer.sharedMaterial = GetSharedCrownMaterial();
        crownRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        crownRenderer.receiveShadows = false;

        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        bodyColliders = GetComponentsInChildren<Collider>(true);
    }

    private void PositionCrownAboveEnemy()
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
        {
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        Bounds bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer renderer = bodyRenderers[i];
            if (renderer == null || renderer == crownRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            crownObject.transform.localPosition = Vector3.up * 2f;
            crownObject.transform.localScale = Vector3.one * 0.32f;
            return;
        }

        float modelTop = Mathf.Max(bounds.max.y, GetColliderTopWorldY());
        float desiredWorldCrownScale = Mathf.Clamp(
            Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.48f,
            0.22f,
            0.6f);
        Vector3 parentWorldScale = transform.lossyScale;
        float horizontalParentScale = Mathf.Max(
            Mathf.Abs(parentWorldScale.x),
            Mathf.Abs(parentWorldScale.z),
            0.0001f);
        float crownLocalScale = desiredWorldCrownScale / horizontalParentScale;
        float clearance = Mathf.Max(
            MinimumCrownClearance,
            desiredWorldCrownScale * CrownClearanceScaleRatio);
        Vector3 worldPosition = new Vector3(
            bounds.center.x,
            modelTop + clearance,
            bounds.center.z);

        crownObject.transform.localPosition = transform.InverseTransformPoint(worldPosition);
        crownObject.transform.localRotation = Quaternion.identity;
        crownObject.transform.localScale = Vector3.one * crownLocalScale;

        // Validate using the crown's actual rendered bounds. This keeps the crown
        // clear even if its mesh or an enemy model is replaced later.
        float requiredBottom = modelTop + clearance;
        float overlap = requiredBottom - crownRenderer.bounds.min.y;
        if (overlap > 0f)
        {
            crownObject.transform.position += Vector3.up * overlap;
        }
    }

    private float GetColliderTopWorldY()
    {
        if (bodyColliders == null || bodyColliders.Length == 0)
        {
            bodyColliders = GetComponentsInChildren<Collider>(true);
        }

        float top = float.NegativeInfinity;
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            Collider targetCollider = bodyColliders[i];
            if (targetCollider == null)
            {
                continue;
            }

            Vector3 localTop;
            if (targetCollider is CapsuleCollider capsule)
            {
                float verticalExtent = capsule.direction == 1
                    ? capsule.height * 0.5f
                    : capsule.radius;
                localTop = capsule.center + Vector3.up * verticalExtent;
            }
            else if (targetCollider is BoxCollider box)
            {
                localTop = box.center + Vector3.up * (box.size.y * 0.5f);
            }
            else if (targetCollider is SphereCollider sphere)
            {
                localTop = sphere.center + Vector3.up * sphere.radius;
            }
            else
            {
                if (targetCollider.enabled)
                {
                    top = Mathf.Max(top, targetCollider.bounds.max.y);
                }
                continue;
            }

            top = Mathf.Max(top, targetCollider.transform.TransformPoint(localTop).y);
        }

        return float.IsNegativeInfinity(top) ? transform.position.y : top;
    }

    private static Mesh GetSharedCrownMesh()
    {
        if (sharedCrownMesh != null)
        {
            return sharedCrownMesh;
        }

        const int segmentCount = 10;
        const float radius = 0.5f;
        const float bottomHeight = 0f;
        const float valleyHeight = 0.22f;
        const float peakHeight = 0.62f;

        List<Vector3> vertices = new List<Vector3>(segmentCount * 2);
        List<int> triangles = new List<int>(segmentCount * 6);

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / segmentCount;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, bottomHeight, z));
            vertices.Add(new Vector3(
                x,
                i % 2 == 0 ? peakHeight : valleyHeight,
                z));
        }

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % segmentCount;
            int bottom = i * 2;
            int top = bottom + 1;
            int nextBottom = next * 2;
            int nextTop = nextBottom + 1;

            triangles.Add(bottom);
            triangles.Add(top);
            triangles.Add(nextTop);
            triangles.Add(bottom);
            triangles.Add(nextTop);
            triangles.Add(nextBottom);
        }

        sharedCrownMesh = new Mesh
        {
            name = "Shared Mini Boss Crown"
        };
        sharedCrownMesh.SetVertices(vertices);
        sharedCrownMesh.SetTriangles(triangles, 0);
        sharedCrownMesh.RecalculateNormals();
        sharedCrownMesh.RecalculateBounds();
        return sharedCrownMesh;
    }

    private static Material GetSharedCrownMaterial()
    {
        if (sharedCrownMaterial != null)
        {
            return sharedCrownMaterial;
        }

        Shader shader = Shader.Find("Gigachad/Megabonk/Toon Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        sharedCrownMaterial = new Material(shader)
        {
            name = "Shared Mini Boss Crown Gold With Outline",
            color = CrownGoldColor,
            enableInstancing = true
        };

        SetColorIfAvailable(sharedCrownMaterial, "_BaseColor", CrownGoldColor);
        SetColorIfAvailable(sharedCrownMaterial, "_Color", CrownGoldColor);
        SetColorIfAvailable(sharedCrownMaterial, "_OutlineColor", CrownOutlineColor);
        SetFloatIfAvailable(sharedCrownMaterial, "_OutlineWidth", 2.4f);
        SetFloatIfAvailable(sharedCrownMaterial, "_Ambient", 0.95f);
        SetFloatIfAvailable(sharedCrownMaterial, "_LightStrength", 0.15f);
        SetFloatIfAvailable(sharedCrownMaterial, "_LightSteps", 3f);
        SetFloatIfAvailable(sharedCrownMaterial, "_Saturation", 1.35f);
        SetFloatIfAvailable(sharedCrownMaterial, "_ShadowFloor", 1f);
        return sharedCrownMaterial;
    }

    private static void SetColorIfAvailable(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfAvailable(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static string RemoveMiniBossSuffix(string objectName)
    {
        const string suffix = " [Mini Boss]";
        return objectName.EndsWith(suffix)
            ? objectName.Substring(0, objectName.Length - suffix.Length)
            : objectName;
    }
}
