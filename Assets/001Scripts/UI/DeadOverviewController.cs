using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the post-death overview and weapon damage panels.
/// The prefab owns the layout; this component only binds the current runtime values.
/// </summary>
public sealed class DeadOverviewController : MonoBehaviour
{
    private const string SummaryPath = "Sumary/Table/BackGroundMap /OverviewContent";
    private const string DamagePath = "Damage/Table/BackGroundMap /DamageContent";
    private const int OverviewSlotCount = 4;

    private Transform summaryContent;
    private Transform damageContent;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        Refresh();
    }

public void Refresh()
    {
        CacheReferences();

        XPSystem xpSystem = FindFirstObjectByType<XPSystem>();
        PlayerCurrency playerCurrency = PlayerCurrency.Instance;
        if (playerCurrency == null)
            playerCurrency = FindFirstObjectByType<PlayerCurrency>();

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        WeaponInventory weaponInventory = FindFirstObjectByType<WeaponInventory>();
        PlayerTomeInventory tomeInventory = FindFirstObjectByType<PlayerTomeInventory>();
        PlayerBaseStats playerStats = playerHealth != null ? playerHealth.stats : null;

        SetSummaryValue("LevelValue", xpSystem != null ? xpSystem.CurrentLevel.ToString("00") : "--");
        SetSummaryValue("KillsValue", playerCurrency != null
            ? Mathf.Max(0, playerCurrency.EnemiesDefeated).ToString()
            : "--");
        SetSummaryValue("GoldValue", playerCurrency != null
            ? Mathf.Max(0, playerCurrency.Gold).ToString()
            : "--");
        SetSummaryValue("RunCoinsValue", playerCurrency != null
            ? "COINS EARNED  +" + Mathf.Max(0, playerCurrency.LastRunReward)
            : "COINS EARNED  --");
        SetSummaryValue("RunTimeValue", FormatRunTime(Time.timeSinceLevelLoad));

        RefreshWeaponRows(weaponInventory, playerStats);
        RefreshTomeRows(tomeInventory);
    }

    private void CacheReferences()
    {
        if (summaryContent == null)
            summaryContent = transform.Find(SummaryPath);

        if (damageContent == null)
            damageContent = transform.Find(DamagePath);

        EnsureDamageRows();
    }

    private void EnsureDamageRows()
    {
        if (damageContent == null)
            return;

        Transform weaponTemplate = damageContent.Find("WeaponRow_0");
        Transform tomeTemplate = damageContent.Find("TomeRow_0");
        if (weaponTemplate == null || tomeTemplate == null)
            return;

        for (int i = 0; i < OverviewSlotCount; i++)
        {
            EnsureDamageRow(weaponTemplate, "WeaponRow_", i, false);
            EnsureDamageRow(tomeTemplate, "TomeRow_", i, true);
        }
    }

    private void EnsureDamageRow(
        Transform template,
        string rowPrefix,
        int index,
        bool rightColumn)
    {
        string rowName = rowPrefix + index;
        Transform row = damageContent.Find(rowName);
        if (row == null)
        {
            GameObject clone = Instantiate(template.gameObject, damageContent, false);
            clone.name = rowName;
            row = clone.transform;
        }

        ConfigureDamageRowLayout(row, index, rightColumn);
    }

    private static void ConfigureDamageRowLayout(
        Transform row,
        int index,
        bool rightColumn)
    {
        RectTransform rowRect = row as RectTransform;
        if (rowRect == null)
            return;

        float top = 0.83f - index * 0.21f;
        float bottom = top - 0.17f;
        float left = rightColumn ? 0.51f : 0.04f;
        float right = rightColumn ? 0.96f : 0.49f;

        rowRect.anchorMin = new Vector2(left, bottom);
        rowRect.anchorMax = new Vector2(right, top);
        rowRect.anchoredPosition = Vector2.zero;
        rowRect.sizeDelta = Vector2.zero;
    }

    private void RefreshWeaponRows(WeaponInventory inventory, PlayerBaseStats playerStats)
    {
        if (damageContent == null)
            return;

        for (int i = 0; i < OverviewSlotCount; i++)
        {
            Transform row = damageContent.Find("WeaponRow_" + i);
            if (row == null)
                continue;

            WeaponBehaviour weapon = inventory != null ? inventory.GetWeaponAtSlot(i) : null;
            bool hasWeapon = weapon != null && weapon.data != null;
            float damage = hasWeapon ? CalculateWeaponDamage(weapon, playerStats) : 0f;

            ConfigureOutputRow(
                row,
                hasWeapon,
                hasWeapon ? GetWeaponName(weapon) : "EMPTY SLOT",
                hasWeapon ? "LEVEL " + weapon.CurrentLevel.ToString("00") : "AVAILABLE",
                "DAMAGE",
                hasWeapon ? damage.ToString("0.0") : "--",
                hasWeapon ? weapon.data.icon : null,
                hasWeapon
                    ? GetRarityColor(weapon.data.rarity.ToString())
                    : new Color(0.35f, 0.35f, 0.35f, 1f));
        }
    }

    private void RefreshTomeRows(PlayerTomeInventory inventory)
    {
        if (damageContent == null)
            return;

        for (int i = 0; i < OverviewSlotCount; i++)
        {
            Transform row = damageContent.Find("TomeRow_" + i);
            if (row == null)
                continue;

            TomeLevelState state = inventory != null && i < inventory.OwnedTomes.Count
                ? inventory.OwnedTomes[i]
                : null;
            bool hasTome = state != null && state.tome != null;
            float bonus = hasTome ? state.tome.GetBonusAtLevel(state.level) : 0f;

            ConfigureOutputRow(
                row,
                hasTome,
                hasTome ? GetTomeName(state.tome) : "EMPTY TOME",
                hasTome ? "LEVEL " + state.level.ToString("00") : "AVAILABLE",
                hasTome ? GetTomeCaption(state.tome.statType) : "BONUS",
                hasTome ? FormatTomeOutput(bonus) : "--",
                hasTome ? state.tome.icon : null,
                hasTome
                    ? GetTomeColor(state.tome.statType)
                    : new Color(0.35f, 0.35f, 0.35f, 1f));
        }
    }

