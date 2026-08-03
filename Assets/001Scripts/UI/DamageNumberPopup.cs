using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays pooled, screen-space damage numbers above enemies.
/// Screen-space rendering keeps the text crisp and readable at every camera distance.
/// </summary>
public sealed class DamageNumberPopup : MonoBehaviour
{
    private const int InitialPoolSize = 48;
    private const int MaximumPoolSize = 128;
    private const int CanvasSortingOrder = 150;
    private const string SharedUiFontName = "SVN-Determination Sans SDF";
    private const float Lifetime = 0.85f;
    private const float FadeStart = 0.58f;
    private const float RiseDistance = 95f;

    private static readonly Color32 DamageColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 OutlineColor = new Color32(0, 0, 0, 255);

    private static DamageNumberPopup instance;

    private readonly Queue<Popup> available = new Queue<Popup>(InitialPoolSize);
    private readonly List<Popup> active = new List<Popup>(MaximumPoolSize);

    private RectTransform canvasRect;
    private TMP_FontAsset sharedUiFont;
    private Camera worldCamera;
    private int createdCount;

    private sealed class Popup
    {
        public RectTransform rectTransform;
        public TextMeshProUGUI label;
        public Vector3 worldPosition;
        public float age;
        public float horizontalDrift;
        public float verticalJitter;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    /// <summary>
    /// Shows the final damage actually applied after armor reduction.
    /// </summary>
    public static void Show(float damage, Vector3 worldPosition)
    {
        if (damage <= 0f)
            return;

        EnsureInstance().ShowInternal(damage, worldPosition);
    }

    private static DamageNumberPopup EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject canvasObject = new GameObject(
            "Damage Number Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(DamageNumberPopup));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the regular HUD, but below upgrade (200), boss (450), and game-over UI.
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        instance = canvasObject.GetComponent<DamageNumberPopup>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        canvasRect = (RectTransform)transform;
        sharedUiFont = ResolveSharedUiFont();
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < InitialPoolSize; i++)
            available.Enqueue(CreatePopup());
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private Popup CreatePopup()
    {
        GameObject labelObject = new GameObject(
            "Damage Number",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.SetParent(canvasRect, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(220f, 72f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (sharedUiFont != null)
            label.font = sharedUiFont;

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 50f;
        label.fontStyle = FontStyles.Bold;
        label.color = DamageColor;
        label.outlineColor = OutlineColor;
        label.outlineWidth = 0.4f;
        label.extraPadding = true;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        labelObject.SetActive(false);
        createdCount++;

        return new Popup
        {
            rectTransform = rectTransform,
            label = label
        };
    }

    private static TMP_FontAsset ResolveSharedUiFont()
    {
        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            TMP_FontAsset font = loadedFonts[i];
            if (font != null && font.name == SharedUiFontName)
                return font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void ShowInternal(float damage, Vector3 worldPosition)
    {
        Popup popup = GetPopup();
        popup.worldPosition = worldPosition;
        popup.age = 0f;
        popup.horizontalDrift = Random.Range(-36f, 36f);
        popup.verticalJitter = Random.Range(-4f, 10f);

        popup.label.SetText("{0:0}", damage);
        popup.label.alpha = 1f;
        popup.rectTransform.localScale = Vector3.one * 0.65f;
        popup.rectTransform.gameObject.SetActive(true);
        active.Add(popup);
    }

    private Popup GetPopup()
    {
        if (available.Count > 0)
            return available.Dequeue();

        if (createdCount < MaximumPoolSize)
            return CreatePopup();

        int oldestIndex = 0;
        float oldestAge = active[0].age;

        for (int i = 1; i < active.Count; i++)
        {
            if (active[i].age <= oldestAge)
                continue;

            oldestAge = active[i].age;
            oldestIndex = i;
        }

        Popup recycled = active[oldestIndex];
        active.RemoveAt(oldestIndex);
        return recycled;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f || active.Count == 0)
            return;

        if (worldCamera == null || !worldCamera.isActiveAndEnabled)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup popup = active[i];
            popup.age += deltaTime;

            if (popup.age >= Lifetime)
            {
                RecycleAt(i);
                continue;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(popup.worldPosition);
            if (screenPosition.z <= 0f)
            {
                RecycleAt(i);
                continue;
            }

            float normalizedAge = popup.age / Lifetime;
            float easedRise = 1f - Mathf.Pow(1f - normalizedAge, 3f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out Vector2 localPoint);

            localPoint.x += popup.horizontalDrift * Mathf.Sin(normalizedAge * Mathf.PI * 0.5f);
            localPoint.y += popup.verticalJitter + RiseDistance * easedRise;
            popup.rectTransform.anchoredPosition = localPoint;

            popup.rectTransform.localScale = Vector3.one * EvaluateScale(normalizedAge);
            popup.label.alpha = normalizedAge < FadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(FadeStart, 1f, normalizedAge);
        }
    }

    private static float EvaluateScale(float normalizedAge)
    {
        if (normalizedAge < 0.12f)
            return Mathf.Lerp(0.65f, 1.18f, normalizedAge / 0.12f);

        if (normalizedAge < 0.32f)
            return Mathf.Lerp(1.18f, 1f, (normalizedAge - 0.12f) / 0.2f);

        return 1f;
    }

    private void RecycleAt(int index)
    {
        Popup popup = active[index];
        int lastIndex = active.Count - 1;
        active[index] = active[lastIndex];
        active.RemoveAt(lastIndex);

        popup.rectTransform.gameObject.SetActive(false);
        available.Enqueue(popup);
    }
}
