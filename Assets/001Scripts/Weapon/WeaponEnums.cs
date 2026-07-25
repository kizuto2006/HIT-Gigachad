/// <summary>
/// Độ hiếm của vũ khí — quyết định màu UI và hệ số upgrade.
/// </summary>
public enum WeaponRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Loại vũ khí — quyết định WeaponBehaviour nào sẽ được dùng.
/// </summary>
public enum WeaponType
{
    Melee,
    Projectile,
    AoE,
    Orbital
}