private static void ConfigureOutputRow(
        Transform row,
        bool hasItem,
        string displayName,
        string displayLevel,
        string outputCaption,
        string outputValue,
        Sprite icon,
        Color accentColor)
    {
        TMP_Text nameText = FindText(row, "WeaponName");
        TMP_Text levelText = FindText(row, "WeaponLevel");
        TMP_Text captionText = FindText(row, "DamageCaption");
        TMP_Text outputText = FindText(row, "WeaponDamage");
        Image iconImage = FindImage(row, "Icon");
        Image accentImage = FindImage(row, "WeaponAccent");

        if (nameText != null)
        {
            nameText.text = displayName;
            nameText.color = hasItem ? Color.white : new Color(0.58f, 0.58f, 0.58f, 1f);
        }

        if (levelText != null)
        {
            levelText.text = displayLevel;
            levelText.color = hasItem
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : new Color(0.45f, 0.45f, 0.45f, 1f);
        }

        if (captionText != null)
            captionText.text = outputCaption;

        if (outputText != null)
        {
            outputText.text = outputValue;
            outputText.color = hasItem ? accentColor : new Color(0.45f, 0.45f, 0.45f, 1f);
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = hasItem
                ? Color.white
                : new Color(0.12f, 0.12f, 0.12f, 1f);
            iconImage.preserveAspect = true;
        }

        if (accentImage != null)
            accentImage.color = accentColor;
    }

private static float CalculateWeaponDamage(WeaponBehaviour weapon, PlayerBaseStats playerStats)
    {
        if (weapon == null || weapon.data == null)
            return 0f;

        float calculatedDamage = weapon.data
            .GetStatsAtLevel(weapon.CurrentLevel, playerStats)
            .damage;
        return Mathf.Max(0f, calculatedDamage);
    }

private static string GetTomeName(TomeData tome)
    {
        if (tome == null)
            return "UNKNOWN TOME";

        if (!string.IsNullOrWhiteSpace(tome.tomeName))
            return tome.tomeName.ToUpperInvariant();

        return tome.statType.ToString().ToUpperInvariant() + " TOME";
    }

private static string GetTomeCaption(TomeStatType statType)
    {
        switch (statType)
        {
            case TomeStatType.Damage:
                return "DAMAGE BONUS";
            case TomeStatType.WeaponSize:
                return "SIZE BONUS";
            case TomeStatType.MoveSpeed:
                return "SPEED BONUS";
            case TomeStatType.MaxHealth:
                return "HEALTH BONUS";
            case TomeStatType.Armor:
                return "ARMOR BONUS";
            case TomeStatType.Cooldown:
                return "COOLDOWN BONUS";
            case TomeStatType.ProjectileSpeed:
                return "PROJECTILE BONUS";
            case TomeStatType.Experience:
                return "XP BONUS";
            default:
                return "BONUS";
        }
    }

private static string FormatTomeOutput(float bonus)
    {
        return "+" + (bonus * 100f).ToString("0.#") + "%";
    }

private static Color GetTomeColor(TomeStatType statType)
    {
        switch (statType)
        {
            case TomeStatType.Damage:
                return new Color(0.95f, 0.45f, 0.30f, 1f);
            case TomeStatType.Cooldown:
            case TomeStatType.ProjectileSpeed:
            case TomeStatType.MoveSpeed:
                return new Color(0.35f, 0.78f, 1f, 1f);
            case TomeStatType.MaxHealth:
            case TomeStatType.Armor:
                return new Color(0.35f, 0.90f, 0.46f, 1f);
            default:
                return new Color(0.95f, 0.82f, 0.35f, 1f);
        }
    }








    private void SetSummaryValue(string objectName, string value)
    {
        if (summaryContent == null)
            return;

        Transform valueTransform = summaryContent.Find(objectName);
        TMP_Text valueText = valueTransform != null
            ? valueTransform.GetComponent<TMP_Text>()
            : null;

        if (valueText != null)
            valueText.text = value;
    }

    private static TMP_Text FindText(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Image FindImage(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static string GetWeaponName(WeaponBehaviour weapon)
    {
        if (weapon == null || weapon.data == null)
            return "UNKNOWN";

        if (!string.IsNullOrWhiteSpace(weapon.data.weaponName))
            return weapon.data.weaponName.ToUpperInvariant();

        return weapon.gameObject.name.ToUpperInvariant();
    }

    private static string FormatHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null || playerHealth.stats == null)
            return "--";

        float maxHealth = Mathf.Max(1f, playerHealth.stats.FinalHp);
        float currentHealth = Mathf.Clamp(playerHealth.currentHp, 0f, maxHealth);
        return Mathf.CeilToInt(currentHealth) + " / " + Mathf.CeilToInt(maxHealth);
    }

    private static string FormatRunTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainder.ToString("00");
    }

    private static Color GetRarityColor(string rarity)
    {
        switch (rarity)
        {
            case "Uncommon":
                return new Color32(62, 177, 173, 255);
            case "Rare":
                return new Color32(156, 90, 225, 255);
            case "Epic":
                return new Color32(224, 72, 72, 255);
            case "Legendary":
                return new Color32(255, 206, 73, 255);
            default:
                return new Color32(70, 220, 108, 255);
        }
    }
}
