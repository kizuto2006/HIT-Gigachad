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
            Terrain terrain = Object.FindObjectOfType<Terrain>();
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
}
