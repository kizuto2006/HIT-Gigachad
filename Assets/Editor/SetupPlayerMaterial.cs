using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor script to create a URP Lit material from PlayerTextures and assign it to the Player model.
/// Run via menu: Tools > Setup Player Material
/// </summary>
public class SetupPlayerMaterial : Editor
{
    private const string TexturePath = "Assets/Model/PlayerTextures/";
    private const string MaterialPath = "Assets/Model/PlayerTextures/PlayerMaterial.mat";

    [MenuItem("Tools/Setup Player Material")]
    public static void CreateAndAssignPlayerMaterial()
    {
        // 1. Fix normal map import settings
        FixNormalMapImportSettings();

        // 2. Fix metallic/roughness to linear (non-sRGB)
        FixLinearTextureImportSettings("muscular_humanoid_3d_model_metallic.JPEG");
        FixLinearTextureImportSettings("muscular_humanoid_3d_model_roughness.JPEG");

        // 3. Load textures
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "muscular_humanoid_3d_model_basecolor.JPEG");
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "muscular_humanoid_3d_model_metallic.JPEG");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "muscular_humanoid_3d_model_normal.JPEG");
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "muscular_humanoid_3d_model_roughness.JPEG");

        if (baseColor == null)
        {
            Debug.LogError("[SetupPlayerMaterial] Could not find basecolor texture at: " + TexturePath);
            return;
        }

        // 4. Create URP Lit material
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogError("[SetupPlayerMaterial] Could not find URP Lit shader. Make sure URP is installed.");
            return;
        }

        Material mat = new Material(urpLitShader);
        mat.name = "PlayerMaterial";

        // Assign Base Color (Albedo)
        mat.SetTexture("_BaseMap", baseColor);
        mat.SetColor("_BaseColor", Color.white);

        // Assign Metallic Map
        if (metallic != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.SetFloat("_Metallic", 1.0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        // Assign Normal Map
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1.0f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // URP uses Smoothness (inverse of roughness)
        // Since we have a roughness map, we invert it via smoothness settings
        // We'll use the metallic alpha channel approach or set smoothness source
        mat.SetFloat("_Smoothness", 0.5f);
        mat.SetFloat("_SmoothnessTextureChannel", 0); // 0 = Metallic Alpha

        // Save material
        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existingMat != null)
        {
            EditorUtility.CopySerialized(mat, existingMat);
            DestroyImmediate(mat);
            mat = existingMat;
            Debug.Log("[SetupPlayerMaterial] Updated existing material: " + MaterialPath);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log("[SetupPlayerMaterial] Created new material: " + MaterialPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5. Find and assign material to Player in scene
        AssignMaterialToPlayer(mat);

        Debug.Log("[SetupPlayerMaterial] ✅ Player material setup complete!");
    }

    private static void FixNormalMapImportSettings()
    {
        string path = TexturePath + "muscular_humanoid_3d_model_normal.JPEG";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            Debug.Log("[SetupPlayerMaterial] Fixed normal map import settings for: " + path);
        }
    }

    private static void FixLinearTextureImportSettings(string fileName)
    {
        string path = TexturePath + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            Debug.Log("[SetupPlayerMaterial] Fixed linear texture settings for: " + path);
        }
    }

    private static void AssignMaterialToPlayer(Material mat)
    {
        // Try to find Player object in scene hierarchy
        // From the screenshot, the hierarchy shows: Player > Megachadd_Model
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            // Try to find by searching all root objects
            foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootObj.name == "Player")
                {
                    player = rootObj;
                    break;
                }
            }
        }

        if (player == null)
        {
            Debug.LogWarning("[SetupPlayerMaterial] Could not find 'Player' object in scene. Please assign the material manually.");
            return;
        }

        // Find all MeshRenderers and SkinnedMeshRenderers in the player hierarchy
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning("[SetupPlayerMaterial] No renderers found on Player object. Please assign the material manually.");
            return;
        }

        int assignedCount = 0;
        foreach (Renderer renderer in renderers)
        {
            // Replace all material slots with our new material
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }
            renderer.sharedMaterials = mats;
            assignedCount++;

            // Mark the renderer's game object as dirty for saving
            EditorUtility.SetDirty(renderer);
        }

        // Also update the prefab if it exists
        UpdatePlayerPrefab(mat);

        Debug.Log($"[SetupPlayerMaterial] Assigned material to {assignedCount} renderer(s) on Player.");
    }

    private static void UpdatePlayerPrefab(Material mat)
    {
        string prefabPath = "Assets/Prefab/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[SetupPlayerMaterial] Could not find Player prefab at: " + prefabPath);
            return;
        }

        // Instantiate prefab to modify it
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }
            renderer.sharedMaterials = mats;
        }

        // Save back to prefab
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        DestroyImmediate(instance);

        Debug.Log("[SetupPlayerMaterial] Updated Player prefab with new material.");
    }
}
