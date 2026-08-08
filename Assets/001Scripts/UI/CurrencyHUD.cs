using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private PlayerCurrency currency;
    [SerializeField] private Sprite coinIcon;

    private TMP_Text amountText;

    private void Start()
    {
        if (currency == null)
            currency = GetComponent<PlayerCurrency>();
        if (currency == null)
            currency = PlayerCurrency.Instance;

        BuildView();

        if (currency != null)
        {
            currency.CoinsChanged += Refresh;
            Refresh(currency.Coins);
        }
    }

    private void OnDestroy()
    {
        if (currency != null)
            currency.CoinsChanged -= Refresh;
    }

    private void BuildView()
    {
        if (amountText != null)
            return;

        GameObject canvasObject = new GameObject(
            "CurrencyHUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = CreateUIObject("GoldCounter", canvasObject.transform);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.04f, 0.045f, 0.055f, 0.92f);
        panel.raycastTarget = false;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = new Vector2(-32f, -30f);
        panelRect.sizeDelta = new Vector2(184f, 58f);

        GameObject accentObject = CreateUIObject("GoldAccent", panelObject.transform);
        Image accent = accentObject.AddComponent<Image>();
        accent.color = new Color(1f, 0.72f, 0.08f, 1f);
        accent.raycastTarget = false;
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(6f, 0f);

        GameObject iconObject = CreateUIObject("CoinIcon", panelObject.transform);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = coinIcon;
        icon.color = coinIcon != null ? Color.white : new Color(1f, 0.75f, 0.08f, 1f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(40f, 0f);
        iconRect.sizeDelta = new Vector2(44f, 44f);

        GameObject textObject = CreateUIObject("CoinAmount", panelObject.transform);
        amountText = textObject.AddComponent<TextMeshProUGUI>();
        amountText.font = TMP_Settings.defaultFontAsset;
        amountText.fontSize = 29f;
        amountText.fontStyle = FontStyles.Bold;
        amountText.color = Color.white;
        amountText.alignment = TextAlignmentOptions.MidlineLeft;
        amountText.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(68f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);
    }

    private void Refresh(int coins)
    {
        if (amountText != null)
            amountText.text = coins.ToString();
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }
}
