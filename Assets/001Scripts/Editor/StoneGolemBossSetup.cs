using System.Linq;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates a gameplay-ready Stone Golem boss from the imported walking FBX.
/// The generated prefab intentionally stays outside EnemySpawn's random pool;
/// place it in a scene or use a dedicated one-shot boss spawner.
/// </summary>
internal static class StoneGolemBossSetup
{
    private const string SourceModelPath =
        "Assets/Model/Enemy/StoneGolem/Meshy_AI_desert_stone_golem_30_biped_Animation_Walking_withSkin.fbx";
    private const string TemplatePrefabPath = "Assets/Prefab/Enemy_SandHunter.prefab";
    private const string PrefabPath = "Assets/Prefab/Enemy_StoneGolemBoss.prefab";
    private const string DataPath = "Assets/Resources/Enemies/EnemyData_StoneGolemBoss.asset";
    
    private const string MaterialPath = "Assets/Model/Enemy/StoneGolem/StoneGolemBoss.mat";
    private const string AnimatorControllerPath = "Assets/Model/Enemy/StoneGolem/StoneGolemBoss.controller";
    private const string AlbedoPath = "Assets/Model/Enemy/StoneGolem/Meshy_AI_desert_stone_golem_30_biped_texture_0.png";
    private const string MetallicPath = "Assets/Model/Enemy/StoneGolem/Meshy_AI_desert_stone_golem_30_biped_texture_0_metallic.png";
private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    // EnemyHealth doubles HP for EnemySize.Large, so effective boss HP is 800.
    private const float BaseHp = 400f;
    private const float Attack = 22f;
    private const float MoveSpeed = 2.4f;
    private const float Armor = 3f;
    
    private const float VisualBaseScale = 1.568f;
private const int XpReward = 75;

public static void CreateStoneGolemBoss()
    {
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);

        if (sourceModel == null)
        {
            Debug.LogError($"[StoneGolemBossSetup] Không tìm thấy model: {SourceModelPath}");
            return;
        }

        if (templatePrefab == null)
        {
            Debug.LogError($"[StoneGolemBossSetup] Không tìm thấy prefab mẫu: {TemplatePrefabPath}");
            return;
        }

        ConfigureSourceAnimationImport();
        AnimatorController animatorController = EnsureAnimatorController();

        EnemyPrefabCreationSettings settings = new EnemyPrefabCreationSettings
        {
            enemyName = "StoneGolemBoss",
            sourceVisual = sourceModel,
            prefabOutputFolder = "Assets/Prefab",
            dataOutputFolder = "Assets/Resources/Enemies",
            hp = BaseHp,
            attack = Attack,
            speed = MoveSpeed,
            armor = Armor,
            size = EnemySize.Large,
            enemyTag = "Untagged",
            enemyLayer = "Default",
            colliderType = EnemyColliderType.CapsuleCollider,
            autoFitCollider = true,
            isTrigger = true,
            addAnimatorIfMissing = true,
            animatorController = animatorController,
            copyConfigurationFromTemplate = true,
            templateEnemyPrefab = templatePrefab
        };

        EnemyPrefabCreationResult result = EnemyPrefabCreatorUtility.CreateEnemy(
            settings,
            PrefabPath,
            DataPath,
            overwrite: true);

        result.enemyData.xpReward = XpReward;
        EditorUtility.SetDirty(result.enemyData);
        
        BakeWalkingFlipbook(result.prefabAsset);
ConfigureBossPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = result.prefabAsset;
        EditorGUIUtility.PingObject(result.prefabAsset);

        Debug.Log(
            "[StoneGolemBossSetup] Đã tạo Stone Golem Boss với animation Walking và material URP. " +
            $"HP thực tế 800, ATK {Attack}, Speed {MoveSpeed}, Armor {Armor}, XP {XpReward}.",
            result.prefabAsset);
    }

    [MenuItem("Tools/Enemy/Update Stone Golem Boss Skills")]
    public static void UpdateStoneGolemBossSkills()
    {
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (bossPrefab == null)
        {
            throw new MissingReferenceException($"Không tìm thấy prefab boss tại {PrefabPath}.");
        }

        ConfigureBossPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = bossPrefab;
        EditorGUIUtility.PingObject(bossPrefab);
        Debug.Log("[StoneGolemBossSetup] Đã cập nhật kỹ năng Sand Burst cho Stone Golem Boss.", bossPrefab);
    }

    [MenuItem("Tools/Enemy/Test Stone Golem Boss In SampleScene")]
    public static void CreateAndPlaceInSampleScene()
    {
        CreateStoneGolemBoss();

        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (bossPrefab == null)
        {
            throw new MissingReferenceException($"Không thể tạo prefab boss tại {PrefabPath}.");
        }

        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        // Keep the navigation managers active while disabling random enemy waves,
        // so the isolated boss can still use the project's normal movement system.
        GameObject enemySpawnRoot = null;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == "EnemySpawn")
            {
                enemySpawnRoot = rootObject;
                break;
            }
        }
        if (enemySpawnRoot != null)
        {
            enemySpawnRoot.SetActive(true);
            EnemySpawn enemySpawn = enemySpawnRoot.GetComponent<EnemySpawn>();
            if (enemySpawn != null)
            {
                enemySpawn.enabled = false;
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<PlayerHealth>() == null)
        {
            player.AddComponent<PlayerHealth>();
        }

        GameObject previousTestBoss = GameObject.Find("StoneGolemBoss_Test");
        if (previousTestBoss != null)
        {
            Object.DestroyImmediate(previousTestBoss);
        }

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
        boss.name = "StoneGolemBoss_Test";

        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        boss.transform.position = playerPosition + new Vector3(12f, 0f, 0f);
        boss.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = boss;

        Debug.Log(
            $"[StoneGolemBossSetup] Đã đặt StoneGolemBoss_Test vào {SampleScenePath}, " +
            "cách Player 12 đơn vị để Play test.",
            boss);
    }
