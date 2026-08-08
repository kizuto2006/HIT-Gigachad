using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Quản lý danh sách vũ khí đang trang bị trên player.
/// Tối đa maxSlots vũ khí cùng lúc. Mỗi vũ khí là một WeaponBehaviour component
/// được add động lên GameObject con.
/// </summary>
public class WeaponInventory : MonoBehaviour
{

    private void Awake()
    {
        EnsureSlotCapacity();
    }

    [Header("── Settings ──")]
    [Tooltip("Số slot vũ khí tối đa.")]


    private const int MaximumSlotCount = 4;
    private int baseSlotCount;
    private bool slotCapacityInitialized;
    public int maxSlots = 2;

    [Header("── References ──")]
    public PlayerBaseStats playerStats;

    [Tooltip("Layer mask của enemy — truyền cho tất cả weapon behaviours.")]
    public LayerMask enemyLayer;

    // Danh sách weapon behaviours đang active
    private readonly List<WeaponBehaviour> equippedWeapons = new List<WeaponBehaviour>();
    public IReadOnlyList<WeaponBehaviour> EquippedWeapons => equippedWeapons;
    public event Action WeaponsChanged;


    public int MaxSlots
    {
        get
        {
            EnsureSlotCapacity();
            return maxSlots;
        }
    }
public int SlotCount => equippedWeapons.Count;
    public bool IsFull
    {
        get
        {
            EnsureSlotCapacity();
            return equippedWeapons.Count >= maxSlots;
        }
    }

    /// <summary>
    /// Thêm vũ khí mới vào inventory. Trả về WeaponBehaviour mới hoặc null nếu đầy.
    /// </summary>
    public WeaponBehaviour AddWeapon(WeaponData data, float rarityMultiplier = 1f)
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponInventory] Không thể thêm weapon null!");
            return null;
        }

        if (IsFull)
        {
            Debug.LogWarning($"[WeaponInventory] Đã đầy {maxSlots} slots! Không thể thêm {data.weaponName}.");
            return null;
        }

        if (HasWeapon(data))
        {
            Debug.LogWarning($"[WeaponInventory] {data.weaponName} đã được trang bị!");
            return null;
        }

        GameObject weaponGO = new GameObject($"Weapon_{data.weaponName}");
        weaponGO.transform.SetParent(transform, false);

        WeaponBehaviour behaviour = AddBehaviourForWeapon(weaponGO, data);
        behaviour.data = data;
        behaviour.Initialize(playerStats, enemyLayer, transform);
        behaviour.SetInitialRarityMultiplier(rarityMultiplier);

        equippedWeapons.Add(behaviour);
        WeaponsChanged?.Invoke();

        Debug.Log($"[WeaponInventory] Đã thêm vũ khí: {data.weaponName} ({data.weaponType})");
        return behaviour;
    }

    /// <summary>
    /// Level up vũ khí đã trang bị. Trả về true nếu thành công.
    /// </summary>
    public bool UpgradeWeapon(WeaponData data, float rarityMultiplier = 1f)
    {
        WeaponBehaviour weapon = GetWeapon(data);
        if (weapon == null)
        {
            Debug.LogWarning($"[WeaponInventory] Không tìm thấy weapon {data.weaponName} để upgrade!");
            return false;
        }

        bool upgraded = weapon.LevelUp(rarityMultiplier);
        if (upgraded)
            WeaponsChanged?.Invoke();

        return upgraded;
    }

    /// <summary>
    /// Check vũ khí đã được trang bị chưa (so sánh bằng WeaponData reference).
    /// </summary>
    public bool HasWeapon(WeaponData data)
    {
        return GetWeapon(data) != null;
    }

    /// <summary>
    /// Lấy WeaponBehaviour theo WeaponData. Trả về null nếu chưa trang bị.
    /// </summary>
    public WeaponBehaviour GetWeapon(WeaponData data)
    {
        foreach (WeaponBehaviour wb in equippedWeapons)
        {
            if (wb != null && wb.data == data) return wb;
        }
        return null;
    }

    /// <summary>
    /// Xóa vũ khí khỏi inventory.
    /// </summary>
    public void RemoveWeapon(WeaponData data)
    {
        WeaponBehaviour weapon = GetWeapon(data);
        if (weapon != null)
        {
            equippedWeapons.Remove(weapon);
            Destroy(weapon.gameObject);
            WeaponsChanged?.Invoke();
            Debug.Log($"[WeaponInventory] Đã xóa vũ khí: {data.weaponName}");
        }
    }

    public WeaponBehaviour GetWeaponAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedWeapons.Count)
            return null;

        return equippedWeapons[slotIndex];
    }

    /// <summary>
    /// Add đúng loại WeaponBehaviour component dựa trên WeaponType.
    /// </summary>
    private WeaponBehaviour AddBehaviourForWeapon(GameObject go, WeaponData weaponData)
    {
        switch (weaponData.attackType)
        {
            case WeaponAttackType.MeleeSlash:
                return go.AddComponent<SwordWeapon>();
            case WeaponAttackType.RadialPulse:
                return go.AddComponent<AuraWeapon>();
            case WeaponAttackType.Firewalker:
                return go.AddComponent<FirewalkerWeapon>();
        }

        // Fallback cho những weapon chưa có implementation riêng.
        switch (weaponData.weaponType)
        {
            case WeaponType.Melee:
                return go.AddComponent<MeleeWeapon>();
            case WeaponType.Projectile:
                return go.AddComponent<ProjectileWeapon>();
            case WeaponType.AoE:
                return go.AddComponent<AoEWeapon>();
            case WeaponType.Orbital:
                return go.AddComponent<OrbitalWeapon>();
            default:
                Debug.LogWarning($"[WeaponInventory] Unknown weapon type: {weaponData.weaponType}, defaulting to Melee.");
                return go.AddComponent<MeleeWeapon>();
        }
    }


    private void EnsureSlotCapacity()
    {
        if(!slotCapacityInitialized)
        {
            baseSlotCount = Mathf.Clamp(maxSlots, 1, MaximumSlotCount);
            slotCapacityInitialized = true;
        }

        maxSlots = ShopUI.GetUnlockedSlotCount(true, baseSlotCount, MaximumSlotCount);
    }
}
