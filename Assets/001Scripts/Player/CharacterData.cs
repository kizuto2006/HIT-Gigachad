using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Data/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("── Thông tin nhân vật ──")]
    public string characterName;
    [TextArea(2, 5)] public string description;

    [Header("── Survival ──")]
    [Min(1f)] public float baseHp = 120f;
    [Tooltip("Tỷ lệ giảm damage nhận vào. 0.35 = giảm 35%.")]
    [Range(0f, 0.95f)] public float baseDef = 0f;

    [Header("── Offense Multipliers ──")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Range(0f, 1f)] public float criticalChance = 0.01f;
    [Min(1f)] public float criticalDamageMultiplier = 2f;
    [Min(0.01f)] public float attackSpeedMultiplier = 1f;
    [Min(0)] public int bonusProjectileCount = 0;

    [Header("── Weapon Multipliers ──")]
    [Min(0.01f)] public float sizeMultiplier = 1f;
    [Min(0.01f)] public float projectileSpeedMultiplier = 1f;
    [Min(0.01f)] public float durationMultiplier = 1f;
    [Min(0f)] public float knockbackMultiplier = 1f;

    [Header("── Mobility ──")]
    [Min(0.01f)] public float moveSpeedMultiplier = 0.95f;
    [Min(0f)] public float jumpHeight = 8f;

    [Header("── Collection ──")]
    [Min(0f)] public float pickupRange = 5f;
    [Min(0f)] public float experienceMultiplier = 1f;

    [Header("── Starting Weapon ──")]
    [Tooltip("Vũ khí mặc định khi bắt đầu game.")]
    public WeaponData startingWeapon;
}
