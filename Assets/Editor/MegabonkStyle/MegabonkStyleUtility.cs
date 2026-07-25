using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MegabonkStyleUtility
{
    private const string ShaderName = "Gigachad/Megabonk/Toon Lit";
    private const string GeneratedRoot = "Assets/Generated/MegabonkStyle";
    private const string BackupRoot = GeneratedRoot + "/MaterialBackups";
    private const string VolumeProfilePath = GeneratedRoot + "/MegabonkStyleVolume.asset";

    private static readonly string[] MaterialRoots =
    {
        "Assets/DesertArena_Materials",
        "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Materials",
        "Assets/Model/Player/PlayerTextures",
        "Assets/Model/Enemy/Mummy",
        "Assets/Model/Enemy/Skeleton",
        "Assets/Generated/MeshFlipbook"
    };

    [MenuItem("Tools/Gigachad/Megabonk Style/Apply Full Art Style")]
    public static void ApplyFullStyle()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(BackupRoot);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException("Missing shader: " + ShaderName);
        }

        int materialCount = ApplyMaterials(shader);
        int prefabCount = ApplyPrefabShadows();
        ApplyPipelineSettings();
        ApplySceneStyle();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Megabonk style applied: {materialCount} materials, {prefabCount} prefabs, scene lighting, fog, volume and URP settings.");
    }

    [MenuItem("Tools/Gigachad/Megabonk Style/Restore Material Backups")]
    public static void RestoreMaterialBackups()
    {
        int restored = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { BackupRoot }))
        {
            string backupPath = AssetDatabase.GUIDToAssetPath(guid);
            string originalGuid = Path.GetFileNameWithoutExtension(backupPath);
            string originalPath = AssetDatabase.GUIDToAssetPath(originalGuid);
            Material backup = AssetDatabase.LoadAssetAtPath<Material>(backupPath);
            Material original = AssetDatabase.LoadAssetAtPath<Material>(originalPath);
            if (backup == null || original == null)
            {
                continue;
            }

            EditorUtility.CopySerialized(backup, original);
            EditorUtility.SetDirty(original);
            restored++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Restored {restored} original material assets. Scene lighting remains in Megabonk style.");
    }

    private static int ApplyMaterials(Shader shader)
    {
        HashSet<string> paths = new HashSet<string>();
        foreach (string root in MaterialRoots)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                continue;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        int changed = 0;
        foreach (string path in paths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!ShouldStyle(material))
            {
                continue;
            }

            BackupMaterial(path, material);
            Texture baseMap = GetFirstTexture(material, "_BaseMap", "_MainTex");
            Color baseColor = GetFirstColor(material, "_BaseColor", "_Color");

            material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", GetPaletteColor(material.name, baseColor));
            material.SetFloat("_Ambient", IsCharacter(material.name) ? 0.82f : 0.76f);
            material.SetFloat("_LightStrength", IsCharacter(material.name) ? 0.31f : 0.42f);
            material.SetFloat("_LightSteps", 3f);
            material.SetFloat("_Saturation", IsCharacter(material.name) ? 1.055f : 1.13f);
            material.SetFloat("_ShadowFloor", 0.72f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            changed++;
        }

        return changed;
    }

    private static bool ShouldStyle(Material material)
    {
        if (material == null || material.shader == null)
        {
            return false;
        }

        string shader = material.shader.name;
        return shader == "Universal Render Pipeline/Lit"
            || shader == "Universal Render Pipeline/Simple Lit"
            || shader == "Gigachad/Flipbook/Enemy Unlit"
            || shader == "Gigachad/VAT/Enemy Unlit"
            || shader == "Gigachad/VAT/Enemy Lit"
            || shader == ShaderName;
    }

    private static void BackupMaterial(string originalPath, Material original)
    {
        string originalGuid = AssetDatabase.AssetPathToGUID(originalPath);
        string backupPath = BackupRoot + "/" + originalGuid + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(backupPath) != null)
        {
            return;
        }

        Material backup = new Material(original) { name = original.name + "_Original" };
        AssetDatabase.CreateAsset(backup, backupPath);
    }

    private static Texture GetFirstTexture(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return Texture2D.whiteTexture;
    }

    private static Color GetFirstColor(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return Color.white;
    }

    private static bool IsCharacter(string materialName)
    {
        string name = materialName.ToLowerInvariant();
        return name.Contains("player") || name.Contains("mummy") || name.Contains("skeleton") || name.Contains("flipbook");
    }

    private static Color GetPaletteColor(string materialName, Color fallback)
    {
        switch (materialName.ToLowerInvariant())
        {
            case "cactus": return new Color(0.315f, 0.55f, 0.195f, fallback.a);
            case "darkwood": return new Color(0.17f, 0.10f, 0.06f, fallback.a);
            case "deadtree": return new Color(0.305f, 0.215f, 0.145f, fallback.a);
            case "pot": return new Color(0.72f, 0.315f, 0.135f, fallback.a);
            case "rock": return new Color(0.615f, 0.48f, 0.35f, fallback.a);
            case "sandstone": return new Color(0.81f, 0.555f, 0.305f, fallback.a);
            case "wood": return new Color(0.385f, 0.22f, 0.11f, fallback.a);
            default: return fallback;
        }
    }

    private static int ApplyPrefabShadows()
    {
        string[] roots =
        {
            "Assets/Prefab/Enemy_Mummy.prefab",
            "Assets/Prefab/Enemy_Skeleton.prefab",
            "Assets/Prefab/Player.prefab",
            "Assets/Generated/MeshFlipbook/MummyWalking/MummyWalking_Flipbook.prefab",
            "Assets/Generated/MeshFlipbook/SkeletonLimpingWalk/SkeletonLimpingWalk_Flipbook.prefab"
        };

        int changed = 0;
        foreach (string path in roots)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

private static void ApplyPipelineSettings()
    {
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            return;
        }

        pipeline.renderScale = 0.82f;
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipeline.shadowDistance = 55f;
        pipeline.shadowCascadeCount = 1;
        pipeline.maxAdditionalLightsCount = 0;
        pipeline.useSRPBatcher = true;
        EditorUtility.SetDirty(pipeline);
    }

    private static void ApplySceneStyle()
    {
        Light sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(light => light.gameObject.scene.IsValid() && light.type == LightType.Directional);
        if (sun != null)
        {
            Undo.RecordObject(sun, "Apply Megabonk lighting");
            Undo.RecordObject(sun.transform, "Apply Megabonk lighting rotation");
            sun.intensity = 1.18f;
            sun.color = new Color(1f, 0.90f, 0.76f);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.65f;
            sun.shadowBias = 0.075f;
            sun.shadowNormalBias = 0.45f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.49f, 0.575f, 0.67f);
        RenderSettings.ambientEquatorColor = new Color(0.435f, 0.365f, 0.295f);
        RenderSettings.ambientGroundColor = new Color(0.225f, 0.16f, 0.11f);
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.reflectionIntensity = 0.275f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.60f, 0.65f, 0.665f);
        RenderSettings.fogStartDistance = 44f;
        RenderSettings.fogEndDistance = 120f;

        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!camera.gameObject.scene.IsValid())
            {
                continue;
            }

            camera.allowHDR = false;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
        }

        CreateOrUpdateVolume();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private static void CreateOrUpdateVolume()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.active = true;
        bloom.intensity.Override(0.15f);
        bloom.threshold.Override(1.075f);
        bloom.scatter.Override(0.515f);
        bloom.highQualityFiltering.Override(false);

        ColorAdjustments colors = GetOrAdd<ColorAdjustments>(profile);
        colors.active = true;
        colors.postExposure.Override(0.18f);
        colors.contrast.Override(10f);
        colors.saturation.Override(8.5f);
        colors.colorFilter.Override(new Color(1f, 0.965f, 0.90f));

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.Override(0.10f);
        vignette.smoothness.Override(0.265f);

        EditorUtility.SetDirty(profile);

        GameObject volumeObject = GameObject.Find("Megabonk Style Volume");
        if (volumeObject == null)
        {
            volumeObject = new GameObject("Megabonk Style Volume");
            Undo.RegisterCreatedObjectUndo(volumeObject, "Create Megabonk style volume");
        }

        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
        }

        return component;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
