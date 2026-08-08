using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class DesertArenaMegaDecorBuilder
{
    private const string RootName = "MegaDecor_Setpieces";
    private const string AssetRoot = "Assets/GeneratedProps/MegaDecor";
    private const string TargetScenePath = "Assets/Scenes/DesertArena.unity";
    private const string RockMaterialPath =
        "Assets/DesertArena_Materials/Rock.mat";
    private const string ToonLitShaderName = "Gigachad/Megabonk/Toon Lit";

    [MenuItem("Gigachad/Environment/Build Mega Desert Decor")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Mega decor builder must run in Edit Mode.");
            return;
        }

        if (GameObject.Find(RootName) != null)
        {
            Debug.Log("Mega decor already exists. Existing setpieces were preserved.");
            return;
        }

        EnsureFolder(AssetRoot);
        GameObject environment = GameObject.Find("Environment");
        if (environment == null)
        {
            Debug.LogError("Environment root was not found.");
            return;
        }

        Terrain terrain = environment.GetComponentInChildren<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("Terrain_Ground was not found under Environment.");
            return;
        }

        Material stone = LoadRockMaterial();
        Material wood = LoadOrCreateMaterial(
            "Assets/GeneratedProps/Mat_Wood.mat",
            "Wood",
            new Color(0.40f, 0.25f, 0.15f, 1f));
        Material house = LoadOrCreateMaterial(
            "Assets/GeneratedProps/Mat_House.mat",
            "House",
            new Color(0.80f, 0.75f, 0.60f, 1f));

        GameObject root = CreateContainer(RootName, environment.transform, true, true);
        GameObject architecture = CreateContainer("Architecture", root.transform, true, true);
        GameObject cliffs = CreateContainer("MegaCliffs", root.transform, true, true);
        GameObject logs = CreateContainer("FallenLogs", root.transform, true, false);
        GameObject landmarks = CreateContainer("StoneLandmarks", root.transform, true, true);

        GameObject housePrefab = BuildMegaHousePrefab(house, wood);
        GameObject gatePrefab = BuildStoneGatePrefab(stone);
        GameObject cliffPrefab = BuildMegaCliffPrefab(stone);
        GameObject logPrefab = BuildMegaLogPrefab(wood);
        GameObject obeliskPrefab = BuildObeliskPrefab(stone);

        Place(housePrefab, architecture.transform, terrain, new Vector3(-178f, 0f, -142f), 24f, 1.45f);
        Place(housePrefab, architecture.transform, terrain, new Vector3(174f, 0f, -148f), -38f, 1.30f);
        Place(housePrefab, architecture.transform, terrain, new Vector3(-176f, 0f, 152f), 154f, 1.35f);
        Place(housePrefab, architecture.transform, terrain, new Vector3(170f, 0f, 154f), 214f, 1.25f);
        Place(gatePrefab, architecture.transform, terrain, new Vector3(0f, 0f, 224f), 180f, 1.35f);

        Place(cliffPrefab, cliffs.transform, terrain, new Vector3(-226f, 0f, 28f), 90f, 1.10f);
        Place(cliffPrefab, cliffs.transform, terrain, new Vector3(226f, 0f, -12f), -90f, 1.05f);
        Place(cliffPrefab, cliffs.transform, terrain, new Vector3(-18f, 0f, -224f), 0f, 1.15f);
        Place(cliffPrefab, cliffs.transform, terrain, new Vector3(-112f, 0f, 220f), 158f, 0.95f);

        Vector3[] logPositions =
        {
            new Vector3(-218f, 0f, -96f), new Vector3(-204f, 0f, -34f),
            new Vector3(-218f, 0f, 92f), new Vector3(-190f, 0f, 196f),
            new Vector3(-146f, 0f, -202f), new Vector3(-92f, 0f, -182f),
            new Vector3(-74f, 0f, 214f), new Vector3(-32f, 0f, 186f),
            new Vector3(62f, 0f, -208f), new Vector3(116f, 0f, -192f),
            new Vector3(208f, 0f, -96f), new Vector3(204f, 0f, 40f),
            new Vector3(214f, 0f, 118f), new Vector3(198f, 0f, 196f),
            new Vector3(136f, 0f, 210f), new Vector3(92f, 0f, 174f),
            new Vector3(-158f, 0f, 106f), new Vector3(146f, 0f, -80f)
        };

        for (int i = 0; i < logPositions.Length; i++)
        {
            Place(
                logPrefab,
                logs.transform,
                terrain,
                logPositions[i],
                (i * 37f) % 360f,
                0.78f + (i % 4) * 0.08f);
        }

        Place(obeliskPrefab, landmarks.transform, terrain, new Vector3(0f, 0f, -154f), 0f, 1.00f);
        Place(obeliskPrefab, landmarks.transform, terrain, new Vector3(-132f, 0f, 32f), 38f, 0.85f);
        Place(obeliskPrefab, landmarks.transform, terrain, new Vector3(136f, 0f, 62f), -22f, 0.95f);
        Place(obeliskPrefab, landmarks.transform, terrain, new Vector3(82f, 0f, -82f), 70f, 0.75f);
        Place(obeliskPrefab, landmarks.transform, terrain, new Vector3(38f, 0f, 154f), -48f, 0.90f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log("Mega desert decor built: 4 houses, 1 U gate, 4 mega cliffs, 18 logs and 5 stone landmarks.");
    }

    [MenuItem("Gigachad/Environment/Rebuild Mega Desert Decor")]
    public static void Rebuild()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Mega decor builder must run in Edit Mode.");
            return;
        }

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(existingRoot);
        }

        if (AssetDatabase.IsValidFolder(AssetRoot))
        {
            AssetDatabase.DeleteAsset(AssetRoot);
        }

        AssetDatabase.Refresh();
        Build();
    }

    public static void RebuildTargetScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Mega decor builder must run in Edit Mode.");
            return;
        }

        Scene targetScene =
            EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError("Could not open " + TargetScenePath + ".");
            return;
        }

        Rebuild();
    }

    private static GameObject CreateContainer(string name, Transform parent, bool batching, bool occluder)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent, false);
        ApplyStaticFlags(container, batching, occluder);
        return container;
    }

    private static Material LoadOrCreateMaterial(string path, string materialName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("No compatible shader found for " + materialName + ".");
            return null;
        }

        material = new Material(shader);
        material.name = materialName;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material LoadRockMaterial()
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
        if (material == null)
        {
            material = LoadOrCreateMaterial(
                RockMaterialPath,
                "Rock",
                new Color(0.615f, 0.48f, 0.35f, 1f));
        }

        Shader toonLitShader = Shader.Find(ToonLitShaderName);
        if (toonLitShader == null)
        {
            Debug.LogError(
                "Could not find the toon-lit shader '" +
                ToonLitShaderName + "'. Rock will keep its current shader.");
            return material;
        }

        if (material.shader != toonLitShader)
        {
            material.shader = toonLitShader;
            EditorUtility.SetDirty(material);
        }

        material.name = "Rock";
        return material;
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static GameObject BuildMegaLogPrefab(Material wood)
    {
        Mesh high = SaveMesh(BuildLogMesh(10, "MegaLog_L0"), AssetRoot + "/Mesh_MegaLog_L0.asset");
        Mesh low = SaveMesh(BuildLogMesh(6, "MegaLog_L1"), AssetRoot + "/Mesh_MegaLog_L1.asset");

        GameObject root = new GameObject("Prop_MegaLog");
        ApplyStaticFlags(root, true, false);
        Renderer highRenderer = AddMeshPart("LOD0_MegaLog", root.transform, high, wood, true, true);
        Renderer lowRenderer = AddMeshPart("LOD1_MegaLog", root.transform, low, wood, false, false);
        SetLodGroup(root, new[] { highRenderer }, new[] { lowRenderer }, 0.48f);
        return SavePrefab(root, AssetRoot + "/Prop_MegaLog.prefab");
    }

    private static GameObject BuildStoneGatePrefab(Material stone)
    {
        Mesh high = SaveMesh(BuildGateMesh(true, "StoneGate_L0"), AssetRoot + "/Mesh_StoneGate_L0.asset");
        Mesh low = SaveMesh(BuildGateMesh(false, "StoneGate_L1"), AssetRoot + "/Mesh_StoneGate_L1.asset");

        GameObject root = new GameObject("Prop_StoneGate_U");
        ApplyStaticFlags(root, true, true);
        Renderer highRenderer = AddMeshPart("LOD0_StoneGate", root.transform, high, stone, true, true);
        Renderer lowRenderer = AddMeshPart("LOD1_StoneGate", root.transform, low, stone, false, false);

        AddBoxCollider(root, new Vector3(-10.5f, 7.5f, 0f), new Vector3(7.2f, 15f, 7.4f));
        AddBoxCollider(root, new Vector3(10.5f, 7.5f, 0f), new Vector3(7.2f, 15f, 7.4f));
        AddBoxCollider(root, new Vector3(0f, 15.1f, 0f), new Vector3(28f, 5.2f, 7.8f));
        SetLodGroup(root, new[] { highRenderer }, new[] { lowRenderer }, 0.62f);
        SetLayerRecursively(root, "Obstacles");
        return SavePrefab(root, AssetRoot + "/Prop_StoneGate_U.prefab");
    }

    private static GameObject BuildMegaCliffPrefab(Material stone)
    {
        Mesh high = SaveMesh(BuildCliffMesh(true, "MegaCliff_L0"), AssetRoot + "/Mesh_MegaCliff_L0.asset");
        Mesh low = SaveMesh(BuildCliffMesh(false, "MegaCliff_L1"), AssetRoot + "/Mesh_MegaCliff_L1.asset");

        GameObject root = new GameObject("Prop_MegaCliff");
        ApplyStaticFlags(root, true, true);
        Renderer highRenderer = AddMeshPart("LOD0_MegaCliff", root.transform, high, stone, true, true);
        Renderer lowRenderer = AddMeshPart("LOD1_MegaCliff", root.transform, low, stone, false, false);
        AddBoxCollider(root, new Vector3(0f, 17f, 0f), new Vector3(28f, 34f, 44f));
        SetLodGroup(root, new[] { highRenderer }, new[] { lowRenderer }, 0.58f);
        SetLayerRecursively(root, "Obstacles");
        return SavePrefab(root, AssetRoot + "/Prop_MegaCliff.prefab");
    }

    private static GameObject BuildMegaHousePrefab(Material house, Material wood)
    {
        Mesh wallHigh = SaveMesh(BuildHouseWallMesh(true, "MegaHouse_Walls_L0"), AssetRoot + "/Mesh_MegaHouse_Walls_L0.asset");
        Mesh roofHigh = SaveMesh(BuildHouseRoofMesh(true, "MegaHouse_Roof_L0"), AssetRoot + "/Mesh_MegaHouse_Roof_L0.asset");
        Mesh accentHigh = SaveMesh(BuildHouseAccentMesh("MegaHouse_Accents_L0"), AssetRoot + "/Mesh_MegaHouse_Accents_L0.asset");
        Mesh wallLow = SaveMesh(BuildHouseWallMesh(false, "MegaHouse_Walls_L1"), AssetRoot + "/Mesh_MegaHouse_Walls_L1.asset");
        Mesh roofLow = SaveMesh(BuildHouseRoofMesh(false, "MegaHouse_Roof_L1"), AssetRoot + "/Mesh_MegaHouse_Roof_L1.asset");

        GameObject root = new GameObject("Prop_MegaHouse");
        ApplyStaticFlags(root, true, true);

        GameObject lod0 = new GameObject("LOD0_MegaHouse");
        lod0.transform.SetParent(root.transform, false);
        ApplyStaticFlags(lod0, true, true);
        Renderer wallRenderer = AddMeshPart("HouseWalls", lod0.transform, wallHigh, house, true, true);
        Renderer roofRenderer = AddMeshPart("HouseRoof", lod0.transform, roofHigh, house, true, true);
        AddMeshPart("HouseWoodAccents", lod0.transform, accentHigh, wood, true, true);

        GameObject lod1 = new GameObject("LOD1_MegaHouse");
        lod1.transform.SetParent(root.transform, false);
        ApplyStaticFlags(lod1, true, false);
        Renderer lowWallRenderer = AddMeshPart("HouseWalls_Low", lod1.transform, wallLow, house, false, false);
        AddMeshPart("HouseRoof_Low", lod1.transform, roofLow, house, false, false);

        AddBoxCollider(root, new Vector3(0f, 4f, 0f), new Vector3(18f, 8f, 14f));
        SetLodGroup(root, new[] { wallRenderer, roofRenderer }, new[] { lowWallRenderer }, 0.66f);
        SetLayerRecursively(root, "Obstacles");
        return SavePrefab(root, AssetRoot + "/Prop_MegaHouse.prefab");
    }

    private static GameObject BuildObeliskPrefab(Material stone)
    {
        Mesh high = SaveMesh(BuildObeliskMesh(true, "StoneObelisk_L0"), AssetRoot + "/Mesh_StoneObelisk_L0.asset");
        Mesh low = SaveMesh(BuildObeliskMesh(false, "StoneObelisk_L1"), AssetRoot + "/Mesh_StoneObelisk_L1.asset");

        GameObject root = new GameObject("Prop_StoneObelisk");
        ApplyStaticFlags(root, true, true);
        Renderer highRenderer = AddMeshPart("LOD0_StoneObelisk", root.transform, high, stone, true, true);
        Renderer lowRenderer = AddMeshPart("LOD1_StoneObelisk", root.transform, low, stone, false, false);
        AddBoxCollider(root, new Vector3(0f, 5f, 0f), new Vector3(7f, 10f, 7f));
        SetLodGroup(root, new[] { highRenderer }, new[] { lowRenderer }, 0.50f);
        SetLayerRecursively(root, "Obstacles");
        return SavePrefab(root, AssetRoot + "/Prop_StoneObelisk.prefab");
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static Renderer AddMeshPart(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        bool castsShadows,
        bool occluder)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = castsShadows;
        renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.allowOcclusionWhenDynamic = false;
        ApplyStaticFlags(part, true, occluder);
        return renderer;
    }

    private static void SetLodGroup(GameObject root, Renderer[] high, Renderer[] low, float highHeight)
    {
        LODGroup group = root.AddComponent<LODGroup>();
        LOD[] lods =
        {
            new LOD(highHeight, high),
            new LOD(0.18f, low),
            new LOD(0.025f, new Renderer[0])
        };
        group.SetLODs(lods);
        group.RecalculateBounds();
        group.fadeMode = LODFadeMode.None;
        group.animateCrossFading = false;
    }

    private static void AddBoxCollider(GameObject root, Vector3 center, Vector3 size)
    {
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
    }

    private static void ApplyStaticFlags(GameObject target, bool batching, bool occluder)
    {
        StaticEditorFlags flags = StaticEditorFlags.OccludeeStatic;
        if (batching)
        {
            flags |= StaticEditorFlags.BatchingStatic;
        }

        if (occluder)
        {
            flags |= StaticEditorFlags.OccluderStatic;
        }

        GameObjectUtility.SetStaticEditorFlags(target, flags);
    }

    private static void SetLayerRecursively(GameObject target, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layerName);
        }
    }

    private static void Place(
        GameObject prefab,
        Transform parent,
        Terrain terrain,
        Vector3 positionXZ,
        float yaw,
        float scale)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = prefab.name + "_" + parent.childCount.ToString("00");
        instance.transform.SetParent(parent, false);
        float terrainY = terrain.SampleHeight(new Vector3(positionXZ.x, 0f, positionXZ.z)) + terrain.transform.position.y;
        instance.transform.position = new Vector3(positionXZ.x, terrainY, positionXZ.z);
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.localScale = Vector3.one * scale;
        ApplyStaticFlags(instance, true, true);
    }

