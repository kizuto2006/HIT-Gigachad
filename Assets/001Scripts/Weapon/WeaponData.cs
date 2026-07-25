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

[System.Flags]
public enum AutomaticWeaponUpgradeStats
{
    Damage = 1 << 0,
    Size = 1 << 1,
    ProjectileSpeed = 1 << 2,
    Cooldown = 1 << 3,
    Knockback = 1 << 4,
    All = Damage | Size | ProjectileSpeed | Cooldown | Knockback
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

    [Tooltip("Hệ số damage của mỗi nhát/projectile phụ sau nhát đầu tiên.")]
    [Range(0f, 1f)] public float additionalProjectileDamageMultiplier = 1f;
    [Min(1)] public int projectileCount = 1;
    [Tooltip("Hệ số kích thước hitbox hoặc projectile.")]
    [Min(0.01f)] public float size = 1f;
    [Tooltip("Hiển thị Size trên thẻ nâng cấp dưới dạng phần trăm so với kích thước gốc.")]
    public bool displaySizeAsPercent;
    [Tooltip("Thời gian chờ giữa hai lần đánh, tính bằng giây.")]
    [Min(0.01f)] public float cooldown = 0.5f;
    [Tooltip("Giới hạn attack-speed multiplier riêng của vũ khí. 0 = không giới hạn.")]
    [Min(0f)] public float maxAttackSpeedMultiplier;

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

    [Header("── Automatic Level Upgrades ──")]
    [Tooltip("Tự động phân phối 1–2 chỉ số mỗi cấp, không cần cấu hình từng level.")]
    public bool useAutomaticLevelUpgrades;
    [Tooltip("Các chỉ số được phép xuất hiện trong vòng nâng cấp tự động.")]
    public AutomaticWeaponUpgradeStats automaticUpgradeStats = AutomaticWeaponUpgradeStats.All;
    [Min(0f)] public float automaticDamageBonus = 2.5f;
    [Min(0f)] public float automaticSizeBonus = 0.12f;
    [Min(0f)] public float automaticProjectileSpeedBonus = 0.75f;
    [Min(0f)] public float automaticCooldownReduction = 0.035f;
    [Min(0f)] public float automaticKnockbackBonus = 0.12f;
    [Tooltip("Cứ bao nhiêu cấp thì nhận thêm chỉ số thứ hai.")]
    [Min(2)] public int automaticSecondStatInterval = 3;
    [Tooltip("Random việc nhận thêm chỉ số thứ hai thay vì dùng mốc level cố định.")]
    public bool randomizeAutomaticSecondStat;
    [Tooltip("Xác suất một level nhận thêm chỉ số thứ hai. Kết quả ổn định theo weapon và level.")]
    [Range(0f, 1f)] public float automaticSecondStatChance = 0.5f;
    [Tooltip("Khoảng cách giữa các lần tăng projectile count sau cấp 2.")]
    [Min(2)] public int automaticProjectileCountInterval = 10;
    [Tooltip("Tắt các mốc tự tăng projectile count nhưng vẫn giữ bonus projectile từ Player.")]
    public bool disableAutomaticProjectileCountUpgrades;
    [Min(1)] public int automaticMaxProjectileCount = 4;
    [Tooltip("Bảo đảm vũ khí đạt ít nhất 2 projectile ngay khi lên cấp 2.")]
    public bool grantSecondProjectileAtLevel2 = true;

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

        float levelDamageBonus = levelOffset * damagePerLevel;
        float levelCooldownReduction = levelOffset * cooldownReductionPerLevel;
        float levelSizeBonus = levelOffset * sizePerLevel;
        float levelProjectileSpeedBonus = 0f;
        int levelProjectileCountBonus = levelOffset * projCountPerLevel;
        float levelKnockbackBonus = 0f;

        if (useAutomaticLevelUpgrades)
        {
            levelDamageBonus = 0f;
            levelCooldownReduction = 0f;
            levelSizeBonus = 0f;

            int automaticStatCount = GetAutomaticStatCount();
            levelProjectileCountBonus = 0;

            for (int targetLevel = 2; targetLevel <= validLevel; targetLevel++)
            {
                if (targetLevel == 2 && grantSecondProjectileAtLevel2 && projectileCount + levelProjectileCountBonus < 2)
                {
                    levelProjectileCountBonus += 2 - (projectileCount + levelProjectileCountBonus);
                    continue;
                }

                int primarySequenceIndex = 0;
                if (automaticStatCount > 0)
                {
                    int firstAutomaticStatLevel = grantSecondProjectileAtLevel2 ? 3 : 2;
                    primarySequenceIndex = (targetLevel - firstAutomaticStatLevel) % automaticStatCount;
                    int primaryStat = GetAutomaticStatAt(primarySequenceIndex);
                    ApplyAutomaticStat(
                        primaryStat,
                        ref levelDamageBonus,
                        ref levelCooldownReduction,
                        ref levelSizeBonus,
                        ref levelProjectileSpeedBonus,
                        ref levelKnockbackBonus);
                }

                bool projectileCountDue = !disableAutomaticProjectileCountUpgrades
                    && automaticProjectileCountInterval > 0
                    && (targetLevel - 2) % automaticProjectileCountInterval == 0
                    && projectileCount + levelProjectileCountBonus < automaticMaxProjectileCount;

                if (projectileCountDue)
                {
                    levelProjectileCountBonus++;
                }
                bool secondStatDue = randomizeAutomaticSecondStat
                    ? RollAutomaticSecondStat(targetLevel)
                    : automaticSecondStatInterval > 0
                        && (targetLevel - 2) % automaticSecondStatInterval == 0;

                if (!projectileCountDue && automaticStatCount > 1 && secondStatDue)
                {
                    int secondaryOffset = Mathf.Min(2, automaticStatCount - 1);
                    int secondaryStat = GetAutomaticStatAt((primarySequenceIndex + secondaryOffset) % automaticStatCount);
                    ApplyAutomaticStat(
                        secondaryStat,
                        ref levelDamageBonus,
                        ref levelCooldownReduction,
                        ref levelSizeBonus,
                        ref levelProjectileSpeedBonus,
                        ref levelKnockbackBonus);
                }
            }
        }

