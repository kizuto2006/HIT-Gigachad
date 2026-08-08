using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Keeps an item icon above a translucent HUD slot background.
/// Assign Icon in the Inspector or call SetIcon at runtime.
/// </summary>
[ExecuteAlways]
public class HUDItemSlot : MonoBehaviour
{
    [SerializeField] private Sprite icon;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    private TMP_FontAsset levelFont;
    private Material levelFontMaterial;

    [SerializeField, Min(0)] private int level;
    [SerializeField] private bool showStackCount;
    private bool isValidating;

    public Sprite Icon => icon;

    public void Configure(Image targetImage)
    {
        iconImage = targetImage;
        ResolveIconImage();
        ApplyIcon();
    }

    public void SetLevelFont(TMP_FontAsset font, Material material = null)
    {
        levelFont = font != null ? font : TMP_Settings.defaultFontAsset;
        levelFontMaterial = material != null
            ? material
            : levelFont != null ? levelFont.material : null;

        if (levelText != null)
            ApplyLevelFont();
    }

    public void SetIcon(Sprite newIcon)
    {
        icon = newIcon;
        ResolveIconImage();
        ApplyIcon();
    }

    public void SetLevel(int newLevel)
    {
        showStackCount = false;
        level = Mathf.Max(0, newLevel);
        EnsureLevelText();
        ApplyLevel();
    }

    public void SetStackCount(int newStackCount)
    {
        showStackCount = true;
        level = Mathf.Max(0, newStackCount);
        EnsureLevelText();
        ApplyLevel();
    }


    public void ClearIcon()
    {
        SetIcon(null);
    }

    private void OnValidate()
    {
        isValidating = true;

        ResolveIconImage();

        if (levelText == null)
        {
            Transform child = transform.Find("LevelText");
            if (child != null)
                levelText = child.GetComponent<TMP_Text>();
        }

        ApplyIcon();
        ApplyLevel();
        isValidating = false;
    }

    private void ResolveIconImage()
    {
        if (iconImage != null)
            return;

        Transform child = transform.Find("ItemIcon");
        if (child != null)
            iconImage = child.GetComponent<Image>();

        if (iconImage == null)
        {
            Image[] childImages = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i].gameObject.name == "ItemIcon")
                {
                    iconImage = childImages[i];
                    break;

                }
            }
        }
        if (iconImage != null || !Application.isPlaying || isValidating)
            return;

        GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = new Vector2(8f, 12f);
        iconRect.offsetMax = new Vector2(-8f, -8f);

        iconImage = iconObject.GetComponent<Image>();
    }

    private void ApplyIcon()
    {
        ResolveIconImage();
        if (iconImage == null) return;

        iconImage.sprite = icon;
        iconImage.color = Color.white;
        iconImage.enabled = icon != null;
        iconImage.gameObject.SetActive(icon != null);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.transform.SetAsFirstSibling();
        if (levelText != null)
            levelText.transform.SetAsLastSibling();
    }

    private void EnsureLevelText()
    {
        if (levelText == null)
        {
            Transform existing = transform.Find("LevelText");
            if (existing != null)
                levelText = existing.GetComponent<TMP_Text>();
        }

        if (levelText == null)
        {
            GameObject labelObject = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            labelObject.transform.SetParent(transform, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 2f);
            rect.sizeDelta = new Vector2(0f, 24f);

            levelText = labelObject.GetComponent<TextMeshProUGUI>();
            levelText.alignment = TextAlignmentOptions.Center;
            levelText.fontSize = 18f;
            levelText.fontStyle = FontStyles.Normal;
            levelText.color = Color.white;
            levelText.textWrappingMode = TextWrappingModes.NoWrap;
            levelText.raycastTarget = false;

            Shadow shadow = labelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        if (levelFont == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);
                if (sample != null)
                {
                    levelFont = sample.font;
                    levelFontMaterial = sample.fontSharedMaterial != null
                        ? sample.fontSharedMaterial
                        : levelFont != null ? levelFont.material : null;
                }
            }
        }

        if (levelFont == null)
            levelFont = TMP_Settings.defaultFontAsset;
        if (levelFontMaterial == null && levelFont != null)
            levelFontMaterial = levelFont.material;

        ApplyLevelFont();
    }

    private void ApplyLevel()
    {
        if (levelText == null)
            return;

        bool visible = icon != null && level > 0;
        levelText.text = visible
            ? showStackCount ? $"x{level}" : $"LEVEL {level}"
            : string.Empty;
        levelText.gameObject.SetActive(visible);
        if (visible)
            levelText.transform.SetAsLastSibling();
    }


    private void ApplyLevelFont()
    {
        if (levelText == null)
            return;

        if (levelFont != null)
            levelText.font = levelFont;
        if (levelFontMaterial != null)
            levelText.fontSharedMaterial = levelFontMaterial;
    }
}