private static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static Mesh BuildLogMesh(int sides, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 1.8f, 16f, sides, Quaternion.identity);
        builder.AddCylinder(new Vector3(7f, 0f, 0f), 0.7f, 4.4f, Mathf.Max(5, sides - 2), Quaternion.Euler(0f, 0f, 78f));
        builder.AddCylinder(new Vector3(-7f, 0f, 0f), 0.55f, 3.6f, Mathf.Max(5, sides - 2), Quaternion.Euler(0f, 0f, -72f));
        return builder.ToMesh(meshName);
    }

    private static Mesh BuildGateMesh(bool detailed, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        int sides = detailed ? 7 : 5;

        builder.AddRock(
            new Vector3(-10.5f, 0f, 0f),
            new Vector3(7.2f, 15.5f, 7.8f),
            Quaternion.Euler(0f, -7f, 0f),
            0.16f,
            sides);
        builder.AddRock(
            new Vector3(10.5f, 0f, 0f),
            new Vector3(7.2f, 15.5f, 7.8f),
            Quaternion.Euler(0f, 7f, 0f),
            0.82f,
            sides);
        builder.AddFacetedBlock(
            new Vector3(0f, 15.1f, 0f),
            new Vector3(28f, 5.2f, 8f),
            Quaternion.Euler(0f, 0f, -1.5f),
            0.23f,
            0.12f);

        if (detailed)
        {
            builder.AddFacetedBlock(
                new Vector3(-10.5f, 14.9f, 0f),
                new Vector3(8f, 2.2f, 8.7f),
                Quaternion.Euler(0f, 0f, -4f),
                0.34f,
                0.15f);
            builder.AddFacetedBlock(
                new Vector3(10.5f, 14.9f, 0f),
                new Vector3(8f, 2.2f, 8.7f),
                Quaternion.Euler(0f, 0f, 4f),
                0.78f,
                0.15f);
            builder.AddFacetedBlock(
                new Vector3(0f, 17.8f, 0f),
                new Vector3(17f, 1.3f, 8.5f),
                Quaternion.Euler(0f, 0f, -1.5f),
                0.52f,
                0.2f);
            builder.AddRock(
                new Vector3(-13.5f, 0f, 0.8f),
                new Vector3(5f, 4f, 6f),
                Quaternion.Euler(0f, -12f, 0f),
                0.42f,
                sides);
            builder.AddRock(
                new Vector3(13.5f, 0f, -0.8f),
                new Vector3(5f, 4f, 6f),
                Quaternion.Euler(0f, 12f, 0f),
                0.94f,
                sides);
        }

        return builder.ToMesh(meshName);
    }

    private static Mesh BuildCliffMesh(bool detailed, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        int sides = detailed ? 6 : 5;

        // The reference is a cluster of tall, broken monoliths rather than a
        // low boulder. Keep five overlapping spires so the silhouette reads
        // clearly from the arena's top-down camera.
        builder.AddRockSpire(
            new Vector3(1.0f, 0f, -15.5f),
            new Vector3(7.5f, 17f, 9.5f),
            Quaternion.Euler(0f, -9f, 0f),
            0.16f,
            sides);
        builder.AddRockSpire(
            new Vector3(-0.6f, 0f, -7.5f),
            new Vector3(10f, 24f, 11f),
            Quaternion.Euler(0f, -4f, 0f),
            0.38f,
            sides);
        builder.AddRockSpire(
            new Vector3(0.4f, 0f, 0f),
            new Vector3(11.5f, 31f, 12f),
            Quaternion.Euler(0f, 4f, 0f),
            0.58f,
            sides);
        builder.AddRockSpire(
            new Vector3(-0.4f, 0f, 8.5f),
            new Vector3(9.5f, 23f, 11f),
            Quaternion.Euler(0f, 8f, 0f),
            0.76f,
            sides);
        builder.AddRockSpire(
            new Vector3(0.9f, 0f, 16f),
            new Vector3(7.5f, 18.5f, 9.5f),
            Quaternion.Euler(0f, 13f, 0f),
            0.94f,
            sides);

        // A shallow foot visually joins the spires without making the asset
        // read as a smooth mound.
        builder.AddRock(
            new Vector3(0f, 0f, 0f),
            new Vector3(30f, 4.2f, 14f),
            Quaternion.Euler(0f, 2f, 0f),
            0.3f,
            sides);

        return builder.ToMesh(meshName);
    }

    private static Mesh BuildHouseWallMesh(bool detailed, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddBox(new Vector3(0f, 4f, 0f), new Vector3(18f, 8f, 14f), Quaternion.identity);
        builder.AddBox(new Vector3(-11f, 3.5f, 1f), new Vector3(4f, 7f, 9f), Quaternion.identity);
        builder.AddBox(new Vector3(11f, 3.5f, 1f), new Vector3(4f, 7f, 9f), Quaternion.identity);

        if (detailed)
        {
            builder.AddBox(new Vector3(0f, 10f, 1f), new Vector3(13f, 3f, 9f), Quaternion.identity);
            builder.AddBox(new Vector3(0f, 4f, 7.2f), new Vector3(5f, 8f, 1f), Quaternion.identity);
        }

        return builder.ToMesh(meshName);
    }

    private static Mesh BuildHouseRoofMesh(bool detailed, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddRoofPrism(new Vector3(0f, 8f, 0f), 22f, 17f, 5f, Quaternion.identity);
        builder.AddRoofPrism(new Vector3(-11f, 7f, 1f), 6f, 11f, 3f, Quaternion.identity);
        builder.AddRoofPrism(new Vector3(11f, 7f, 1f), 6f, 11f, 3f, Quaternion.identity);

        if (detailed)
        {
            builder.AddBox(new Vector3(0f, 13.7f, 0f), new Vector3(2f, 0.7f, 17f), Quaternion.identity);
            builder.AddBox(new Vector3(0f, 11f, -8.8f), new Vector3(6f, 1.5f, 3f), Quaternion.identity);
        }

        return builder.ToMesh(meshName);
    }

    private static Mesh BuildHouseAccentMesh(string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddBox(new Vector3(0f, 3f, -7.25f), new Vector3(3.2f, 6f, 0.7f), Quaternion.identity);
        builder.AddBox(new Vector3(-6f, 4.5f, -7.3f), new Vector3(3f, 2.6f, 0.7f), Quaternion.identity);
        builder.AddBox(new Vector3(6f, 4.5f, -7.3f), new Vector3(3f, 2.6f, 0.7f), Quaternion.identity);
        builder.AddBox(new Vector3(0f, 7.2f, -7.3f), new Vector3(15f, 0.7f, 0.7f), Quaternion.identity);
        builder.AddBox(new Vector3(-8.6f, 4.3f, -7.3f), new Vector3(0.7f, 8f, 0.7f), Quaternion.identity);
        builder.AddBox(new Vector3(8.6f, 4.3f, -7.3f), new Vector3(0.7f, 8f, 0.7f), Quaternion.identity);
        return builder.ToMesh(meshName);
    }

    private static Mesh BuildObeliskMesh(bool detailed, string meshName)
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddBox(new Vector3(0f, 0.75f, 0f), new Vector3(7f, 1.5f, 7f), Quaternion.identity);
        builder.AddRock(new Vector3(0f, 1.5f, 0f), new Vector3(4.6f, 10f, 4.6f), Quaternion.Euler(0f, 10f, 0f), 0.4f);

        if (detailed)
        {
            builder.AddRock(new Vector3(-2.4f, 0.9f, 1.5f), new Vector3(2.4f, 3.6f, 2.3f), Quaternion.Euler(0f, -15f, 0f), 0.8f);
            builder.AddRock(new Vector3(2.2f, 0.8f, -1.8f), new Vector3(2f, 3f, 2f), Quaternion.Euler(0f, 26f, 0f), 0.2f);
        }

        return builder.ToMesh(meshName);
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            int start = vertices.Count;
            Vector3 half = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, half.z),
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x, half.y, -half.z),
                new Vector3(half.x, half.y, -half.z),
                new Vector3(half.x, half.y, half.z),
                new Vector3(-half.x, half.y, half.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                vertices.Add(center + rotation * corners[i]);
            }

            AddQuad(start + 0, start + 1, start + 2, start + 3);
            AddQuad(start + 7, start + 6, start + 5, start + 4);
            AddQuad(start + 0, start + 4, start + 5, start + 1);
            AddQuad(start + 1, start + 5, start + 6, start + 2);
            AddQuad(start + 2, start + 6, start + 7, start + 3);
            AddQuad(start + 4, start + 0, start + 3, start + 7);
        }

        public void AddCylinder(Vector3 center, float radius, float length, int sides, Quaternion rotation)
        {
            int start = vertices.Count;
            float halfLength = length * 0.5f;

            for (int ring = 0; ring < 2; ring++)
            {
                float x = ring == 0 ? -halfLength : halfLength;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    Vector3 local = new Vector3(x, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    vertices.Add(center + rotation * local);
                }
            }

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddTriangle(start + side, start + sides + side, start + sides + next);
                AddTriangle(start + side, start + sides + next, start + next);
            }

            int leftCenter = vertices.Count;
            vertices.Add(center + rotation * new Vector3(-halfLength, 0f, 0f));
            int rightCenter = vertices.Count;
            vertices.Add(center + rotation * new Vector3(halfLength, 0f, 0f));

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddTriangle(leftCenter, start + next, start + side);
                AddTriangle(rightCenter, start + sides + side, start + sides + next);
            }
        }

        public void AddRock(Vector3 center, Vector3 size, Quaternion rotation, float variation, int sideCount = 7)
        {
            int sides = Mathf.Clamp(sideCount, 5, 9);
            int bottomStart = vertices.Count;
            int topStart = bottomStart + sides;
            float phase = variation * 2.7f;

            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float factor = 0.78f + 0.20f * Mathf.Sin(side * 1.73f + phase);
                Vector3 local = new Vector3(
                    Mathf.Cos(angle) * size.x * 0.5f * factor,
                    0f,
                    Mathf.Sin(angle) * size.z * 0.5f * factor);
                vertices.Add(center + rotation * local);
            }

            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float factor = 0.48f + 0.22f * Mathf.Cos(side * 1.41f + phase);
                float topY = size.y * (0.75f + 0.20f * Mathf.Abs(Mathf.Sin(side + phase)));
                Vector3 local = new Vector3(
                    Mathf.Cos(angle) * size.x * 0.5f * factor,
                    topY,
                    Mathf.Sin(angle) * size.z * 0.5f * factor);
                vertices.Add(center + rotation * local);
            }

            int bottomCenter = vertices.Count;
            vertices.Add(center + rotation * Vector3.zero);
            int topCenter = vertices.Count;
            vertices.Add(center + rotation * new Vector3(0f, size.y, 0f));

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int bottomA = bottomStart + side;
                int topA = topStart + side;
                int bottomB = bottomStart + next;
                int topB = topStart + next;
                AddTriangle(bottomA, topA, topB);
                AddTriangle(bottomA, topB, bottomB);
                AddTriangle(bottomCenter, bottomB, bottomA);
                AddTriangle(topCenter, topA, topB);
            }
        }


        public void AddRockSpire(
            Vector3 center,
            Vector3 size,
            Quaternion rotation,
            float variation,
            int sideCount = 6)
        {
            int sides = Mathf.Clamp(sideCount, 5, 8);
            float phase = variation * 5.3f;
            Vector3[] bottom = new Vector3[sides];
            Vector3[] top = new Vector3[sides];

            for (int side = 0; side < sides; side++)
            {
                float angle =
                    side * Mathf.PI * 2f / sides +
                    0.08f * Mathf.Sin(phase);
                float baseFactor =
                    0.88f + 0.10f * Mathf.Sin(side * 1.61f + phase);
                float taper =
                    0.58f + 0.16f * Mathf.Cos(side * 1.37f + phase);
                float topHeight =
                    0.78f +
                    0.20f *
                    (0.5f + 0.5f * Mathf.Sin(side * 1.83f + phase));

                Vector3 bottomLocal = new Vector3(
                    Mathf.Cos(angle) * size.x * 0.5f * baseFactor,
                    0f,
                    Mathf.Sin(angle) * size.z * 0.5f * baseFactor);
                Vector3 topLocal = new Vector3(
                    Mathf.Cos(angle) * size.x * 0.5f * taper,
                    size.y * topHeight,
                    Mathf.Sin(angle) * size.z * 0.5f * taper);

                bottom[side] = center + rotation * bottomLocal;
                top[side] = center + rotation * topLocal;
            }

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddFlatQuad(
                    bottom[side],
                    top[side],
                    top[next],
                    bottom[next]);
            }

            Vector3 topCenter = center + rotation * new Vector3(
                size.x * 0.04f * Mathf.Sin(phase + 0.6f),
                size.y * (0.91f + 0.05f * Mathf.Cos(phase)),
                size.z * 0.04f * Mathf.Cos(phase + 0.3f));
            int topCenterIndex = vertices.Count;
            vertices.Add(topCenter);
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int topA = vertices.Count;
                vertices.Add(top[side]);
                int topB = vertices.Count;
                vertices.Add(top[next]);
                AddTriangle(topCenterIndex, topB, topA);
            }

            int bottomCenterIndex = vertices.Count;
            vertices.Add(center + rotation * Vector3.zero);
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int bottomA = vertices.Count;
                vertices.Add(bottom[side]);
                int bottomB = vertices.Count;
                vertices.Add(bottom[next]);
                AddTriangle(bottomCenterIndex, bottomA, bottomB);
            }
        }
        public void AddFacetedBlock(
            Vector3 center,
            Vector3 size,
            Quaternion rotation,
            float variation,
            float topInset)
        {
            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;
            float halfZ = size.z * 0.5f;
            float inset = Mathf.Clamp(topInset, 0.02f, 0.45f);
            float topX = halfX * (1f - inset);
            float topZ = halfZ * (1f - inset);
            float phase = variation * 5.1f;
            float topY0 = halfY * (1f + 0.05f * Mathf.Sin(phase));
            float topY1 = halfY * (1f + 0.05f * Mathf.Cos(phase + 0.8f));
            float topY2 = halfY * (1f + 0.05f * Mathf.Sin(phase + 1.7f));
            float topY3 = halfY * (1f + 0.05f * Mathf.Cos(phase + 2.4f));

            Vector3[] corners =
            {
                new Vector3(
                    -halfX * (1f + 0.04f * Mathf.Sin(phase)),
                    -halfY,
                    -halfZ * (1f + 0.03f * Mathf.Cos(phase))),
                new Vector3(
                    halfX * (1f + 0.04f * Mathf.Cos(phase + 0.5f)),
                    -halfY,
                    -halfZ * (1f + 0.03f * Mathf.Sin(phase + 0.5f))),
                new Vector3(
                    halfX * (1f + 0.04f * Mathf.Sin(phase + 1.1f)),
                    -halfY,
                    halfZ * (1f + 0.03f * Mathf.Cos(phase + 1.1f))),
                new Vector3(
                    -halfX * (1f + 0.04f * Mathf.Cos(phase + 1.7f)),
                    -halfY,
                    halfZ * (1f + 0.03f * Mathf.Sin(phase + 1.7f))),
                new Vector3(
                    -topX * (1f + 0.05f * Mathf.Sin(phase + 0.2f)),
                    topY0,
                    -topZ * (1f + 0.05f * Mathf.Cos(phase + 0.2f))),
                new Vector3(
                    topX * (1f + 0.05f * Mathf.Cos(phase + 0.7f)),
                    topY1,
                    -topZ * (1f + 0.05f * Mathf.Sin(phase + 0.7f))),
                new Vector3(
                    topX * (1f + 0.05f * Mathf.Sin(phase + 1.3f)),
                    topY2,
                    topZ * (1f + 0.05f * Mathf.Cos(phase + 1.3f))),
                new Vector3(
                    -topX * (1f + 0.05f * Mathf.Cos(phase + 1.9f)),
                    topY3,
                    topZ * (1f + 0.05f * Mathf.Sin(phase + 1.9f)))
            };

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = center + rotation * corners[i];
            }

            AddFlatQuad(corners[0], corners[1], corners[2], corners[3]);
            AddFlatQuad(corners[7], corners[6], corners[5], corners[4]);
            AddFlatQuad(corners[0], corners[4], corners[5], corners[1]);
            AddFlatQuad(corners[1], corners[5], corners[6], corners[2]);
            AddFlatQuad(corners[2], corners[6], corners[7], corners[3]);
            AddFlatQuad(corners[4], corners[0], corners[3], corners[7]);
        }
        public void AddRoofPrism(Vector3 baseCenter, float width, float depth, float height, Quaternion rotation)
        {
            int start = vertices.Count;
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            Vector3[] local =
            {
                new Vector3(-halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(0f, height, -halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(halfWidth, 0f, halfDepth),
                new Vector3(0f, height, halfDepth)
            };

            for (int i = 0; i < local.Length; i++)
            {
                vertices.Add(baseCenter + rotation * local[i]);
            }

            AddTriangle(start + 0, start + 1, start + 2);
            AddTriangle(start + 3, start + 5, start + 4);
            AddQuad(start + 0, start + 3, start + 4, start + 1);
            AddQuad(start + 0, start + 2, start + 5, start + 3);
            AddQuad(start + 1, start + 4, start + 5, start + 2);
        }

        public Mesh ToMesh(string meshName)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }


        private void AddFlatQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            AddTriangle(start + 0, start + 1, start + 2);
            AddTriangle(start + 0, start + 2, start + 3);
        }
        private void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        private void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
