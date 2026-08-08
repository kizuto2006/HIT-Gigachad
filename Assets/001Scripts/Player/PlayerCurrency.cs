using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCurrency : MonoBehaviour
{
    private const string PersistentCoinsKey = "Gigachad.PersistentCoins";
    private static bool persistentCoinsLoaded;
    private static int persistentCoins;

    public static event Action<int> PersistentCoinsChanged;

    public static int PersistentCoins
    {
        get
        {
            EnsurePersistentCoinsLoaded();
            return persistentCoins;
        }
    }
    public static PlayerCurrency Instance { get; private set; }

    [Header("Starting Progress")]

    [SerializeField, Min(0)] private int normalEnemyGold = 1;
    [SerializeField, Min(0)] private int eliteEnemyGold = 5;
    [SerializeField, Min(0)] private int startingGold;

    [Header("End Of Run Reward")]
    [SerializeField, Min(1f)] private float rewardIntervalSeconds = 60f;
    [SerializeField, Min(1)] private int minimumRunReward = 1;
    [SerializeField, Min(1)] private int maximumRunReward = 10;

    public int Gold { get; private set; }
    public int Coins => PersistentCoins;
    public int EnemiesDefeated { get; private set; }
    public int LastRunReward { get; private set; }

    public event Action<int> GoldChanged;
    public event Action<int> CoinsChanged;
    

    private bool runRewardGranted;
    public event Action<int> EnemiesDefeatedChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsurePersistentCoinsLoaded();
        Gold = Mathf.Max(0, startingGold);
        EnemiesDefeated = 0;
        LastRunReward = 0;
        runRewardGranted = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanAfford(int amount)
    {
        return amount <= 0 || Gold >= amount;
    }

    public bool TrySpend(int amount)
    {
        int validAmount = Mathf.Max(0, amount);
        if (!CanAfford(validAmount))
            return false;

        Gold -= validAmount;
        NotifyGoldChanged();
        return true;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        Gold += amount;
        NotifyGoldChanged();
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        EnsurePersistentCoinsLoaded();
        persistentCoins += amount;
        SavePersistentCoins();
        NotifyCoinsChanged();
    }

    public void RegisterEnemyDefeat(bool isElite)
    {
        EnemiesDefeated++;
        EnemiesDefeatedChanged?.Invoke(EnemiesDefeated);

        int reward = isElite ? eliteEnemyGold : normalEnemyGold;
        if (reward > 0)
            AddGold(reward);
    }

    private void NotifyGoldChanged()
    {
        GoldChanged?.Invoke(Gold);
    }

    private void NotifyCoinsChanged()
    {
        CoinsChanged?.Invoke(Coins);
        PersistentCoinsChanged?.Invoke(persistentCoins);
    }



    private static void BroadcastPersistentChange()
    {
        if (Instance != null)
        {
            Instance.NotifyCoinsChanged();
            return;
        }

        PersistentCoinsChanged?.Invoke(persistentCoins);
    }


    private static void SavePersistentCoins()
    {
        PlayerPrefs.SetInt(PersistentCoinsKey, Mathf.Max(0, persistentCoins));
        PlayerPrefs.Save();
    }


    private static void EnsurePersistentCoinsLoaded(int fallback = 0)
    {
        if (persistentCoinsLoaded)
            return;

        persistentCoins = PlayerPrefs.GetInt(PersistentCoinsKey, Mathf.Max(0, fallback));
        persistentCoinsLoaded = true;
        SavePersistentCoins();
    }


    public int CalculateRunReward(float survivalSeconds)
    {
        float interval = Mathf.Max(1f, rewardIntervalSeconds);
        int minimum = Mathf.Max(1, minimumRunReward);
        int maximum = Mathf.Max(minimum, maximumRunReward);
        int reward = Mathf.CeilToInt(Mathf.Max(0f, survivalSeconds) / interval);
        return Mathf.Clamp(reward, minimum, maximum);
    }


    public int AwardRunReward(float survivalSeconds)
    {
        if (runRewardGranted)
            return 0;

        runRewardGranted = true;
        int reward = CalculateRunReward(survivalSeconds);
        LastRunReward = reward;
        AddCoins(reward);
        return reward;
    }


    public static bool TrySpendPersistentCoins(int amount)
    {
        EnsurePersistentCoinsLoaded();
        int validAmount = Mathf.Max(0, amount);
        if (persistentCoins < validAmount)
            return false;

        persistentCoins -= validAmount;
        SavePersistentCoins();
        BroadcastPersistentChange();
        return true;
    }
}
