using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;

public static class SurvivalHUDBuilder
{
    private static readonly Color Ink = Hex("101419");
    private static readonly Color Panel = Hex("171D24");
    private static readonly Color PanelLight = Hex("222B34");
    private static readonly Color Line = Hex("52606B");
    private static readonly Color Gold = Hex("F0B94E");
    private static readonly Color Red = Hex("E93A3A");
    private static readonly Color RedDark = Hex("6B151B");
    private static readonly Color Cyan = Hex("17C7E8");
    private static readonly Color CyanDark = Hex("0A4658");
    private static readonly Color ExperienceGreen = Hex("22D96F");
    private static readonly Color Muted = Hex("82909C");

    private static Font font;
    private static TMP_FontAsset tmpFont;

    [InitializeOnLoadMethod]
    private static void BuildOnFirstImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.GetActiveScene().name == "DesertArena" &&
                GameObject.Find("HUD_Canvas") == null)
            {
                Build();
            }
        };
    }

    [MenuItem("Tools/Gigachad UI/Build Survival HUD")]
    public static void Build()
    {
        GameObject existing = GameObject.Find("HUD_Canvas");
        if (existing != null)
            Object.DestroyImmediate(existing);

        const string fontPath = "Assets/UI/Fonts/SVN-Determination Sans.otf";
        ConfigurePixelFontImporter(fontPath);
        font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("[SurvivalHUDBuilder] SVN-Determination Sans was not found. Using LegacyRuntime fallback.");
        }
        tmpFont = GetOrCreateTMPFontAsset(font);
        ConfigureTMPFontMaterial(tmpFont);

        GameObject canvasObject = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        SurvivalHUD hud = canvasObject.AddComponent<SurvivalHUD>();

        // Full-width experience panel.
        RectTransform xpPanel = PanelObject("ExperiencePanel", canvasObject.transform, Ink, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -30f), new Vector2(-16f, -12f));
        AddOutline(xpPanel.gameObject, Line, new Vector2(1f, -1f));
        RectTransform xpTrack = PanelObject("ExperienceTrack", xpPanel, CyanDark, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image xpFill = BarFill("ExperienceFill", xpTrack, ExperienceGreen);
        xpFill.type = Image.Type.Simple;
        xpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        xpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        TMP_Text xpText = Label("ExperienceText", xpTrack, "XP  0 / 15", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);

        // Timer panel, top-centre and visually separated from gameplay counters.
        RectTransform timerPanel = FixedPanel("TimerPanel", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0f, -51f), new Vector2(216f, 70f), Panel);
        AddOutline(timerPanel.gameObject, Line, new Vector2(1f, -1f));
        Label("TimerCaption", timerPanel, "THOI GIAN", 16, Muted, TextAnchor.UpperCenter, FontStyle.Normal, new Vector2(0f, -8f), new Vector2(190f, 24f));
        TMP_Text timerText = Label("TimerText", timerPanel, "00:00", 34, Color.white, TextAnchor.LowerCenter, FontStyle.Normal, new Vector2(0f, -1f), new Vector2(190f, 47f));

        // Level panel, pinned to the top-right safe margin.
        RectTransform levelPanel = FixedPanel("LevelPanel", canvasObject.transform, Vector2.one, new Vector2(-28f, -52f), new Vector2(190f, 64f), Panel);
        AddOutline(levelPanel.gameObject, Gold, new Vector2(1f, -1f));
        TMP_Text levelText = Label("LevelText", levelPanel, "LEVEL 01", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Normal, Vector2.zero, new Vector2(170f, 54f));

        // Left-side player status group. Every section is its own named panel.
        RectTransform playerStatus = FixedPanel("PlayerStatusPanel", canvasObject.transform, new Vector2(0f, 1f), new Vector2(28f, -79f), new Vector2(344f, 298f), new Color(0f, 0f, 0f, 0f));

        RectTransform hpPanel = FixedPanel("HealthPanel", playerStatus, new Vector2(0f, 1f), Vector2.zero, new Vector2(344f, 62f), Panel);
        AddOutline(hpPanel.gameObject, Red, new Vector2(1f, -1f));
        RectTransform hpBadge = FixedPanel("HealthBadge", hpPanel, new Vector2(0f, 0.5f), new Vector2(11f, 0f), new Vector2(46f, 42f), Red);
        Label("HealthIcon", hpBadge, "+", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
        RectTransform hpTrack = FixedPanel("HealthTrack", hpPanel, new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(258f, 32f), RedDark);
        Image hpFill = BarFill("HealthFill", hpTrack, Red);
        TMP_Text hpText = Label("HealthText", hpTrack, "101 / 101", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);

        RectTransform inventoryPanel = FixedPanel("InventoryPanel", playerStatus, new Vector2(0f, 1f), new Vector2(0f, -76f), new Vector2(344f, 222f), new Color(0f, 0f, 0f, 0f));
        Label("WeaponsTitle", inventoryPanel, "VU KHI", 16, Gold, TextAnchor.MiddleLeft, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(170f, 25f), new Vector2(0f, 1f));
        Label("BooksTitle", inventoryPanel, "SACH NANG CAP", 16, Cyan, TextAnchor.MiddleRight, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(170f, 25f), new Vector2(1f, 1f));

        RectTransform weaponPanel = FixedPanel("WeaponSlotsPanel", inventoryPanel, new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(344f, 88f), new Color(0f, 0f, 0f, 0f));
        CreateSlot("WeaponSlot_01", weaponPanel, new Vector2(0f, 0f), "W1", "WEAPON", Gold);
        CreateSlot("WeaponSlot_02", weaponPanel, new Vector2(92f, 0f), "W2", "WEAPON", Gold);

        RectTransform bookPanel = FixedPanel("UpgradeBookSlotsPanel", inventoryPanel, new Vector2(0f, 1f), new Vector2(0f, -130f), new Vector2(344f, 88f), new Color(0f, 0f, 0f, 0f));
        CreateSlot("UpgradeBookSlot_01", bookPanel, new Vector2(0f, 0f), "I", "BOOK", Cyan);
        CreateSlot("UpgradeBookSlot_02", bookPanel, new Vector2(92f, 0f), "II", "BOOK", Cyan);

        SerializedObject serializedHUD = new SerializedObject(hud);
        serializedHUD.FindProperty("playerHealth").objectReferenceValue = Object.FindFirstObjectByType<PlayerHealth>();
        serializedHUD.FindProperty("displayFont").objectReferenceValue = tmpFont;
        XPSystem xpSystem = Object.FindFirstObjectByType<XPSystem>();
        if (xpSystem == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                xpSystem = player.AddComponent<XPSystem>();
        }
        serializedHUD.FindProperty("xpSystem").objectReferenceValue = xpSystem;
        serializedHUD.FindProperty("healthFill").objectReferenceValue = hpFill;
        serializedHUD.FindProperty("healthText").objectReferenceValue = hpText;
        serializedHUD.FindProperty("experienceFill").objectReferenceValue = xpFill;
        serializedHUD.FindProperty("experienceText").objectReferenceValue = xpText;
        serializedHUD.FindProperty("levelText").objectReferenceValue = levelText;
        serializedHUD.FindProperty("timerText").objectReferenceValue = timerText;
        serializedHUD.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = canvasObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[SurvivalHUDBuilder] HUD created and scene saved.");
    }

    private static void ConfigurePixelFontImporter(string path)
    {
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer == null) return;

        bool changed = importer.fontRenderingMode != FontRenderingMode.HintedRaster ||
                       importer.fontTextureCase != FontTextureCase.Dynamic ||
                       importer.fontSize != 32 ||
                       !importer.shouldRoundAdvanceValue;

        if (!changed) return;

        importer.fontRenderingMode = FontRenderingMode.HintedRaster;
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.fontSize = 32;
        importer.characterPadding = 0;
        importer.characterSpacing = 0;
        importer.shouldRoundAdvanceValue = true;
        importer.SaveAndReimport();
    }

    private static TMP_FontAsset GetOrCreateTMPFontAsset(Font sourceFont)
    {
        const string assetPath = "Assets/UI/Fonts/SVN-Determination Sans SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            96,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (created == null)
        {
            Debug.LogError("[SurvivalHUDBuilder] Could not create the SVN-Determination Sans TMP font asset.");
            return null;
        }

        created.name = "SVN-Determination Sans SDF";
        AssetDatabase.CreateAsset(created, assetPath);

        if (created.material != null && !AssetDatabase.Contains(created.material))
        {
            created.material.name = "SVN-Determination Sans SDF Material";
            AssetDatabase.AddObjectToAsset(created.material, created);
        }

        Texture2D atlasTexture = created.atlasTexture;
        if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture))
        {
            atlasTexture.name = "SVN-Determination Sans SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlasTexture, created);
        }

        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    private static void ConfigureTMPFontMaterial(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null || fontAsset.material == null) return;

        ShaderUtilities.GetShaderPropertyIDs();
        Material material = fontAsset.material;
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.14f);
        material.SetFloat(ShaderUtilities.ID_WeightNormal, 0.12f);
        material.SetFloat(ShaderUtilities.ID_Sharpness, 0.35f);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        ShaderUtilities.UpdateShaderRatios(material);
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
    }

    private static void CreateSlot(string name, Transform parent, Vector2 position, string icon, string caption, Color accent)
    {
        Color translucentSlot = new Color32(36, 44, 45, 184);
        RectTransform slot = FixedPanel(name, parent, new Vector2(0f, 1f), position, new Vector2(80f, 80f), translucentSlot);

        GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slot, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(8f, 8f);
        iconRect.offsetMax = new Vector2(-8f, -8f);

        Image itemIcon = iconObject.GetComponent<Image>();
        itemIcon.sprite = null;
        itemIcon.color = Color.white;
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;
        itemIcon.enabled = false;

        HUDItemSlot itemSlot = slot.gameObject.AddComponent<HUDItemSlot>();
        itemSlot.Configure(itemIcon);
    }

    private static RectTransform PanelObject(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    private static RectTransform FixedPanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    private static Image BarFill(string name, Transform parent, Color color)
    {
        RectTransform rect = PanelObject(name, parent, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = rect.GetComponent<Image>();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, FontStyle style, Vector2? position = null, Vector2? dimensions = null, Vector2? anchor = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        if (dimensions.HasValue)
        {
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = a;
            rect.anchorMax = a;
            rect.pivot = a;
            rect.anchoredPosition = position ?? Vector2.zero;
            rect.sizeDelta = dimensions.Value;
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = tmpFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = ConvertAlignment(alignment);
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.extraPadding = true;
        return text;
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void AddOutline(GameObject go, Color color, Vector2 distance)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color color);
        return color;
    }
}
