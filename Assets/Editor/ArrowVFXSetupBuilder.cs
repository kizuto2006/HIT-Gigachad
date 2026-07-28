using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ArrowVFXSetupBuilder
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootFolder = "Assets/Prefab/WeaponVFX/Arrow";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string ReleasePrefabPath = RootFolder + "/Arrow_Release.prefab";
    private const string ImpactPrefabPath = RootFolder + "/Arrow_Impact.prefab";
    private const string ProjectilePrefabPath = RootFolder + "/Arrow_Projectile.prefab";
    private const string WeaponDataPath = "Assets/Resources/Weapons/Arrow.asset";
    private const string ModelMaterialPath =
        MaterialsFolder + "/Arrow_Model.mat";

    [MenuItem("Tools/Weapons/Rebuild Arrow VFX Test")]
    public static void RebuildArrowVFXTest()
    {
        BuildAssetsAndConfigureSampleScene();
        Debug.Log(
            "[ArrowVFXSetup] Rebuilt Arrow assets and configured the " +
            "SampleScene Player instance with Arrow only.");
    }

    private static void BuildAssetsAndConfigureSampleScene()
    {
        EnsureFolder("Assets/Prefab/WeaponVFX");
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        ConfigureArrowTextureImporter();

        Shader vfxShader = Shader.Find("Gigachad/FireballBillboard");
        if (vfxShader == null)
        {
            Debug.LogError(
                "[ArrowVFXSetup] Missing shader Gigachad/FireballBillboard.");
            return;
        }

        Shader modelShader = Shader.Find(
            "Universal Render Pipeline/Unlit");
        Texture2D modelTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Model/Arrow/arrow.png");
        if (modelShader == null || modelTexture == null)
        {
            Debug.LogError(
                "[ArrowVFXSetup] Missing URP Unlit shader or Arrow texture.");
            return;
        }

        Material modelMaterial = CreateOrUpdateArrowModelMaterial(
            ModelMaterialPath,
            modelShader,
            modelTexture);
        Material flashMaterial = CreateOrUpdateVfxMaterial(
            MaterialsFolder + "/Arrow_Flash.mat",
            vfxShader,
            new Color(1.35f, 1.2f, 1.45f, 1f),
            3.5f,
            0f,
            0.06f);
        Material ringMaterial = CreateOrUpdateVfxMaterial(
            MaterialsFolder + "/Arrow_Ring.mat",
            vfxShader,
            new Color(0.2f, 1.15f, 1.4f, 1f),
            3f,
            1f,
            0.08f);
        Material sparkMaterial = CreateOrUpdateVfxMaterial(
            MaterialsFolder + "/Arrow_Spark.mat",
            vfxShader,
            new Color(0.32f, 0.95f, 1.5f, 1f),
            3.25f,
            2f,
            0.05f);
        Material trailOuterMaterial = CreateOrUpdateVfxMaterial(
            MaterialsFolder + "/Arrow_TrailOuter.mat",
            vfxShader,
            new Color(1.15f, 0.34f, 1.05f, 1f),
            1.8f,
            2f,
            0.08f);
        Material trailCoreMaterial = CreateOrUpdateVfxMaterial(
            MaterialsFolder + "/Arrow_TrailCore.mat",
            vfxShader,
            new Color(1.35f, 1.25f, 1.45f, 1f),
            3.6f,
            2f,
            0.04f);

        GameObject releasePrefab = BuildReleasePrefab(
            flashMaterial,
            ringMaterial,
            sparkMaterial);
        GameObject impactPrefab = BuildImpactPrefab(
            flashMaterial,
            ringMaterial,
            sparkMaterial);
        GameObject projectilePrefab = BuildProjectilePrefab(
            impactPrefab,
            modelMaterial,
            trailOuterMaterial,
            trailCoreMaterial);
        WeaponData arrowData = BuildWeaponData(
            projectilePrefab,
            releasePrefab);

        ConfigureSampleScenePlayer(arrowData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject BuildReleasePrefab(
        Material flashMaterial,
        Material ringMaterial,
        Material sparkMaterial)
    {
        GameObject root = new GameObject("Arrow_Release");

        CreateBurstParticle(
            root.transform,
            "ReleaseFlash",
            flashMaterial,
            1,
            0.075f,
            0f,
            0.58f,
            false,
            false,
            new Color(1f, 1f, 1f, 1f),
            0.1f,
            1.2f);
        CreateBurstParticle(
            root.transform,
            "ReleaseRing",
            ringMaterial,
            1,
            0.14f,
            0f,
            0.42f,
            false,
            false,
            new Color(0.4f, 1f, 1f, 1f),
            0.2f,
            1.65f);
        CreateBurstParticle(
            root.transform,
            "ReleaseSparks",
            sparkMaterial,
            7,
            0.2f,
            2.4f,
            0.09f,
            true,
            false,
            new Color(0.25f, 0.9f, 1f, 1f),
            1f,
            0.2f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            root,
            ReleasePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildImpactPrefab(
        Material flashMaterial,
        Material ringMaterial,
        Material sparkMaterial)
    {
        GameObject root = new GameObject("Arrow_Impact");

        CreateBurstParticle(
            root.transform,
            "ImpactFlash",
            flashMaterial,
            1,
            0.07f,
            0f,
            0.9f,
            false,
            false,
            Color.white,
            0.15f,
            1.25f);
        CreateBurstParticle(
            root.transform,
            "ImpactRing",
            ringMaterial,
            1,
            0.22f,
            0f,
            0.82f,
            false,
            false,
            new Color(0.35f, 1f, 1f, 1f),
            0.12f,
            2f);
        CreateBurstParticle(
            root.transform,
            "ImpactStreaks",
            sparkMaterial,
            10,
            0.28f,
            3.8f,
            0.1f,
            true,
            true,
            new Color(0.25f, 0.85f, 1f, 1f),
            1f,
            0.1f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            root,
            ImpactPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildProjectilePrefab(
        GameObject impactPrefab,
        Material modelMaterial,
        Material trailOuterMaterial,
        Material trailCoreMaterial)
    {
        GameObject root = new GameObject("Arrow_Projectile");

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.center = new Vector3(0f, 0f, 0.85f);
        collider.radius = 0.11f;

        Projectile projectile = root.AddComponent<Projectile>();

        projectile.maxTravelDistance = 60f;
        projectile.lifetime = 3f;
        projectile.impactLayer = ~0;
        projectile.hitEffectPrefab = impactPrefab;
        projectile.explosionRadius = 0f;
        projectile.explosionDamageMultiplier = 0f;
        projectile.explosionEffectLifetime = 0.5f;
        projectile.homingTurnSpeed = 480f;

        GameObject visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(root.transform, false);
        visualRoot.transform.localPosition = new Vector3(0f, 0f, 0.2f);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Model/Arrow/Arrow.fbx");
        if (modelAsset != null)
        {
            GameObject model = PrefabUtility.InstantiatePrefab(
                modelAsset) as GameObject;
            if (model != null)
            {
                model.name = "ArrowModel";
                model.transform.SetParent(visualRoot.transform, false);
                AlignAndScaleArrowModel(model);
                ApplyMaterialToRenderers(model, modelMaterial);

                Animator animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                    Object.DestroyImmediate(animator);
            }
        }

        GameObject trailAnchor = new GameObject("TrailAnchor");
        trailAnchor.transform.SetParent(root.transform, false);
        trailAnchor.transform.localPosition = new Vector3(0f, 0f, -0.7f);

        TrailRenderer outerTrail = CreateTrail(
            trailAnchor.transform,
            "TrailOuter",
            trailOuterMaterial,
            0.19f,
            0.22f);
        TrailRenderer coreTrail = CreateTrail(
            trailAnchor.transform,
            "TrailCore",
            trailCoreMaterial,
            0.15f,
            0.07f);

        ProjectileTrailVFX trailVfx =
            root.AddComponent<ProjectileTrailVFX>();
        SerializedObject trailVfxObject = new SerializedObject(trailVfx);
        trailVfxObject.FindProperty("projectileVisualRoot")
            .objectReferenceValue = visualRoot;
        SerializedProperty trailsProperty =
            trailVfxObject.FindProperty("trails");
        trailsProperty.arraySize = 2;
        trailsProperty.GetArrayElementAtIndex(0).objectReferenceValue =
            outerTrail;
        trailsProperty.GetArrayElementAtIndex(1).objectReferenceValue =
            coreTrail;
        trailVfxObject.FindProperty("releaseDelay").floatValue = 0.25f;
        trailVfxObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            root,
            ProjectilePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static WeaponData BuildWeaponData(
        GameObject projectilePrefab,
        GameObject releasePrefab)
    {
        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
            WeaponDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(data, WeaponDataPath);
        }

        data.id = "arrow";
        data.weaponName = "Bow";
        data.description =
            "Locks onto one enemy, then pierces straight through every enemy in its path.";
        data.weaponType = WeaponType.Projectile;
        data.rarity = WeaponRarity.Common;
        data.attackType = WeaponAttackType.BowShot;
        data.atk = 18f;
        data.crit = 0.1f;
        data.projectileSpeed = 42f;
        data.projectileCount = 1;
        data.additionalProjectileDamageMultiplier = 0.8f;
        data.size = 1f;
        data.cooldown = 0.8f;
        data.pierce = int.MaxValue;
        data.knockback = 0.35f;
        data.maxLevel = 5;
        data.damagePerLevel = 4f;
        data.cooldownReductionPerLevel = 0.04f;
        data.sizePerLevel = 0.08f;

        data.useAutomaticLevelUpgrades = true;
        data.automaticUpgradeStats =
            AutomaticWeaponUpgradeStats.Damage |
            AutomaticWeaponUpgradeStats.CriticalChance |
            AutomaticWeaponUpgradeStats.ProjectileCount |
            AutomaticWeaponUpgradeStats.Cooldown;
        data.automaticDamageBonus = 4f;
        data.automaticCriticalChanceBonus = 0.05f;
        data.automaticProjectileCountBonus = 1;
        data.automaticCooldownReduction = 0.08f;

        data.randomizeAutomaticSecondStat = true;
        data.automaticSecondStatChance = 0f;
        data.automaticSecondStatInterval = 999;
        data.disableAutomaticProjectileCountUpgrades = true;
        data.automaticMaxProjectileCount = 2;
        data.grantSecondProjectileAtLevel2 = false;
        data.projCountPerLevel = 0;
        data.projectilePrefab = projectilePrefab;
        data.attackEffectPrefab = releasePrefab;

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Icons/Weapons/Bow.png");
        if (icon != null)
            data.icon = icon;

        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ConfigureSampleScenePlayer(WeaponData arrowData)
    {
        Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
        bool openedByBuilder = !scene.IsValid() || !scene.isLoaded;
        if (openedByBuilder)
        {
            scene = EditorSceneManager.OpenScene(
                SampleScenePath,
                OpenSceneMode.Additive);
        }

        WeaponController targetController = null;
        WeaponController[] controllers =
            Object.FindObjectsByType<WeaponController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (WeaponController controller in controllers)
        {
            if (controller.gameObject.scene == scene)
            {
                targetController = controller;
                break;
            }
        }

        if (targetController == null)
        {
            Debug.LogError(
                "[ArrowVFXSetup] No WeaponController found in SampleScene.");
            if (openedByBuilder && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
            return;
        }

        SerializedObject controllerObject =
            new SerializedObject(targetController);
        controllerObject.FindProperty("autoEquipStartingWeapon").boolValue =
            true;
        controllerObject.FindProperty("weaponSlot1").objectReferenceValue =
            arrowData;
        controllerObject.FindProperty("weaponSlot2").objectReferenceValue =
            null;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            targetController);

        WeaponInventory inventory =
            targetController.GetComponent<WeaponInventory>();
        if (inventory != null)
        {
            inventory.maxSlots = 1;
            EditorUtility.SetDirty(inventory);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
        }

        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (openedByBuilder)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static Material CreateOrUpdateVfxMaterial(
        string path,
        Shader shader,
        Color tint,
        float intensity,
        float shape,
        float softness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_Tint", tint);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Shape", shape);
        material.SetFloat("_Softness", softness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateArrowModelMaterial(
        string path,
        Shader shader,
        Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.25f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.SetFloat("_Smoothness", 0.15f);
        material.SetFloat("_Metallic", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureArrowTextureImporter()
    {
        const string texturePath = "Assets/Model/Arrow/arrow.png";
        TextureImporter importer =
            AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return;

        bool changed =
            importer.alphaIsTransparency == false ||
            importer.mipmapEnabled ||
            importer.filterMode != FilterMode.Point ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            importer.textureCompression !=
                TextureImporterCompression.Uncompressed;
        if (!changed)
            return;

        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ApplyMaterialToRenderers(
        GameObject model,
        Material material)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static ParticleSystem CreateBurstParticle(
        Transform parent,
        string objectName,
        Material material,
        int count,
        float lifetime,
        float speed,
        float size,
        bool stretch,
        bool sphereShape,
        Color color,
        float startScale,
        float endScale)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(parent, false);

        ParticleSystem particles =
            particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(4, count * 2);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)count)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = speed > 0f;
        if (speed > 0f)
        {
            shape.shapeType = sphereShape
                ? ParticleSystemShapeType.Sphere
                : ParticleSystemShapeType.Cone;
            shape.radius = sphereShape ? 0.06f : 0.035f;
            if (!sphereShape)
                shape.angle = 28f;
        }

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = colorGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.Linear(0f, startScale, 1f, endScale));

        ParticleSystemRenderer renderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 10;
        if (stretch)
        {
            renderer.renderMode =
                ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.18f;
            renderer.lengthScale = 2.2f;
        }
        else
        {
            renderer.renderMode =
                ParticleSystemRenderMode.Billboard;
        }

        return particles;
    }

    private static TrailRenderer CreateTrail(
        Transform parent,
        string objectName,
        Material material,
        float trailTime,
        float startWidth)
    {
        GameObject trailObject = new GameObject(objectName);
        trailObject.transform.SetParent(parent, false);

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = trailTime;
        trail.minVertexDistance = 0.025f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.55f),
            new Keyframe(1f, 0f));
        trail.widthMultiplier = startWidth;
        trail.colorGradient = CreateTrailGradient();
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 2;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;
        trail.sortingOrder = objectName.Contains("Core") ? 4 : 3;
        return trail;
    }

    private static Gradient CreateTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(
                    new Color(1f, 0.48f, 0.95f),
                    0.55f),
                new GradientColorKey(
                    new Color(0.35f, 0.8f, 1f),
                    1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void AlignAndScaleArrowModel(GameObject model)
    {
        MeshFilter meshFilter = model.GetComponentInChildren<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 size = meshFilter.sharedMesh.bounds.size;
            if (size.x >= size.y && size.x >= size.z)
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            else if (size.y >= size.x && size.y >= size.z)
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        Matrix4x4 worldToModel = model.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in meshFilters)
        {
            if (filter.sharedMesh == null)
                continue;

            Matrix4x4 meshToModel =
                worldToModel * filter.transform.localToWorldMatrix;
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = center + Vector3.Scale(
                    extents,
                    new Vector3(x, y, z));
                Vector3 transformedCorner =
                    meshToModel.MultiplyPoint3x4(corner);
                if (!hasBounds)
                {
                    bounds = new Bounds(
                        transformedCorner,
                        Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(transformedCorner);
                }
            }
        }

        if (!hasBounds)
            return;

        Vector3 boundsSize = bounds.size;
        float longestSide = Mathf.Max(
            boundsSize.x,
            boundsSize.y,
            boundsSize.z);
        if (longestSide > 0.001f)
        {
            // Keep the projectile model compact so it reads as an arrow rather
            // than a large spear next to the player and enemies.
            float baseScale = 1f / longestSide;
            Vector3 compactScale = Vector3.one * (baseScale * 0.3f);

            if (boundsSize.x >= boundsSize.y &&
                boundsSize.x >= boundsSize.z)
            {
                compactScale.x = baseScale;
            }
            else if (boundsSize.y >= boundsSize.x &&
                     boundsSize.y >= boundsSize.z)
            {
                compactScale.y = baseScale;
            }
            else
            {
                compactScale.z = baseScale;
            }

            model.transform.localScale = compactScale;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)
            ?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

}