        float damageMultiplier = playerStats != null ? playerStats.FinalDamageMultiplier : 1f;
        float attackSpeedMultiplier = playerStats != null ? playerStats.FinalAttackSpeedMultiplier : 1f;
        if (maxAttackSpeedMultiplier > 0f)
            attackSpeedMultiplier = Mathf.Min(attackSpeedMultiplier, maxAttackSpeedMultiplier);
        float sizeMultiplier = playerStats != null ? playerStats.FinalWeaponSizeMultiplier : 1f;
        float projectileSpeedMultiplier = playerStats != null ? 1f + playerStats.bonusProjSpeedPct : 1f;
        float knockbackMultiplier = playerStats != null ? playerStats.FinalKnockbackMultiplier : 1f;

        int bonusProjectiles = playerStats != null ? playerStats.bonusProjCountFlat : 0;
        int finalProjectileCount = projectileCount + levelProjectileCountBonus + bonusProjectiles;
        if (useAutomaticLevelUpgrades && automaticMaxProjectileCount > 0)
            finalProjectileCount = Mathf.Min(finalProjectileCount, automaticMaxProjectileCount);

        return new WeaponStatsSnapshot
        {
            level = validLevel,
            damage = (atk + levelDamageBonus) * damageMultiplier,
            cooldown = Mathf.Max(0.05f, cooldown - levelCooldownReduction) / attackSpeedMultiplier,
            size = Mathf.Max(0.01f, size + levelSizeBonus) * sizeMultiplier,
            projectileSpeed = Mathf.Max(0f, projectileSpeed + levelProjectileSpeedBonus) * projectileSpeedMultiplier,
            projectileCount = Mathf.Max(1, finalProjectileCount),
            knockback = Mathf.Max(0f, knockback + levelKnockbackBonus) * knockbackMultiplier
        };
    }

    private void ApplyAutomaticStat(
        int statIndex,
        ref float damageBonus,
        ref float cooldownReduction,
        ref float sizeBonus,
        ref float projectileSpeedBonus,
        ref float knockbackBonus)
    {
        switch (statIndex)
        {
            case 0:
                damageBonus += automaticDamageBonus;
                break;
            case 1:
                sizeBonus += automaticSizeBonus;
                break;
            case 2:
                projectileSpeedBonus += automaticProjectileSpeedBonus;
                break;
            case 3:
                cooldownReduction += automaticCooldownReduction;
                break;
            case 4:
                knockbackBonus += automaticKnockbackBonus;
                break;
        }
    }

    private bool RollAutomaticSecondStat(int targetLevel)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string stableId = string.IsNullOrEmpty(id) ? weaponName : id;
            if (!string.IsNullOrEmpty(stableId))
            {
                for (int i = 0; i < stableId.Length; i++)
                {
                    hash ^= stableId[i];
                    hash *= 16777619u;
                }
            }

            hash ^= (uint)targetLevel;
            hash *= 16777619u;
            hash ^= hash >> 16;
            float roll = (hash & 0x00FFFFFFu) / 16777216f;
            return roll < automaticSecondStatChance;
        }
    }


    private void OnValidate()
    {
        id = id != null ? id.Trim() : string.Empty;
        atk = Mathf.Max(0f, atk);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileCount = Mathf.Max(1, projectileCount);
        size = Mathf.Max(0.01f, size);

        additionalProjectileDamageMultiplier = Mathf.Clamp01(additionalProjectileDamageMultiplier);
        maxAttackSpeedMultiplier = Mathf.Max(0f, maxAttackSpeedMultiplier);
        cooldown = Mathf.Max(0.01f, cooldown);
        maxLevel = Mathf.Max(1, maxLevel);
        automaticSecondStatInterval = Mathf.Max(2, automaticSecondStatInterval);
        automaticSecondStatChance = Mathf.Clamp01(automaticSecondStatChance);
        automaticProjectileCountInterval = Mathf.Max(2, automaticProjectileCountInterval);
        automaticMaxProjectileCount = Mathf.Max(projectileCount, automaticMaxProjectileCount);
    }


    private int GetAutomaticStatCount()
    {
        int count = 0;
        for (int statIndex = 0; statIndex < 5; statIndex++)
        {
            AutomaticWeaponUpgradeStats stat = (AutomaticWeaponUpgradeStats)(1 << statIndex);
            if ((automaticUpgradeStats & stat) != 0)
                count++;
        }

        return count;
    }

    private int GetAutomaticStatAt(int sequenceIndex)
    {
        int currentIndex = 0;
        for (int statIndex = 0; statIndex < 5; statIndex++)
        {
            AutomaticWeaponUpgradeStats stat = (AutomaticWeaponUpgradeStats)(1 << statIndex);
            if ((automaticUpgradeStats & stat) == 0)
                continue;

            if (currentIndex == sequenceIndex)
                return statIndex;

            currentIndex++;
        }

        return 0;
    }
}
