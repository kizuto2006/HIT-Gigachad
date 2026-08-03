using UnityEngine;

public enum TomeStatType
{
    Damage,
    WeaponSize,
    MoveSpeed,
    MaxHealth,
    Armor,
    Cooldown,
    ProjectileSpeed,
    Experience
}

[CreateAssetMenu(fileName = "TomeData", menuName = "Data/Tome Data")]
public class TomeData : ScriptableObject
{
    [Header("Identity")]
    public string tomeName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Upgrade")]
    public TomeStatType statType;
    [Min(1)] public int maxLevel = 5;
    [Tooltip("Bonus added per level. 0.1 means +10%.")]
    [Min(0f)] public float bonusPerLevel = 0.1f;

    public float GetBonusAtLevel(int level)
    {
        return Mathf.Clamp(level, 0, maxLevel) * bonusPerLevel;
    }

    private void OnValidate()
    {
        maxLevel = Mathf.Max(1, maxLevel);
        bonusPerLevel = Mathf.Max(0f, bonusPerLevel);
    }
}
