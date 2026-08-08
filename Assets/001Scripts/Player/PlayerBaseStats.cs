using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Data/Player Base Stats")]
public class PlayerBaseStats : ScriptableObject
{
    [System.NonSerialized] private float runtimeTomeDamageBonusPct;
    [System.NonSerialized] private float runtimeTomeSizeBonusPct;
    [System.NonSerialized] private float runtimeTomeMoveSpeedBonusPct;
    [System.NonSerialized] private float runtimeTomeMaxHealthBonusPct;
    [System.NonSerialized] private float runtimeTomeArmorBonusPct;
    [System.NonSerialized] private float runtimeTomeCooldownBonusPct;
    [System.NonSerialized] private float runtimeTomeProjectileSpeedBonusPct;
    [System.NonSerialized] private float runtimeTomeExperienceBonusPct;

    // ═══════════════════════════════════════════
    //  CHARACTER DATA SOURCE
    // ═══════════════════════════════════════════
    [Header("── Character Data ──")]
    [Tooltip("Kéo CharacterData asset vào đây, rồi chuột phải > Import từ CharacterData")]
    public CharacterData characterData;

    /// <summary>
    /// Copy các base stats từ CharacterData vào các field base tương ứng.
    /// Chuột phải vào component trong Inspector → "Import từ CharacterData"
    /// </summary>
    [ContextMenu("Import từ CharacterData")]
    private void ImportFromCharacter()
    {
        if (characterData == null)
        {
            Debug.LogWarning("[PlayerBaseStats] characterData chưa được gán!");
            return;
        }

        baseHp = characterData.baseHp;
        armorReduction = characterData.baseDef;
        bonusAtkPct = characterData.damageMultiplier - 1f;
        criticalChance = characterData.criticalChance;
        criticalDamageMultiplier = characterData.criticalDamageMultiplier;
        attackSpeedMultiplier = characterData.attackSpeedMultiplier;
        bonusProjCountFlat = characterData.bonusProjectileCount;
        weaponSizeMultiplier = characterData.sizeMultiplier;
        bonusProjSpeedPct = characterData.projectileSpeedMultiplier - 1f;
        durationMultiplier = characterData.durationMultiplier;
        knockbackMultiplier = characterData.knockbackMultiplier;
        bonusSpeedPct = characterData.moveSpeedMultiplier - 1f;
        baseJumpHeight = characterData.jumpHeight;
        pickupRange = characterData.pickupRange;
        experienceMultiplier = characterData.experienceMultiplier;

        Debug.Log($"[PlayerBaseStats] Đã import stats từ '{characterData.characterName}' thành công!");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ═══════════════════════════════════════════
    //  SURVIVAL
    // ═══════════════════════════════════════════
    [Header("── Survival ──")]
    [Tooltip("Base hit points")]
    public float baseHp = 100f;
    [Tooltip("Flat bonus HP added before percentage")]
    public float bonusHpFlat = 0f;
    [Tooltip("Percentage bonus HP (0.2 = +20%)")]
    public float bonusHpPct = 0f;

    [Range(0f, 0.95f)]
    [Tooltip("Tỷ lệ giảm damage nhận vào.")]
    public float armorReduction = 0f;

    [Range(0f, 0.95f)]
    [Tooltip("Tỷ lệ né tránh damage.")]
    public float dodgeChance = 0f;

    // ═══════════════════════════════════════════
    //  OFFENSE
    // ═══════════════════════════════════════════
    [Header("── Offense ──")]
    [Tooltip("Base attack damage")]
    public float baseAtk = 10f;
    [Tooltip("Percentage bonus ATK (0.5 = +50%)")]
    public float bonusAtkPct = 0f;

    [Space(5)]
    [Range(0f, 1f)] public float criticalChance = 0.01f;
    [Min(1f)] public float criticalDamageMultiplier = 2f;
    [Min(0.01f)] public float attackSpeedMultiplier = 1f;

    [Space(5)]
    [Tooltip("Base projectile speed")]
    public float baseProjSpeed = 15f;
    [Tooltip("Percentage bonus projectile speed")]
    public float bonusProjSpeedPct = 0f;

    [Space(5)]
    [Tooltip("Base number of projectiles per shot")]
    public int baseProjCount = 1;
    [Tooltip("Flat bonus projectile count")]
    public int bonusProjCountFlat = 0;

    [Space(5)]
    [Min(0.01f)] public float weaponSizeMultiplier = 1f;
    [Min(0.01f)] public float durationMultiplier = 1f;
    [Min(0f)] public float knockbackMultiplier = 1f;

    // ═══════════════════════════════════════════
    //  MOBILITY
    // ═══════════════════════════════════════════
    [Header("── Mobility ──")]
    [Tooltip("Base movement speed")]
    public float baseSpeed = 5f;
    [Tooltip("Percentage bonus movement speed")]
    public float bonusSpeedPct = 0f;

    [Space(5)]
    [Tooltip("Base jump height imported from CharacterData")]
    public float baseJumpHeight = 2.5f;
    [Tooltip("Percentage bonus jump height (0.2 = +20%)")]
    public float bonusJumpHeightPct = 0f;

    [Header("── Collection ──")]
    [Min(0f)] public float pickupRange = 1f;
    [Min(0f)] public float experienceMultiplier = 1f;

    [Header("── Powerups ──")]
    [Range(0f, 1f)]
    [Tooltip("Base chance for an enemy to drop a temporary powerup.")]
    public float powerupDropChance = 0.01f;
    [Min(1f)]
    [Tooltip("Scales the strength and duration of active powerups.")]
    public float powerupMultiplier = 1f;
    // ═══════════════════════════════════════════
    //  COMPUTED PROPERTIES
    // ═══════════════════════════════════════════

    /// <summary>(baseHp + bonusHpFlat) * (1 + bonusHpPct)</summary>
    public float FinalHp => (baseHp + bonusHpFlat + runtimeItemMaxHealthFlat) * Mathf.Max(0f, 1f + bonusHpPct + runtimeTomeMaxHealthBonusPct + runtimeItemMaxHealthBonusPct);

    /// <summary>baseAtk * (1 + bonusAtkPct)</summary>
    public float FinalAtk => baseAtk * FinalDamageMultiplier;

    /// <summary>baseSpeed * (1 + bonusSpeedPct)</summary>
    public float FinalSpeed => baseSpeed * Mathf.Max(0f, 1f + bonusSpeedPct + runtimeTomeMoveSpeedBonusPct + runtimeItemMoveSpeedBonusPct);

    /// <summary>baseJumpHeight * (1 + bonusJumpHeightPct)</summary>
    public float FinalJumpHeight => baseJumpHeight * (1f + bonusJumpHeightPct);

    /// <summary>baseProjSpeed * (1 + bonusProjSpeedPct)</summary>
    public float FinalProjSpeed => baseProjSpeed * FinalProjectileSpeedMultiplier;

    /// <summary>baseProjCount + bonusProjCountFlat</summary>
    public int FinalProjCount => baseProjCount + bonusProjCountFlat;

    public float FinalArmorReduction => Mathf.Clamp(armorReduction + runtimeTomeArmorBonusPct, 0f, 0.95f);
    public float FinalCriticalChance => Mathf.Clamp01(criticalChance);
    public float FinalCriticalDamageMultiplier => Mathf.Max(1f, criticalDamageMultiplier + runtimeItemCriticalDamageBonus);
    public float FinalAttackSpeedMultiplier => Mathf.Max(0.01f, attackSpeedMultiplier + runtimeTomeCooldownBonusPct + runtimeItemAttackSpeedBonusPct);
    public float FinalWeaponSizeMultiplier => Mathf.Max(0.01f, weaponSizeMultiplier + runtimeTomeSizeBonusPct);
    public float FinalDurationMultiplier => Mathf.Max(0.01f, durationMultiplier);
    public float FinalKnockbackMultiplier => Mathf.Max(0f, knockbackMultiplier);
    public float FinalPickupRange => Mathf.Max(0f, pickupRange);
    public float FinalExperienceMultiplier => Mathf.Max(0f, experienceMultiplier + runtimeTomeExperienceBonusPct + runtimeItemExperienceBonusPct);
    public float FinalProjectileSpeedMultiplier => Mathf.Max(0f, 1f + bonusProjSpeedPct + runtimeTomeProjectileSpeedBonusPct);
    public float FinalBorgarDropChance => Mathf.Clamp01(runtimeItemBorgarDropChancePct);
    public float FinalLuck => Mathf.Max(0f, runtimeItemLuckPct);
    public float FinalDodgeChance => Mathf.Clamp01(dodgeChance + runtimeItemDodgeChancePct);
    public float FinalHighHealthEnemyDamageBonus => Mathf.Max(0f, runtimeItemHighHealthDamageBonusPct);
    public float FinalHighHealthEnemyDamageThreshold => Mathf.Clamp01(runtimeItemHighHealthDamageThreshold);
    public float FinalPowerupDropChance => Mathf.Clamp01(powerupDropChance);
    public float FinalPowerupMultiplier => Mathf.Max(1f, powerupMultiplier);
    public float FinalDamageMultiplier => Mathf.Max(0f, 1f + bonusAtkPct + runtimeTomeDamageBonusPct);
    public float TomeDamageBonusPct => runtimeTomeDamageBonusPct;
    public float TomeSizeBonusPct => runtimeTomeSizeBonusPct;
    public float TomeMoveSpeedBonusPct => runtimeTomeMoveSpeedBonusPct;
    public float TomeMaxHealthBonusPct => runtimeTomeMaxHealthBonusPct;
    public float TomeArmorBonusPct => runtimeTomeArmorBonusPct;
    public float TomeCooldownBonusPct => runtimeTomeCooldownBonusPct;
    public float TomeProjectileSpeedBonusPct => runtimeTomeProjectileSpeedBonusPct;
    public float TomeExperienceBonusPct => runtimeTomeExperienceBonusPct;
    [System.NonSerialized] private float runtimeItemBorgarDropChancePct;
    [System.NonSerialized] private float runtimeItemCriticalDamageBonus;
    [System.NonSerialized] private float runtimeItemLuckPct;
    [System.NonSerialized] private float runtimeItemExperienceBonusPct;
    [System.NonSerialized] private float runtimeItemMaxHealthFlat;
    [System.NonSerialized] private float runtimeItemMaxHealthBonusPct;
    [System.NonSerialized] private float runtimeItemDodgeChancePct;
    [System.NonSerialized] private float runtimeItemAttackSpeedBonusPct;
    [System.NonSerialized] private float runtimeItemHighHealthDamageBonusPct;
    [System.NonSerialized] private float runtimeItemHighHealthDamageThreshold = 0.9f;
    [System.NonSerialized] private float runtimeItemMoveSpeedBonusPct;

    public void SetRuntimeTomeBonuses(
        float damageBonusPct,
        float sizeBonusPct,
        float moveSpeedBonusPct,
        float maxHealthBonusPct,
        float armorBonusPct,
        float cooldownBonusPct,
        float projectileSpeedBonusPct,
        float experienceBonusPct)
    {
        runtimeTomeDamageBonusPct = Mathf.Max(0f, damageBonusPct);
        runtimeTomeSizeBonusPct = Mathf.Max(0f, sizeBonusPct);
        runtimeTomeMoveSpeedBonusPct = Mathf.Max(0f, moveSpeedBonusPct);
        runtimeTomeMaxHealthBonusPct = Mathf.Max(0f, maxHealthBonusPct);
        runtimeTomeArmorBonusPct = Mathf.Max(0f, armorBonusPct);
        runtimeTomeCooldownBonusPct = Mathf.Max(0f, cooldownBonusPct);
        runtimeTomeProjectileSpeedBonusPct = Mathf.Max(0f, projectileSpeedBonusPct);
        runtimeTomeExperienceBonusPct = Mathf.Max(0f, experienceBonusPct);
    }

    public void ClearRuntimeTomeBonuses()
    {
        SetRuntimeTomeBonuses(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
    }

    public void SetRuntimeItemBonuses(
        float borgarDropChancePct,
        float criticalDamageBonus,
        float luckPct,
        float experienceBonusPct,
        float maxHealthFlat,
        float maxHealthBonusPct,
        float dodgeChancePct,
        float attackSpeedBonusPct,
        float highHealthDamageBonusPct,
        float highHealthDamageThreshold,
        float moveSpeedBonusPct)
    {
        runtimeItemBorgarDropChancePct = Mathf.Max(0f, borgarDropChancePct);
        runtimeItemCriticalDamageBonus = Mathf.Max(0f, criticalDamageBonus);
        runtimeItemLuckPct = Mathf.Max(0f, luckPct);
        runtimeItemExperienceBonusPct = Mathf.Max(0f, experienceBonusPct);
        runtimeItemMaxHealthFlat = Mathf.Max(0f, maxHealthFlat);
        runtimeItemMaxHealthBonusPct = Mathf.Max(0f, maxHealthBonusPct);
        runtimeItemDodgeChancePct = Mathf.Max(0f, dodgeChancePct);
        runtimeItemAttackSpeedBonusPct = Mathf.Max(0f, attackSpeedBonusPct);
        runtimeItemHighHealthDamageBonusPct = Mathf.Max(0f, highHealthDamageBonusPct);
        runtimeItemHighHealthDamageThreshold = Mathf.Clamp01(highHealthDamageThreshold);
        runtimeItemMoveSpeedBonusPct = Mathf.Max(0f, moveSpeedBonusPct);
    }

    public void ClearRuntimeItemBonuses()
    {
        SetRuntimeItemBonuses(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.9f, 0f);
    }


}
