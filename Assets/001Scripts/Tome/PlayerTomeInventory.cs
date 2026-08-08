using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TomeLevelState
{
    public TomeData tome;
    [Min(1)] public int level = 1;
    [Min(0f)] public float extraBonus;

}

public class PlayerTomeInventory : MonoBehaviour
{

    private const int MaximumSlotCount = 4;
    private int baseSlotCount;
    private bool slotCapacityInitialized;
    [SerializeField, Min(1)] private int maxSlots = 2;
    [SerializeField] private PlayerBaseStats playerStats;
    [SerializeField] private List<TomeLevelState> ownedTomes = new List<TomeLevelState>();

    public IReadOnlyList<TomeLevelState> OwnedTomes => ownedTomes;
    public int MaxSlots
    {
        get
        {
            EnsureSlotCapacity();
            return maxSlots;
        }
    }
    public bool IsFull
    {
        get
        {
            EnsureSlotCapacity();
            return ownedTomes.Count >= maxSlots;
        }
    }
    public event Action TomesChanged;

    private void Awake()
    {
        EnsureSlotCapacity();
        if (playerStats == null)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
                playerStats = health.stats;
        }

        RecalculatePlayerStats();
    }

    private void OnValidate()
    {
        maxSlots = Mathf.Clamp(maxSlots, 1, MaximumSlotCount);

        for (int i = 0; i < ownedTomes.Count; i++)
        {
            TomeLevelState state = ownedTomes[i];
            if (state != null && state.tome != null)
                state.level = Mathf.Clamp(state.level, 1, state.tome.maxLevel);
        }

        if (!Application.isPlaying)
            return;

        RecalculatePlayerStats();
        TomesChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.ClearRuntimeTomeBonuses();
    }

    public bool AddOrUpgradeTome(TomeData tome, float rarityMultiplier = 1f)
    {
        if (tome == null || playerStats == null)
            return false;

        float safeMultiplier = Mathf.Max(1f, rarityMultiplier);
        TomeLevelState state = FindState(tome);
        if (state == null)
        {
            if (IsFull)
                return false;

            float baseBonus = tome.GetBonusAtLevel(1);
            ownedTomes.Add(new TomeLevelState
            {
                tome = tome,
                level = 1,
                extraBonus = baseBonus * (safeMultiplier - 1f)
            });
        }
        else
        {
            if (state.level >= tome.maxLevel)
                return false;

            float previousBaseBonus = tome.GetBonusAtLevel(state.level);
            state.level++;
            float nextBaseBonus = tome.GetBonusAtLevel(state.level);
            state.extraBonus += (nextBaseBonus - previousBaseBonus) * (safeMultiplier - 1f);
        }

        RecalculatePlayerStats();
        TomesChanged?.Invoke();
        return true;
    }

    public int GetLevel(TomeData tome)
    {
        TomeLevelState state = FindState(tome);
        return state != null ? state.level : 0;
    }

    public bool IsMaxLevel(TomeData tome)
    {
        return tome != null && GetLevel(tome) >= tome.maxLevel;
    }

    public void RecalculatePlayerStats()
    {
        if (playerStats == null)
            return;

        float previousMaxHealth = playerStats.FinalHp;
        float damageBonus = 0f;
        float sizeBonus = 0f;
        float moveSpeedBonus = 0f;
        float maxHealthBonus = 0f;
        float armorBonus = 0f;
        float cooldownBonus = 0f;
        float projectileSpeedBonus = 0f;
        float experienceBonus = 0f;

        for (int i = ownedTomes.Count - 1; i >= 0; i--)
        {
            TomeLevelState state = ownedTomes[i];
            if (state == null || state.tome == null)
            {
                ownedTomes.RemoveAt(i);
                continue;
            }

            state.level = Mathf.Clamp(state.level, 1, state.tome.maxLevel);
            float bonus = state.tome.GetBonusAtLevel(state.level) + state.extraBonus;
            switch (state.tome.statType)
            {
                case TomeStatType.Damage:
                    damageBonus += bonus;
                    break;
                case TomeStatType.WeaponSize:
                    sizeBonus += bonus;
                    break;
                case TomeStatType.MoveSpeed:
                    moveSpeedBonus += bonus;
                    break;
                case TomeStatType.MaxHealth:
                    maxHealthBonus += bonus;
                    break;
                case TomeStatType.Armor:
                    armorBonus += bonus;
                    break;
                case TomeStatType.Cooldown:
                    cooldownBonus += bonus;
                    break;
                case TomeStatType.ProjectileSpeed:
                    projectileSpeedBonus += bonus;
                    break;
                case TomeStatType.Experience:
                    experienceBonus += bonus;
                    break;
            }
        }

        playerStats.SetRuntimeTomeBonuses(
            damageBonus,
            sizeBonus,
            moveSpeedBonus,
            maxHealthBonus,
            armorBonus,
            cooldownBonus,
            projectileSpeedBonus,
            experienceBonus);

        SyncCurrentHealth(previousMaxHealth, playerStats.FinalHp);
    }

    private void SyncCurrentHealth(float previousMaxHealth, float newMaxHealth)
    {
        PlayerHealth health = GetComponentInParent<PlayerHealth>();
        if (health == null || health.currentHp <= 0f)
            return;

        float gainedMaxHealth = Mathf.Max(0f, newMaxHealth - previousMaxHealth);
        health.Heal(gainedMaxHealth);
    }

    private TomeLevelState FindState(TomeData tome)
    {
        for (int i = 0; i < ownedTomes.Count; i++)
        {
            if (ownedTomes[i] != null && ownedTomes[i].tome == tome)
                return ownedTomes[i];
        }

        return null;
    }


    private void EnsureSlotCapacity()
    {
        if(!slotCapacityInitialized)
        {
            baseSlotCount = Mathf.Clamp(maxSlots, 1, MaximumSlotCount);
            slotCapacityInitialized = true;
        }

        if(Application.isPlaying)
            maxSlots = ShopUI.GetUnlockedSlotCount(false, baseSlotCount, MaximumSlotCount);
        else
            maxSlots = Mathf.Clamp(maxSlots, 1, MaximumSlotCount);
    }
}
