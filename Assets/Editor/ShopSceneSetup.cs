using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ShopSceneSetup
{
    private static int uiLayer;
    private static Button menuButtonTemplate;
    private static Button actionButtonTemplate;
    private static Button backButtonTemplate;
    private static TMP_FontAsset font;
    private static Material fontMaterial;

    [MenuItem("Tools/Gigachad/Shop/Rebuild Shop UI")]
    public static void RebuildShopUI()
    {
        GameObject menu = GameObject.Find("GigabonkMenuPanel");
        if(menu == null)
            throw new MissingReferenceException("Open GigabonkMenu scene before rebuilding Shop UI.");

        StartUI startUI = menu.GetComponent<StartUI>();
        Transform settingsTransform = FindChild(menu.transform, "btnSetting");
        Transform playTransform = FindChild(menu.transform, "btnPlay");
        Transform exitTransform = FindChild(menu.transform, "btnExit");
        if(startUI == null || settingsTransform == null || playTransform == null || exitTransform == null)
            throw new MissingReferenceException("Main menu objects required by Shop setup were not found.");

        uiLayer = menu.layer;
        menuButtonTemplate = settingsTransform.GetComponent<Button>();

        Transform canvasTransform = menu.transform.parent;
        Transform startingWeaponPanel = FindChild(canvasTransform, "PannelSelectStartingWeapon");
        if(startingWeaponPanel == null)
            throw new MissingReferenceException("Starting Weapon panel template was not found.");

        Transform startingConfirm = FindChild(startingWeaponPanel, "btnConfirm");
        Transform startingBack = FindChild(startingWeaponPanel, "btnBack");
        if(startingConfirm == null || startingBack == null)
            throw new MissingReferenceException("Starting Weapon button templates were not found.");

        actionButtonTemplate = startingConfirm.GetComponent<Button>();
        backButtonTemplate = startingBack.GetComponent<Button>();
        TextMeshProUGUI templateText = startingConfirm.GetComponentInChildren<TextMeshProUGUI>(true);
        font = templateText.font;
        fontMaterial = templateText.fontSharedMaterial;

        RemoveExisting(FindChild(menu.transform, "btnShop"));
        GameObject shopObject = Object.Instantiate(menuButtonTemplate.gameObject, menu.transform);
        shopObject.name = "btnShop";
        Button shopButton = shopObject.GetComponent<Button>();
        shopButton.onClick.RemoveAllListeners();
        shopObject.GetComponentInChildren<TextMeshProUGUI>(true).text = "SHOP";

        SetButtonY(playTransform, 125f);
        SetButtonY(settingsTransform, 23f);
        SetButtonY(shopObject.transform, -79f);
        SetButtonY(exitTransform, -181f);
        RectTransform menuRect = (RectTransform)menu.transform;
        menuRect.sizeDelta = new Vector2(menuRect.sizeDelta.x, 440f);

        RemoveExisting(FindChild(canvasTransform, "ShopPanel"));
        GameObject panel = CreatePanel(canvasTransform);
        ShopReferences references = CreateShopWindow(panel.transform);

        ShopUI shopUI = canvasTransform.GetComponent<ShopUI>();
        if(shopUI == null)
            shopUI = canvasTransform.gameObject.AddComponent<ShopUI>();

        shopUI.ConfigureSceneReferences(
            panel,
            references.BackButton,
            references.ItemButtons,
            references.ItemBackgrounds,
            references.ItemBorders,
            references.ItemIcons,
            references.SelectedIcon,
            references.SelectedIconImage,
            references.SilverIcon,
            references.SilverAmount,
            references.InfoTitle,
            references.InfoDescription,
            references.InfoPrice,
            references.Feedback,
            references.BuyButton,
            references.RefundButton);

        SerializedObject serializedStart = new SerializedObject(startUI);
        serializedStart.FindProperty("btnShop").objectReferenceValue = shopButton;
        serializedStart.FindProperty("shopUI").objectReferenceValue = shopUI;
        serializedStart.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(canvasTransform.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = panel;
        Debug.Log("Shop UI was created as editable scene objects and saved.");
    }

    private static GameObject CreatePanel(Transform canvasTransform)
    {
        GameObject panel = NewUI("ShopPanel", canvasTransform);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image blocker = panel.AddComponent<Image>();
        blocker.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);
        return panel;
    }

    private static ShopReferences CreateShopWindow(Transform panel)
    {
        Color dark = new Color(0.4f, 0.4f, 0.4f, 1f);
        Color card = Color.black;
        Color cyan = new Color(0.24150053f, 0.6320754f, 0.4564455f, 1f);
        Color gold = new Color(0.78f, 0.58f, 0.25f, 1f);
        Color muted = new Color(0.76f, 0.76f, 0.76f, 1f);

        GameObject window = Box("ShopWindow", panel, Vector2.zero, new Vector2(980f, 780f), dark, Color.black, 4f, true);
        Box("HeaderPanel", window.transform, new Vector2(0f, 330f), new Vector2(930f, 92f), Color.black, Color.black, 3f, true);
        Text("Title", window.transform, "SHOP", 58f, FontStyles.Bold, Color.white, new Vector2(0f, 340f), new Vector2(620f, 62f), TextAlignmentOptions.Center);
        Text("Subtitle", window.transform, "PERMANENT UPGRADES", 19f, FontStyles.Bold, Color.white, new Vector2(0f, 303f), new Vector2(620f, 30f), TextAlignmentOptions.Center);

        ShopReferences result = new ShopReferences
        {
            BackButton = TemplateButton("btnBack", window.transform, "BACK", new Vector2(-398f, 330f), new Vector2(72f, 72f), backButtonTemplate),
            ItemButtons = new Button[5],
            ItemBackgrounds = new Image[5],
            ItemBorders = new Outline[5],
            ItemIcons = new Image[5]
        };
        PriceGroup balanceGroup = CreatePriceGroup(window.transform, "SilverBalance", "0", new Vector2(371f, 330f), new Vector2(170f, 50f), 28f, 26f);
        result.SilverIcon = balanceGroup.Icon;
        result.SilverAmount = balanceGroup.Amount;

        string[] iconPaths =
        {
            "Assets/PicVid/ShopIcon_01.png",
            "Assets/PicVid/ShopIcon_02.png",
            "Assets/PicVid/ShopIcon_03.png",
            "Assets/PicVid/ShopIcon_04.png",
            "Assets/PicVid/ShopIcon_05.png"
        };
        string[] titles = { "WEAPON SLOT", "TOME SLOT", "REROLL", "SKIP", "REMOVE" };
        string[] prices = { "24", "24", "9", "9", "9" };
        for(int i = 0; i < 5; i++)
        {
            float x = -384f + i * 192f;
            GameObject item = Box("Item_" + titles[i].Replace(' ', '_'), window.transform, new Vector2(x, 142f), new Vector2(174f, 218f), card, new Color(0.02f, 0.02f, 0.02f), 3f, true);
            Button itemButton = item.AddComponent<Button>();
            itemButton.targetGraphic = item.GetComponent<Image>();
            GameObject iconFrame = Box("IconFrame", item.transform, new Vector2(0f, 34f), new Vector2(92f, 92f), new Color(0.14f, 0.14f, 0.14f), gold, 3f);
            result.ItemIcons[i] = IconImage(iconFrame.transform, iconPaths[i], new Vector2(88f, 88f));
            Text("ItemName", item.transform, titles[i], 16f, FontStyles.Bold, Color.white, new Vector2(0f, -57f), new Vector2(164f, 48f), TextAlignmentOptions.Center);
            CreatePriceGroup(item.transform, "Price", prices[i], new Vector2(0f, -91f), new Vector2(164f, 30f), 20f, 15f, gold);
            result.ItemButtons[i] = itemButton;
            result.ItemBackgrounds[i] = item.GetComponent<Image>();
            result.ItemBorders[i] = item.GetComponent<Outline>();
        }

        GameObject infoBox = Box("InfoPanel", window.transform, new Vector2(0f, -154f), new Vector2(874f, 280f), dark, Color.black, 3f, true);
        GameObject selectedFrame = Box("SelectedIconFrame", infoBox.transform, new Vector2(-334f, 40f), new Vector2(150f, 150f), card, gold, 3f, true);
        result.SelectedIcon = Text("SelectedIcon", selectedFrame.transform, "W+", 56f, FontStyles.Bold, gold, Vector2.zero, new Vector2(140f, 130f), TextAlignmentOptions.Center);
        result.SelectedIconImage = IconImage(selectedFrame.transform, iconPaths[0], new Vector2(146f, 146f));
        result.InfoTitle = Text("InfoTitle", infoBox.transform, "WEAPON SLOT", 31f, FontStyles.Bold, gold, new Vector2(45f, 92f), new Vector2(560f, 46f), TextAlignmentOptions.Left);
        result.InfoDescription = Text("InfoDescription", infoBox.transform, "Unlock one additional slot for carrying a weapon.", 20f, FontStyles.Normal, Color.white, new Vector2(45f, 35f), new Vector2(560f, 75f), TextAlignmentOptions.TopLeft);
        result.InfoDescription.textWrappingMode = TextWrappingModes.Normal;
        result.InfoDescription.overflowMode = TextOverflowModes.Ellipsis;
        result.InfoPrice = Text("InfoPrice", infoBox.transform, "PRICE  24     OWNED  --", 19f, FontStyles.Bold, gold, new Vector2(45f, -31f), new Vector2(560f, 34f), TextAlignmentOptions.Left);
        result.BuyButton = TemplateButton("btnBuy", infoBox.transform, "BUY", new Vector2(-85f, -94f), new Vector2(218f, 64f));
        result.RefundButton = TemplateButton("btnRefund", infoBox.transform, "REFUND", new Vector2(165f, -94f), new Vector2(218f, 64f));
        result.Feedback = Text("Feedback", window.transform, "UI PREVIEW - SHOP LOGIC NOT CONNECTED", 16f, FontStyles.Bold, muted, new Vector2(0f, -334f), new Vector2(800f, 30f), TextAlignmentOptions.Center);
        return result;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
            if(child.name == childName)
                return child;
        return null;
    }

    private static void RemoveExisting(Transform target)
    {
        if(target != null)
            Object.DestroyImmediate(target.gameObject);
    }

    private static void SetButtonY(Transform target, float y)
    {
        RectTransform rect = (RectTransform)target;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        MenuButtonAnimator animator = target.GetComponent<MenuButtonAnimator>();
        if(animator != null)
            animator.RefreshRestingPosition();
    }

    private static GameObject NewUI(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = uiLayer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static GameObject Box(string objectName, Transform parent, Vector2 position, Vector2 size, Color color, Color border, float borderSize, bool addStartingWeaponBevel = false)
    {
        GameObject result = NewUI(objectName, parent);
        Center((RectTransform)result.transform, position, size);
        Image image = result.AddComponent<Image>();
        image.color = color;
        Outline outline = result.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(borderSize, -borderSize);
        outline.useGraphicAlpha = true;
        if(addStartingWeaponBevel)
            AddStartingWeaponBevel(result.transform, objectName.StartsWith("Item_") || objectName == "SelectedIconFrame");
        return result;
    }

    private static void AddStartingWeaponBevel(Transform parent, bool goldAccent)
    {
        AddEdge("BevelTop", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2.5f), new Vector2(0f, 5f), new Color(0.56f, 0.56f, 0.56f));
        AddEdge("BevelLeft", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2.5f, 0f), new Vector2(5f, 0f), new Color(0.56f, 0.56f, 0.56f));
        AddEdge("BevelRight", parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2.5f, 0f), new Vector2(5f, 0f), new Color(0.14f, 0.14f, 0.14f));
        AddEdge("BevelBottom", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2.5f), new Vector2(0f, 5f), new Color(0.14f, 0.14f, 0.14f));

        if(goldAccent)
            AddEdge("StyleAccent", parent, new Vector2(0.18f, 1f), new Vector2(0.82f, 1f), new Vector2(0f, -8f), new Vector2(0f, 4f), new Color(0.78f, 0.58f, 0.25f));
    }

    private static void AddEdge(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject edge = NewUI(objectName, parent);
        RectTransform rect = (RectTransform)edge.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = edge.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI Text(string objectName, Transform parent, string value, float size, FontStyles style, Color color, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
    {
        GameObject textObject = NewUI(objectName, parent);
        Center((RectTransform)textObject.transform, position, dimensions);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSharedMaterial = fontMaterial;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.margin = new Vector4(6f, 4f, 6f, 4f);
        return text;
    }
    private static PriceGroup CreatePriceGroup(Transform parent, string objectName, string amount, Vector2 position, Vector2 size, float iconSize, float amountSize)
    {
        return CreatePriceGroup(parent, objectName, amount, position, size, iconSize, amountSize, Color.white);
    }

    private static PriceGroup CreatePriceGroup(Transform parent, string objectName, string amount, Vector2 position, Vector2 size, float iconSize, float amountSize, Color amountColor)
    {
        GameObject groupObject = NewUI(objectName, parent);
        Center((RectTransform)groupObject.transform, position, size);

        HorizontalLayoutGroup layout = groupObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObject = NewUI("SilverIcon", groupObject.transform);
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/Coin/Coin.png");
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.minWidth = iconLayout.preferredWidth = iconSize;
        iconLayout.minHeight = iconLayout.preferredHeight = iconSize;

        GameObject amountObject = NewUI("Amount", groupObject.transform);
        RectTransform amountRect = (RectTransform)amountObject.transform;
        amountRect.anchorMin = amountRect.anchorMax = amountRect.pivot = new Vector2(0.5f, 0.5f);
        amountRect.sizeDelta = new Vector2(Mathf.Max(40f, size.x - iconSize - layout.spacing), size.y);
        TextMeshProUGUI amountText = amountObject.AddComponent<TextMeshProUGUI>();
        amountText.text = amount;
        amountText.font = font;
        amountText.fontSharedMaterial = fontMaterial;
        amountText.fontSize = amountSize;
        amountText.fontStyle = FontStyles.Bold;
        amountText.color = amountColor;
        amountText.alignment = TextAlignmentOptions.MidlineLeft;
        amountText.raycastTarget = false;
        amountText.margin = Vector4.zero;
        LayoutElement amountLayout = amountObject.AddComponent<LayoutElement>();
        amountLayout.minWidth = amountLayout.preferredWidth = amountRect.sizeDelta.x;
        amountLayout.minHeight = amountLayout.preferredHeight = size.y;

        return new PriceGroup
        {
            Icon = icon,
            Amount = amountText
        };
    }
    private static Image IconImage(Transform parent, string assetPath)
    {
        return IconImage(parent, assetPath, new Vector2(104f, 104f));
    }

    private static Image IconImage(Transform parent, string assetPath, Vector2 size)
    {
        GameObject iconObject = NewUI("IconImage", parent);
        Center((RectTransform)iconObject.transform, Vector2.zero, size);
        Image image = iconObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    private static Button TemplateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, Button sourceTemplate = null)
    {
        Button template = sourceTemplate != null ? sourceTemplate : actionButtonTemplate;
        GameObject result = Object.Instantiate(template.gameObject, parent);
        result.name = objectName;
        result.SetActive(true);
        Center((RectTransform)result.transform, position, size);
        Button button = result.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        TextMeshProUGUI text = result.GetComponentInChildren<TextMeshProUGUI>(true);
        if(text != null)
        {
            text.text = label;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 30f;
        }
        MenuButtonAnimator animator = result.GetComponent<MenuButtonAnimator>();
        if(animator != null)
            animator.RefreshRestingPosition();
        return button;
    }

    private sealed class PriceGroup
    {
        public Image Icon;
        public TextMeshProUGUI Amount;
    }
    private sealed class ShopReferences
    {
        public Button BackButton;
        public Button[] ItemButtons;
        public Image[] ItemBackgrounds;
        public Outline[] ItemBorders;
        public Image[] ItemIcons;
        public TextMeshProUGUI SelectedIcon;
        public Image SelectedIconImage;
        public Image SilverIcon;
        public TextMeshProUGUI SilverAmount;
        public TextMeshProUGUI InfoTitle;
        public TextMeshProUGUI InfoDescription;
        public TextMeshProUGUI InfoPrice;
        public TextMeshProUGUI Feedback;
        public Button BuyButton;
        public Button RefundButton;
    }
}
