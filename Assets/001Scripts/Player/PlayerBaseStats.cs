using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Data/Player Base Stats")]
public class PlayerBaseStats : ScriptableObject
{
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
        baseShield = characterData.baseShield;
        baseAtk = characterData.baseAtk;
        baseProjSpeed = characterData.baseProjSpeed;
        baseProjCount = characterData.baseProjCount;
        baseSpeed = characterData.baseSpeed;
        baseJumpHeight = characterData.jumpHeight;

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

    [Space(5)]
    [Tooltip("Base shield value")]
    public float baseShield = 30f;
    [Tooltip("Flat bonus shield")]
    public float bonusShieldFlat = 0f;

    // ═══════════════════════════════════════════
    //  OFFENSE
    // ═══════════════════════════════════════════
    [Header("── Offense ──")]
    [Tooltip("Base attack damage")]
    public float baseAtk = 10f;
    [Tooltip("Percentage bonus ATK (0.5 = +50%)")]
    public float bonusAtkPct = 0f;

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

    // ═══════════════════════════════════════════
    //  COMPUTED PROPERTIES
    // ═══════════════════════════════════════════

    /// <summary>(baseHp + bonusHpFlat) * (1 + bonusHpPct)</summary>
    public float FinalHp => (baseHp + bonusHpFlat) * (1f + bonusHpPct);

    /// <summary>baseShield + bonusShieldFlat</summary>
    public float FinalShield => baseShield + bonusShieldFlat;

    /// <summary>baseAtk * (1 + bonusAtkPct)</summary>
    public float FinalAtk => baseAtk * (1f + bonusAtkPct);

    /// <summary>baseSpeed * (1 + bonusSpeedPct)</summary>
    public float FinalSpeed => baseSpeed * (1f + bonusSpeedPct);

    /// <summary>baseJumpHeight * (1 + bonusJumpHeightPct)</summary>
    public float FinalJumpHeight => baseJumpHeight * (1f + bonusJumpHeightPct);

    /// <summary>baseProjSpeed * (1 + bonusProjSpeedPct)</summary>
    public float FinalProjSpeed => baseProjSpeed * (1f + bonusProjSpeedPct);

    /// <summary>baseProjCount + bonusProjCountFlat</summary>
    public int FinalProjCount => baseProjCount + bonusProjCountFlat;
}
