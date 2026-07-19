using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class EnemyPrefabCreatorUtility
{
    private const float MinimumColliderSize = 0.1f;

    public static string SanitizeEnemyName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        char[] buffer = rawName.Trim().ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char character = buffer[i];
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                buffer[i] = '_';
            }
        }

        string sanitized = new string(buffer).Trim('_', '-', ' ');
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        return sanitized;
    }

    public static string GetPrefabPath(EnemyPrefabCreationSettings settings)
    {
        string safeName = SanitizeEnemyName(settings.enemyName);
        return CombineAssetPath(settings.prefabOutputFolder, $"Enemy_{safeName}.prefab");
    }

    public static string GetDataPath(EnemyPrefabCreationSettings settings)
    {
        string safeName = SanitizeEnemyName(settings.enemyName);
        return CombineAssetPath(settings.dataOutputFolder, $"EnemyData_{safeName}.asset");
    }

    public static EnemyPrefabValidationResult Validate(EnemyPrefabCreationSettings settings)
    {
        EnemyPrefabValidationResult result = new EnemyPrefabValidationResult();
        string safeName = SanitizeEnemyName(settings.enemyName);

        if (string.IsNullOrEmpty(safeName))
            result.errors.Add("Enemy Name không được để trống và phải chứa ít nhất một ký tự hợp lệ.");
        else if (!string.Equals(settings.enemyName.Trim(), safeName, StringComparison.Ordinal))
            result.warnings.Add($"Tên file sẽ được chuẩn hóa thành '{safeName}'.");

        ValidateSourceVisual(settings, result);
        ValidateFolder(settings.prefabOutputFolder, "Prefab Output Folder", result);
        ValidateFolder(settings.dataOutputFolder, "Data Output Folder", result);

        if (settings.hp <= 0f) result.errors.Add("HP phải lớn hơn 0.");
        if (settings.attack < 0f) result.errors.Add("Attack không được âm.");
        if (settings.speed < 0f) result.errors.Add("Speed không được âm.");
        if (settings.armor < 0f) result.errors.Add("Armor không được âm.");

        if (!TagExists(settings.enemyTag))
            result.errors.Add($"Tag '{settings.enemyTag}' không tồn tại trong project.");

        if (LayerMask.NameToLayer(settings.enemyLayer) < 0)
            result.errors.Add($"Layer '{settings.enemyLayer}' không tồn tại trong project.");

        ValidateTemplate(settings, result);
        ValidateCollider(settings, result);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            result.errors.Add("Không thể tạo prefab khi Editor đang ở hoặc sắp vào Play Mode.");

        if (EditorApplication.isCompiling)
            result.errors.Add("Project đang compile. Hãy chờ compile hoàn tất.");

        if (!string.IsNullOrEmpty(safeName)
            && AssetDatabase.IsValidFolder(settings.prefabOutputFolder)
            && AssetDatabase.IsValidFolder(settings.dataOutputFolder))
        {
            result.information.Add($"Prefab: {GetPrefabPath(settings)}");
            result.information.Add($"Data: {GetDataPath(settings)}");
        }

        result.information.Add(
            "EnemyAI tự kết nối EnemyManager/FlowFieldManager khi chạy; prefab không lưu scene reference.");

        return result;
    }

    public static EnemyPrefabCreationResult CreateEnemy(
        EnemyPrefabCreationSettings settings,
        string prefabPath,
        string dataPath,
        bool overwrite)
    {
        EnemyPrefabValidationResult validation = Validate(settings);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join("\n", validation.errors));

        EnemyData enemyData = null;
        bool createdNewData = false;
        EnemyDataSnapshot previousData = default;
        bool hasPreviousData = false;

        try
        {
            enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            if (enemyData != null)
            {
                if (!overwrite)
                    throw new InvalidOperationException($"EnemyData đã tồn tại: {dataPath}");

                previousData = new EnemyDataSnapshot(enemyData);
                hasPreviousData = true;
            }
            else
            {
                Object existingAsset = AssetDatabase.LoadMainAssetAtPath(dataPath);
                if (existingAsset != null)
                    throw new InvalidOperationException($"Đường dẫn data đang chứa asset khác loại: {dataPath}");

                enemyData = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(enemyData, dataPath);
                createdNewData = true;
            }

            ApplyEnemyData(settings, enemyData);
            EditorUtility.SetDirty(enemyData);
            AssetDatabase.SaveAssets();

            GameObject prefab = BuildAndSavePrefab(settings, enemyData, prefabPath, overwrite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new EnemyPrefabCreationResult
            {
                prefabPath = prefabPath,
                dataPath = dataPath,
                prefabAsset = prefab,
                enemyData = enemyData
            };
        }
        catch
        {
            if (createdNewData && AssetDatabase.LoadMainAssetAtPath(dataPath) != null)
            {
                AssetDatabase.DeleteAsset(dataPath);
            }
            else if (hasPreviousData && enemyData != null)
            {
                previousData.Restore(enemyData);
                EditorUtility.SetDirty(enemyData);
                AssetDatabase.SaveAssets();
            }

            throw;
        }
    }

    public static string[] GetExpectedComponentNames(EnemyPrefabCreationSettings settings)
    {
        string collider = settings.colliderType switch
        {
            EnemyColliderType.CapsuleCollider => nameof(CapsuleCollider),
            EnemyColliderType.BoxCollider => nameof(BoxCollider),
            EnemyColliderType.SphereCollider => nameof(SphereCollider),
            EnemyColliderType.UseExistingCollider => "Existing Collider(s)",
            _ => "No Collider"
        };

        if (settings.colliderType == EnemyColliderType.UseExistingCollider)
        {
            return new[]
            {
                nameof(EnemyAI),
                nameof(EnemyHealth),
                nameof(EnemyContactDamage),
                collider,
                nameof(EnemyContactDamageRelay)
            };
        }

        return new[]
        {
            nameof(EnemyAI),
            nameof(EnemyHealth),
            nameof(EnemyContactDamage),
            collider
        };
    }

    public static void ApplyTemplateDefaults(EnemyPrefabCreationSettings settings)
    {
        GameObject template = settings.templateEnemyPrefab;
        if (!IsPrefabAsset(template)) return;

        settings.enemyTag = template.tag;
        string layerName = LayerMask.LayerToName(template.layer);
        settings.enemyLayer = string.IsNullOrEmpty(layerName) ? "Default" : layerName;

        Collider templateCollider = template.GetComponent<Collider>();
        if (templateCollider is CapsuleCollider) settings.colliderType = EnemyColliderType.CapsuleCollider;
        else if (templateCollider is BoxCollider) settings.colliderType = EnemyColliderType.BoxCollider;
        else if (templateCollider is SphereCollider) settings.colliderType = EnemyColliderType.SphereCollider;
        else settings.colliderType = EnemyColliderType.CapsuleCollider;

        Animator animator = template.GetComponentInChildren<Animator>(true);
        if (animator != null)
            settings.animatorController = animator.runtimeAnimatorController;
    }

    private static GameObject BuildAndSavePrefab(
        EnemyPrefabCreationSettings settings,
        EnemyData enemyData,
        string prefabPath,
        bool overwrite)
    {
        Object existing = AssetDatabase.LoadMainAssetAtPath(prefabPath);
        if (existing != null && (!overwrite || existing is not GameObject))
            throw new InvalidOperationException($"Prefab output đã tồn tại hoặc không đúng loại: {prefabPath}");

        Scene workingScene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;

        try
        {
            string safeName = SanitizeEnemyName(settings.enemyName);
            root = InstantiateVisual(settings.sourceVisual, workingScene);
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.UnpackPrefabInstance(
                    root,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            StripToEmptyRoot(root);
            root.name = $"Enemy_{safeName}";
            root.tag = settings.enemyTag;
            root.layer = LayerMask.NameToLayer(settings.enemyLayer);
            ResetTransform(root.transform);

            GameObject visualContainer = Object.Instantiate(root, root.transform, false);
            visualContainer.name = "Visual";
            visualContainer.transform.localPosition = settings.visualLocalPosition;
            visualContainer.transform.localEulerAngles = settings.visualLocalRotation;
            visualContainer.transform.localScale = settings.visualLocalScale;

            EnemyAI enemyAI = root.AddComponent<EnemyAI>();
            EnemyHealth enemyHealth = root.AddComponent<EnemyHealth>();
            EnemyContactDamage contactDamage = root.AddComponent<EnemyContactDamage>();

            CopyKnownTemplateConfiguration(settings, enemyAI, enemyHealth, contactDamage);
            enemyHealth.data = enemyData;
            contactDamage.data = enemyData;

            GameObject visualInstance = InstantiateVisual(settings.sourceVisual, workingScene);
            visualInstance.name = settings.sourceVisual.name;
            visualInstance.transform.SetParent(visualContainer.transform, false);
            ResetTransform(visualInstance.transform);

            RemoveDuplicateGameplayComponents(visualInstance);
            ConfigureAnimator(settings, visualInstance);

            Collider rootCollider = ConfigureCollider(settings, root, visualContainer);
            SetLayerRecursively(root, root.layer);
            
            if (rootCollider == null
                && settings.colliderType != EnemyColliderType.UseExistingCollider
                && settings.colliderType != EnemyColliderType.None)
            {
                throw new InvalidOperationException("Không thể tạo collider theo cấu hình đã chọn.");
            }

            bool success;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
            if (!success || prefab == null)
                throw new InvalidOperationException($"Unity không thể lưu prefab tại {prefabPath}");

            return prefab;
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(workingScene);
        }
    }

    private static void StripToEmptyRoot(GameObject root)
    {
        while (root.transform.childCount > 0)
            Object.DestroyImmediate(root.transform.GetChild(0).gameObject);

        Component[] components = root.GetComponents<Component>();
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component != null && component is not Transform)
                Object.DestroyImmediate(component);
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
    }

    private static GameObject InstantiateVisual(GameObject source, Scene previewScene)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
        if (instance != null) return instance;

        instance = Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(instance, previewScene);
        return instance;
    }

    private static void CopyKnownTemplateConfiguration(
        EnemyPrefabCreationSettings settings,
        EnemyAI targetAI,
        EnemyHealth targetHealth,
        EnemyContactDamage targetContactDamage)
    {
        if (!settings.copyConfigurationFromTemplate || settings.templateEnemyPrefab == null) return;

        CopyComponentValues(settings.templateEnemyPrefab.GetComponent<EnemyAI>(), targetAI);
        CopyComponentValues(settings.templateEnemyPrefab.GetComponent<EnemyHealth>(), targetHealth);
        CopyComponentValues(settings.templateEnemyPrefab.GetComponent<EnemyContactDamage>(), targetContactDamage);
    }

    private static void CopyComponentValues(Component source, Component destination)
    {
        if (source == null || destination == null) return;
        ComponentUtility.CopyComponent(source);
        ComponentUtility.PasteComponentValues(destination);
        ClearSceneObjectReferences(destination);
    }

    private static void ClearSceneObjectReferences(Component component)
    {
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Object reference = property.objectReferenceValue;
            if (reference != null && !EditorUtility.IsPersistent(reference))
                property.objectReferenceValue = null;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveDuplicateGameplayComponents(GameObject visualInstance)
    {
        DestroyComponents(visualInstance.GetComponentsInChildren<EnemyAI>(true));
        DestroyComponents(visualInstance.GetComponentsInChildren<EnemyHealth>(true));
        DestroyComponents(visualInstance.GetComponentsInChildren<EnemyContactDamage>(true));
        DestroyComponents(visualInstance.GetComponentsInChildren<EnemyContactDamageRelay>(true));
    }

    private static void DestroyComponents<T>(T[] components) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
            Object.DestroyImmediate(components[i]);
    }

    private static void ConfigureAnimator(EnemyPrefabCreationSettings settings, GameObject visualInstance)
    {
        Animator animator = visualInstance.GetComponentInChildren<Animator>(true);
        if (animator == null && settings.addAnimatorIfMissing)
            animator = visualInstance.AddComponent<Animator>();

        if (animator != null && settings.animatorController != null)
            animator.runtimeAnimatorController = settings.animatorController;
    }

    private static Collider ConfigureCollider(
        EnemyPrefabCreationSettings settings,
        GameObject root,
        GameObject visualContainer)
    {
        Collider[] visualColliders = visualContainer.GetComponentsInChildren<Collider>(true);
        if (settings.colliderType == EnemyColliderType.UseExistingCollider)
        {
            EnemyContactDamage contactDamage = root.GetComponent<EnemyContactDamage>();
            for (int i = 0; i < visualColliders.Length; i++)
            {
                visualColliders[i].isTrigger = settings.isTrigger;
                EnemyContactDamageRelay relay = visualColliders[i].GetComponent<EnemyContactDamageRelay>();
                if (relay == null)
                    relay = visualColliders[i].gameObject.AddComponent<EnemyContactDamageRelay>();
                relay.SetReceiver(contactDamage);
            }
            return visualColliders.Length > 0 ? visualColliders[0] : null;
        }

        for (int i = 0; i < visualColliders.Length; i++)
            Object.DestroyImmediate(visualColliders[i]);

        if (settings.colliderType == EnemyColliderType.None) return null;

        Collider collider = settings.colliderType switch
        {
            EnemyColliderType.BoxCollider => root.AddComponent<BoxCollider>(),
            EnemyColliderType.SphereCollider => root.AddComponent<SphereCollider>(),
            _ => root.AddComponent<CapsuleCollider>()
        };

        CopyTemplateColliderConfiguration(settings, collider);
        collider.isTrigger = settings.isTrigger;

        if (settings.autoFitCollider)
        {
            if (!TryCalculateLocalRendererBounds(root.transform, visualContainer, out Bounds bounds))
                throw new InvalidOperationException("Source visual không có Renderer hợp lệ để Auto Fit Collider.");
            FitCollider(collider, bounds);
        }

        return collider;
    }

    private static void CopyTemplateColliderConfiguration(
        EnemyPrefabCreationSettings settings,
        Collider targetCollider)
    {
        if (!settings.copyConfigurationFromTemplate || settings.templateEnemyPrefab == null) return;
        Collider sourceCollider = settings.templateEnemyPrefab.GetComponent(targetCollider.GetType()) as Collider;
        CopyComponentValues(sourceCollider, targetCollider);
    }

    private static bool TryCalculateLocalRendererBounds(
        Transform prefabRoot,
        GameObject visualContainer,
        out Bounds localBounds)
    {
        Renderer[] renderers = visualContainer.GetComponentsInChildren<Renderer>(true);
        localBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 localPoint = prefabRoot.InverseTransformPoint(worldPoint);

                if (!hasBounds)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        if (!hasBounds) return false;
        localBounds.size = new Vector3(
            Mathf.Max(MinimumColliderSize, localBounds.size.x),
            Mathf.Max(MinimumColliderSize, localBounds.size.y),
            Mathf.Max(MinimumColliderSize, localBounds.size.z));
        return true;
    }

    private static void FitCollider(Collider collider, Bounds bounds)
    {
        if (collider is CapsuleCollider capsule)
        {
            capsule.direction = 1;
            capsule.center = bounds.center;
            capsule.radius = Mathf.Max(MinimumColliderSize * 0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            capsule.height = Mathf.Max(bounds.size.y, capsule.radius * 2f);
        }
        else if (collider is BoxCollider box)
        {
            box.center = bounds.center;
            box.size = bounds.size;
        }
        else if (collider is SphereCollider sphere)
        {
            sphere.center = bounds.center;
            sphere.radius = Mathf.Max(MinimumColliderSize * 0.5f, bounds.extents.magnitude);
        }
    }

    private static void SetScaleTarget(EnemyHealth health, Transform scaleTarget)
    {
        SerializedObject serialized = new SerializedObject(health);
        SerializedProperty property = serialized.FindProperty("scaleTarget");
        if (property == null)
            throw new MissingFieldException(nameof(EnemyHealth), "scaleTarget");
        property.objectReferenceValue = scaleTarget;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyEnemyData(EnemyPrefabCreationSettings settings, EnemyData data)
    {
        data.hp = settings.hp;
        data.atk = settings.attack;
        data.speed = settings.speed;
        data.armor = settings.armor;
        data.size = settings.size;
    }

    private static void ValidateSourceVisual(
        EnemyPrefabCreationSettings settings,
        EnemyPrefabValidationResult result)
    {
        if (settings.sourceVisual == null)
        {
            result.errors.Add("Source Visual Asset chưa được chọn.");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(settings.sourceVisual);
        if (string.IsNullOrEmpty(sourcePath) || !EditorUtility.IsPersistent(settings.sourceVisual))
        {
            result.errors.Add("Source Visual phải là GameObject asset trong Project, không phải scene object.");
            return;
        }

        if (!IsPrefabAsset(settings.sourceVisual))
        {
            result.errors.Add("Source Visual phải là Model Prefab hoặc Prefab asset hợp lệ.");
            return;
        }

        if (string.Equals(sourcePath, GetPrefabPath(settings), StringComparison.Ordinal))
            result.errors.Add("Prefab Output không được ghi đè trực tiếp Source Visual Asset.");

        if (settings.templateEnemyPrefab != null
            && string.Equals(
                AssetDatabase.GetAssetPath(settings.templateEnemyPrefab),
                GetPrefabPath(settings),
                StringComparison.Ordinal))
        {
            result.errors.Add("Prefab Output không được ghi đè trực tiếp Template Enemy Prefab.");
        }

        if (settings.sourceVisual.GetComponentInChildren<EnemyAI>(true) != null
            || settings.sourceVisual.GetComponentInChildren<EnemyHealth>(true) != null
            || settings.sourceVisual.GetComponentInChildren<EnemyContactDamage>(true) != null)
        {
            result.warnings.Add("Source Visual có gameplay component; bản sao sẽ loại bỏ EnemyAI/EnemyHealth/EnemyContactDamage để tránh duplicate.");
        }

        if (settings.sourceVisual.GetComponentsInChildren<Renderer>(true).Length == 0)
            result.warnings.Add("Source Visual không có Renderer; Auto Fit Collider sẽ không hoạt động.");

        if (settings.sourceVisual.GetComponentInChildren<Animator>(true) == null
            && !settings.addAnimatorIfMissing)
        {
            result.warnings.Add("Source Visual không có Animator và Add Animator If Missing đang tắt.");
        }
    }

    private static void ValidateTemplate(
        EnemyPrefabCreationSettings settings,
        EnemyPrefabValidationResult result)
    {
        if (!settings.copyConfigurationFromTemplate) return;
        if (!IsPrefabAsset(settings.templateEnemyPrefab))
        {
            result.errors.Add("Template Enemy Prefab phải là prefab asset hợp lệ.");
            return;
        }

        if (settings.templateEnemyPrefab.GetComponent<EnemyAI>() == null)
            result.warnings.Add("Template không có EnemyAI; tool vẫn sẽ thêm component với giá trị mặc định.");
        if (settings.templateEnemyPrefab.GetComponent<EnemyHealth>() == null)
            result.warnings.Add("Template không có EnemyHealth; tool sẽ thêm và gán EnemyData mới.");
        if (settings.templateEnemyPrefab.GetComponent<EnemyContactDamage>() == null)
            result.warnings.Add("Template không có EnemyContactDamage; tool sẽ thêm và gán EnemyData mới.");

        result.information.Add(
            $"Template valid: {AssetDatabase.GetAssetPath(settings.templateEnemyPrefab)}");
    }

    private static void ValidateCollider(
        EnemyPrefabCreationSettings settings,
        EnemyPrefabValidationResult result)
    {
        if (settings.colliderType == EnemyColliderType.None)
        {
            result.warnings.Add("Không có Collider: contact damage và melee hit detection có thể không hoạt động.");
            return;
        }

        if (!settings.isTrigger)
        {
            result.warnings.Add(
                "Is Trigger đang tắt trong khi EnemyContactDamage dùng trigger callback; hãy kiểm tra lại collision setup.");
        }

        if (settings.colliderType == EnemyColliderType.UseExistingCollider
            && settings.sourceVisual != null
            && settings.sourceVisual.GetComponentInChildren<Collider>(true) == null)
        {
            result.errors.Add("Use Existing Collider được chọn nhưng Source Visual không có Collider.");
        }

        if (settings.autoFitCollider
            && settings.colliderType != EnemyColliderType.UseExistingCollider
            && settings.sourceVisual != null
            && settings.sourceVisual.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            result.errors.Add("Auto Fit Collider cần ít nhất một Renderer trong Source Visual.");
        }
    }

    private static void ValidateFolder(
        string path,
        string fieldName,
        EnemyPrefabValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.StartsWith("Assets", StringComparison.Ordinal)
            || !AssetDatabase.IsValidFolder(path))
        {
            result.errors.Add($"{fieldName} phải là folder Unity hợp lệ bắt đầu bằng 'Assets/'.");
        }
    }

    private static bool TagExists(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        string[] tags = InternalEditorUtility.tags;
        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i] == tag) return true;
        }
        return false;
    }

    private static bool IsPrefabAsset(GameObject gameObject)
    {
        if (gameObject == null || !EditorUtility.IsPersistent(gameObject)) return false;
        return PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab;
    }

    private static string CombineAssetPath(string folder, string fileName)
    {
        return $"{folder.TrimEnd('/')}/{fileName}";
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
    }

    private static void ResetTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private readonly struct EnemyDataSnapshot
    {
        private readonly float hp;
        private readonly float attack;
        private readonly float speed;
        private readonly float armor;
        private readonly EnemySize size;

        public EnemyDataSnapshot(EnemyData data)
        {
            hp = data.hp;
            attack = data.atk;
            speed = data.speed;
            armor = data.armor;
            size = data.size;
        }

        public void Restore(EnemyData data)
        {
            data.hp = hp;
            data.atk = attack;
            data.speed = speed;
            data.armor = armor;
            data.size = size;
        }
    }
}
