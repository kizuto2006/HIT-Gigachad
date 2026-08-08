using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inventory and live player stats shown only as part of the upgrade screen.
/// The inventory rows mirror the player's current weapons, tomes, and items.
/// </summary>
public sealed class UpgradeInventoryStatsView : MonoBehaviour
{
    private const int EmptyItemSlotCount = 4;
    private const int ItemSlotsPerRow = 4;
    private const float ItemSlotSize = 76f;
    private const float ItemSlotSpacing = 12f;
    // Exact palette sampled from WeaponUpgradePanel.prefab.
    private static readonly Color PanelColor = new Color32(31, 31, 31, 255);
    private static readonly Color SectionColor = new Color32(31, 31, 31, 255);
    private static readonly Color BorderColor = new Color32(217, 211, 190, 255);
    private static readonly Color HeaderColor = new Color32(39, 77, 64, 255);
    private static readonly Color HeaderHighlightColor = new Color32(88, 173, 114, 255);
    private static readonly Color SlotColor = new Color32(57, 62, 61, 255);
    private static readonly Color EmptySlotColor = new Color32(43, 47, 46, 255);
    private static readonly Color LabelColor = new Color32(226, 207, 101, 255);
    private static readonly Color ValueColor = Color.white;

    private WeaponInventory weaponInventory;
    private PlayerTomeInventory tomeInventory;
    private PlayerItemInventory itemInventory;

    private PlayerBaseStats playerStats;
    private TMP_FontAsset displayFont;
    private Material displayFontMaterial;
    private HUDItemSlot[] weaponSlots;
    private HUDItemSlot[] tomeSlots;
    private HUDItemSlot[] itemSlots;
    private RectTransform itemSection;

    private RectTransform inventoryPanel;
    private RectTransform statsPanel;
    private readonly List<TMP_Text> statValues = new List<TMP_Text>();
    private Coroutine openAnimation;
    private bool built;
    private bool subscribed;

    public void Configure(WeaponInventory weapons, PlayerTomeInventory tomes,
        PlayerItemInventory items, PlayerBaseStats stats, TMP_FontAsset font)
    {
        Unsubscribe();
        weaponInventory = weapons;
        tomeInventory = tomes;
        itemInventory = items;
        playerStats = stats;
        displayFont = font != null ? font : TMP_Settings.defaultFontAsset;
        displayFontMaterial = displayFont != null ? displayFont.material : null;

        if (!built)
            Build();
        Subscribe();
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshWeapons();
        RefreshTomes();
        RefreshItems();
        RefreshStats();
    }

    public void PlayOpenAnimation()
    {
        if (!isActiveAndEnabled || inventoryPanel == null || statsPanel == null)
            return;
        if (openAnimation != null)
            StopCoroutine(openAnimation);
        openAnimation = StartCoroutine(AnimateOpen());
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (openAnimation != null)
            StopCoroutine(openAnimation);
        openAnimation = null;
        SetPanelScale(inventoryPanel, 1f);
        SetPanelScale(statsPanel, 1f);
    }

    private void Subscribe()
    {
        if (subscribed || !isActiveAndEnabled)
            return;
        if (weaponInventory != null)
            weaponInventory.WeaponsChanged += RefreshWeapons;
        if (tomeInventory != null)
            tomeInventory.TomesChanged += HandleTomesChanged;
        if (itemInventory != null)
            itemInventory.ItemsChanged += HandleItemsChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;
        if (weaponInventory != null)
            weaponInventory.WeaponsChanged -= RefreshWeapons;
        if (tomeInventory != null)
            tomeInventory.TomesChanged -= HandleTomesChanged;
        if (itemInventory != null)
            itemInventory.ItemsChanged -= HandleItemsChanged;
        subscribed = false;
    }

    private void HandleTomesChanged()
    {
        RefreshTomes();
        RefreshStats();
    }

    private void HandleItemsChanged()
    {
        RefreshItems();
        RefreshStats();
    }


    private void Build()
    {
        built = true;
        inventoryPanel = CreatePanel("UpgradeInventory", transform, PanelColor);
        SetSidePanel(inventoryPanel, false, new Vector2(420f, 520f));
        BuildInventory(inventoryPanel);

        statsPanel = CreatePanel("UpgradeStats", transform, PanelColor);
        SetSidePanel(statsPanel, true, new Vector2(360f, 520f));
        BuildStats(statsPanel);
    }

