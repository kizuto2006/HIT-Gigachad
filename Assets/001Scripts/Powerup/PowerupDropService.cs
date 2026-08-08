using System.Collections.Generic;
using UnityEngine;

public static class PowerupDropService
{
    private const float DefaultDropChance = 0.01f;
    private const float EliteDropChanceMultiplier = 3f;

    private static PowerupData[] cachedPowerups;

    public static bool TryDrop(Vector3 position, bool isElite, bool isBoss = false)
    {
        PowerupData data = SelectPowerup(isElite, isBoss);
        if (data == null)
            return false;

        PlayerBaseStats stats = ResolvePlayerStats();
        float dropChance = stats != null
            ? stats.FinalPowerupDropChance
            : DefaultDropChance;

        if (isBoss)
            dropChance = 1f;
        else if (isElite)
            dropChance *= EliteDropChanceMultiplier;

        if (Random.value >= Mathf.Clamp01(dropChance))
            return false;

        return PowerupPickup.Spawn(position, data) != null;
    }

    public static PowerupPickup SpawnRandom(Vector3 position, bool isElite = false, bool isBoss = false)
    {
        PowerupData data = SelectPowerup(isElite, isBoss);
        return data != null ? PowerupPickup.Spawn(position, data) : null;
    }

    private static PowerupData SelectPowerup(bool isElite, bool isBoss)
    {
        EnsurePowerupsLoaded();
        if (cachedPowerups == null || cachedPowerups.Length == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < cachedPowerups.Length; i++)
        {
            PowerupData data = cachedPowerups[i];
            if (!CanDrop(data, isElite, isBoss))
                continue;

            totalWeight += Mathf.Max(0.01f, data.dropWeight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;
        for (int i = 0; i < cachedPowerups.Length; i++)
        {
            PowerupData data = cachedPowerups[i];
            if (!CanDrop(data, isElite, isBoss))
                continue;

            roll -= Mathf.Max(0.01f, data.dropWeight);
            if (roll <= 0f)
                return data;
        }

        return cachedPowerups[cachedPowerups.Length - 1];
    }

    private static bool CanDrop(PowerupData data, bool isElite, bool isBoss)
    {
        if (data == null)
            return false;

        if (isBoss)
            return data.canDropFromBoss;

        return isElite
            ? data.canDropFromElite
            : data.canDropFromNormalEnemy;
    }

    private static void EnsurePowerupsLoaded()
    {
        if (cachedPowerups == null)
            cachedPowerups = Resources.LoadAll<PowerupData>("Powerups");
    }

    private static PlayerBaseStats ResolvePlayerStats()
    {
        if (PlayerPowerupController.ActiveInstance != null &&
            PlayerPowerupController.ActiveInstance.Stats != null)
        {
            return PlayerPowerupController.ActiveInstance.Stats;
        }

        PlayerHealth playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        return playerHealth != null ? playerHealth.stats : null;
    }
}
