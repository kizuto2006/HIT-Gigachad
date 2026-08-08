using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class DesertArenaTitleSplash : MonoBehaviour
{
    private const string TargetSceneName = "DesertArena";
    private const string LoadingOverlayName = "MenuLoadingOverlay";
    private const string SharedFontName = "SVN-Determination Sans SDF";

    [Header("Desert Arena title")]
    [SerializeField] private string title = "DESERT ARENA";
    [SerializeField] private float revealDelay = 0.08f;
    [SerializeField] private float revealDuration = 0.72f;
    [SerializeField] private float holdDuration = 0.85f;
    [SerializeField] private float fadeDuration = 0.65f;
    [SerializeField] private float startScale = 0.78f;
    [SerializeField] private float startOffset = -24f;

    private TMP_FontAsset sharedFont;
    private Material sharedFontMaterial;
    private CanvasGroup overlayGroup;
    private CanvasGroup titleGroup;
    private RectTransform titleRect;
    private Vector2 titleBasePosition;
    private Vector3 titleBaseScale;
    private bool hasStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName)
            return;

        if (FindFirstObjectByType<DesertArenaTitleSplash>() != null)
            return;

        GameObject root = new GameObject("DesertArenaTitleSplash");
        SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
        DesertArenaTitleSplash splash = root.AddComponent<DesertArenaTitleSplash>();
        splash.Begin();
    }

    private void Start()
    {
        Begin();
    }

    private void Begin()
    {
        if (hasStarted)
            return;

        hasStarted = true;
        StartCoroutine(ShowWhenSceneReady());
    }

    private IEnumerator ShowWhenSceneReady()
    {
        yield return null;

        while (GameObject.Find(LoadingOverlayName) != null)
            yield return null;

        yield return null;

        ResolveSceneTypography();
        CreateOverlay();

        if (titleGroup == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (revealDelay > 0f)
            yield return new WaitForSecondsRealtime(revealDelay);

        float elapsed = 0f;
        Vector2 startPosition = titleBasePosition + Vector2.up * startOffset;
        Vector3 startTitleScale = titleBaseScale * Mathf.Max(0.1f, startScale);

        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = revealDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / revealDuration);
            float eased = EaseOutCubic(normalized);
            float scaleEased = EaseOutBack(normalized);

            titleGroup.alpha = eased;
            titleRect.anchoredPosition = Vector2.LerpUnclamped(
                startPosition,
                titleBasePosition,
                eased);
            titleRect.localScale = Vector3.LerpUnclamped(
                startTitleScale,
                titleBaseScale,
                scaleEased);
            yield return null;
        }

        titleGroup.alpha = 1f;
        titleRect.anchoredPosition = titleBasePosition;
        titleRect.localScale = titleBaseScale;

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = fadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / fadeDuration);
            overlayGroup.alpha = 1f - EaseInCubic(normalized);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void ResolveSceneTypography()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        TMP_Text preferredSource = null;
        TMP_Text fallbackSource = null;
        TMP_Text[] labels = Resources.FindObjectsOfTypeAll<TMP_Text>();

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || label.font == null || label.gameObject.scene != activeScene)
                continue;

            if (fallbackSource == null)
                fallbackSource = label;

            if (label.font.name == SharedFontName)
            {
                preferredSource = label;
                break;
            }
        }

        TMP_Text source = preferredSource != null ? preferredSource : fallbackSource;
        sharedFont = source != null && source.font != null
            ? source.font
            : TMP_Settings.defaultFontAsset;
        sharedFontMaterial = source != null && source.fontSharedMaterial != null
            ? source.fontSharedMaterial
            : sharedFont != null ? sharedFont.material : null;

        if (sharedFont == null)
            return;

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || label.gameObject.scene != activeScene)
                continue;

            label.font = sharedFont;
            if (sharedFontMaterial != null)
                label.fontSharedMaterial = sharedFontMaterial;
        }
    }

    private void CreateOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2000;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayGroup = gameObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 1f;
        overlayGroup.interactable = true;
        overlayGroup.blocksRaycasts = true;

        StretchToParent((RectTransform)transform);

        GameObject backgroundObject = new GameObject(
            "DesertArenaTitleBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        backgroundObject.transform.SetParent(transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        StretchToParent(backgroundRect);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.018f, 0.024f, 0.027f, 0.84f);
        background.raycastTarget = true;

        GameObject titleObject = new GameObject(
            "DesertArenaTitle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup),
            typeof(Outline));
        titleObject.transform.SetParent(transform, false);

        titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(1500f, 220f);
        titleRect.localScale = Vector3.one;
        titleBasePosition = titleRect.anchoredPosition;
        titleBaseScale = titleRect.localScale;

        TextMeshProUGUI titleLabel = titleObject.GetComponent<TextMeshProUGUI>();
        titleLabel.text = string.IsNullOrWhiteSpace(title)
            ? "DESERT ARENA"
            : title.ToUpperInvariant();
        titleLabel.font = sharedFont != null ? sharedFont : TMP_Settings.defaultFontAsset;
        if (sharedFontMaterial != null)
            titleLabel.fontSharedMaterial = sharedFontMaterial;
        titleLabel.fontSize = 112f;
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 48f;
        titleLabel.fontSizeMax = 112f;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;
        titleLabel.characterSpacing = 4f;
        titleLabel.color = new Color(0.96f, 0.82f, 0.42f, 1f);
        titleLabel.raycastTarget = false;

        Outline titleOutline = titleObject.GetComponent<Outline>();
        titleOutline.effectColor = new Color(0.01f, 0.012f, 0.014f, 0.95f);
        titleOutline.effectDistance = new Vector2(5f, -5f);
        titleOutline.useGraphicAlpha = true;

        titleGroup = titleObject.GetComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.interactable = false;
        titleGroup.blocksRaycasts = false;
        titleRect.localScale = titleBaseScale * Mathf.Max(0.1f, startScale);
        titleRect.anchoredPosition = titleBasePosition + Vector2.up * startOffset;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        return value * value * value;
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = value - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }
}
