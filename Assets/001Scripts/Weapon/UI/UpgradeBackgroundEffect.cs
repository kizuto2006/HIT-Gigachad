using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and animates the warm radial burst shown behind the level-up choices.
/// The effect uses unscaled time because the upgrade screen pauses gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class UpgradeBackgroundEffect : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color32(5, 18, 18, 92);
    private static readonly Color RayColor = new Color32(255, 239, 166, 214);

    private RectTransform effectRoot;
    private RectTransform burstTransform;
    private CanvasGroup canvasGroup;
    private Image backdropImage;
    private Coroutine animationRoutine;

    public void Configure(Transform dimmedBackground)
    {
        if (dimmedBackground == null)
            return;

        backdropImage = dimmedBackground.GetComponent<Image>();

        Transform existing = dimmedBackground.Find("LevelUpBackgroundEffect");
        if (existing != null)
        {
            effectRoot = existing as RectTransform;
            burstTransform = existing.Find("Sunburst") as RectTransform;
            canvasGroup = existing.GetComponent<CanvasGroup>();
            return;
        }

        GameObject rootObject = new GameObject(
            "LevelUpBackgroundEffect",
            typeof(RectTransform),
            typeof(CanvasGroup));
        effectRoot = rootObject.GetComponent<RectTransform>();
        effectRoot.SetParent(dimmedBackground, false);
        effectRoot.anchorMin = Vector2.zero;
        effectRoot.anchorMax = Vector2.one;
        effectRoot.offsetMin = Vector2.zero;
        effectRoot.offsetMax = Vector2.zero;
        effectRoot.SetAsFirstSibling();

        canvasGroup = rootObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject burstObject = new GameObject(
            "Sunburst",
            typeof(RectTransform),
            typeof(UpgradeSunburstGraphic));
        burstTransform = burstObject.GetComponent<RectTransform>();
        burstTransform.SetParent(effectRoot, false);
        burstTransform.anchorMin = new Vector2(0.5f, 0.5f);
        burstTransform.anchorMax = new Vector2(0.5f, 0.5f);
        burstTransform.pivot = new Vector2(0.5f, 0.5f);
        burstTransform.anchoredPosition = Vector2.zero;
        burstTransform.sizeDelta = new Vector2(2800f, 2800f);
        burstTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);

        UpgradeSunburstGraphic burst = burstObject.GetComponent<UpgradeSunburstGraphic>();
        burst.color = RayColor;
        burst.raycastTarget = false;

        rootObject.SetActive(false);
    }

    public void Play()
    {
        if (effectRoot == null)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        if (backdropImage != null)
            backdropImage.color = BackdropColor;

        effectRoot.gameObject.SetActive(true);
        animationRoutine = StartCoroutine(AnimateOpen());
    }

    public void Hide()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = null;

        if (effectRoot != null)
            effectRoot.gameObject.SetActive(false);
    }

    private IEnumerator AnimateOpen()
    {
        const float duration = 0.28f;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        burstTransform.localScale = Vector3.one * 0.42f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Clamp01(progress / 0.5f);

            float scaleProgress = EaseOutBack(progress);
            float scale = Mathf.LerpUnclamped(0.42f, 1f, scaleProgress);
            burstTransform.localScale = new Vector3(scale, scale, 1f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        burstTransform.localScale = Vector3.one;
        animationRoutine = null;
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.35f;
        float shifted = value - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted
            + overshoot * shifted * shifted;
    }
}

/// <summary>
/// Procedural alternating triangular rays, avoiding a texture dependency.
/// </summary>
public sealed class UpgradeSunburstGraphic : MaskableGraphic
{
    [SerializeField, Range(8, 32)] private int rayCount = 18;
    [SerializeField, Range(0.15f, 0.9f)] private float rayFill = 0.5f;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f;
        float sector = Mathf.PI * 2f / rayCount;
        float halfRay = sector * rayFill * 0.5f;
        Color32 vertexColor = color;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * sector;
            Vector2 left = center + new Vector2(
                Mathf.Cos(angle - halfRay),
                Mathf.Sin(angle - halfRay)) * radius;
            Vector2 right = center + new Vector2(
                Mathf.Cos(angle + halfRay),
                Mathf.Sin(angle + halfRay)) * radius;

            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, vertexColor, new Vector2(0.5f, 0.5f));
            vertexHelper.AddVert(left, vertexColor, Vector2.zero);
            vertexHelper.AddVert(right, vertexColor, Vector2.one);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
        }
    }
}
