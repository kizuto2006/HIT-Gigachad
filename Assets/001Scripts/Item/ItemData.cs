using UnityEngine;

public enum ItemEffectType
{
    BorgarDropChance,
    CriticalDamage,
    Luck,
    ExperienceGain,
    MaximumHealth,
    DodgeChance,
    AttackSpeed,
    Healing,
    HighHealthEnemyDamage,
    MovementSpeed
}

public enum ItemValueType
{
    Percentage,
    Flat
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Data/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Effect")]
    public ItemEffectType effectType;
    public ItemValueType valueType = ItemValueType.Percentage;
    [Min(0f)] public float valuePerLevel;
    [Min(1)] public int maxLevel = 99;

    [Tooltip("Optional normalized threshold used by conditional effects.")]
    [Range(0f, 1f)] public float conditionThreshold;

    [Header("Chest Reward")]
    [Tooltip("Relative chance for this item to appear in a chest reward pool.")]
    [Min(0f)] public float chestWeight = 1f;
public float GetValueAtLevel(int level)
    {
        return GetValueAtStackCount(level);
    }

public string GetFormattedValueAtLevel(int level)
    {
        return GetFormattedValueAtStackCount(level);
    }

    public float GetValueAtStackCount(int stackCount)
    {
        return Mathf.Max(0, stackCount) * valuePerLevel;
    }

    public string GetFormattedValueAtStackCount(int stackCount)
    {
        float totalValue = GetValueAtStackCount(stackCount);
        return valueType == ItemValueType.Percentage
            ? $"{totalValue * 100f:0.#}%"
            : $"{totalValue:0.#}";
    }

    
private void OnValidate()
    {
        valuePerLevel = Mathf.Max(0f, valuePerLevel);
        maxLevel = Mathf.Max(1, maxLevel);
        conditionThreshold = Mathf.Clamp01(conditionThreshold);
        chestWeight = Mathf.Max(0f, chestWeight);
    }
}
