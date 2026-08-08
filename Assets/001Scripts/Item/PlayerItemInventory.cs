using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemStackState
{
    public ItemData item;
    [Min(0)] public int stackCount = 0;
}

[DisallowMultipleComponent]
public sealed class PlayerItemInventory : MonoBehaviour
{
    private const float DefaultHighHealthDamageThreshold = 0.9f;
    [SerializeField] private PlayerBaseStats playerStats;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private List<ItemStackState> ownedItems = new List<ItemStackState>();

    public IReadOnlyList<ItemStackState> OwnedItems => ownedItems;
    public int ItemTypeCount => ownedItems.Count;
    public event Action ItemsChanged;

    private void Awake()
    {
        ResolveReferences();
        RecalculatePlayerStats();
    }

    private void OnValidate()
    {
        if (ownedItems == null)
            ownedItems = new List<ItemStackState>();

        for (int i = ownedItems.Count - 1; i >= 0; i--)
        {
            ItemStackState state = ownedItems[i];
            if (state == null || state.item == null || state.stackCount <= 0)
            {
                ownedItems.RemoveAt(i);
                continue;
            }

            state.stackCount = Mathf.Max(0, state.stackCount);
        }

        if (!Application.isPlaying)
            return;

        ResolveReferences();
        RecalculatePlayerStats();
        ItemsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.ClearRuntimeItemBonuses();
    }

    public int GetStackCount(ItemData item)
    {
        ItemStackState state = FindState(item);
        return state != null ? state.stackCount : 0;
    }

    public bool AddOrUpgradeItem(ItemData item)
    {
        if (item == null)
            return false;

        ResolveReferences();
        ItemStackState state = FindState(item);
        if (state == null)
        {
            state = new ItemStackState
            {
                item = item,
                stackCount = 1
            };
            ownedItems.Add(state);
        }
        else
        {
            if (state.stackCount == int.MaxValue)
                return false;

            state.stackCount++;
        }

        RecalculatePlayerStats();

        if (item.effectType == ItemEffectType.Healing && playerHealth != null)
            playerHealth.Heal(item.GetValueAtStackCount(1));

        ItemsChanged?.Invoke();
        return true;
    }

    public void RecalculatePlayerStats()
    {
        ResolveReferences();
        if (playerStats == null)
            return;

        float previousMaxHealth = playerStats.FinalHp;
        float borgarDropChance = 0f;
        float criticalDamage = 0f;
        float luck = 0f;
        float experience = 0f;
        float maxHealthFlat = 0f;
        float maxHealthPercent = 0f;
        float dodgeChance = 0f;
        float attackSpeed = 0f;
        float highHealthDamage = 0f;
        float highHealthDamageThreshold = DefaultHighHealthDamageThreshold;
        bool hasHighHealthDamage = false;
        float moveSpeed = 0f;

        for (int i = ownedItems.Count - 1; i >= 0; i--)
        {
            ItemStackState state = ownedItems[i];
            if (state == null || state.item == null || state.stackCount <= 0)
            {
                ownedItems.RemoveAt(i);
                continue;
            }

            float totalValue = state.item.GetValueAtStackCount(state.stackCount);
            switch (state.item.effectType)
            {
                case ItemEffectType.BorgarDropChance:
                    borgarDropChance += totalValue;
                    break;
                case ItemEffectType.CriticalDamage:
                    criticalDamage += totalValue;
                    break;
                case ItemEffectType.Luck:
                    luck += totalValue;
                    break;
                case ItemEffectType.ExperienceGain:
                    experience += totalValue;
                    break;
                case ItemEffectType.MaximumHealth:
                    if (state.item.valueType == ItemValueType.Flat)
                        maxHealthFlat += totalValue;
                    else
                        maxHealthPercent += totalValue;
                    break;
                case ItemEffectType.DodgeChance:
                    dodgeChance += totalValue;
                    break;
                case ItemEffectType.AttackSpeed:
                    attackSpeed += totalValue;
                    break;
                case ItemEffectType.HighHealthEnemyDamage:
                {
                    highHealthDamage += totalValue;
                    float itemThreshold = state.item.conditionThreshold > 0f
                        ? state.item.conditionThreshold
                        : DefaultHighHealthDamageThreshold;
                    highHealthDamageThreshold = hasHighHealthDamage
                        ? Mathf.Min(highHealthDamageThreshold, itemThreshold)
                        : itemThreshold;
                    hasHighHealthDamage = true;
                    break;
                }
                case ItemEffectType.MovementSpeed:
                    moveSpeed += totalValue;
                    break;
                case ItemEffectType.Healing:
                    break;
            }
        }

        playerStats.SetRuntimeItemBonuses(
            borgarDropChance,
            criticalDamage,
            luck,
            experience,
            maxHealthFlat,
            maxHealthPercent,
            dodgeChance,
            attackSpeed,
            highHealthDamage,
            highHealthDamageThreshold,
            moveSpeed);

        SyncCurrentHealth(previousMaxHealth, playerStats.FinalHp);
    }

    private void SyncCurrentHealth(float previousMaxHealth, float newMaxHealth)
    {
        if (playerHealth == null || playerHealth.currentHp <= 0f)
            return;

        float gainedMaxHealth = Mathf.Max(0f, newMaxHealth - previousMaxHealth);
        playerHealth.Heal(gainedMaxHealth);
    }

    private ItemStackState FindState(ItemData item)
    {
        if (item == null)
            return null;

        for (int i = 0; i < ownedItems.Count; i++)
        {
            ItemStackState state = ownedItems[i];
            if (state != null && state.item == item)
                return state;
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = GetComponentInChildren<PlayerHealth>(true);

        if (playerStats == null && playerHealth != null)
            playerStats = playerHealth.stats;
    }
}
