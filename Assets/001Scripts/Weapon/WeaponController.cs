using UnityEngine;

/// <summary>
/// Gắn trên Player GameObject. Là bridge giữa WeaponInventory và player systems.
/// Khởi tạo starting weapon từ CharacterData, quản lý weapon pause/resume.
/// </summary>
[RequireComponent(typeof(WeaponInventory))]
public class WeaponController : MonoBehaviour
{
    [Header("── References ──")]
    public PlayerBaseStats playerStats;

    [Header("── Debug ──")]
    [Tooltip("Tự động add starting weapon khi Start()")]
    public bool autoEquipStartingWeapon = true;

    [Header("── Weapon Slots (kéo WeaponData vào đây) ──")]
    [SerializeField] private WeaponData weaponSlot1;
    [SerializeField] private WeaponData weaponSlot2;

    public WeaponData WeaponSlot1 => weaponSlot1;
    public WeaponData WeaponSlot2 => weaponSlot2;

    private WeaponInventory inventory;

    public WeaponInventory Inventory => inventory;

    void Awake()
    {
        inventory = GetComponent<WeaponInventory>();

        // Đảm bảo inventory có reference tới player stats
        if (inventory.playerStats == null)
            inventory.playerStats = playerStats;
    }

    void Start()
    {
        if (autoEquipStartingWeapon)
        {
            EquipWeaponSlots();
        }
    }

    /// <summary>
    /// Trang bị starting weapon từ CharacterData.
    /// </summary>
    private void EquipWeaponSlots()
    {
        bool equippedSlotWeapon = false;

        if (weaponSlot1 != null)
        {
            inventory.AddWeapon(weaponSlot1);
            equippedSlotWeapon = true;
        }

        if (weaponSlot2 != null && weaponSlot2 != weaponSlot1)
        {
            inventory.AddWeapon(weaponSlot2);
            equippedSlotWeapon = true;
        }

        // Giữ tương thích với CharacterData cũ nếu cả hai slot chưa được gán.
        if (!equippedSlotWeapon && playerStats != null && playerStats.characterData != null)
        {
            WeaponData fallbackWeapon = playerStats.characterData.startingWeapon;
            if (fallbackWeapon != null)
                inventory.AddWeapon(fallbackWeapon);
        }
    }

    /// <summary>
    /// Thêm vũ khí hoặc upgrade nếu đã có. Dùng bởi UpgradeManager.
    /// </summary>
    public bool AddOrUpgradeWeapon(WeaponData data)
    {
        if (data == null)
            return false;

        if (inventory.HasWeapon(data))
        {
            return inventory.UpgradeWeapon(data);
        }

        return inventory.AddWeapon(data) != null;
    }

    /// <summary>
    /// Tạm dừng tất cả vũ khí (khi pause game / show upgrade menu).
    /// </summary>
    public void PauseAllWeapons()
    {
        foreach (WeaponBehaviour wb in inventory.EquippedWeapons)
        {
            if (wb != null) wb.enabled = false;
        }
    }

    /// <summary>
    /// Resume tất cả vũ khí.
    /// </summary>
    public void ResumeAllWeapons()
    {
        foreach (WeaponBehaviour wb in inventory.EquippedWeapons)
        {
            if (wb != null) wb.enabled = true;
        }
    }
}
