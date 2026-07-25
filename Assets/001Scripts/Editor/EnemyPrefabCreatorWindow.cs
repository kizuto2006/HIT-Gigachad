using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public sealed class EnemyPrefabCreatorWindow : EditorWindow
{
    private const string MenuPath = "Tools/HIT-Gigachad/Enemy Prefab Creator";
    private const string DefaultTemplatePath = "Assets/Prefab/Mummy.prefab";

    private EnemyPrefabCreationSettings settings;
    private EnemyPrefabValidationResult validation;
    private Vector2 scrollPosition;
    private bool showValidationDetails = true;
    private GameObject lastCreatedPrefab;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        EnemyPrefabCreatorWindow window = GetWindow<EnemyPrefabCreatorWindow>();
        window.titleContent = new GUIContent("Enemy Prefab Creator");
        window.minSize = new Vector2(480f, 650f);
        window.Show();
    }

    private void OnEnable()
    {
        if (settings == null)
            ResetSettings();
    }

    private void OnGUI()
    {
        EnsureSettings();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Enemy Prefab Creator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tạo một EnemyData và prefab gameplay có cấu trúc thống nhất từ model/FBX/prefab trong Project.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        DrawIdentitySection();
        DrawStatsSection();
        DrawPrefabSetupSection();
        DrawComponentsSection();
        DrawPreviewSection();
        bool changed = EditorGUI.EndChangeCheck();

        if (changed || validation == null)
            validation = EnemyPrefabCreatorUtility.Validate(settings);

        DrawValidationSection();
        DrawActionButtons();
        EditorGUILayout.Space(10f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawIdentitySection()
    {
        DrawSectionHeader("Enemy Identity");
        settings.enemyName = EditorGUILayout.TextField(
            new GUIContent("Enemy Name", "Tên được chuẩn hóa để dùng làm tên file asset."),
            settings.enemyName);
        settings.sourceVisual = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Source Visual Asset", "Chọn model prefab, FBX hoặc GameObject prefab trong Project."),
            settings.sourceVisual,
            typeof(GameObject),
            false);
        settings.prefabOutputFolder = DrawAssetFolderField("Prefab Output Folder", settings.prefabOutputFolder);
        settings.dataOutputFolder = DrawAssetFolderField("Data Output Folder", settings.dataOutputFolder);

        string safeName = EnemyPrefabCreatorUtility.SanitizeEnemyName(settings.enemyName);
        if (!string.IsNullOrEmpty(safeName))
        {
            EditorGUILayout.LabelField("Prefab File", $"Enemy_{safeName}.prefab");
            EditorGUILayout.LabelField("Data File", $"EnemyData_{safeName}.asset");
        }
    }

    private void DrawStatsSection()
    {
        DrawSectionHeader("Base Stats");
        settings.hp = EditorGUILayout.FloatField("HP", settings.hp);
        settings.attack = EditorGUILayout.FloatField("Attack", settings.attack);
        settings.speed = EditorGUILayout.FloatField("Speed", settings.speed);
        settings.armor = EditorGUILayout.FloatField("Armor", settings.armor);
        settings.size = (EnemySize)EditorGUILayout.EnumPopup("Size", settings.size);
    }

    private void DrawPrefabSetupSection()
    {
        DrawSectionHeader("Prefab Setup");
        settings.enemyTag = EditorGUILayout.TagField("Enemy Tag", settings.enemyTag);
        settings.enemyLayer = DrawLayerField(settings.enemyLayer);
        settings.colliderType = (EnemyColliderType)EditorGUILayout.EnumPopup("Collider Type", settings.colliderType);

        using (new EditorGUI.DisabledScope(
                   settings.colliderType == EnemyColliderType.UseExistingCollider
                   || settings.colliderType == EnemyColliderType.None))
        {
            settings.autoFitCollider = EditorGUILayout.Toggle(
                new GUIContent("Auto Fit Collider", "Tính collider từ tất cả Renderer đang bật trong visual hierarchy."),
                settings.autoFitCollider);
        }

        using (new EditorGUI.DisabledScope(settings.colliderType == EnemyColliderType.None))
            settings.isTrigger = EditorGUILayout.Toggle("Is Trigger", settings.isTrigger);

        settings.visualLocalPosition = EditorGUILayout.Vector3Field("Visual Local Position", settings.visualLocalPosition);
        settings.visualLocalRotation = EditorGUILayout.Vector3Field("Visual Local Rotation", settings.visualLocalRotation);
        settings.visualLocalScale = EditorGUILayout.Vector3Field("Visual Local Scale", settings.visualLocalScale);
        settings.addAnimatorIfMissing = EditorGUILayout.Toggle("Add Animator If Missing", settings.addAnimatorIfMissing);
        settings.animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller",
            settings.animatorController,
            typeof(RuntimeAnimatorController),
            false);

        EditorGUILayout.Space(3f);
        settings.copyConfigurationFromTemplate = EditorGUILayout.Toggle(
            new GUIContent("Copy Configuration From Template Enemy", "Sao chép các setting chung của component, không sao chép scene reference."),
            settings.copyConfigurationFromTemplate);

        using (new EditorGUI.DisabledScope(!settings.copyConfigurationFromTemplate))
        {
            GameObject previousTemplate = settings.templateEnemyPrefab;
            settings.templateEnemyPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Template Enemy Prefab",
                settings.templateEnemyPrefab,
                typeof(GameObject),
                false);

            if (previousTemplate != settings.templateEnemyPrefab && settings.templateEnemyPrefab != null)
                EnemyPrefabCreatorUtility.ApplyTemplateDefaults(settings);

            if (GUILayout.Button("Use Template Tag, Layer, Collider And Animator"))
                EnemyPrefabCreatorUtility.ApplyTemplateDefaults(settings);
        }
    }

    private void DrawComponentsSection()
    {
        DrawSectionHeader("Components");
        string[] components = EnemyPrefabCreatorUtility.GetExpectedComponentNames(settings);
        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < components.Length; i++)
                EditorGUILayout.TextField($"{i + 1}.", components[i]);
        }
    }

    private void DrawPreviewSection()
    {
        DrawSectionHeader("Output Preview");
        string safeName = EnemyPrefabCreatorUtility.SanitizeEnemyName(settings.enemyName);
        if (string.IsNullOrEmpty(safeName))
        {
            EditorGUILayout.HelpBox("Nhập Enemy Name để xem đường dẫn output.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Prefab", EnemyPrefabCreatorUtility.GetPrefabPath(settings), EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Data", EnemyPrefabCreatorUtility.GetDataPath(settings), EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Tag", settings.enemyTag);
        EditorGUILayout.LabelField("Layer", settings.enemyLayer);
    }

    private void DrawValidationSection()
    {
        DrawSectionHeader("Validation");
        showValidationDetails = EditorGUILayout.Foldout(showValidationDetails, "Validation Details", true);
        if (!showValidationDetails) return;

        if (validation.IsValid && validation.warnings.Count == 0)
            EditorGUILayout.HelpBox("Dữ liệu hợp lệ và sẵn sàng tạo prefab.", MessageType.Info);

        DrawMessages(validation.errors, MessageType.Error);
        DrawMessages(validation.warnings, MessageType.Warning);
        DrawMessages(validation.information, MessageType.Info);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate", GUILayout.Height(28f)))
            {
                validation = EnemyPrefabCreatorUtility.Validate(settings);
                showValidationDetails = true;
                Repaint();
            }

            if (GUILayout.Button("Reset Form", GUILayout.Height(28f)))
                ResetSettings();
        }

        using (new EditorGUI.DisabledScope(validation == null || !validation.IsValid))
        {
            if (GUILayout.Button("Create Enemy Prefab", GUILayout.Height(36f)))
                CreateEnemy();
        }

        using (new EditorGUI.DisabledScope(lastCreatedPrefab == null))
        {
            if (GUILayout.Button("Open Prefab"))
                AssetDatabase.OpenAsset(lastCreatedPrefab);
        }
    }

    private void CreateEnemy()
    {
        validation = EnemyPrefabCreatorUtility.Validate(settings);
        if (!validation.IsValid)
        {
            showValidationDetails = true;
            return;
        }

        string prefabPath = EnemyPrefabCreatorUtility.GetPrefabPath(settings);
        string dataPath = EnemyPrefabCreatorUtility.GetDataPath(settings);
        bool prefabExists = AssetDatabase.LoadMainAssetAtPath(prefabPath) != null;
        bool dataExists = AssetDatabase.LoadMainAssetAtPath(dataPath) != null;
        bool overwrite = false;

        if (prefabExists || dataExists)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Enemy asset đã tồn tại",
                "Một hoặc cả hai output path đã tồn tại. Chọn cách xử lý:",
                "Create Unique Name",
                "Cancel",
                "Overwrite");

            if (choice == 1) return;
            if (choice == 0)
            {
                prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                dataPath = AssetDatabase.GenerateUniqueAssetPath(dataPath);
            }
            else
            {
                overwrite = true;
            }
        }

        try
        {
            EnemyPrefabCreationResult result = EnemyPrefabCreatorUtility.CreateEnemy(
                settings,
                prefabPath,
                dataPath,
                overwrite);

            lastCreatedPrefab = result.prefabAsset;
            Selection.activeObject = result.prefabAsset;
            EditorGUIUtility.PingObject(result.prefabAsset);
            Debug.Log($"[Enemy Prefab Creator] Created prefab: {result.prefabPath} | data: {result.dataPath}");
            EditorUtility.DisplayDialog(
                "Enemy Prefab Creator",
                $"Tạo enemy thành công.\n\nPrefab: {result.prefabPath}\nData: {result.dataPath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Không thể tạo Enemy",
                $"Tool đã cleanup object tạm.\n\n{exception.Message}",
                "OK");
        }
        finally
        {
            validation = EnemyPrefabCreatorUtility.Validate(settings);
        }
    }

    private void ResetSettings()
    {
        settings = new EnemyPrefabCreationSettings
        {
            templateEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath)
        };

        if (settings.templateEnemyPrefab != null)
            EnemyPrefabCreatorUtility.ApplyTemplateDefaults(settings);

        validation = EnemyPrefabCreatorUtility.Validate(settings);
        lastCreatedPrefab = null;
        Repaint();
    }

    private void EnsureSettings()
    {
        if (settings == null)
            ResetSettings();
    }

    private static void DrawSectionHeader(string title)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void DrawMessages(System.Collections.Generic.List<string> messages, MessageType type)
    {
        for (int i = 0; i < messages.Count; i++)
            EditorGUILayout.HelpBox(messages[i], type);
    }

    private static string DrawAssetFolderField(string label, string currentPath)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            currentPath = EditorGUILayout.TextField(label, currentPath);
            if (GUILayout.Button("Select", GUILayout.Width(58f)))
            {
                string projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
                string absolutePath = EditorUtility.OpenFolderPanel(label, Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(absolutePath)
                    && !string.IsNullOrEmpty(projectRoot)
                    && absolutePath.StartsWith(projectRoot, StringComparison.Ordinal))
                {
                    currentPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                }
            }
        }

        return currentPath.Replace('\\', '/').TrimEnd('/');
    }

    private static string DrawLayerField(string currentLayerName)
    {
        string[] layers = InternalEditorUtility.layers;
        int currentIndex = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == currentLayerName)
            {
                currentIndex = i;
                break;
            }
        }

        int selectedIndex = EditorGUILayout.Popup("Enemy Layer", currentIndex, layers);
        return layers.Length > 0 ? layers[Mathf.Clamp(selectedIndex, 0, layers.Length - 1)] : currentLayerName;
    }
}