private static void ConfigureBossPrefab()
    {
        Material material = EnsureMaterial();
        AnimatorController animatorController = EnsureAnimatorController();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Transform visualRoot = root.transform.Find("Visual");
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * VisualBaseScale;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(material, slotCount).ToArray();
            }

            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
            }

            EnemyContactDamage contactDamage = root.GetComponent<EnemyContactDamage>();
            if (contactDamage != null)
            {
                contactDamage.damageCooldown = 1.25f;
                contactDamage.contactPushSpeed = 4f;
            }

            EnemyAI enemyAI = root.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                SerializedObject serializedAI = new SerializedObject(enemyAI);
                SerializedProperty damping = serializedAI.FindProperty("knockbackDamping");
                if (damping != null)
                {
                    damping.floatValue = 14f;
                    serializedAI.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (root.GetComponent<StoneGolemSandBurstAttack>() == null)
            {
                root.AddComponent<StoneGolemSandBurstAttack>();
            }

            if (root.GetComponent<StoneGolemBossAttackLock>() == null)
            {
                root.AddComponent<StoneGolemBossAttackLock>();
            }

            if (root.GetComponent<StoneGolemSeismicRingAttack>() == null)
            {
                root.AddComponent<StoneGolemSeismicRingAttack>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }


private static Material EnsureMaterial()
    {
        TextureImporter metallicImporter = AssetImporter.GetAtPath(MetallicPath) as TextureImporter;
        if (metallicImporter != null && metallicImporter.sRGBTexture)
        {
            metallicImporter.sRGBTexture = false;
            metallicImporter.SaveAndReimport();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new MissingReferenceException("Không tìm thấy shader Universal Render Pipeline/Lit.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "StoneGolemBoss",
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
            material.enableInstancing = true;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);

        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_MainTex", albedo);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_MetallicGlossMap", metallic);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 0.25f);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }


private static AnimatorController EnsureAnimatorController()
    {
        AnimationClip walkingClip = AssetDatabase.LoadAllAssetsAtPath(SourceModelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

        if (walkingClip == null)
        {
            throw new MissingReferenceException($"FBX không có animation clip: {SourceModelPath}");
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState walkingState = stateMachine.states
            .Select(childState => childState.state)
            .FirstOrDefault(state => state.name == "Walking");

        if (walkingState == null)
        {
            walkingState = stateMachine.AddState("Walking");
        }

        walkingState.motion = walkingClip;
        walkingState.speed = 1f;
        stateMachine.defaultState = walkingState;
        EditorUtility.SetDirty(controller);
        return controller;
    }


private static void ConfigureSourceAnimationImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(SourceModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new MissingReferenceException($"Không thể đọc ModelImporter: {SourceModelPath}");
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool needsReimport = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (!clips[i].loopTime)
            {
                clips[i].loopTime = true;
                needsReimport = true;
            }
        }

        if (needsReimport || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }


private static void BakeWalkingFlipbook(GameObject targetEnemyPrefab)
    {
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        AnimationClip walkingClip = GenericEnemyMeshFlipbookBaker.FindFirstRuntimeClip(SourceModelPath);
        Material material = EnsureMaterial();

        if (sourceModel == null || walkingClip == null || targetEnemyPrefab == null)
        {
            throw new MissingReferenceException("Thiếu source, clip Walking hoặc prefab khi bake StoneGolem.");
        }

        GenericEnemyMeshFlipbookBaker.Bake(new MeshFlipbookBakeRequest
        {
            sourceModel = sourceModel,
            animationClip = walkingClip,
            materialOverrides = new[] { material },
            poseCount = 8,
            playbackFramesPerSecond = 8f,
            phaseBuckets = 4,
            outputRoot = "Assets/Generated/MeshFlipbook",
            outputName = "StoneGolemWalking",
            targetEnemyPrefab = targetEnemyPrefab,
            replaceTargetVisual = true,
            targetVisualPath = "Visual"
        });
    }
}