    private IEnumerator AnimateOpen()
    {
        const float startScale = 0.68f;
        const float duration = 0.28f;
        const float statsDelay = 0.06f;
        float elapsed = 0f;
        SetPanelScale(inventoryPanel, startScale);
        SetPanelScale(statsPanel, startScale);

        while (elapsed < duration + statsDelay)
        {
            float inventoryProgress = Mathf.Clamp01(elapsed / duration);
            float statsProgress = Mathf.Clamp01((elapsed - statsDelay) / duration);
            SetPanelScale(inventoryPanel, Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(inventoryProgress)));
            SetPanelScale(statsPanel, Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(statsProgress)));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetPanelScale(inventoryPanel, 1f);
        SetPanelScale(statsPanel, 1f);
        openAnimation = null;
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.70158f;
        float shifted = value - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted
            + overshoot * shifted * shifted;
    }

    private static void SetPanelScale(RectTransform panel, float scale)
    {
        if (panel != null)
            panel.localScale = new Vector3(scale, scale, 1f);
    }

    private void BuildInventory(Transform parent)
    {
        CreateHeader("INVENTORY", parent, new Vector2(18f, -14f), new Vector2(384f, 38f), 26f);
        weaponSlots = CreateSlotRow("Weapons", "WEAPONS", parent, 65f,
            Mathf.Max(1, weaponInventory != null ? weaponInventory.MaxSlots : 2));
        tomeSlots = CreateSlotRow("Tomes", "TOMES", parent, 205f,
            Mathf.Max(1, tomeInventory != null ? tomeInventory.MaxSlots : 2));
        itemSlots = CreateSlotRow("Items", "ITEMS", parent, 345f, EmptyItemSlotCount, true);
    }

    private HUDItemSlot[] CreateSlotRow(string objectName, string title, Transform parent,
        float y, int slotCount, bool forceEmpty = false)
    {
        RectTransform section = CreatePanel(objectName, parent, SectionColor);
        int rowCount = forceEmpty ? GetItemRowCount(slotCount) : 1;
        float sectionHeight = forceEmpty ? GetItemSectionHeight(rowCount) : 126f;
        SetTopLeft(section, new Vector2(14f, -y), new Vector2(392f, sectionHeight));
        CreateHeader(title, section, new Vector2(12f, -7f), new Vector2(368f, 25f), 17f);

        if (forceEmpty)
            itemSection = section;

        HUDItemSlot[] slots = new HUDItemSlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            slots[i] = CreateInventorySlot(section, i, forceEmpty);

        if (forceEmpty)
            UpdateInventoryPanelHeight(rowCount);

        return slots;
    }

    private HUDItemSlot CreateInventorySlot(RectTransform section, int index, bool forceEmpty)
    {
        int column = forceEmpty ? index % ItemSlotsPerRow : index;
        int row = forceEmpty ? index / ItemSlotsPerRow : 0;

        RectTransform border = CreatePanel("Slot_" + (index + 1).ToString("00"), section, BorderColor);
        SetTopLeft(border,
            new Vector2(12f + column * (ItemSlotSize + ItemSlotSpacing),
                -(38f + row * (ItemSlotSize + ItemSlotSpacing))),
            new Vector2(ItemSlotSize, ItemSlotSize));

        RectTransform background = CreatePanel("SlotBackground", border,
            forceEmpty ? EmptySlotColor : SlotColor, false);
        SetStretch(background, 3f);

        RectTransform iconRect = CreateUIObject("ItemIcon", background);
        SetStretch(iconRect, 7f, 7f, 7f, 21f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.enabled = false;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        HUDItemSlot slot = border.gameObject.AddComponent<HUDItemSlot>();
        slot.Configure(icon);
        slot.SetLevelFont(displayFont, displayFontMaterial);

        slot.SetLevel(0);
        return slot;
    }

    private void EnsureItemSlotCount(int requiredCount)
    {
        if (itemSection == null || itemSlots == null)
            return;

        requiredCount = Mathf.Max(EmptyItemSlotCount, requiredCount);
        if (itemSlots.Length >= requiredCount)
            return;

        HUDItemSlot[] expandedSlots = new HUDItemSlot[requiredCount];
        for (int i = 0; i < itemSlots.Length; i++)
            expandedSlots[i] = itemSlots[i];

        for (int i = itemSlots.Length; i < requiredCount; i++)
            expandedSlots[i] = CreateInventorySlot(itemSection, i, true);

        itemSlots = expandedSlots;
    }

    private void UpdateItemGridLayout(int slotCount)
    {
        if (itemSection == null || itemSlots == null)
            return;

        int rowCount = GetItemRowCount(slotCount);
        SetTopLeft(itemSection, new Vector2(14f, -345f),
            new Vector2(392f, GetItemSectionHeight(rowCount)));

        for (int i = 0; i < itemSlots.Length; i++)
        {
            int column = i % ItemSlotsPerRow;
            int row = i / ItemSlotsPerRow;
            SetTopLeft(itemSlots[i].GetComponent<RectTransform>(),
                new Vector2(12f + column * (ItemSlotSize + ItemSlotSpacing),
                    -(38f + row * (ItemSlotSize + ItemSlotSpacing))),
                new Vector2(ItemSlotSize, ItemSlotSize));
        }

        UpdateInventoryPanelHeight(rowCount);
    }

    private void UpdateInventoryPanelHeight(int itemRowCount)
    {
        if (inventoryPanel == null)
            return;

        float requiredHeight = 345f + GetItemSectionHeight(itemRowCount) + 14f;
        Vector2 size = inventoryPanel.sizeDelta;
        size.y = Mathf.Max(520f, requiredHeight);
        inventoryPanel.sizeDelta = size;
    }

    private static int GetItemRowCount(int slotCount)
    {
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, slotCount) / (float)ItemSlotsPerRow));
    }

    private static float GetItemSectionHeight(int rowCount)
    {
        return 126f + Mathf.Max(0, rowCount - 1) * (ItemSlotSize + ItemSlotSpacing);
    }

    private void RefreshWeapons()
    {
        if (weaponSlots == null)
            return;
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            WeaponBehaviour weapon = weaponInventory != null
                ? weaponInventory.GetWeaponAtSlot(i)
                : null;
            weaponSlots[i].SetIcon(weapon != null && weapon.data != null ? weapon.data.icon : null);
            weaponSlots[i].SetLevel(weapon != null ? weapon.CurrentLevel : 0);
        }
    }

    private void RefreshTomes()
    {
        if (tomeSlots == null)
            return;
        for (int i = 0; i < tomeSlots.Length; i++)
        {
            TomeLevelState state = tomeInventory != null && i < tomeInventory.OwnedTomes.Count
                ? tomeInventory.OwnedTomes[i]
                : null;
            TomeData tome = state != null ? state.tome : null;
            tomeSlots[i].SetIcon(tome != null ? tome.icon : null);
            tomeSlots[i].SetLevel(tome != null ? state.level : 0);
        }
    }

    private void RefreshItems()
    {
        if (itemSlots == null)
            return;

        IReadOnlyList<ItemStackState> ownedItems = itemInventory != null
            ? itemInventory.OwnedItems
            : null;

        int ownedCount = 0;
        if (ownedItems != null)
        {
            for (int i = 0; i < ownedItems.Count; i++)
            {
                ItemStackState state = ownedItems[i];
                if (state != null && state.item != null && state.stackCount > 0)
                    ownedCount++;
            }
        }

        int visibleSlotCount = Mathf.Max(EmptyItemSlotCount, ownedCount);
        EnsureItemSlotCount(visibleSlotCount);
        UpdateItemGridLayout(visibleSlotCount);

        int ownedIndex = 0;
        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (i >= visibleSlotCount)
            {
                itemSlots[i].SetIcon(null);
                itemSlots[i].SetStackCount(0);
                itemSlots[i].gameObject.SetActive(false);
                continue;
            }

            ItemData item = null;
            int stackCount = 0;
            while (ownedItems != null && ownedIndex < ownedItems.Count)
            {
                ItemStackState state = ownedItems[ownedIndex++];
                if (state == null || state.item == null || state.stackCount <= 0)
                    continue;

                item = state.item;
                stackCount = state.stackCount;
                break;
            }

            itemSlots[i].gameObject.SetActive(true);
            itemSlots[i].SetIcon(item != null ? item.icon : null);
            itemSlots[i].SetStackCount(item != null ? stackCount : 0);
        }
    }


    private void BuildStats(Transform parent)
    {
        statValues.Clear();
        CreateHeader("STATS", parent, new Vector2(18f, -14f), new Vector2(324f, 38f), 26f);
        string[] labels =
        {
            "MAX HEALTH", "DAMAGE", "ARMOR", "MOVE SPEED", "ATTACK SPEED",
            "CRIT CHANCE", "CRIT DAMAGE", "PROJECTILES", "WEAPON SIZE",
            "PROJECTILE SPEED", "DURATION", "KNOCKBACK", "PICKUP RANGE", "XP GAIN"
        };

        RectTransform list = CreatePanel("StatsList", parent, SectionColor);
        SetTopLeft(list, new Vector2(14f, -65f), new Vector2(332f, 440f));
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 11f + i * 30.3f;
            CreateText(labels[i], list, new Vector2(12f, -y), new Vector2(216f, 26f),
                15f, TextAlignmentOptions.MidlineLeft, LabelColor);
            TMP_Text value = CreateText("N/A", list, new Vector2(226f, -y), new Vector2(94f, 26f),
                16f, TextAlignmentOptions.MidlineRight, ValueColor);
            statValues.Add(value);
        }
    }

    private void RefreshStats()
    {
        if (playerStats == null || statValues.Count < 14)
            return;
        statValues[0].text = FormatNumber(playerStats.FinalHp);
        statValues[1].text = FormatNumber(playerStats.FinalAtk);
        statValues[2].text = FormatPercent(playerStats.FinalArmorReduction);
        statValues[3].text = FormatNumber(playerStats.FinalSpeed);
        statValues[4].text = FormatMultiplier(playerStats.FinalAttackSpeedMultiplier);
        statValues[5].text = FormatPercent(playerStats.FinalCriticalChance);
        statValues[6].text = FormatMultiplier(playerStats.FinalCriticalDamageMultiplier);
        statValues[7].text = Mathf.Max(0, playerStats.FinalProjCount).ToString();
        statValues[8].text = FormatMultiplier(playerStats.FinalWeaponSizeMultiplier);
        statValues[9].text = FormatNumber(playerStats.FinalProjSpeed);
        statValues[10].text = FormatMultiplier(playerStats.FinalDurationMultiplier);
        statValues[11].text = FormatMultiplier(playerStats.FinalKnockbackMultiplier);
        statValues[12].text = FormatNumber(playerStats.FinalPickupRange);
        statValues[13].text = FormatMultiplier(playerStats.FinalExperienceMultiplier);
    }

    private static string FormatNumber(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) < 0.01f
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }

    private static string FormatPercent(float value)
    {
        return (value * 100f).ToString("0.#") + "%";
    }

    private static string FormatMultiplier(float value)
    {
        return "x" + value.ToString("0.##");
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color,
        bool outlined = true)
    {
        GameObject panelPrefab = null;
        if (objectName == "UpgradeInventory")
            panelPrefab = Resources.Load<GameObject>("UI/UpgradeInventoryPanel");
        else if (objectName == "UpgradeStats")
            panelPrefab = Resources.Load<GameObject>("UI/UpgradeStatsPanel");

        RectTransform rect = null;
        if (panelPrefab != null)
        {
            GameObject instance = Instantiate(panelPrefab, parent, false);
            instance.name = objectName;
            rect = instance.GetComponent<RectTransform>();
            if (rect == null)
            {
                Destroy(instance);
                rect = null;
            }
        }

        if (rect == null)
            rect = CreateUIObject(objectName, parent);

        Image image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (outlined)
        {
            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
                outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        return rect;
    }

    private TMP_Text CreateHeader(string text, Transform parent, Vector2 position,
        Vector2 size, float fontSize)
    {
        RectTransform header = CreateUIObject(text + " Header", parent);
        SetTopLeft(header, position, size);
        Image background = header.gameObject.AddComponent<Image>();
        background.color = HeaderColor;
        background.raycastTarget = false;

        RectTransform highlight = CreateUIObject("Highlight", header);
        highlight.anchorMin = new Vector2(0f, 1f);
        highlight.anchorMax = new Vector2(1f, 1f);
        highlight.pivot = new Vector2(0.5f, 1f);
        highlight.anchoredPosition = Vector2.zero;
        highlight.sizeDelta = new Vector2(0f, 3f);
        Image highlightImage = highlight.gameObject.AddComponent<Image>();
        highlightImage.color = HeaderHighlightColor;
        highlightImage.raycastTarget = false;

        TMP_Text label = CreateText(text, header, new Vector2(9f, -2f),
            new Vector2(size.x - 18f, size.y - 4f), fontSize,
            TextAlignmentOptions.MidlineLeft, Color.white);
        label.fontStyle = FontStyles.Bold;
        return label;
    }

    private TMP_Text CreateText(string text, Transform parent, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateUIObject(text + " Text", parent);
        SetTopLeft(rect, position, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = displayFont;
        if (displayFontMaterial != null)
            label.fontSharedMaterial = displayFontMaterial;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        return label;
    }

    private static RectTransform CreateUIObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetSidePanel(RectTransform rect, bool right, Vector2 size)
    {
        float x = right ? -28f : 28f;
        float anchorX = right ? 1f : 0f;
        rect.anchorMin = new Vector2(anchorX, 0.5f);
        rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(anchorX, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = size;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect, float inset)
    {
        SetStretch(rect, inset, inset, inset, inset);
    }

    private static void SetStretch(RectTransform rect, float left, float right,
        float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
