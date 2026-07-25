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
    [SerializeField, Min(0)] private int level;

    public Sprite Icon => icon;

    public void Configure(Image targetImage)
    {
        iconImage = targetImage;
        ApplyIcon();
    }

    public void SetIcon(Sprite newIcon)
    {
        icon = newIcon;
        ApplyIcon();
    }

    public void SetLevel(int newLevel)
    {
        level = Mathf.Max(0, newLevel);
        EnsureLevelText();
        ApplyLevel();
    }

    public void ClearIcon()
    {
        SetIcon(null);
    }

    private void OnValidate()
    {
        if (iconImage == null)
        {
            Transform child = transform.Find("ItemIcon");
            if (child != null)
                iconImage = child.GetComponent<Image>();
        }

        if (levelText == null)
        {
            Transform child = transform.Find("LevelText");
            if (child != null)
                levelText = child.GetComponent<TMP_Text>();
        }

        ApplyIcon();
        ApplyLevel();
    }

    private void ApplyIcon()
    {
        if (iconImage == null) return;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.transform.SetAsFirstSibling();
        if (levelText != null)
            levelText.transform.SetAsLastSibling();
    }

    private void EnsureLevelText()
    {
        if (levelText != null)
            return;

        Transform existing = transform.Find("LevelText");
        if (existing != null)
            levelText = existing.GetComponent<TMP_Text>();

        if (levelText != null)
            return;

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

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);
            if (sample != null)
                levelText.font = sample.font;
        }

        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void ApplyLevel()
    {
        if (levelText == null)
            return;

        bool visible = icon != null && level > 0;
        levelText.text = visible ? $"LEVEL {level}" : string.Empty;
        levelText.gameObject.SetActive(visible);
        if (visible)
            levelText.transform.SetAsLastSibling();
    }
}
