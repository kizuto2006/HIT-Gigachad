using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectCharacterUI : MonoBehaviour
{
    private static readonly string[] CharacterNames =
    {
        "GIGACHAD",
        "MAGICFOX",
        "KNIGHT",
        "COWBOY"
    };

    [Header("Panel")]
    [SerializeField] private GameObject panelSelectCharacter;

    [Header("Button")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnBack;

    [Header("Info Character")]
    [SerializeField] private Sprite Icon;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private TextMeshProUGUI textDescription;

    private readonly List<Button> characterButtons = new List<Button>();
    private readonly List<GameObject> selectionAccents = new List<GameObject>();

    private GameObject infoCharacterRoot;
    private Image infoPortrait;
    private GameObject weaponInfoRoot;
    private Sprite gigachadPortrait;
    private Color gigachadPortraitColor;
    private TextMeshProUGUI lockedQuestionMark;
    private int selectedCharacterIndex = -1;
    private bool isStartingGame;

    private void Awake()
    {
        CacheCharacterSlots();
        CacheInfoPanel();

        for (int i = 0; i < characterButtons.Count; i++)
        {
            int characterIndex = i;
            characterButtons[i].onClick.AddListener(() => SelectCharacter(characterIndex));
        }

        if (btnConfirm != null)
            btnConfirm.onClick.AddListener(OnClickButonConfirm);

        if (btnBack != null)
            btnBack.onClick.AddListener(OnClickBack);

        ClearSelection();
    }

    private void CacheCharacterSlots()
    {
        Transform slotRoot = transform.Find("SelectCharacter/BackGroundCharacter/CharacterSlot");
        if (slotRoot == null)
            return;

        Material sharedSlotTextMaterial = null;

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            Transform slot = slotRoot.GetChild(i);
            Button button = slot.GetComponent<Button>();
            if (button == null)
                continue;

            if (characterButtons.Count >= CharacterNames.Length)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            characterButtons.Add(button);
            if (button.GetComponent<MenuButtonAnimator>() == null)
                button.gameObject.AddComponent<MenuButtonAnimator>();

            // Locked characters must still receive clicks so their locked details can be shown.
            button.interactable = true;
            Transform accent = slot.Find("StyleAccent");
            selectionAccents.Add(accent != null ? accent.gameObject : null);

            Transform avatar = slot.Find("GigachadAvatarDisplay");
            if (avatar != null)
                avatar.gameObject.SetActive(characterButtons.Count == 1);

            // Some cloned slots contain an old full-size Border graphic that renders
            // over the name plate even when its image color is transparent.
            Transform legacyBorder = slot.Find("Border");
            if (legacyBorder != null)
                legacyBorder.gameObject.SetActive(false);

            TextMeshProUGUI slotName = slot.GetComponentInChildren<TextMeshProUGUI>(true);
            if (slotName != null)
            {
                slotName.text = CharacterNames[characterButtons.Count - 1];
                slotName.gameObject.SetActive(true);
                slotName.enabled = true;
                slotName.color = Color.white;

                if (sharedSlotTextMaterial == null)
                    sharedSlotTextMaterial = slotName.fontSharedMaterial;
                else
                    slotName.fontSharedMaterial = sharedSlotTextMaterial;

                slotName.ForceMeshUpdate();
            }
        }
    }

    private void CacheInfoPanel()
    {
        Transform infoTransform = transform.Find("InfoCharacter");
        infoCharacterRoot = infoTransform != null ? infoTransform.gameObject : null;
        if (infoTransform == null)
            return;

        Transform nameTransform = infoTransform.Find("Info/textNameCharacter");
        if (nameTransform != null)
            textName = nameTransform.GetComponent<TextMeshProUGUI>();

        Transform descriptionTransform = infoTransform.Find("Info/textDescription");
        if (descriptionTransform != null)
            textDescription = descriptionTransform.GetComponent<TextMeshProUGUI>();

        Transform portraitTransform = infoTransform.Find("Info/IconCharacter");
        if (portraitTransform == null)
            return;

        infoPortrait = portraitTransform.GetComponent<Image>();
        Transform weaponInfoTransform = infoTransform.Find("Info/WeaponInfo");
        weaponInfoRoot = weaponInfoTransform != null ? weaponInfoTransform.gameObject : null;
        if (infoPortrait != null)
        {
            gigachadPortrait = infoPortrait.sprite != null ? infoPortrait.sprite : Icon;
            gigachadPortraitColor = infoPortrait.color;
        }

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

    public void OnClickButonConfirm()
    {
        if (selectedCharacterIndex != 0 || isStartingGame)
            return;

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

    private void SelectCharacter(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= CharacterNames.Length)
            return;

        selectedCharacterIndex = characterIndex;
        bool isGigachad = characterIndex == 0;

        if (infoCharacterRoot != null)
            infoCharacterRoot.SetActive(true);

        for (int i = 0; i < selectionAccents.Count; i++)
        {
            if (selectionAccents[i] != null)
                selectionAccents[i].SetActive(i == characterIndex);
        }

        if (textName != null)
            textName.text = CharacterNames[characterIndex];

        if (textDescription != null)
        {
            textDescription.text = isGigachad
                ? "THE STRONGEST SURVIVOR.\nPOWERFUL, SIMPLE, UNSTOPPABLE."
                : "LOCKED CHARACTER";
        }

        if (infoPortrait != null)
        {
            infoPortrait.sprite = isGigachad ? gigachadPortrait : null;
            infoPortrait.color = isGigachad ? gigachadPortraitColor : new Color32(64, 114, 139, 255);
        }

        if (lockedQuestionMark != null)
            lockedQuestionMark.gameObject.SetActive(!isGigachad);

        if (weaponInfoRoot != null)
            weaponInfoRoot.SetActive(isGigachad);

        if (btnConfirm != null)
            btnConfirm.interactable = isGigachad;
    }

    private void ClearSelection()
    {
        isStartingGame = false;
        selectedCharacterIndex = -1;

        if (infoCharacterRoot != null)
            infoCharacterRoot.SetActive(false);

        if (btnConfirm != null)
            btnConfirm.interactable = false;

        if (lockedQuestionMark != null)
            lockedQuestionMark.gameObject.SetActive(false);

        if (weaponInfoRoot != null)
            weaponInfoRoot.SetActive(true);

        for (int i = 0; i < selectionAccents.Count; i++)
        {
            if (selectionAccents[i] != null)
                selectionAccents[i].SetActive(false);
        }
    }
}

internal sealed class MenuLoadingOverlay : MonoBehaviour
{
    private const float FadeToBlackDuration = 0.55f;
    private const float FadeFromBlackDuration = 0.35f;
    private const float MinimumLoadingDuration = 5f;
    private const float DotBlinkSpeed = 2.5f;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI loadingText;
    private Material loadingFontMaterial;

    public static void Begin(string sceneName, System.Action onFullyCovered)
    {
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
        GameObject menuLogo = GameObject.Find("GigabonkLogo");
        TextMeshProUGUI menuFontSource = menuLogo != null
            ? menuLogo.GetComponent<TextMeshProUGUI>()
            : null;

        loadingText.font = menuFontSource != null ? menuFontSource.font : TMP_Settings.defaultFontAsset;
        if (menuFontSource != null)
        {
            loadingFontMaterial = new Material(menuFontSource.fontSharedMaterial)
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
