using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class BossHealthUIBuilder
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootName = "BossHealthUI_Sample";
    private const string DesertScenePath = "Assets/Scenes/DesertArena.unity";
    private const string DesertRootName = "BossHealthUI_Desert";
    private const string EncounterRootName = "StoneGolemBossEncounter";
    private const string BossPrefabPath = "Assets/Prefab/Enemy_StoneGolemBoss.prefab";
    private const string DisplayFontPath = "Assets/UI/Fonts/SVN-Determination Sans SDF.asset";

    [MenuItem("Tools/UI/Create Boss Health Bar In SampleScene")]
    public static void CreateInSampleScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SampleScenePath)
        {
            scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
        EnemyHealth bossHealth = FindSampleBoss();

        GameObject root = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(BossHealthBarUI));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        TextMeshProUGUI bossName = CreateText(
            "BossName",
            root.transform,
            font,
            "STONE GOLEM",
            28f,
            Color.white);
        SetTopCenter(bossName.rectTransform, new Vector2(0f, -18f), new Vector2(700f, 38f));
        bossName.fontStyle = FontStyles.Bold;
        bossName.outlineWidth = 0.14f;
        bossName.outlineColor = Color.black;

        GameObject barBackground = CreateImage("HealthBarBackground", root.transform, new Color32(25, 25, 25, 230));
        SetTopCenter(barBackground.GetComponent<RectTransform>(), new Vector2(0f, -58f), new Vector2(720f, 26f));

        Image healthFill = CreateImage("HealthFill", barBackground.transform, new Color32(210, 35, 35, 255)).GetComponent<Image>();
        ConfigureFill(healthFill);
        SetStretch(healthFill.rectTransform, 3f, 3f, 3f, 3f);

        BossHealthBarUI presenter = root.GetComponent<BossHealthBarUI>();
        presenter.Configure(
            bossHealth,
            healthFill,
            null,
            bossName,
            null,
            canvasGroup,
            "STONE GOLEM");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("[BossHealthUIBuilder] Đã tạo Boss Health UI trong SampleScene.", root);
    }

    [MenuItem("Tools/Boss/Setup Stone Golem Encounter In DesertArena")]
    public static void CreateDesertEncounter()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DesertScenePath)
        {
            scene = EditorSceneManager.OpenScene(DesertScenePath, OpenSceneMode.Single);
        }

        GameObject hudCanvas = GameObject.Find("HUD_Canvas");
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (hudCanvas == null || bossPrefab == null || playerObject == null)
        {
            Debug.LogError("[BossHealthUIBuilder] DesertArena thiếu HUD_Canvas, boss prefab hoặc Player.");
            return;
        }

        DestroyExisting(DesertRootName);
        DestroyExisting(EncounterRootName);

        GameObject encounter = new GameObject(EncounterRootName, typeof(TimedBossSpawner));
        encounter.GetComponent<TimedBossSpawner>().Configure(
            bossPrefab,
            playerObject.transform,
            180f,
            18f);

        GameObject root = new GameObject(
            DesertRootName,
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(BossHealthBarUI));
        root.transform.SetParent(hudCanvas.transform, false);
        SetFullScreen(root.GetComponent<RectTransform>());

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        TextMeshProUGUI bossName = CreateText(
            "BossName",
            root.transform,
            font,
            "STONE GOLEM",
            24f,
            Color.white);
        SetTopCenter(bossName.rectTransform, new Vector2(0f, -59f), new Vector2(620f, 30f));
        bossName.fontStyle = FontStyles.Bold;
        bossName.outlineWidth = 0.14f;
        bossName.outlineColor = Color.black;

        GameObject barBackground = CreateImage(
            "HealthBarBackground",
            root.transform,
            new Color32(22, 18, 16, 235));
        SetTopCenter(
            barBackground.GetComponent<RectTransform>(),
            new Vector2(0f, -91f),
            new Vector2(620f, 20f));

        Image healthFill = CreateImage(
            "HealthFill",
            barBackground.transform,
            new Color32(205, 38, 32, 255)).GetComponent<Image>();
        ConfigureFill(healthFill);
        SetStretch(healthFill.rectTransform, 3f, 3f, 3f, 3f);

        root.GetComponent<BossHealthBarUI>().Configure(
            null,
            healthFill,
            null,
            bossName,
            null,
            canvasGroup,
            "STONE GOLEM");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = encounter;
        EditorGUIUtility.PingObject(encounter);
        Debug.Log("[BossHealthUIBuilder] Đã thêm Stone Golem mốc 03:00 và UI vào DesertArena.", encounter);
    }

    private static EnemyHealth FindSampleBoss()
    {
        GameObject bossObject = GameObject.Find("StoneGolemBoss_Test");
        if (bossObject != null)
        {
            return bossObject.GetComponent<EnemyHealth>();
        }

        StoneGolemSandBurstAttack bossAttack = Object.FindFirstObjectByType<StoneGolemSandBurstAttack>();
        return bossAttack != null ? bossAttack.GetComponent<EnemyHealth>() : null;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return gameObject;
    }

    private static void DestroyExisting(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void SetFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string text,
        float fontSize,
        Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void ConfigureFill(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
        image.raycastTarget = false;
    }

    private static void SetTopCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
