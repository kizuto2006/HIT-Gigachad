using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TomeLevelState
{
    public TomeData tome;
    [Min(1)] public int level = 1;
}

public class PlayerTomeInventory : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxSlots = 2;
    [SerializeField] private PlayerBaseStats playerStats;
    [SerializeField] private List<TomeLevelState> ownedTomes = new List<TomeLevelState>();

    public IReadOnlyList<TomeLevelState> OwnedTomes => ownedTomes;
    public int MaxSlots => maxSlots;
    public bool IsFull => ownedTomes.Count >= maxSlots;
    public event Action TomesChanged;

    private void Awake()
    {
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
        maxSlots = Mathf.Max(1, maxSlots);

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

    public bool AddOrUpgradeTome(TomeData tome)
    {
        if (tome == null || playerStats == null)
            return false;

        TomeLevelState state = FindState(tome);
        if (state == null)
        {
            if (IsFull)
                return false;

            ownedTomes.Add(new TomeLevelState { tome = tome, level = 1 });
        }
        else
        {
            if (state.level >= tome.maxLevel)
                return false;

            state.level++;
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

        float damageBonus = 0f;
        float sizeBonus = 0f;
        float moveSpeedBonus = 0f;

        for (int i = ownedTomes.Count - 1; i >= 0; i--)
        {
            TomeLevelState state = ownedTomes[i];
            if (state == null || state.tome == null)
            {
                ownedTomes.RemoveAt(i);
                continue;
            }

            state.level = Mathf.Clamp(state.level, 1, state.tome.maxLevel);
            float bonus = state.tome.GetBonusAtLevel(state.level);

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
            }
        }

        playerStats.SetRuntimeTomeBonuses(damageBonus, sizeBonus, moveSpeedBonus);
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
}
