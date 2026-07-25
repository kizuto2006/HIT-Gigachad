using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class XPGemModelGenerator
{
    private const string RootFolder = "Assets/GeneratedProps/XPGem";
    private const string PrefabFolder = "Assets/Prefab/XP";
    private const string MeshPath = RootFolder + "/XPGem_Cyan_Mesh.asset";
    private const string LightMaterialPath = RootFolder + "/XPGem_Light.mat";
    private const string CyanMaterialPath = RootFolder + "/XPGem_Cyan.mat";
    private const string DarkMaterialPath = RootFolder + "/XPGem_Dark.mat";
    private const string PrefabPath = PrefabFolder + "/XPGem_Cyan.prefab";
    private const string MummyPrefabPath = "Assets/Prefab/Mummy.prefab";

    [MenuItem("Tools/Gigachad/Generate Cyan XP Gem")]
    public static void Generate()
    {
        EnsureFolder("Assets/GeneratedProps", "XPGem");
        EnsureFolder("Assets/Prefab", "XP");

        Mesh mesh = CreateFacetedGemMesh();
        SaveOrReplaceAsset(mesh, MeshPath);

        Material lightMaterial = CreateGemMaterial(
            "XP Gem Light",
            new Color(0.35f, 1f, 1f, 1f),
            new Color(0.12f, 1.7f, 1.8f));
        SaveOrReplaceAsset(lightMaterial, LightMaterialPath);

        Material cyanMaterial = CreateGemMaterial(
            "XP Gem Cyan",
            new Color(0.02f, 0.82f, 0.9f, 1f),
            new Color(0f, 1.05f, 1.25f));
        SaveOrReplaceAsset(cyanMaterial, CyanMaterialPath);

        Material darkMaterial = CreateGemMaterial(
            "XP Gem Dark",
            new Color(0.01f, 0.25f, 0.34f, 1f),
            new Color(0f, 0.42f, 0.55f));
        SaveOrReplaceAsset(darkMaterial, DarkMaterialPath);

        GameObject prefab = CreateGemPrefab(
            AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath),
            AssetDatabase.LoadAssetAtPath<Material>(LightMaterialPath),
            AssetDatabase.LoadAssetAtPath<Material>(CyanMaterialPath),
            AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath));

        AssignGemToMummy(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[XPGemModelGenerator] Generated XP gem prefab at {PrefabPath}");
    }

    public static void GenerateFromCommandLine()
    {
        Generate();
    }

    private static Mesh CreateFacetedGemMesh()
    {
        const int segments = 8;
        Vector3[] topRing = CreateRing(segments, 0.47f, 0.34f, 0.78f, 22.5f);
        Vector3[] girdleRing = CreateRing(segments, 0.78f, 0.06f, 0.78f, 22.5f);
        Vector3[] lowerRing = CreateRing(segments, 0.47f, -0.3f, 0.78f, 22.5f);
        Vector3 tableCenter = new Vector3(0f, 0.35f, 0f);
        Vector3 bottomPoint = new Vector3(0f, -0.82f, 0f);

        List<Vector3> vertices = new List<Vector3>(segments * 21);
        List<int>[] triangles =
        {
            new List<int>(segments * 6),
            new List<int>(segments * 9),
            new List<int>(segments * 6)
        };

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            AddTriangle(vertices, triangles[0], tableCenter, topRing[next], topRing[i]);

            int crownMaterialA = i % 3 == 0 ? 0 : 1;
            int crownMaterialB = i % 2 == 0 ? 0 : 1;
            AddTriangle(vertices, triangles[crownMaterialA], topRing[i], topRing[next], girdleRing[i]);
            AddTriangle(vertices, triangles[crownMaterialB], topRing[next], girdleRing[next], girdleRing[i]);

            int pavilionMaterialA = i % 2 == 0 ? 1 : 2;
            int pavilionMaterialB = i % 3 == 0 ? 2 : 1;
            AddTriangle(vertices, triangles[pavilionMaterialA], girdleRing[i], girdleRing[next], lowerRing[i]);
            AddTriangle(vertices, triangles[pavilionMaterialB], girdleRing[next], lowerRing[next], lowerRing[i]);

            int bottomMaterial = i % 2 == 0 ? 1 : 2;
            AddTriangle(vertices, triangles[bottomMaterial], lowerRing[i], lowerRing[next], bottomPoint);
        }

        Mesh mesh = new Mesh
        {
            name = "XPGem_Cyan_Faceted"
        };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = triangles.Length;

        for (int i = 0; i < triangles.Length; i++)
        {
            mesh.SetTriangles(triangles[i], i, false);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3[] CreateRing(
        int segments,
        float radius,
        float y,
        float zScale,
        float angleOffsetDegrees)
    {
        Vector3[] ring = new Vector3[segments];
        float offset = angleOffsetDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments + offset;
            ring[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                y,
                Mathf.Sin(angle) * radius * zScale);
        }

        return ring;
    }

    private static void AddTriangle(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        int firstIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(firstIndex);
        triangles.Add(firstIndex + 1);
        triangles.Add(firstIndex + 2);
    }

    private static Material CreateGemMaterial(string materialName, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = baseColor,
            enableInstancing = true
        };

        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_Color", baseColor);
        material.SetFloat("_Metallic", 0.2f);
        material.SetFloat("_Smoothness", 0.58f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emissionColor);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return material;
    }

    private static GameObject CreateGemPrefab(
        Mesh mesh,
        Material lightMaterial,
        Material cyanMaterial,
        Material darkMaterial)
    {
        GameObject gemObject = new GameObject("XPGem_Cyan");
        MeshFilter meshFilter = gemObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gemObject.AddComponent<MeshRenderer>();
        SphereCollider collider = gemObject.AddComponent<SphereCollider>();
        XPGem xpGem = gemObject.AddComponent<XPGem>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterials = new[] { lightMaterial, cyanMaterial, darkMaterial };
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        collider.isTrigger = true;
        collider.center = new Vector3(0f, -0.12f, 0f);
        collider.radius = 0.72f;

        xpGem.xpAmount = 1;
        xpGem.playerHeightRatio = 0.1f;
        xpGem.magnetRange = 1f;
        xpGem.magnetSpeed = 10f;
        xpGem.pickupRange = 0.25f;
        xpGem.lifetime = 30f;
        xpGem.dropHeight = 1.5f;
        xpGem.dropDuration = 0.45f;
        xpGem.bounceHeight = 0.12f;
        xpGem.bounceDuration = 0.18f;
        xpGem.groundHoverHeight = 0.16f;
        xpGem.rotateSpeed = 90f;
        xpGem.bobAmplitude = 0.15f;
        xpGem.bobSpeed = 2f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gemObject, PrefabPath);
        Object.DestroyImmediate(gemObject);
        return prefab;
    }

    private static void AssignGemToMummy(GameObject gemPrefab)
    {
        if (gemPrefab == null || !AssetDatabase.LoadAssetAtPath<GameObject>(MummyPrefabPath))
        {
            return;
        }

        GameObject mummyRoot = PrefabUtility.LoadPrefabContents(MummyPrefabPath);
        try
        {
            EnemyHealth enemyHealth = mummyRoot.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.xpGemPrefab = gemPrefab;
                PrefabUtility.SaveAsPrefabAsset(mummyRoot, MummyPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(mummyRoot);
        }
    }

    private static void SaveOrReplaceAsset(Object asset, string path)
    {
        Object existingAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (existingAsset != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(asset, path);
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        string fullPath = parentFolder + "/" + childFolder;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}
