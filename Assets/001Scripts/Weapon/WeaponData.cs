using UnityEngine;

[System.Serializable]
public enum WeaponAttackType
{
    MeleeSlash,
    BowShot,
    GunShot,
    RadialPulse,
    Custom
}

[CreateAssetMenu(fileName = "WeaponData", menuName = "Data/Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("── Identity ──")]
    [Tooltip("ID duy nhất dùng để lưu/load và tra cứu vũ khí.")]
    public string id;
    public string weaponName;
    [TextArea(2, 5)] public string description;
    [Tooltip("Icon hiển thị trong kho đồ và UI.")]
    public Sprite icon;

    [Header("── Auto-Attack Classification ──")]
    [Tooltip("Loại vũ khí cho hệ thống auto-attack (Melee, Projectile, AoE, Orbital).")]
    public WeaponType weaponType = WeaponType.Melee;
    [Tooltip("Độ hiếm của vũ khí — ảnh hưởng màu UI.")]
    public WeaponRarity rarity = WeaponRarity.Common;

    [Header("── Combat Stats ──")]
    [Min(0f)] public float atk = 10f;
    [Tooltip("Tỷ lệ chí mạng. 0.2 tương đương 20%.")]
    [Range(0f, 1f)] public float crit = 0.1f;
    [Min(0f)] public float projectileSpeed = 15f;
    [Min(1)] public int projectileCount = 1;
    [Tooltip("Hệ số kích thước hitbox hoặc projectile.")]
    [Min(0.01f)] public float size = 1f;
    [Tooltip("Thời gian chờ giữa hai lần đánh, tính bằng giây.")]
    [Min(0.01f)] public float cooldown = 0.5f;

    [Header("── Projectile / Melee ──")]
    [Tooltip("Số enemy mà projectile xuyên qua trước khi hủy. 0 = không xuyên.")]
    [Min(0)] public int pierce = 0;
    [Tooltip("Lực đẩy lùi enemy khi trúng đòn.")]
    [Min(0f)] public float knockback = 0f;

    [Header("── AoE Settings ──")]
    [Tooltip("Thời gian tồn tại của vùng AoE (giây).")]
    [Min(0f)] public float duration = 3f;
    [Tooltip("Khoảng thời gian giữa các tick damage trong vùng AoE (giây).")]
    [Min(0.1f)] public float hitInterval = 0.5f;

    [Header("── Orbital Settings ──")]
    [Tooltip("Bán kính quỹ đạo quay quanh player.")]
    [Min(0.1f)] public float orbitRadius = 2f;
    [Tooltip("Tốc độ quay (độ/giây).")]
    [Min(0f)] public float orbitSpeed = 180f;

    [Header("── Level Scaling ──")]
    [Tooltip("Level tối đa của vũ khí.")]
    [Min(1)] public int maxLevel = 5;
    [Tooltip("Bonus ATK cộng thêm mỗi level.")]
    [Min(0f)] public float damagePerLevel = 5f;
    [Tooltip("Giảm cooldown mỗi level (giây).")]
    [Min(0f)] public float cooldownReductionPerLevel = 0.05f;
    [Tooltip("Tăng kích thước hitbox/projectile mỗi level.")]
    [Min(0f)] public float sizePerLevel = 0.1f;
    [Tooltip("Thêm số projectile mỗi level (chỉ áp dụng Projectile type).")]
    [Min(0)] public int projCountPerLevel = 0;

    [Header("── Attack Presentation (gán sau) ──")]
    [Tooltip("Loại đòn đánh để hệ thống combat sau này chọn cách xử lý phù hợp.")]
    public WeaponAttackType attackType;
    [Tooltip("VFX như nhát chém, muzzle flash hoặc hiệu ứng phép.")]
    public GameObject attackEffectPrefab;
    [Tooltip("Prefab mũi tên/đạn. Vũ khí melee có thể để trống.")]
    public GameObject projectilePrefab;
    [Tooltip("Animation tấn công riêng của vũ khí nếu có.")]
    public AnimationClip attackAnimation;
    [Tooltip("Âm thanh phát khi vũ khí tấn công.")]
    public AudioClip attackSound;

    public WeaponStatsSnapshot GetStatsAtLevel(int level, PlayerBaseStats playerStats = null)
    {
        int validLevel = Mathf.Clamp(level, 1, maxLevel);
        int levelOffset = validLevel - 1;

        float damageMultiplier = playerStats != null ? playerStats.FinalDamageMultiplier : 1f;
        float attackSpeedMultiplier = playerStats != null ? playerStats.FinalAttackSpeedMultiplier : 1f;
        float sizeMultiplier = playerStats != null ? playerStats.FinalWeaponSizeMultiplier : 1f;
        int bonusProjectiles = playerStats != null ? playerStats.bonusProjCountFlat : 0;

        return new WeaponStatsSnapshot
        {
            level = validLevel,
            damage = (atk + levelOffset * damagePerLevel) * damageMultiplier,
            cooldown = Mathf.Max(0.05f, cooldown - levelOffset * cooldownReductionPerLevel) / attackSpeedMultiplier,
            size = (size + levelOffset * sizePerLevel) * sizeMultiplier,
            projectileCount = Mathf.Max(1, projectileCount + levelOffset * projCountPerLevel + bonusProjectiles)
        };
    }

    private void OnValidate()
    {
        id = id != null ? id.Trim() : string.Empty;
        atk = Mathf.Max(0f, atk);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileCount = Mathf.Max(1, projectileCount);
        size = Mathf.Max(0.01f, size);
        cooldown = Mathf.Max(0.01f, cooldown);
        maxLevel = Mathf.Max(1, maxLevel);
    }
}
