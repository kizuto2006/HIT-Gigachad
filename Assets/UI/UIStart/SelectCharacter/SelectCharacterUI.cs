using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectCharacterUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelSelectCharacter;

    [Header("Button")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnBack;

    [Header("Info Character")]
    [SerializeField] private Sprite Icon;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private TextMeshProUGUI textDescription;

    private readonly List<Button> weaponButtons = new List<Button>();
    private readonly List<GameObject> selectionAccents = new List<GameObject>();
    private WeaponData[] startingWeapons;

    private GameObject infoCharacterRoot;
    private Image infoWeaponIcon;
    private GameObject weaponInfoRoot;
    private TextMeshProUGUI lockedQuestionMark;
    private WeaponData selectedWeapon;
    private bool isStartingGame;

    private void Awake()
    {
        LoadStartingWeapons();
        CacheWeaponSlots();
        CacheInfoPanel();

        for (int i = 0; i < weaponButtons.Count; i++)
        {
            int weaponIndex = i;
            weaponButtons[i].onClick.AddListener(() => SelectWeapon(weaponIndex));
        }

        if (btnConfirm != null)
            btnConfirm.onClick.AddListener(OnClickButonConfirm);

        if (btnBack != null)
            btnBack.onClick.AddListener(OnClickBack);

        ClearSelection();
    }

    private void LoadStartingWeapons()
    {
        startingWeapons = Resources.LoadAll<WeaponData>("Weapons");
        System.Array.Sort(startingWeapons, (left, right) =>
            string.Compare(left.weaponName, right.weaponName, System.StringComparison.OrdinalIgnoreCase));
    }

    private void CacheWeaponSlots()
    {
        Transform slotRoot = transform.Find("SelectStartingWeapon/BackGroundWeapon/WeaponSlot");
        if (slotRoot == null)
            slotRoot = transform.Find("SelectCharacter/BackGroundCharacter/CharacterSlot");
        if (slotRoot == null)
            return;

        while (slotRoot.childCount < startingWeapons.Length && slotRoot.childCount > 0)
        {
            Transform template = slotRoot.GetChild(Mathf.Min(1, slotRoot.childCount - 1));
            GameObject clone = Instantiate(template.gameObject, slotRoot);
            clone.name = "Weapon (" + (slotRoot.childCount - 1) + ")";
            clone.SetActive(true);
        }

        Material sharedSlotTextMaterial = null;

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            Transform slot = slotRoot.GetChild(i);
            Button button = slot.GetComponent<Button>();
            if (button == null)
                continue;

            if (weaponButtons.Count >= startingWeapons.Length)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            int weaponIndex = weaponButtons.Count;
            WeaponData weapon = startingWeapons[weaponIndex];
            slot.gameObject.name = "Weapon (" + weaponIndex + ")";
            slot.gameObject.SetActive(true);
            weaponButtons.Add(button);
            if (button.GetComponent<MenuButtonAnimator>() == null)
                button.gameObject.AddComponent<MenuButtonAnimator>();

            button.interactable = true;
            Transform accent = slot.Find("StyleAccent");
            selectionAccents.Add(accent != null ? accent.gameObject : null);

            Transform avatar = slot.Find("GigachadAvatarDisplay");
            if (avatar != null)
                avatar.gameObject.SetActive(false);

            // Some cloned slots contain an old full-size Border graphic that renders
            // over the name plate even when its image color is transparent.
            Transform legacyBorder = slot.Find("Border");
            if (legacyBorder != null)
                legacyBorder.gameObject.SetActive(false);

            TextMeshProUGUI slotName = slot.GetComponentInChildren<TextMeshProUGUI>(true);
            if (slotName != null)
            {
                slotName.text = string.IsNullOrWhiteSpace(weapon.weaponName)
                    ? weapon.name.ToUpperInvariant()
                    : weapon.weaponName.ToUpperInvariant();
                slotName.gameObject.SetActive(true);
                slotName.enabled = true;
                slotName.color = Color.white;
                slotName.enableAutoSizing = true;
                slotName.fontSizeMin = 12f;
                slotName.fontSizeMax = 20f;
                slotName.textWrappingMode = TextWrappingModes.NoWrap;
                slotName.overflowMode = TextOverflowModes.Ellipsis;
                slotName.margin = new Vector4(4f, 0f, 4f, 0f);

                if (sharedSlotTextMaterial == null)
                    sharedSlotTextMaterial = slotName.fontSharedMaterial;
                else
                    slotName.fontSharedMaterial = sharedSlotTextMaterial;

                slotName.ForceMeshUpdate();
            }

            CreateOrGetSlotIcon(slot, weapon.icon);
        }
    }

    private static void CreateOrGetSlotIcon(Transform slot, Sprite icon)
    {
        Transform iconTransform = slot.Find("WeaponIcon");
        GameObject iconObject;
        if (iconTransform == null)
        {
            iconObject = new GameObject("WeaponIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(slot, false);
            iconObject.transform.SetAsFirstSibling();
        }
        else
        {
            iconObject = iconTransform.gameObject;
        }

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 10f);
        iconRect.sizeDelta = new Vector2(72f, 72f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    private void CacheInfoPanel()
    {
        Transform infoTransform = transform.Find("InfoWeapon");
        if (infoTransform == null)
            infoTransform = transform.Find("InfoCharacter");
        infoCharacterRoot = infoTransform != null ? infoTransform.gameObject : null;
        if (infoTransform == null)
            return;

        Transform nameTransform = infoTransform.Find("Info/textNameWeapon");
        if (nameTransform == null)
            nameTransform = infoTransform.Find("Info/textNameCharacter");
        if (nameTransform != null)
            textName = nameTransform.GetComponent<TextMeshProUGUI>();

        Transform descriptionTransform = infoTransform.Find("Info/textDescription");
        if (descriptionTransform != null)
            textDescription = descriptionTransform.GetComponent<TextMeshProUGUI>();

        Transform portraitTransform = infoTransform.Find("Info/IconWeapon");
        if (portraitTransform == null)
            portraitTransform = infoTransform.Find("Info/IconCharacter");
        if (portraitTransform == null)
            return;

        infoWeaponIcon = portraitTransform.GetComponent<Image>();
        ConfigureInfoPanelLayout();
        Transform weaponInfoTransform = infoTransform.Find("Info/WeaponInfo");
        weaponInfoRoot = weaponInfoTransform != null ? weaponInfoTransform.gameObject : null;

        Transform skinInfo = infoTransform.Find("Info/SkinInfo");
        if (skinInfo != null)
            skinInfo.gameObject.SetActive(false);

        if (weaponInfoRoot != null)
            weaponInfoRoot.SetActive(false);

        Transform existingQuestion = portraitTransform.Find("LockedQuestionMark");
        if (existingQuestion != null)
        {
            lockedQuestionMark = existingQuestion.GetComponent<TextMeshProUGUI>();
            return;
        }

        GameObject questionObject = new GameObject("LockedQuestionMark", typeof(RectTransform), typeof(TextMeshProUGUI));
        questionObject.transform.SetParent(portraitTransform, false);

        RectTransform questionRect = questionObject.GetComponent<RectTransform>();
        questionRect.anchorMin = Vector2.zero;
        questionRect.anchorMax = Vector2.one;
        questionRect.offsetMin = Vector2.zero;
        questionRect.offsetMax = Vector2.zero;

        lockedQuestionMark = questionObject.GetComponent<TextMeshProUGUI>();
        lockedQuestionMark.text = "?";
        lockedQuestionMark.alignment = TextAlignmentOptions.Center;
        lockedQuestionMark.fontSize = 108f;
        lockedQuestionMark.fontStyle = FontStyles.Bold;
        lockedQuestionMark.color = Color.white;
        lockedQuestionMark.outlineColor = Color.black;
        lockedQuestionMark.outlineWidth = 0.35f;
        lockedQuestionMark.raycastTarget = false;
    }

    private void ConfigureInfoPanelLayout()
    {
        RectTransform iconRect = infoWeaponIcon != null ? infoWeaponIcon.rectTransform : null;
        RectTransform nameRect = textName != null ? textName.rectTransform : null;
        RectTransform descriptionRect = textDescription != null ? textDescription.rectTransform : null;

        SetInfoRect(iconRect, new Vector2(-155f, 175f), new Vector2(140f, 140f));
        SetInfoRect(nameRect, new Vector2(75f, 220f), new Vector2(300f, 70f));
        SetInfoRect(descriptionRect, new Vector2(75f, 82f), new Vector2(300f, 180f));

        EnsureInfoFrame(iconRect, "WeaponIconFrame", new Vector2(18f, 18f));
        EnsureInfoFrame(nameRect, "WeaponNameFrame", new Vector2(14f, 10f));
        EnsureInfoFrame(descriptionRect, "WeaponDescriptionFrame", new Vector2(14f, 12f));

        if (infoWeaponIcon != null)
        {
            infoWeaponIcon.preserveAspect = true;
            infoWeaponIcon.raycastTarget = false;
        }

        if (textName != null)
        {
            textName.enableAutoSizing = true;
            textName.fontSizeMin = 20f;
            textName.fontSizeMax = 34f;
            textName.textWrappingMode = TextWrappingModes.NoWrap;
            textName.overflowMode = TextOverflowModes.Ellipsis;
            textName.alignment = TextAlignmentOptions.Center;
            textName.margin = new Vector4(10f, 5f, 10f, 5f);
            textName.raycastTarget = false;
        }

        if (textDescription != null)
        {
            textDescription.enableAutoSizing = true;
            textDescription.fontSizeMin = 14f;
            textDescription.fontSizeMax = 23f;
            textDescription.textWrappingMode = TextWrappingModes.Normal;
            textDescription.overflowMode = TextOverflowModes.Ellipsis;
            textDescription.alignment = TextAlignmentOptions.TopLeft;
            textDescription.margin = new Vector4(10f, 9f, 10f, 9f);
            textDescription.raycastTarget = false;
        }
    }

    private static void SetInfoRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void EnsureInfoFrame(RectTransform content, string frameName, Vector2 padding)
    {
        if (content == null || content.parent == null)
            return;

        Transform parent = content.parent;
        Transform existingFrame = parent.Find(frameName);
        GameObject frameObject;
        if (existingFrame == null)
        {
            frameObject = new GameObject(
                frameName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            frameObject.transform.SetParent(parent, false);
        }
        else
        {
            frameObject = existingFrame.gameObject;
        }

        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = content.anchorMin;
        frameRect.anchorMax = content.anchorMax;
        frameRect.pivot = content.pivot;
        frameRect.anchoredPosition = content.anchoredPosition;
        frameRect.sizeDelta = content.sizeDelta + padding;
        frameObject.transform.SetSiblingIndex(content.GetSiblingIndex());

        Image frameImage = frameObject.GetComponent<Image>();
        frameImage.color = new Color(0.055f, 0.075f, 0.085f, 0.96f);
        frameImage.raycastTarget = false;

        Outline frameOutline = frameObject.GetComponent<Outline>();
        frameOutline.effectColor = new Color(0.22f, 0.72f, 0.82f, 0.95f);
        frameOutline.effectDistance = new Vector2(3f, -3f);
        frameOutline.useGraphicAlpha = false;
    }

    public void OnClickButonConfirm()
    {
        if (selectedWeapon == null || isStartingGame)
            return;

        StartingWeaponSelection.Select(selectedWeapon);
        isStartingGame = true;

        if (btnConfirm != null)
            btnConfirm.interactable = false;

        var menuFlow = FindFirstObjectByType<GigabonkMenuFlow>();
        System.Action onFullyCovered = menuFlow != null ? menuFlow.ExitMenu : null;
        MenuLoadingOverlay.Begin("DesertArena", onFullyCovered);
    }

    public void OnClickBack()
    {
        var menuFlow = FindFirstObjectByType<GigabonkMenuFlow>();
        if (menuFlow != null)
        {
            menuFlow.ReturnToMainMenu();
            return;
        }

        panelSelectCharacter.SetActive(false);
        UIController.Instance.StartUI.SetActiveStartPanel(true);
    }

    public void SetActiveCharacter(bool active)
    {
        panelSelectCharacter.SetActive(active);

        if (active)
            ClearSelection();
    }

    private void SelectWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= startingWeapons.Length)
            return;

        selectedWeapon = startingWeapons[weaponIndex];

        if (infoCharacterRoot != null)
            infoCharacterRoot.SetActive(true);

        for (int i = 0; i < selectionAccents.Count; i++)
        {
            if (selectionAccents[i] != null)
                selectionAccents[i].SetActive(i == weaponIndex);
        }

        if (textName != null)
            textName.text = string.IsNullOrWhiteSpace(selectedWeapon.weaponName)
                ? selectedWeapon.name.ToUpperInvariant()
                : selectedWeapon.weaponName.ToUpperInvariant();

        if (textDescription != null)
        {
            textDescription.text = string.IsNullOrWhiteSpace(selectedWeapon.description)
                ? "NO DESCRIPTION AVAILABLE."
                : selectedWeapon.description;
        }

        if (infoWeaponIcon != null)
        {
            infoWeaponIcon.sprite = selectedWeapon.icon;
            infoWeaponIcon.color = Color.white;
            infoWeaponIcon.preserveAspect = true;
        }

        if (lockedQuestionMark != null)
            lockedQuestionMark.gameObject.SetActive(false);

        if (btnConfirm != null)
            btnConfirm.interactable = true;
    }

    private void ClearSelection()
    {
        isStartingGame = false;
        selectedWeapon = null;
        StartingWeaponSelection.Clear();

        if (infoCharacterRoot != null)
            infoCharacterRoot.SetActive(false);

        if (btnConfirm != null)
            btnConfirm.interactable = false;

        if (lockedQuestionMark != null)
            lockedQuestionMark.gameObject.SetActive(false);

        if (weaponInfoRoot != null)
            weaponInfoRoot.SetActive(false);

        for (int i = 0; i < selectionAccents.Count; i++)
        {
            if (selectionAccents[i] != null)
                selectionAccents[i].SetActive(false);
        }
    }
}

