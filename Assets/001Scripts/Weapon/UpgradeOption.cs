using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeOptionType
{
    Weapon,
    Tome,
    Item
}

public enum WeaponStatKind
{
    Damage,
    CriticalChance,
    Cooldown,
    Size,
    ProjectileSpeed,
    ProjectileCount,
    Knockback,
    TomeBonus
}

[Serializable]
public struct WeaponStatsSnapshot
{
    public int level;
    public float damage;
    public float crit;
    public float cooldown;
    public float size;
    public float projectileSpeed;
    public int projectileCount;
    public float knockback;

    public static WeaponStatsSnapshot Empty => new WeaponStatsSnapshot { level = 0 };
}

[Serializable]
public struct WeaponStatChange
{
    public WeaponStatKind kind;
    public string label;
    public float currentValue;
    public float nextValue;
    public bool isInteger;
    public bool isPercent;
    public bool lowerIsBetter;

    public string CurrentText => Format(currentValue);
    public string NextText => Format(nextValue);

    private string Format(float value)
    {
        if (isPercent)
            return $"{value * 100f:0.#}%";

        return isInteger ? Mathf.RoundToInt(value).ToString() : value.ToString("0.##");
    }
}

[Serializable]
public class UpgradeOption
{
    public UpgradeOptionType type;
    public WeaponData weapon;
    public TomeData tome;
    public ItemData item;
    public bool isNewItem;
    public int targetLevel;
    public string description;
    public WeaponStatsSnapshot currentStats;
    public WeaponStatsSnapshot nextStats;
    public float currentTomeBonus;
    public float nextTomeBonus;
    public WeaponRarity rarity = WeaponRarity.Common;
    private bool rarityApplied;


    public bool IsWeapon => type == UpgradeOptionType.Weapon;
    public bool IsTome => type == UpgradeOptionType.Tome;
    public bool IsItem => type == UpgradeOptionType.Item;
    public bool isNewWeapon => IsWeapon && isNewItem;
    public bool IsNewEquipment => (IsWeapon || IsTome) && isNewItem;
    public int CurrentLevel => IsWeapon ? currentStats.level : Mathf.Max(0, targetLevel - 1);
    public Sprite Icon => IsWeapon ? (weapon != null ? weapon.icon : null) : IsTome ? (tome != null ? tome.icon : null) : item != null ? item.icon : null;
    public string DisplayName => IsWeapon ? (weapon != null ? weapon.weaponName : string.Empty) : IsTome ? (tome != null ? tome.tomeName : string.Empty) : item != null ? item.itemName : string.Empty;
    public WeaponRarity Rarity => rarity;
    public float RarityMultiplier => IsNewEquipment ? 1f : UpgradeRarityUtility.GetBuffMultiplier(rarity);
    public bool HasRarity => !IsNewEquipment && rarityApplied;
    public string RarityDisplayName => HasRarity ? UpgradeRarityUtility.GetDisplayName(rarity) : string.Empty;
    public int MaxLevel => IsWeapon ? (weapon != null ? weapon.maxLevel : 0) : IsTome ? (tome != null ? tome.maxLevel : 0) : int.MaxValue;
    public bool IsMaxLevel => (IsWeapon || IsTome) && targetLevel >= MaxLevel;

    public static UpgradeOption CreateNewWeapon(WeaponData data, PlayerBaseStats playerStats)
    {
        return new UpgradeOption
        {
            type = UpgradeOptionType.Weapon,
            weapon = data,
            isNewItem = true,
            targetLevel = 1,
            currentStats = WeaponStatsSnapshot.Empty,
            nextStats = data.GetStatsAtLevel(1, playerStats),
            description = data.description
        };
    }

    public static UpgradeOption CreateWeaponLevelUp(WeaponBehaviour behaviour)
    {
        int nextLevel = Mathf.Min(behaviour.CurrentLevel + 1, behaviour.data.maxLevel);
        return new UpgradeOption
        {
            type = UpgradeOptionType.Weapon,
            weapon = behaviour.data,
            isNewItem = false,
            targetLevel = nextLevel,
            currentStats = behaviour.GetCurrentStatsSnapshot(),
            nextStats = behaviour.GetStatsSnapshotAtLevel(nextLevel),
            description = behaviour.data.description
        };
    }

