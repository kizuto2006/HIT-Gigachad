using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PowerupHUD : MonoBehaviour
{
    [SerializeField] private PlayerPowerupController playerPowerups;
    [SerializeField] private RectTransform panel;
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private bool showPowerupStatus = false;

    private readonly List<PowerupHUDSlot> slots = new List<PowerupHUDSlot>(5);
    private RectTransform content;
    private float refreshTimer;

    private void Awake()
    {
        if (!showPowerupStatus)
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            return;
        }

        ResolvePlayer();
        BuildPanel();
    }

    private void OnEnable()
    {
        if (!showPowerupStatus)
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            return;
        }

        ResolvePlayer();
        if (playerPowerups != null)
            playerPowerups.PowerupsChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (playerPowerups != null)
            playerPowerups.PowerupsChanged -= Refresh;
    }

    private void Update()
    {
        if (!showPowerupStatus)
            return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = 0.08f;
        Refresh();
    }

    private void ResolvePlayer()
    {
        if (playerPowerups == null)
            playerPowerups = FindFirstObjectByType<PlayerPowerupController>();
    }

    private void BuildPanel()
    {
        if (panel == null)
        {
            GameObject panelObject = new GameObject(
                "PowerupStatusPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup));

            panelObject.transform.SetParent(transform, false);
            panel = panelObject.GetComponent<RectTransform>();

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.05f, 0.78f);
            background.raycastTarget = false;

            HorizontalLayoutGroup layout = panelObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -112f);
        panel.sizeDelta = new Vector2(420f, 76f);
        panel.gameObject.SetActive(false);

        if (content == null)
            content = panel;
    }

    private PowerupHUDSlot CreateSlot(int index)
    {
        GameObject slotObject = new GameObject(
            "PowerupSlot_" + (index + 1).ToString("00"),
            typeof(RectTransform),
            typeof(Image),
            typeof(PowerupHUDSlot));

        slotObject.transform.SetParent(content, false);

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(64f, 64f);

        Image background = slotObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.055f, 0.09f, 0.96f);
        background.raycastTarget = false;

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5f, 8f);
        iconRect.offsetMax = new Vector2(-5f, -5f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Duration",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(slotObject.transform, false);
        fillObject.SetActive(false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(-8f, 6f);

        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;

        GameObject timerObject = new GameObject(
            "Timer",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        timerObject.transform.SetParent(slotObject.transform, false);
        timerObject.SetActive(false);
        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 1f);
        timerRect.anchorMax = Vector2.one;
        timerRect.offsetMin = new Vector2(3f, -22f);
        timerRect.offsetMax = new Vector2(-3f, -3f);

        TextMeshProUGUI timer = timerObject.GetComponent<TextMeshProUGUI>();
        timer.alignment = TextAlignmentOptions.TopRight;
        timer.fontSize = 12f;
        timer.fontStyle = FontStyles.Bold;
        timer.color = Color.white;
        timer.raycastTarget = false;

        GameObject chargesObject = new GameObject(
            "Charges",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        chargesObject.transform.SetParent(slotObject.transform, false);
        RectTransform chargesRect = chargesObject.GetComponent<RectTransform>();
        chargesRect.anchorMin = Vector2.zero;
        chargesRect.anchorMax = new Vector2(0.5f, 0.5f);
        chargesRect.offsetMin = new Vector2(5f, 5f);
        chargesRect.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI charges = chargesObject.GetComponent<TextMeshProUGUI>();
        charges.alignment = TextAlignmentOptions.BottomLeft;
        charges.fontSize = 12f;
        charges.fontStyle = FontStyles.Bold;
        charges.color = Color.white;
        charges.raycastTarget = false;

        PowerupHUDSlot slot = slotObject.GetComponent<PowerupHUDSlot>();
        slot.Configure(icon, fill, timer, charges, background);
        return slot;
    }

    private void Refresh()
    {
        if (!showPowerupStatus)
        {
            if (panel != null)
                panel.gameObject.SetActive(false);
            return;
        }

        ResolvePlayer();
        if (playerPowerups == null || panel == null)
            return;

        IReadOnlyList<PowerupRuntimeState> active = playerPowerups.ActivePowerups;
        int visibleCount = Mathf.Min(active.Count, Mathf.Max(1, maxSlots));

        while (slots.Count < visibleCount)
            slots.Add(CreateSlot(slots.Count));

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < visibleCount)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].SetState(active[i]);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        panel.gameObject.SetActive(visibleCount > 0);
    }
}

public sealed class PowerupHUDSlot : MonoBehaviour
{
    private Image icon;
    private Image durationFill;
    private TMP_Text timer;
    private TMP_Text charges;
    private Image background;

    public void Configure(
        Image iconImage,
        Image fillImage,
        TMP_Text timerText,
        TMP_Text chargesText,
        Image backgroundImage)
    {
        icon = iconImage;
        durationFill = fillImage;
        timer = timerText;
        charges = chargesText;
        background = backgroundImage;
    }

    public void SetState(PowerupRuntimeState state)
    {
        if (state == null || state.data == null)
            return;

        Color tint = state.data.tint;
        tint.a = 1f;

        if (icon != null)
        {
            icon.sprite = state.data.icon;
            icon.color = Color.white;
        }

        if (background != null)
            background.color = new Color(tint.r * 0.28f, tint.g * 0.28f, tint.b * 0.28f, 0.96f);

        if (durationFill != null)
        {
            durationFill.color = tint;
            durationFill.fillAmount = state.data.duration > 0f
                ? state.NormalizedTime
                : 1f;
            durationFill.gameObject.SetActive(false);
        }

        if (timer != null)
        {
            timer.gameObject.SetActive(false);
            timer.text = state.data.duration > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, state.remainingDuration)) + "s"
                : string.Empty;
        }

        if (charges != null)
        {
            charges.text = state.data.powerupType == PowerupType.Shield
                ? "x" + Mathf.Max(0, state.charges)
                : string.Empty;
        }
    }
}