internal static class StartingWeaponSelection
{
    public static WeaponData SelectedWeapon { get; private set; }

    public static void Select(WeaponData weapon)
    {
        SelectedWeapon = weapon;
    }

    public static void Clear()
    {
        SelectedWeapon = null;
    }
}

internal sealed class MenuLoadingOverlay : MonoBehaviour
{
    private const string SharedLoadingFontName = "SVN-Determination Sans SDF";
    private const float FadeToBlackDuration = 0.55f;
    private const float FadeFromBlackDuration = 0.35f;
    private const float MinimumLoadingDuration = 5f;
    private const float DotBlinkSpeed = 2.5f;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI loadingText;
    private Material loadingFontMaterial;
    private TMP_FontAsset requestedFont;
    private Material requestedFontMaterial;

    public static void Begin(string sceneName, System.Action onFullyCovered,
        TMP_FontAsset font = null, Material fontMaterial = null)
    {
        MusicAudioManager.Instance?.StopMenuMusic();

        GameObject root = new GameObject(
            "MenuLoadingOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(MenuLoadingOverlay));

        DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        MenuLoadingOverlay overlay = root.GetComponent<MenuLoadingOverlay>();
        overlay.requestedFont = font;
        overlay.requestedFontMaterial = fontMaterial;
        overlay.canvasGroup = root.GetComponent<CanvasGroup>();
        overlay.canvasGroup.alpha = 0f;
        overlay.canvasGroup.interactable = true;
        overlay.canvasGroup.blocksRaycasts = true;
        overlay.BuildVisuals();
        overlay.StartCoroutine(overlay.LoadSceneRoutine(sceneName, onFullyCovered));
    }

    private void BuildVisuals()
    {
        GameObject backgroundObject = new GameObject("BlackBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        StretchToParent(backgroundRect);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = true;

        GameObject textObject = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        StretchToParent(textRect);

        loadingText = textObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI sharedFontSource = FindSharedFontSource();
        loadingText.font = sharedFontSource != null
            ? sharedFontSource.font
            : requestedFont != null
                ? requestedFont
                : TMP_Settings.defaultFontAsset;
        Material sourceMaterial = sharedFontSource != null
            ? sharedFontSource.font.material
            : requestedFontMaterial != null
                ? requestedFontMaterial
                : loadingText.font != null
                    ? loadingText.font.material
                    : null;
        if (sourceMaterial != null)
        {
            loadingFontMaterial = new Material(sourceMaterial)
            {
                name = "Loading Pixel Font (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            loadingFontMaterial.SetColor("_OutlineColor", Color.black);
            loadingFontMaterial.SetFloat("_OutlineWidth", 0.18f);
            loadingFontMaterial.SetFloat("_OutlineSoftness", 0f);
            loadingFontMaterial.SetFloat("_FaceDilate", 0.08f);
            loadingFontMaterial.EnableKeyword("OUTLINE_ON");
            loadingText.fontSharedMaterial = loadingFontMaterial;
        }

        loadingText.text = "LOADING...";
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.fontSize = 80f;
        loadingText.fontStyle = FontStyles.Bold;
        loadingText.color = Color.white;
        loadingText.outlineColor = Color.black;
        loadingText.outlineWidth = 0.25f;
        loadingText.raycastTarget = false;
        loadingText.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI FindSharedFontSource()
    {
        TextMeshProUGUI fallback = null;
        TextMeshProUGUI[] texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI candidate = texts[i];
            if (candidate == null || candidate.font == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.font.name == SharedLoadingFontName)
                return candidate;

            if (fallback == null && candidate.font.name.Contains("Determination Sans"))
                fallback = candidate;
        }

        return fallback;
    }

    private IEnumerator LoadSceneRoutine(string sceneName, System.Action onFullyCovered)
    {
        float elapsed = 0f;
        while (elapsed < FadeToBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / FadeToBlackDuration);
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, normalized);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        onFullyCovered?.Invoke();
        loadingText.gameObject.SetActive(true);

        float loadingStartedAt = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            loadingText.text = "LOAD FAILED";
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f || Time.unscaledTime - loadingStartedAt < MinimumLoadingDuration)
        {
            UpdateLoadingText();
            yield return null;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            UpdateLoadingText();
            yield return null;
        }

        yield return null;
        loadingText.gameObject.SetActive(false);

        elapsed = 0f;
        while (elapsed < FadeFromBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / FadeFromBlackDuration);
            canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, normalized);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (loadingFontMaterial != null)
            Destroy(loadingFontMaterial);
    }


    private void UpdateLoadingText()
    {
        bool showLastDot = Mathf.FloorToInt(Time.unscaledTime * DotBlinkSpeed) % 2 == 0;
        loadingText.text = showLastDot
            ? "LOADING..."
            : "LOADING..<color=#00000000>.</color>";
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