    public static UpgradeOption CreateNewTome(TomeData data)
    {
        return new UpgradeOption
        {
            type = UpgradeOptionType.Tome,
            tome = data,
            isNewItem = true,
            targetLevel = 1,
            currentTomeBonus = 0f,
            nextTomeBonus = data.GetBonusAtLevel(1),
            description = data.description
        };
    }

    public static UpgradeOption CreateTomeLevelUp(
        TomeData data,
        int currentLevel,
        float currentExtraBonus = 0f)
    {
        int nextLevel = Mathf.Min(currentLevel + 1, data.maxLevel);
        return new UpgradeOption
        {
            type = UpgradeOptionType.Tome,
            tome = data,
            isNewItem = false,
            targetLevel = nextLevel,
            currentTomeBonus = data.GetBonusAtLevel(currentLevel) + currentExtraBonus,
            nextTomeBonus = data.GetBonusAtLevel(nextLevel) + currentExtraBonus,
            description = data.description
        };
    }

public static UpgradeOption CreateNewItem(ItemData data)
    {
        return new UpgradeOption
        {
            type = UpgradeOptionType.Item,
            item = data,
            isNewItem = true,
            targetLevel = 1,
            description = data != null ? data.description : string.Empty
        };
    }

public static UpgradeOption CreateItemStack(ItemData data, int currentStackCount)
    {
        return new UpgradeOption
        {
            type = UpgradeOptionType.Item,
            item = data,
            isNewItem = false,
            targetLevel = Mathf.Max(0, currentStackCount) + 1,
            description = data != null ? data.description : string.Empty
        };
    }

    public void ApplyRarity(WeaponRarity selectedRarity)
    {
        if (rarityApplied || IsNewEquipment)
            return;

        rarity = selectedRarity;
        rarityApplied = true;
        float multiplier = RarityMultiplier;

        if (IsWeapon)
        {
            nextStats.damage = ScaleUpgrade(currentStats.damage, nextStats.damage, multiplier);
            nextStats.crit = Mathf.Clamp01(ScaleUpgrade(currentStats.crit, nextStats.crit, multiplier));
            nextStats.cooldown = Mathf.Max(
                0.05f,
                isNewItem
                    ? nextStats.cooldown / multiplier
                    : ScaleUpgrade(currentStats.cooldown, nextStats.cooldown, multiplier));
            nextStats.size = ScaleUpgrade(currentStats.size, nextStats.size, multiplier);
            nextStats.projectileSpeed = ScaleUpgrade(
                currentStats.projectileSpeed,
                nextStats.projectileSpeed,
                multiplier);
            nextStats.projectileCount = Mathf.Max(
                nextStats.projectileCount,
                Mathf.RoundToInt(ScaleUpgrade(
                    currentStats.projectileCount,
                    nextStats.projectileCount,
                    multiplier)));
            nextStats.knockback = ScaleUpgrade(
                currentStats.knockback,
                nextStats.knockback,
                multiplier);
        }
        else if (IsTome)
        {
            nextTomeBonus = ScaleUpgrade(currentTomeBonus, nextTomeBonus, multiplier);
        }
    }

    private static float ScaleUpgrade(float current, float next, float multiplier)
    {
        return current + (next - current) * multiplier;
    }




