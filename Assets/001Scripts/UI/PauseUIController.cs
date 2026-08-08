using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime-built pause screen for gameplay. It reuses the same live
/// Inventory/Stats view as the upgrade screen and pauses with Escape.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseUIController : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color32(4, 16, 19, 218);
    private static readonly Color PanelColor = new Color32(31, 31, 31, 255);
    private static readonly Color BorderColor = new Color32(217, 211, 190, 255);
    private static readonly Color HeaderColor = new Color32(39, 77, 64, 255);
    private static readonly Color HeaderHighlightColor = new Color32(88, 173, 114, 255);

    private GameObject pauseCanvasObject;
    private RectTransform centerContent;
    private GameObject quitConfirmationRoot;
    private RectTransform quitConfirmationPanel;
    private UpgradeInventoryStatsView inventoryStatsView;
    private TMP_FontAsset displayFont;
    private Material displayFontMaterial;
    private Coroutine openAnimation;
    private Coroutine confirmationAnimation;
    private bool isPaused;
    private bool confirmationOpen;
    private bool transitioning;
    private bool musicDuckActive;
    private GameObject settingsPanelObject;
    [SerializeField] private GameObject pauseCanvasPrefab;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureForGameplay();
    }

    public static void EnsureForGameplay()
    {
        if (SceneManager.GetActiveScene().name != "DesertArena")
            return;
        if (FindFirstObjectByType<PauseUIController>() != null)
            return;

        GameObject controller = new GameObject("PauseUIController");
        controller.AddComponent<PauseUIController>();
    }

    private void Awake()
    {
        ResolveFont();
        Build();
        pauseCanvasObject.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (confirmationOpen)
        {
            HideQuitConfirmation();
            return;
        }

        if (isPaused)
        {
            ResumeGame();
            return;
        }

        UpgradeManager upgradeManager = FindFirstObjectByType<UpgradeManager>();
        if ((upgradeManager != null && upgradeManager.IsShowingUpgrade) || Time.timeScale <= 0f)
            return;

        ShowPause();
    }

    private void OnDestroy()
    {
        ReleaseMusicDuck();

        if (isPaused)
            Time.timeScale = 1f;
    }

    public void ShowPause()
    {
        if (isPaused || pauseCanvasObject == null)
            return;

        isPaused = true;
        RequestMusicDuck();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();

        pauseCanvasObject.SetActive(true);
        if (settingsPanelObject != null)
            settingsPanelObject.SetActive(false);
        inventoryStatsView.RefreshAll();
        inventoryStatsView.PlayOpenAnimation();
        PlayOpenAnimation();
    }

    public void ResumeGame()
    {
        if (!isPaused || transitioning)
            return;

        isPaused = false;
        ReleaseMusicDuck();
        if (openAnimation != null)
            StopCoroutine(openAnimation);
        openAnimation = null;
        centerContent.localScale = Vector3.one;
        if (settingsPanelObject != null)
            settingsPanelObject.SetActive(false);
        pauseCanvasObject.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartRun()
    {
        if (transitioning)
            return;

        transitioning = true;
        isPaused = false;
        ReleaseMusicDuck();
        Scene activeScene = SceneManager.GetActiveScene();
        MenuLoadingOverlay.Begin(
            activeScene.name,
            () =>
            {
                isPaused = false;
                Time.timeScale = 1f;
                pauseCanvasObject.SetActive(false);
            },
            displayFont,
            displayFontMaterial);
    }

    public void QuitToMenu()
    {
        ShowQuitConfirmation();
    }

    private void ConfirmQuitToMenu()
    {
        if (transitioning)
            return;

        transitioning = true;
        confirmationOpen = false;
        isPaused = false;
        ReleaseMusicDuck();
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToStartScene();
        else
            SceneManager.LoadScene("GigabonkMenu");
    }

    private void Build()
    {
        if (TryBuildFromPrefab())
            return;

        pauseCanvasObject = new GameObject(
            "PauseCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        pauseCanvasObject.transform.SetParent(transform, false);

        Canvas canvas = pauseCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = pauseCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = pauseCanvasObject.GetComponent<RectTransform>();
        RectTransform overlay = CreateUIObject("DimmedBackground", canvasRect);
        SetStretch(overlay);
        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.raycastTarget = true;

        centerContent = CreateUIObject("PauseCenter", canvasRect);
        centerContent.anchorMin = new Vector2(0.5f, 0.5f);
        centerContent.anchorMax = new Vector2(0.5f, 0.5f);
        centerContent.pivot = new Vector2(0.5f, 0.5f);
        centerContent.anchoredPosition = new Vector2(0f, 18f);
        centerContent.sizeDelta = new Vector2(560f, 650f);

        BuildTitle(centerContent);
        CreateButton("ResumeButton", "RESUME", centerContent, 125f, ResumeGame);
        CreateButton("RestartButton", "RESTART", centerContent, 34f, RestartRun);
        CreateButton("SettingsButton", "SETTINGS", centerContent, -57f, ShowSettingsPanel);
        CreateButton("QuitButton", "QUIT", centerContent, -148f, ShowQuitConfirmation);

        inventoryStatsView = pauseCanvasObject.AddComponent<UpgradeInventoryStatsView>();
        WeaponInventory weapons = FindFirstObjectByType<WeaponInventory>();
        PlayerTomeInventory tomes = FindFirstObjectByType<PlayerTomeInventory>();
        PlayerItemInventory items = FindFirstObjectByType<PlayerItemInventory>();
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        inventoryStatsView.Configure(
            weapons,
            tomes,
            items,
            health != null ? health.stats : null,
            displayFont);

        BuildQuitConfirmation(canvasRect);
        quitConfirmationRoot.SetActive(false);
        BuildSettingsPanel();
    }

    private bool TryBuildFromPrefab()
    {
        if (pauseCanvasPrefab == null)
            pauseCanvasPrefab = Resources.Load<GameObject>("UI/PauseCanvas");
        if (pauseCanvasPrefab == null)
            return false;

        pauseCanvasObject = Instantiate(pauseCanvasPrefab, transform, false);
        Canvas canvas = pauseCanvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            Destroy(pauseCanvasObject);
            pauseCanvasObject = null;
            return false;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 900;

        Transform center = FindDeepChild(pauseCanvasObject.transform, "PauseCenter");
        Transform confirmationRoot = FindDeepChild(pauseCanvasObject.transform, "QuitConfirmation");
        Transform confirmationPanel = FindDeepChild(pauseCanvasObject.transform, "ConfirmationPanel");
        if (center == null || confirmationRoot == null || confirmationPanel == null)
        {
            Destroy(pauseCanvasObject);
            pauseCanvasObject = null;
            return false;
        }

        centerContent = center.GetComponent<RectTransform>();
        quitConfirmationRoot = confirmationRoot.gameObject;
        quitConfirmationPanel = confirmationPanel.GetComponent<RectTransform>();
        if (centerContent == null || quitConfirmationPanel == null)
        {
            Destroy(pauseCanvasObject);
            pauseCanvasObject = null;
            return false;
        }

        BindButton("ResumeButton", ResumeGame);
        BindButton("RestartButton", RestartRun);
        EnsureSettingsButton();
        BindButton("QuitButton", ShowQuitConfirmation);
        BindButton("CancelButton", HideQuitConfirmation);
        BindButton("ConfirmQuitButton", ConfirmQuitToMenu);

        inventoryStatsView = pauseCanvasObject.GetComponent<UpgradeInventoryStatsView>();
        if (inventoryStatsView == null)
            inventoryStatsView = pauseCanvasObject.AddComponent<UpgradeInventoryStatsView>();

        WeaponInventory weapons = FindFirstObjectByType<WeaponInventory>();
        PlayerTomeInventory tomes = FindFirstObjectByType<PlayerTomeInventory>();
        PlayerItemInventory items = FindFirstObjectByType<PlayerItemInventory>();
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        inventoryStatsView.Configure(
            weapons,
            tomes,
            items,
            health != null ? health.stats : null,
            displayFont);

        quitConfirmationRoot.SetActive(false);
        BuildSettingsPanel();
        return true;
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction onClick)
    {
        Transform child = FindDeepChild(pauseCanvasObject.transform, objectName);
        Button button = child != null ? child.GetComponent<Button>() : null;
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
    }

    private void EnsureSettingsButton()
    {
        Transform settingsButton = FindDeepChild(pauseCanvasObject.transform, "SettingsButton");
        if (settingsButton == null)
        {
            CreateButton("SettingsButton", "SETTINGS", centerContent, -57f, ShowSettingsPanel);
        }
        else
        {
            BindButton("SettingsButton", ShowSettingsPanel);
        }

        SetButtonPosition("ResumeButton", 125f);
        SetButtonPosition("RestartButton", 34f);
        SetButtonPosition("SettingsButton", -57f);
        SetButtonPosition("QuitButton", -148f);
    }

    private void SetButtonPosition(string objectName, float y)
    {
        Transform button = FindDeepChild(pauseCanvasObject.transform, objectName);
        RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
        if (rect != null)
            rect.anchoredPosition = new Vector2(0f, y);
    }

    private void BuildSettingsPanel()
    {
        if (settingsPanelObject != null || pauseCanvasObject == null)
            return;

        Transform existingPanel = FindDeepChild(pauseCanvasObject.transform, "SettingsPanel");
        if (existingPanel != null)
            settingsPanelObject = existingPanel.gameObject;

        if (settingsPanelObject != null)
        {
            UISettings existingSettings = settingsPanelObject.GetComponentInChildren<UISettings>(true);
            if (existingSettings != null)
                existingSettings.SetPanelRoot(settingsPanelObject);

            settingsPanelObject.SetActive(false);
            return;
        }

        GameObject settingsPrefab = Resources.Load<GameObject>("UI/SettingsPanel");
        if (settingsPrefab == null)
            return;

        settingsPanelObject = Instantiate(settingsPrefab, pauseCanvasObject.transform, false);
        settingsPanelObject.name = "SettingsPanel";
        settingsPanelObject.transform.SetAsLastSibling();

        UISettings settings = settingsPanelObject.GetComponentInChildren<UISettings>(true);
        if (settings != null)
            settings.SetPanelRoot(settingsPanelObject);

        settingsPanelObject.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        if (!isPaused || settingsPanelObject == null)
            return;

        settingsPanelObject.SetActive(true);
        settingsPanelObject.transform.SetAsLastSibling();
    }

    private void RequestMusicDuck()
    {
        if (musicDuckActive || MusicAudioManager.Instance == null)
            return;

        MusicAudioManager.Instance.PushMusicDuck();
        musicDuckActive = true;
    }

    private void ReleaseMusicDuck()
    {
        if (!musicDuckActive)
            return;

        if (MusicAudioManager.Instance != null)
            MusicAudioManager.Instance.PopMusicDuck();
        musicDuckActive = false;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }




    private void BuildTitle(Transform parent)
    {
        RectTransform titleFrame = CreateUIObject("PauseTitleFrame", parent);
        titleFrame.anchorMin = new Vector2(0.5f, 0.5f);
        titleFrame.anchorMax = new Vector2(0.5f, 0.5f);
        titleFrame.pivot = new Vector2(0.5f, 0.5f);
        titleFrame.anchoredPosition = new Vector2(0f, 245f);
        titleFrame.sizeDelta = new Vector2(500f, 135f);

        TMP_Text title = CreateText(
            "PAUSE",
            titleFrame,
            Vector2.zero,
            new Vector2(480f, 118f),
            92f,
            TextAlignmentOptions.Center,
            Color.white);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
    }

    private void BuildQuitConfirmation(RectTransform canvasRect)
    {
        quitConfirmationRoot = new GameObject("QuitConfirmation", typeof(RectTransform));
        RectTransform rootRect = quitConfirmationRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvasRect, false);
        SetStretch(rootRect);

        Image blocker = quitConfirmationRoot.AddComponent<Image>();
        blocker.color = new Color32(1, 7, 9, 205);
        blocker.raycastTarget = true;

        quitConfirmationPanel = CreateUIObject("ConfirmationPanel", rootRect);
        quitConfirmationPanel.anchorMin = new Vector2(0.5f, 0.5f);
        quitConfirmationPanel.anchorMax = new Vector2(0.5f, 0.5f);
        quitConfirmationPanel.pivot = new Vector2(0.5f, 0.5f);
        quitConfirmationPanel.anchoredPosition = Vector2.zero;
        quitConfirmationPanel.sizeDelta = new Vector2(690f, 350f);

        Image panelImage = quitConfirmationPanel.gameObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        Outline panelOutline = quitConfirmationPanel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = BorderColor;
        panelOutline.effectDistance = new Vector2(4f, -4f);
        panelOutline.useGraphicAlpha = false;

        RectTransform header = CreateUIObject("ConfirmationHeader", quitConfirmationPanel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -14f);
        header.sizeDelta = new Vector2(-28f, 72f);
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = HeaderColor;
        headerImage.raycastTarget = false;

        RectTransform accent = CreateUIObject("Highlight", header);
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, 5f);
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = HeaderHighlightColor;
        accentImage.raycastTarget = false;

        TMP_Text headerText = CreateText(
            "QUIT GAME?",
            header,
            Vector2.zero,
            new Vector2(620f, 66f),
            38f,
            TextAlignmentOptions.Center,
            Color.white);
        headerText.fontStyle = FontStyles.Bold;

        TMP_Text message = CreateText(
            "ARE YOU SURE YOU WANT TO QUIT?",
            quitConfirmationPanel,
            new Vector2(0f, 25f),
            new Vector2(620f, 80f),
            27f,
            TextAlignmentOptions.Center,
            Color.white);
        message.fontStyle = FontStyles.Bold;

        CreateConfirmationButton(
            "CancelButton",
            "CANCEL",
            quitConfirmationPanel,
            new Vector2(-165f, -105f),
            HeaderColor,
            HeaderHighlightColor,
            HideQuitConfirmation);
        CreateConfirmationButton(
            "ConfirmQuitButton",
            "QUIT",
            quitConfirmationPanel,
            new Vector2(165f, -105f),
            new Color32(139, 39, 37, 255),
            new Color32(205, 58, 52, 255),
            ConfirmQuitToMenu);
    }

    private void CreateConfirmationButton(string objectName, string labelText,
        Transform parent, Vector2 position, Color normalColor, Color highlightedColor,
        UnityEngine.Events.UnityAction onClick)
    {
        RectTransform border = CreateUIObject(objectName, parent);
        border.anchorMin = new Vector2(0.5f, 0.5f);
        border.anchorMax = new Vector2(0.5f, 0.5f);
        border.pivot = new Vector2(0.5f, 0.5f);
        border.anchoredPosition = position;
        border.sizeDelta = new Vector2(250f, 66f);

        Image borderImage = border.gameObject.AddComponent<Image>();
        borderImage.color = BorderColor;
        borderImage.raycastTarget = false;

        RectTransform body = CreateUIObject("ButtonBody", border);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(4f, 4f);
        body.offsetMax = new Vector2(-4f, -4f);
        Image bodyImage = body.gameObject.AddComponent<Image>();
        bodyImage.color = normalColor;

        Button button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = bodyImage;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = Color.Lerp(highlightedColor, Color.white, 0.18f);
        colors.selectedColor = highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateText(
            labelText,
            body,
            Vector2.zero,
            new Vector2(234f, 54f),
            26f,
            TextAlignmentOptions.Center,
            Color.white).fontStyle = FontStyles.Bold;
    }

    private void ShowQuitConfirmation()
    {
        if (!isPaused || transitioning || confirmationOpen)
            return;

        confirmationOpen = true;
        quitConfirmationRoot.SetActive(true);
        if (confirmationAnimation != null)
            StopCoroutine(confirmationAnimation);
        confirmationAnimation = StartCoroutine(AnimateConfirmationOpen());
    }

    private void HideQuitConfirmation()
    {
        if (!confirmationOpen)
            return;

        confirmationOpen = false;
        if (confirmationAnimation != null)
            StopCoroutine(confirmationAnimation);
        confirmationAnimation = null;
        quitConfirmationPanel.localScale = Vector3.one;
        quitConfirmationRoot.SetActive(false);
    }

    private IEnumerator AnimateConfirmationOpen()
    {
        const float duration = 0.22f;
        const float startScale = 0.68f;
        float elapsed = 0f;
        quitConfirmationPanel.localScale = Vector3.one * startScale;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(progress));
            quitConfirmationPanel.localScale = new Vector3(scale, scale, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        quitConfirmationPanel.localScale = Vector3.one;
        confirmationAnimation = null;
    }

    private void CreateButton(string objectName, string labelText, Transform parent,
        float y, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform border = CreateUIObject(objectName, parent);
        border.anchorMin = new Vector2(0.5f, 0.5f);
        border.anchorMax = new Vector2(0.5f, 0.5f);
        border.pivot = new Vector2(0.5f, 0.5f);
        border.anchoredPosition = new Vector2(0f, y);
        border.sizeDelta = new Vector2(310f, 66f);

        Image borderImage = border.gameObject.AddComponent<Image>();
        borderImage.color = BorderColor;
        borderImage.raycastTarget = false;

        RectTransform body = CreateUIObject("ButtonBody", border);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(4f, 4f);
        body.offsetMax = new Vector2(-4f, -4f);
        Image bodyImage = body.gameObject.AddComponent<Image>();
        bodyImage.color = PanelColor;

        Button button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = bodyImage;
        ColorBlock colors = button.colors;
        colors.normalColor = PanelColor;
        colors.highlightedColor = HeaderColor;
        colors.pressedColor = HeaderHighlightColor;
        colors.selectedColor = HeaderColor;
        colors.disabledColor = new Color32(70, 70, 70, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        RectTransform highlight = CreateUIObject("Highlight", body);
        highlight.anchorMin = new Vector2(0f, 1f);
        highlight.anchorMax = new Vector2(1f, 1f);
        highlight.pivot = new Vector2(0.5f, 1f);
        highlight.anchoredPosition = Vector2.zero;
        highlight.sizeDelta = new Vector2(0f, 3f);
        Image highlightImage = highlight.gameObject.AddComponent<Image>();
        highlightImage.color = HeaderHighlightColor;
        highlightImage.raycastTarget = false;

        CreateText(
            labelText,
            body,
            Vector2.zero,
            new Vector2(294f, 54f),
            28f,
            TextAlignmentOptions.Center,
            Color.white).fontStyle = FontStyles.Bold;
    }

    private void PlayOpenAnimation()
    {
        if (openAnimation != null)
            StopCoroutine(openAnimation);
        openAnimation = StartCoroutine(AnimateOpen());
    }

    private IEnumerator AnimateOpen()
    {
        const float duration = 0.25f;
        const float startScale = 0.68f;
        float elapsed = 0f;
        centerContent.localScale = Vector3.one * startScale;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(progress));
            centerContent.localScale = new Vector3(scale, scale, 1f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        centerContent.localScale = Vector3.one;
        openAnimation = null;
    }

    private void ResolveFont()
    {
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || candidate.font == null || !candidate.gameObject.scene.IsValid())
                continue;

            displayFont = candidate.font;
            displayFontMaterial = candidate.fontSharedMaterial != null
                ? candidate.fontSharedMaterial
                : displayFont.material;
            break;
        }

        if (displayFont == null)
        {
            displayFont = TMP_Settings.defaultFontAsset;
            displayFontMaterial = displayFont != null ? displayFont.material : null;
        }
    }

    private TMP_Text CreateText(string text, Transform parent, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateUIObject(text + " Text", parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = displayFont;
        if (displayFontMaterial != null)
            label.fontSharedMaterial = displayFontMaterial;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return label;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private static RectTransform CreateUIObject(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.70158f;
        float shifted = value - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted
            + overshoot * shifted * shifted;
    }
}
