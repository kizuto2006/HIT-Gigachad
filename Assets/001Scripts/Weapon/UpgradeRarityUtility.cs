using UnityEngine;

public static class UpgradeRarityUtility
{
    public static float GetBuffMultiplier(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Uncommon:
                return 1.10f;
            case WeaponRarity.Rare:
                return 1.22f;
            case WeaponRarity.Epic:
                return 1.38f;
            case WeaponRarity.Legendary:
                return 1.60f;
            default:
                return 1f;
        }
    }

    public static string GetDisplayName(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Uncommon:
                return "UNCOMMON";
            case WeaponRarity.Rare:
                return "RARE";
            case WeaponRarity.Epic:
                return "EPIC";
            case WeaponRarity.Legendary:
                return "LEGENDARY";
            default:
                return "COMMON";
        }
    }

    public static Color GetColor(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Uncommon:
                return new Color32(62, 177, 173, 255);
            case WeaponRarity.Rare:
                return new Color32(156, 90, 225, 255);
            case WeaponRarity.Epic:
                return new Color32(224, 72, 72, 255);
            case WeaponRarity.Legendary:
                return new Color32(255, 206, 73, 255);
            default:
                return new Color32(70, 220, 108, 255);
        }
    }

    public static Color GetCardTint(WeaponRarity rarity)
    {
        return Color.Lerp(new Color32(31, 31, 31, 255), GetColor(rarity), 0.18f);
    }
}