    public List<WeaponStatChange> GetStatChanges(bool includeUnchanged = false)
    {
        List<WeaponStatChange> changes = new List<WeaponStatChange>(8);
        if (IsTome)
        {
            if (tome != null)
                AddChange(changes, WeaponStatKind.TomeBonus, GetTomeStatLabel(tome.statType), currentTomeBonus, nextTomeBonus, false, true, false, includeUnchanged);
            return changes;
        }

        if (IsItem)
            return changes;

        AddChange(changes, WeaponStatKind.Damage, "DAMAGE", currentStats.damage, nextStats.damage, false, false, false, includeUnchanged);
        AddChange(changes, WeaponStatKind.CriticalChance, "CRIT", currentStats.crit, nextStats.crit, false, true, false, includeUnchanged);
        AddChange(changes, WeaponStatKind.Cooldown, "COOLDOWN", currentStats.cooldown, nextStats.cooldown, false, false, true, includeUnchanged);
        if (weapon != null && weapon.displaySizeAsPercent && weapon.size > 0.0001f)
        {
            AddChange(
                changes,
                WeaponStatKind.Size,
                "SIZE",
                currentStats.size / weapon.size,
                nextStats.size / weapon.size,
                false,
                true,
                false,
                includeUnchanged);
        }
        else
        {
            AddChange(changes, WeaponStatKind.Size, "SIZE", currentStats.size, nextStats.size, false, false, false, includeUnchanged);
        }
        AddChange(changes, WeaponStatKind.ProjectileSpeed, "PROJECTILE SPEED", currentStats.projectileSpeed, nextStats.projectileSpeed, false, false, false, includeUnchanged);
        AddChange(changes, WeaponStatKind.ProjectileCount, "PROJECTILES", currentStats.projectileCount, nextStats.projectileCount, true, false, false, includeUnchanged);
        AddChange(changes, WeaponStatKind.Knockback, "KNOCKBACK", currentStats.knockback, nextStats.knockback, false, false, false, includeUnchanged);
        return changes;
    }

    public string GetDisplayDescription()
    {
        if (IsItem && item != null)
        {
            string effectLabel = GetItemEffectLabel(item.effectType);
            return $"{item.description}\nSTACK: x{targetLevel}  {effectLabel}: {item.GetFormattedValueAtStackCount(targetLevel)}";
        }

        List<WeaponStatChange> changes = GetStatChanges();
        if (changes.Count == 0)
            return description ?? string.Empty;

        List<string> lines = new List<string>(changes.Count);
        for (int i = 0; i < changes.Count; i++)
        {
            WeaponStatChange change = changes[i];
            lines.Add($"{change.label}: {change.CurrentText}  <color=#D9D3BE>></color>  <color=#16D22D>{change.NextText}</color>");
        }

        return string.Join("\n", lines);
    }

    private static string GetItemEffectLabel(ItemEffectType effectType)
    {
        switch (effectType)
        {
            case ItemEffectType.BorgarDropChance: return "BORGAR DROP";
            case ItemEffectType.CriticalDamage: return "CRIT DAMAGE";
            case ItemEffectType.Luck: return "LUCK";
            case ItemEffectType.ExperienceGain: return "XP GAIN";
            case ItemEffectType.MaximumHealth: return "MAX HEALTH";
            case ItemEffectType.DodgeChance: return "DODGE";
            case ItemEffectType.AttackSpeed: return "ATTACK SPEED";
            case ItemEffectType.Healing: return "HEALING";
            case ItemEffectType.HighHealthEnemyDamage: return "HIGH-HP DAMAGE";
            case ItemEffectType.MovementSpeed: return "MOVE SPEED";
            default: return "EFFECT";
        }
    }

    private static string GetTomeStatLabel(TomeStatType statType)
    {
        switch (statType)
        {
            case TomeStatType.Damage: return "DAMAGE";
            case TomeStatType.WeaponSize: return "SIZE";
            case TomeStatType.MoveSpeed: return "MOVE SPEED";
            case TomeStatType.MaxHealth: return "MAX HEALTH";
            case TomeStatType.Armor: return "ARMOR";
            case TomeStatType.Cooldown: return "COOLDOWN";
            case TomeStatType.ProjectileSpeed: return "PROJECTILE SPEED";
            case TomeStatType.Experience: return "XP GAIN";
            default: return "STAT";
        }
    }

    private static void AddChange(
        List<WeaponStatChange> changes,
        WeaponStatKind kind,
        string label,
        float current,
        float next,
        bool isInteger,
        bool isPercent,
        bool lowerIsBetter,
        bool includeUnchanged)
    {
        if (!includeUnchanged && Mathf.Approximately(current, next))
            return;

        changes.Add(new WeaponStatChange
        {
            kind = kind,
            label = label,
            currentValue = current,
            nextValue = next,
            isInteger = isInteger,
            isPercent = isPercent,
            lowerIsBetter = lowerIsBetter
        });
    }
}
