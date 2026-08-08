using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPowerupController : MonoBehaviour
{
    public static PlayerPowerupController ActiveInstance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerBaseStats playerStats;
    [SerializeField] private PowerupVfxController vfxController;

    private readonly List<PowerupRuntimeState> activePowerups = new List<PowerupRuntimeState>(8);

    public event Action PowerupsChanged;
    public IReadOnlyList<PowerupRuntimeState> ActivePowerups => activePowerups;
    public PlayerBaseStats Stats => playerStats;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (ActiveInstance == null || ActiveInstance == this)
            ActiveInstance = this;
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;

        ClearAllPowerups();
    }

    private void Update()
    {
        bool changed = false;

        for (int i = activePowerups.Count - 1; i >= 0; i--)
        {
            PowerupRuntimeState state = activePowerups[i];
            if (state == null || state.data == null)
            {
                activePowerups.RemoveAt(i);
                changed = true;
                continue;
            }

            if (state.data.duration <= 0f)
                continue;

            state.remainingDuration -= Time.deltaTime;
            if (state.remainingDuration <= 0f)
            {
                activePowerups.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            NotifyStateChanged();
    }

    public bool TryApply(PowerupData data)
    {
        if (data == null)
            return false;

        ResolveReferences();

        if (data.powerupType == PowerupType.Heal)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null)
                health = GetComponentInChildren<PlayerHealth>(true);

            if (health == null)
                return false;

            health.HealToFull();
            return true;
        }

        float multiplier = GetPowerupMultiplier();
        float scaledDuration = data.GetScaledDuration(multiplier);
        PowerupRuntimeState existing = FindState(data.powerupType);

        if (existing == null)
        {
            existing = new PowerupRuntimeState
            {
                data = data,
                remainingDuration = scaledDuration,
                totalDuration = scaledDuration,
                charges = Mathf.Max(1, data.charges)
            };
            activePowerups.Add(existing);
        }
        else
        {
            switch (data.stackPolicy)
            {
                case PowerupStackPolicy.AddDuration:
                    existing.remainingDuration += scaledDuration;
                    existing.totalDuration += scaledDuration;
                    break;

                case PowerupStackPolicy.AddCharges:
                    existing.charges = Mathf.Clamp(
                        existing.charges + Mathf.Max(1, data.charges),
                        1,
                        Mathf.Max(1, data.maxCharges));
                    break;

                default:
                    existing.remainingDuration = scaledDuration;
                    existing.totalDuration = scaledDuration;
                    existing.charges = Mathf.Max(1, data.charges);
                    break;
            }
        }

        NotifyStateChanged();
        return true;
    }

    public bool TryConsumeShield()
    {
        return IsInvulnerable;
    }

    public bool IsInvulnerable => HasPowerup(PowerupType.Shield);

    public float GetMoveSpeedMultiplier()
    {
        PowerupRuntimeState state = FindState(PowerupType.SpeedUp);
        return state != null ? 1f + state.data.GetScaledMagnitude(GetPowerupMultiplier()) : 1f;
    }

    public float GetAttackSpeedMultiplier()
    {
        PowerupRuntimeState state = FindState(PowerupType.Rage);
        return state != null ? 1f + state.data.GetScaledMagnitude(GetPowerupMultiplier()) : 1f;
    }

    public float GetDamageMultiplier()
    {
        PowerupRuntimeState state = FindState(PowerupType.Rage);
        return state != null ? 1f + state.data.GetScaledMagnitude(GetPowerupMultiplier()) : 1f;
    }

    public int GetBonusGoldPerKill()
    {
        return 0;
    }

    public float GetPickupRangeMultiplier()
    {
        return 1f;
    }

    public bool HasPowerup(PowerupType type)
    {
        return FindState(type) != null;
    }

    public float GetRemainingTime(PowerupType type)
    {
        PowerupRuntimeState state = FindState(type);
        return state != null ? Mathf.Max(0f, state.remainingDuration) : 0f;
    }

    public int GetCharges(PowerupType type)
    {
        PowerupRuntimeState state = FindState(type);
        return state != null ? Mathf.Max(0, state.charges) : 0;
    }

    public void ClearAllPowerups()
    {
        if (activePowerups.Count == 0)
        {
            if (vfxController != null)
                vfxController.RefreshActivePowerups(activePowerups);
            return;
        }

        activePowerups.Clear();
        NotifyStateChanged();
    }

    private PowerupRuntimeState FindState(PowerupType type)
    {
        for (int i = 0; i < activePowerups.Count; i++)
        {
            PowerupRuntimeState state = activePowerups[i];
            if (state != null && state.data != null && state.data.powerupType == type)
                return state;
        }

        return null;
    }

    private float GetPowerupMultiplier()
    {
        return playerStats != null ? playerStats.FinalPowerupMultiplier : 1f;
    }

    private void ResolveReferences()
    {
        if (playerStats == null)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
                playerStats = health.stats;
        }

        if (vfxController == null)
            vfxController = GetComponent<PowerupVfxController>();
    }

    private void NotifyStateChanged()
    {
        if (vfxController == null)
            vfxController = GetComponent<PowerupVfxController>();

        if (vfxController != null)
            vfxController.RefreshActivePowerups(activePowerups);

        PowerupsChanged?.Invoke();
    }

    public static PlayerPowerupController FindFor(Transform player)
    {
        if (player == null)
            return null;

        PlayerPowerupController controller = player.GetComponent<PlayerPowerupController>();
        if (controller == null)
            controller = player.GetComponentInChildren<PlayerPowerupController>(true);

        return controller;
    }

    public static float GetMoveSpeedMultiplierFor(Transform player)
    {
        PlayerPowerupController controller = FindFor(player);
        return controller != null ? controller.GetMoveSpeedMultiplier() : 1f;
    }

    public static float GetAttackSpeedMultiplierFor(Transform player)
    {
        PlayerPowerupController controller = FindFor(player);
        return controller != null ? controller.GetAttackSpeedMultiplier() : 1f;
    }

    public static float GetDamageMultiplierFor(Transform player)
    {
        PlayerPowerupController controller = FindFor(player);
        return controller != null ? controller.GetDamageMultiplier() : 1f;
    }

    public static bool AreEnemyActionsFrozen =>
        ActiveInstance != null && ActiveInstance.HasPowerup(PowerupType.Stopwatch);

    public static int GetBonusGoldPerKillFor(Transform player)
    {
        PlayerPowerupController controller = FindFor(player);
        return controller != null ? controller.GetBonusGoldPerKill() : 0;
    }

    public static float GetPickupRangeMultiplierFor(Transform player)
    {
        PlayerPowerupController controller = FindFor(player);
        return controller != null ? controller.GetPickupRangeMultiplier() : 1f;
    }
}
