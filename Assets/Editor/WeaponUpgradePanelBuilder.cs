using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WeaponUpgradePanelBuilder
{
    private const string PrefabFolder = "Assets/UI/Prefabs";
    private const string PrefabPath = PrefabFolder + "/WeaponUpgradePanel.prefab";
    private const string FontPath = "Assets/UI/Fonts/SVN-Determination Sans SDF.asset";

    private static readonly Color Backdrop = new Color32(4, 10, 12, 190);
    private static readonly Color Border = new Color32(217, 211, 190, 255);
    private static readonly Color BorderShadow = new Color32(41, 38, 34, 255);
    private static readonly Color Window = new Color32(17, 18, 18, 255);
    private static readonly Color Header = new Color32(39, 77, 64, 255);
    private static readonly Color Card = new Color32(31, 31, 31, 255);
    private static readonly Color Gold = new Color32(255, 188, 20, 255);
    private static readonly Color Stat = new Color32(226, 207, 101, 255);
    private static readonly Color Upgrade = new Color32(22, 210, 45, 255);

    private static TMP_FontAsset font;

    [MenuItem("Tools/Gigachad UI/Build Weapon Upgrade Panel")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError("[WeaponUpgradePanelBuilder] Missing font: " + FontPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/UI", "Prefabs");

        GameObject root = new GameObject(
            "WeaponUpgradePanel",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        root.AddComponent<UpgradeUI>();

        RectTransform backdrop = StretchPanel("DimmedBackground", root.transform, Backdrop, Vector2.zero, Vector2.zero);
        backdrop.GetComponent<Image>().raycastTarget = true;

        RectTransform windowBorder = FixedPanel("UpgradeWindow", backdrop, Vector2.zero, new Vector2(940f, 940f), Border);
        RectTransform windowShadow = StretchPanel("BorderShadow", windowBorder, BorderShadow, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        RectTransform window = StretchPanel("MainPanel", windowShadow, Window, new Vector2(5f, 5f), new Vector2(-5f, -5f));

        AddCornerOrnaments(windowBorder);

        RectTransform headerBorder = FixedPanel("HeaderBorder", window, new Vector2(0f, 408f), new Vector2(880f, 94f), Border);
        RectTransform headerShadow = StretchPanel("HeaderShadow", headerBorder, BorderShadow, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        RectTransform header = StretchPanel("HeaderPanel", headerShadow, Header, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        CreateText("Title", header, "UPGRADE SELECTION", 42f, Color.white, TextAlignmentOptions.MidlineLeft,
            new Vector2(31f, 0f), new Vector2(790f, 72f));
        CreatePanel("HeaderHighlight", header, new Color32(92, 127, 105, 255),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 5f), new Vector2(-8f, 8f));

        RectTransform choiceAreaBorder = FixedPanel("ChoiceAreaBorder", window, new Vector2(0f, 45f), new Vector2(880f, 612f), Border);
        RectTransform choiceAreaShadow = StretchPanel("ChoiceAreaShadow", choiceAreaBorder, BorderShadow, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        RectTransform choiceArea = StretchPanel("ChoiceArea", choiceAreaShadow, Window, new Vector2(3f, 3f), new Vector2(-3f, -3f));

        CreateChoiceCard(choiceArea, "UpgradeChoice_01", new Vector2(0f, 193f), Card, null,
            string.Empty, string.Empty, string.Empty, string.Empty);
        CreateChoiceCard(choiceArea, "UpgradeChoice_02", new Vector2(0f, 0f), Card, null,
            string.Empty, string.Empty, string.Empty, string.Empty);
        CreateChoiceCard(choiceArea, "UpgradeChoice_03", new Vector2(0f, -193f), Card, null,
            string.Empty, string.Empty, string.Empty, string.Empty);

        RectTransform footerBorder = FixedPanel("FooterBorder", window, new Vector2(0f, -384f), new Vector2(880f, 130f), Border);
        RectTransform footerShadow = StretchPanel("FooterShadow", footerBorder, BorderShadow, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        RectTransform footer = StretchPanel("FooterPanel", footerShadow, new Color32(25, 34, 34, 255), new Vector2(3f, 3f), new Vector2(-3f, -3f));

        CreateButton(footer, "RemoveButton", new Vector2(-255f, -5f), new Vector2(170f, 72f),
            new Color32(190, 49, 48, 255), "REMOVE");
        CreateButton(footer, "SkipButton", new Vector2(0f, -5f), new Vector2(170f, 72f),
            new Color32(32, 145, 67, 255), "SKIP");
        CreateButton(footer, "RerollButton", new Vector2(255f, -5f), new Vector2(170f, 72f),
            new Color32(41, 124, 181, 255), "REROLL");

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("[WeaponUpgradePanelBuilder] Created: " + PrefabPath);
    }

    private static void CreateChoiceCard(
        Transform parent,
        string name,
        Vector2 position,
        Color background,
        Sprite icon,
        string rarity,
        string itemName,
        string statLine,
        string level)
    {
        RectTransform border = FixedPanel(name, parent, position, new Vector2(790f, 164f), Border);
        Button button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = border.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        button.transition = Selectable.Transition.ColorTint;

        RectTransform inner = StretchPanel("CardBackground", border, background, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        RectTransform iconBorder = FixedPanel("IconBorder", inner, new Vector2(-321f, 0f), new Vector2(122f, 122f), Border);
        RectTransform iconBackground = StretchPanel("IconBackground", iconBorder, new Color32(57, 62, 61, 255), new Vector2(4f, 4f), new Vector2(-4f, -4f));

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(iconBackground, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(8f, 8f);
        iconRect.offsetMax = new Vector2(-8f, -8f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        Color rarityColor = rarity == "COMMON" ? new Color32(29, 238, 117, 255) : Color.white;
        CreateText("RarityText", inner, rarity, 23f, rarityColor, TextAlignmentOptions.Center,
            new Vector2(-321f, 54f), new Vector2(122f, 32f));
        CreateText("ItemNameText", inner, itemName, 31f, Color.white, TextAlignmentOptions.MidlineLeft,
            new Vector2(10f, 14f), new Vector2(480f, 43f));
        CreateText("StatText", inner, statLine, 24f, Stat, TextAlignmentOptions.MidlineLeft,
            new Vector2(10f, -37f), new Vector2(480f, 36f));
        CreateText("LevelText", inner, level, 29f, Gold, TextAlignmentOptions.MidlineRight,
            new Vector2(301f, 42f), new Vector2(140f, 38f));
    }

    private static void CreateButton(Transform parent, string name, Vector2 position, Vector2 size, Color color, string label)
    {
        RectTransform shadow = FixedPanel(name + "Shadow", parent, position + new Vector2(5f, -7f), size, new Color32(4, 7, 8, 255));
        shadow.GetComponent<Image>().raycastTarget = false;
        RectTransform rect = FixedPanel(name, parent, position, size, color);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        button.colors = colors;
        CreateText("Label", rect, label, 27f, Color.white, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(12f, 8f));
        CreatePanel("TopHighlight", rect, Color.Lerp(color, Color.white, 0.25f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(5f, -6f), new Vector2(-5f, -3f));
    }

    private static void AddCornerOrnaments(Transform parent)
    {
        Vector2[] anchors = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        for (int i = 0; i < anchors.Length; i++)
        {
            RectTransform corner = FixedPanel("Corner_" + i, parent, Vector2.zero, new Vector2(18f, 18f), Border);
            corner.anchorMin = anchors[i];
            corner.anchorMax = anchors[i];
            corner.pivot = anchors[i];
            corner.anchoredPosition = new Vector2(anchors[i].x == 0f ? -3f : 3f, anchors[i].y == 0f ? -3f : 3f);
            corner.GetComponent<Image>().raycastTarget = false;
        }
    }

    private static RectTransform FixedPanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform StretchPanel(string name, Transform parent, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        return CreatePanel(name, parent, color, Vector2.zero, Vector2.one, offsetMin, offsetMax);
    }

    private static RectTransform CreatePanel(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 position,
        Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.extraPadding = true;
        text.raycastTarget = false;
        return text;
    }
}
