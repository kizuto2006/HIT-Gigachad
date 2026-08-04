using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class DuneBurrowerSetup
{
    private const string ModelPath =
        "Assets/Model/Enemy/DuneBurrower/Meshy_AI_dune_burrower_30k_quadruped_model_Animation_Walking_withSkin.fbx";
    private const string AlbedoTexturePath =
        "Assets/Model/Enemy/DuneBurrower/Meshy_AI_dune_burrower_30k_quadruped_texture_0.png";
    private const string PlayerModelPath =
        "Assets/Model/Player/PlayerAnimation/Megachadd@Breathing Idle.fbx";
    private const string ControllerPath =
        "Assets/Model/Enemy/DuneBurrower/DuneBurrower.controller";
    private const string PrefabPath =
        "Assets/Prefab/Enemy/Enemy_DuneBurrower.prefab";
    private const string DataPath =
        "Assets/Resources/Enemies/EnemyData_DuneBurrower.asset";
    private const string TemplatePath =
        "Assets/Prefab/Enemy/Enemy_Mummy.prefab";
    private const string SpawnerPath =
        "Assets/Prefab/Enemy/EnemySpawn.prefab";
    private const string FlipbookPrefabPath =
        "Assets/Generated/MeshFlipbook/DuneBurrowerWalking/DuneBurrowerWalking_Flipbook.prefab";
    private const float TargetPlayerHeightRatio = 0.75f;

    static DuneBurrowerSetup()
    {
        EditorApplication.delayCall += SetupIfMissing;
    }

    [MenuItem("Tools/Gigachad/Setup DuneBurrower")]
    private static void SetupFromMenu()
    {
        Setup(true);
    }

    private static void SetupIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += SetupIfMissing;
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null
            || AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath) == null
            || AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(FlipbookPrefabPath) == null)
        {
            Setup(false);
        }
    }

    private static void Setup(bool showDialog)
    {
        try
        {
            ConfigureModelImporter();

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (sourceModel == null || playerModel == null || template == null)
            {
                throw new InvalidOperationException(
                    "Missing DuneBurrower model, Player model, or enemy template.");
            }

            AnimationClip walkingClip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip =>
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (walkingClip == null)
            {
                throw new InvalidOperationException(
                    "DuneBurrower FBX does not contain a usable animation clip.");
            }

            AnimatorController controller = CreateOrUpdateController(walkingClip);
            Bounds sourceBounds = CalculateBounds(sourceModel);
            Bounds playerBounds = CalculateBounds(playerModel);
            float targetRuntimeHeight = playerBounds.size.y * TargetPlayerHeightRatio;
            float visualScale = targetRuntimeHeight / sourceBounds.size.y;

            EnemyPrefabCreationSettings settings = new EnemyPrefabCreationSettings
            {
                enemyName = "DuneBurrower",
                sourceVisual = sourceModel,
                prefabOutputFolder = "Assets/Prefab/Enemy",
                dataOutputFolder = "Assets/Resources/Enemies",
                hp = 40f,
                attack = 12f,
                speed = 2.35f,
                armor = 2.5f,
                size = EnemySize.Medium,
                colliderType = EnemyColliderType.BoxCollider,
                autoFitCollider = true,
                isTrigger = true,
                visualLocalScale = Vector3.one * visualScale,
                visualLocalPosition = Vector3.up * (-sourceBounds.min.y * visualScale),
                addAnimatorIfMissing = true,
                animatorController = controller,
                copyConfigurationFromTemplate = true,
                templateEnemyPrefab = template
            };

            EnemyPrefabCreatorUtility.ApplyTemplateDefaults(settings);
            settings.colliderType = EnemyColliderType.BoxCollider;
            settings.animatorController = controller;
            settings.visualLocalScale = Vector3.one * visualScale;
            settings.visualLocalPosition =
                Vector3.up * (-sourceBounds.min.y * visualScale);

            EnemyPrefabCreationResult result = EnemyPrefabCreatorUtility.CreateEnemy(
                settings,
                PrefabPath,
                DataPath,
                true);
            result.enemyData.xpReward = 7;
            EditorUtility.SetDirty(result.enemyData);

            BakeWalkingFlipbook(result.prefabAsset);
            EnsureEliteSupport();
            AddToSpawner();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message =
                $"DuneBurrower setup complete. Runtime height: {targetRuntimeHeight:F2} " +
                $"({TargetPlayerHeightRatio:P0} Player), visual scale: {visualScale:F4}.";
            Debug.Log($"[DuneBurrowerSetup] {message}");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("DuneBurrower Setup", message, "OK");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "DuneBurrower Setup Failed",
                    exception.Message,
                    "OK");
            }
        }
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                $"Cannot load ModelImporter at {ModelPath}.");
        }

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = true;
            clips[i].loopPose = true;
        }

        if (clips.Length > 0)
        {
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
    }

    private static AnimatorController CreateOrUpdateController(AnimationClip walkingClip)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == "Walking");
        if (state == null)
        {
            state = stateMachine.AddState("Walking");
        }

        state.motion = walkingClip;
        state.speed = 1f;
        state.writeDefaultValues = true;
        stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Bounds CalculateBounds(GameObject asset)
    {
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            instance = PrefabUtility.InstantiatePrefab(asset, previewScene) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(asset);
                SceneManager.MoveGameObjectToScene(instance, previewScene);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null
                    || renderer is ParticleSystemRenderer
                    || renderer is TrailRenderer)
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

            if (!hasBounds || bounds.size.y <= 0.001f)
            {
                throw new InvalidOperationException(
                    $"{asset.name} has no valid renderer bounds.");
            }

            return bounds;
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void EnsureEliteSupport()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<EnemyMiniBoss>() == null)
            {
                root.AddComponent<EnemyMiniBoss>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddToSpawner()
    {
        GameObject duneBurrower =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(SpawnerPath);
        try
        {
            EnemySpawn spawner = root.GetComponent<EnemySpawn>();
            if (spawner == null)
            {
                throw new InvalidOperationException(
                    "EnemySpawn component is missing from the spawner prefab.");
            }

            List<GameObject> additional = spawner.additionalEnemyPrefabs != null
                ? spawner.additionalEnemyPrefabs
                    .Where(prefab => prefab != null)
                    .ToList()
                : new List<GameObject>();
            if (!additional.Contains(duneBurrower))
            {
                additional.Add(duneBurrower);
            }
            spawner.additionalEnemyPrefabs = additional.ToArray();

            List<EnemySpawn.EnemySpawnType> types = spawner.enemyTypes != null
                ? spawner.enemyTypes.Where(type => type != null).ToList()
                : new List<EnemySpawn.EnemySpawnType>();
            EnemySpawn.EnemySpawnType duneBurrowerType =
                types.FirstOrDefault(type => type.prefab == duneBurrower);
            if (duneBurrowerType == null)
            {
                duneBurrowerType = new EnemySpawn.EnemySpawnType();
                types.Add(duneBurrowerType);
            }

            duneBurrowerType.prefab = duneBurrower;
            duneBurrowerType.earlyWeight = 10f;
            duneBurrowerType.lateWeight = 25f;
            spawner.enemyTypes = types.ToArray();

            EditorUtility.SetDirty(spawner);
            PrefabUtility.SaveAsPrefabAsset(root, SpawnerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BakeWalkingFlipbook(GameObject targetEnemyPrefab)
    {
        GameObject sourceModel =
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        AnimationClip walkingClip =
            GenericEnemyMeshFlipbookBaker.FindFirstRuntimeClip(ModelPath);
        if (sourceModel == null || walkingClip == null || targetEnemyPrefab == null)
        {
            throw new MissingReferenceException(
                "Missing DuneBurrower source model, walking clip, or target enemy prefab.");
        }

        MeshFlipbookBakeResult bakeResult =
            GenericEnemyMeshFlipbookBaker.Bake(new MeshFlipbookBakeRequest
            {
                sourceModel = sourceModel,
                animationClip = walkingClip,
                poseCount = 8,
                playbackFramesPerSecond = 8f,
                phaseBuckets = 4,
                outputRoot = "Assets/Generated/MeshFlipbook",
                outputName = "DuneBurrowerWalking",
                targetEnemyPrefab = targetEnemyPrefab,
                replaceTargetVisual = true,
                targetVisualPath = "Visual"
            });

        EnableBakedFlipbookShadows(bakeResult.prefab);
        AlignBakedFlipbookToGround(targetEnemyPrefab, bakeResult.frames);

        Texture2D albedo =
            AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoTexturePath);
        if (albedo == null)
        {
            throw new MissingReferenceException(
                $"Missing DuneBurrower albedo texture at {AlbedoTexturePath}.");
        }

        foreach (Material material in bakeResult.materials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", albedo);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
    }

    private static void EnableBakedFlipbookShadows(GameObject flipbookPrefab)
    {
        if (flipbookPrefab == null)
        {
            throw new MissingReferenceException(
                "DuneBurrower flipbook prefab is missing while enabling shadows.");
        }

        string prefabPath = AssetDatabase.GetAssetPath(flipbookPrefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            throw new InvalidOperationException(
                "DuneBurrower flipbook prefab does not have an asset path.");
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            MeshRenderer[] renderers =
                prefabRoot.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "DuneBurrower flipbook prefab does not contain a renderer.");
            }

            foreach (MeshRenderer renderer in renderers)
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AlignBakedFlipbookToGround(
        GameObject targetEnemyPrefab,
        Mesh[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            throw new InvalidOperationException(
                "DuneBurrower flipbook does not contain any mesh frames.");
        }

        bool hasFrame = false;
        for (int i = 0; i < frames.Length; i++)
        {
            Mesh frame = frames[i];
            if (frame == null)
            {
                continue;
            }

            Vector3[] vertices = frame.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                continue;
            }

            float frameMinimumY = frame.bounds.min.y;
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                vertices[vertexIndex].y -= frameMinimumY;
            }

            frame.vertices = vertices;
            frame.RecalculateBounds();
            EditorUtility.SetDirty(frame);
            hasFrame = true;
        }

        if (!hasFrame)
        {
            throw new InvalidOperationException(
                "DuneBurrower flipbook does not contain a valid mesh frame.");
        }

        string targetPath = AssetDatabase.GetAssetPath(targetEnemyPrefab);
        GameObject targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
        try
        {
            Transform visualRoot = targetRoot.transform.Find("Visual");
            if (visualRoot == null || visualRoot.childCount == 0)
            {
                throw new InvalidOperationException(
                    "DuneBurrower prefab does not contain a baked child under Visual.");
            }

            visualRoot.localPosition = Vector3.zero;
            Transform bakedVisual = visualRoot.GetChild(0);
            bakedVisual.localPosition = Vector3.zero;

            PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(targetRoot);
        }
    }
}
