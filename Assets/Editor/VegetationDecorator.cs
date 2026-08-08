using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class VegetationDecorator : Editor
{
    // Prefab paths from the Low Poly Desert Environment pack
    static readonly string[] CACTUS_PREFABS = {
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Cactus_01.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Cactus_02.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Cactus_03.prefab",
    };

    static readonly string[] TREE_PREFABS = {
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Tree_01.prefab",
    };

    static readonly string[] ROCK_PREFABS = {
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Rock_01.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Rock_02.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Rock_03.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Rock_04.prefab",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Rock_05.prefab",
    };

    [MenuItem("Tools/Decorate Map with Vegetation")]
    public static void DecorateMap()
    {
        if (!EditorUtility.DisplayDialog("Decorate Map",
            "This will add Cactus, Tree, and extra Rock decorations to the Vegetation group.\n\nContinue?",
            "Decorate!", "Cancel")) return;

        try
        {
            EditorUtility.DisplayProgressBar("Vegetation Decorator", "Finding Vegetation group...", 0.1f);

            // Find or create Vegetation group
            GameObject env = GameObject.Find("Environment");
            if (env == null)
            {
                Debug.LogError("Could not find 'Environment' object.");
                EditorUtility.ClearProgressBar();
                return;
            }

            Transform vegTransform = env.transform.Find("Vegetation");
            GameObject vegetation;
            if (vegTransform != null)
            {
                vegetation = vegTransform.gameObject;
            }
            else
            {
                vegetation = new GameObject("Vegetation");
                vegetation.transform.parent = env.transform;
                vegetation.transform.localPosition = Vector3.zero;
            }

            // Ensure Vegetation is active
            vegetation.SetActive(true);

            // Get terrain bounds
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            Vector3 terrainPos = Vector3.zero;
            Vector3 terrainSize = new Vector3(600, 10, 600);

            if (terrain != null)
            {
                terrainPos = terrain.transform.position;
                terrainSize = terrain.terrainData.size;
            }

            float minX = terrainPos.x + 10f;
            float maxX = terrainPos.x + terrainSize.x - 10f;
            float minZ = terrainPos.z + 10f;
            float maxZ = terrainPos.z + terrainSize.z - 10f;

            // Center exclusion zone (player spawn area)
            float centerExclusion = 15f;
            Vector3 center = new Vector3(
                terrainPos.x + terrainSize.x / 2f,
                0,
                terrainPos.z + terrainSize.z / 2f
            );

            // Collect existing positions to avoid overlap
            List<Vector3> usedPositions = new List<Vector3>();
            foreach (Transform child in vegetation.transform)
            {
                usedPositions.Add(child.position);
            }

            float minDistance = 5f; // Minimum distance between vegetation objects

            // Load prefabs
            var cactusPrefabs = LoadPrefabs(CACTUS_PREFABS);
            var treePrefabs = LoadPrefabs(TREE_PREFABS);
            var rockPrefabs = LoadPrefabs(ROCK_PREFABS);

            if (cactusPrefabs.Count == 0 && treePrefabs.Count == 0 && rockPrefabs.Count == 0)
            {
                Debug.LogError("No prefabs found! Check prefab paths.");
                EditorUtility.ClearProgressBar();
                return;
            }

            int cactusCount = 40;
            int treeCount = 15;
            int rockCount = 30;
            int totalToPlace = cactusCount + treeCount + rockCount;
            int placed = 0;

            // Place Cactus
            EditorUtility.DisplayProgressBar("Vegetation Decorator", "Placing Cactus...", 0.3f);
            placed += PlaceVegetation(cactusPrefabs, cactusCount, vegetation.transform,
                minX, maxX, minZ, maxZ, center, centerExclusion, minDistance,
                usedPositions, 0.8f, 1.5f, terrain);

            // Place Trees
            EditorUtility.DisplayProgressBar("Vegetation Decorator", "Placing Trees...", 0.5f);
            placed += PlaceVegetation(treePrefabs, treeCount, vegetation.transform,
                minX, maxX, minZ, maxZ, center, centerExclusion, minDistance * 2f,
                usedPositions, 0.7f, 1.3f, terrain);

            // Place extra Rocks
            EditorUtility.DisplayProgressBar("Vegetation Decorator", "Placing Rocks...", 0.7f);
            placed += PlaceVegetation(rockPrefabs, rockCount, vegetation.transform,
                minX, maxX, minZ, maxZ, center, centerExclusion, minDistance * 0.8f,
                usedPositions, 0.5f, 2.0f, terrain);

            EditorUtility.DisplayProgressBar("Vegetation Decorator", "Saving scene...", 0.9f);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            EditorUtility.ClearProgressBar();
            Debug.Log($"✅ Vegetation decoration complete! Placed {placed} objects ({cactusCount} cactus, {treeCount} trees, {rockCount} rocks).");
            EditorUtility.DisplayDialog("Done!", $"Placed {placed} vegetation objects on the map!", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Vegetation Decorator failed: " + e);
        }
    }

    static List<GameObject> LoadPrefabs(string[] paths)
    {
        var prefabs = new List<GameObject>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                prefabs.Add(prefab);
            else
                Debug.LogWarning($"Could not load prefab: {path}");
        }
        return prefabs;
    }

    static int PlaceVegetation(List<GameObject> prefabs, int count, Transform parent,
        float minX, float maxX, float minZ, float maxZ,
        Vector3 center, float centerExclusion, float minDist,
        List<Vector3> usedPositions, float minScale, float maxScale,
        Terrain terrain)
    {
        if (prefabs.Count == 0) return 0;

        int placed = 0;
        int maxAttempts = count * 20;
        int attempts = 0;

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;

            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);

            // Skip center exclusion zone
            if (Mathf.Abs(x - center.x) < centerExclusion && Mathf.Abs(z - center.z) < centerExclusion)
                continue;

            // Check minimum distance from existing objects
            Vector3 candidate = new Vector3(x, 0, z);
            bool tooClose = false;
            foreach (var pos in usedPositions)
            {
                float dist2D = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(x, z));
                if (dist2D < minDist)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // Get terrain height at position
            float y = 0f;
            if (terrain != null)
            {
                y = terrain.SampleHeight(new Vector3(x, 0, z));
            }

            Vector3 worldPos = new Vector3(x, y, z);

            // Pick random prefab
            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.parent = parent;
            instance.transform.position = worldPos;

            // Random rotation (Y axis only)
            instance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            // Random scale
            float scale = Random.Range(minScale, maxScale);
            instance.transform.localScale = Vector3.one * scale;

            // Mark as static for batching
            instance.isStatic = true;

            // Add collider if not present
            if (instance.GetComponent<Collider>() == null)
            {
                var meshFilter = instance.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var col = instance.AddComponent<MeshCollider>();
                    col.convex = false;
                }
            }

            usedPositions.Add(worldPos);
            placed++;
        }

        return placed;
    }

    [MenuItem("Tools/Decorate Map with Terrain-Aware Vegetation")]
    public static void DecorateTerrainAwareMap()
    {
        const string targetScenePath = "Assets/Scenes/DesertArena.unity";
        const string additionalGroupName = "Vegetation_TerrainAware";

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "Terrain-aware vegetation decoration must run outside Play Mode.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() ||
            !activeScene.isLoaded ||
            activeScene.path != targetScenePath)
        {
            Debug.LogError(
                "Open Assets/Scenes/DesertArena.unity before decorating vegetation.");
            return;
        }

        GameObject environment = GameObject.Find("Environment");
        Terrain terrain =
            environment != null
                ? environment.GetComponentInChildren<Terrain>(true)
                : null;
        if (environment == null || terrain == null || terrain.terrainData == null)
        {
            Debug.LogError(
                "Environment or Terrain_Ground could not be found.");
            return;
        }

        Transform vegetationRoot = environment.transform.Find("Vegetation");
        if (vegetationRoot == null)
        {
            GameObject vegetationObject = new GameObject("Vegetation");
            vegetationObject.transform.SetParent(environment.transform, false);
            vegetationRoot = vegetationObject.transform;
        }

        Transform previousAdditional =
            vegetationRoot.Find(additionalGroupName);
        if (previousAdditional != null)
        {
            Undo.DestroyObjectImmediate(previousAdditional.gameObject);
        }

        GameObject additionalGroup = new GameObject(additionalGroupName);
        additionalGroup.transform.SetParent(vegetationRoot, false);
        additionalGroup.isStatic = true;
        GameObjectUtility.SetStaticEditorFlags(
            additionalGroup,
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccludeeStatic);

        List<Vector3> usedPositions = new List<Vector3>(1024);
        Transform[] existingVegetation =
            vegetationRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < existingVegetation.Length; i++)
        {
            Transform existing = existingVegetation[i];
            if (existing == vegetationRoot ||
                existing.IsChildOf(additionalGroup.transform))
            {
                continue;
            }

            usedPositions.Add(existing.position);
        }

        List<Bounds> reservedBounds = CollectReservedBounds(
            environment.transform.Find("MegaDecor_Setpieces"));
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        Vector3 terrainCenter = terrainOrigin +
            new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);

        float minX = terrainOrigin.x + 14f;
        float maxX = terrainOrigin.x + terrainSize.x - 14f;
        float minZ = terrainOrigin.z + 14f;
        float maxZ = terrainOrigin.z + terrainSize.z - 14f;

        List<GameObject> cactusPrefabs = LoadPrefabs(CACTUS_PREFABS);
        List<GameObject> treePrefabs = LoadPrefabs(TREE_PREFABS);
        if (cactusPrefabs.Count == 0 && treePrefabs.Count == 0)
        {
            Debug.LogError(
                "No existing cactus/tree prefabs were found.");
            return;
        }

        UnityEngine.Random.State previousRandomState =
            UnityEngine.Random.state;
        int placedCactus = 0;
        int placedTree = 0;
        try
        {
            UnityEngine.Random.InitState(6082026);

            placedCactus = PlaceTerrainAware(
                cactusPrefabs,
                72,
                additionalGroup.transform,
                minX,
                maxX,
                minZ,
                maxZ,
                terrainCenter,
                28f,
                6.5f,
                24f,
                usedPositions,
                reservedBounds,
                0.72f,
                1.30f,
                terrain);

            placedTree = PlaceTerrainAware(
                treePrefabs,
                12,
                additionalGroup.transform,
                minX,
                maxX,
                minZ,
                maxZ,
                terrainCenter,
                34f,
                10f,
                17f,
                usedPositions,
                reservedBounds,
                0.74f,
                1.18f,
                terrain);
        }
        finally
        {
            UnityEngine.Random.state = previousRandomState;
        }

        if (placedCactus == 0 && placedTree == 0)
        {
            Undo.DestroyObjectImmediate(additionalGroup);
            Debug.LogWarning(
                "No terrain-safe vegetation position was available.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        // Include the new source renderers in the existing spatial bake.
        EnvironmentChunkBaker.BakeDesertArena();

        Debug.Log(
            "Terrain-aware vegetation added: " +
            placedCactus + " cactus and " +
            placedTree + " tree prefabs. " +
            "Only existing project prefabs were used.");
    }

    private static List<Bounds> CollectReservedBounds(Transform root)
    {
        List<Bounds> bounds = new List<Bounds>();
        if (root == null)
        {
            return bounds;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds value = renderer.bounds;
            value.Expand(8f);
            bounds.Add(value);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            Bounds value = collider.bounds;
            value.Expand(5f);
            bounds.Add(value);
        }

        return bounds;
    }

    private static int PlaceTerrainAware(
        List<GameObject> prefabs,
        int count,
        Transform parent,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        Vector3 terrainCenter,
        float centerExclusion,
        float minDistance,
        float maxSlope,
        List<Vector3> usedPositions,
        List<Bounds> reservedBounds,
        float minScale,
        float maxScale,
        Terrain terrain)
    {
        if (prefabs.Count == 0)
        {
            return 0;
        }

        int placed = 0;
        int maxAttempts = Mathf.Max(count * 80, 240);
        for (int attempt = 0;
             attempt < maxAttempts && placed < count;
             attempt++)
        {
            Vector3 candidate = new Vector3(
                UnityEngine.Random.Range(minX, maxX),
                0f,
                UnityEngine.Random.Range(minZ, maxZ));

            if (Vector2.Distance(
                    new Vector2(candidate.x, candidate.z),
                    new Vector2(terrainCenter.x, terrainCenter.z)) <
                centerExclusion)
            {
                continue;
            }

            if (!TrySampleTerrain(
                    terrain,
                    candidate,
                    maxSlope,
                    out Vector3 worldPosition))
            {
                continue;
            }

            if (IsTooClose(
                    worldPosition,
                    usedPositions,
                    minDistance) ||
                IsInsideReservedBounds(worldPosition, reservedBounds))
            {
                continue;
            }

            GameObject prefab =
                prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                continue;
            }

            instance.name =
                prefab.name + "_TerrainAware_" + placed.ToString("000");
            instance.transform.SetParent(parent, false);
            instance.transform.position = worldPosition;
            instance.transform.rotation =
                Quaternion.Euler(
                    0f,
                    UnityEngine.Random.Range(0f, 360f),
                    0f);
            instance.transform.localScale =
                Vector3.one * UnityEngine.Random.Range(minScale, maxScale);
            instance.isStatic = true;
            GameObjectUtility.SetStaticEditorFlags(
                instance,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
            EnsureVegetationCollider(instance);

            usedPositions.Add(worldPosition);
            placed++;
        }

        return placed;
    }

    private static bool TrySampleTerrain(
        Terrain terrain,
        Vector3 candidate,
        float maxSlope,
        out Vector3 worldPosition)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float u = (candidate.x - origin.x) / size.x;
        float v = (candidate.z - origin.z) / size.z;

        worldPosition = Vector3.zero;
        if (u < 0.02f ||
            u > 0.98f ||
            v < 0.02f ||
            v > 0.98f)
        {
            return false;
        }

        Vector3 normal = data.GetInterpolatedNormal(u, v);
        if (Vector3.Angle(normal, Vector3.up) > maxSlope)
        {
            return false;
        }

        float y = terrain.SampleHeight(candidate) + origin.y;
        worldPosition = new Vector3(candidate.x, y, candidate.z);
        return true;
    }

    private static bool IsTooClose(
        Vector3 candidate,
        List<Vector3> positions,
        float minDistance)
    {
        float minDistanceSqr = minDistance * minDistance;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 delta = candidate - positions[i];
            delta.y = 0f;
            if (delta.sqrMagnitude < minDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideReservedBounds(
        Vector3 candidate,
        List<Bounds> bounds)
    {
        for (int i = 0; i < bounds.Count; i++)
        {
            if (bounds[i].Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureVegetationCollider(GameObject instance)
    {
        if (instance.GetComponent<Collider>() != null)
        {
            return;
        }

        MeshFilter meshFilter = instance.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        MeshCollider meshCollider = instance.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
    }

}
