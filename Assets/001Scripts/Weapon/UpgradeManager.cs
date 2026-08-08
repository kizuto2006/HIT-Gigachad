using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private PlayerTomeInventory tomeInventory;
    [SerializeField] private PlayerItemInventory itemInventory;
    [SerializeField] private GameObject upgradeUIPrefab;

    [Header("Weapon Pool")]
    [SerializeField] private WeaponData[] allWeapons;
    [SerializeField] private TomeData[] allTomes;
    [SerializeField] private ItemData[] allItems;

    [Header("Settings")]
    [SerializeField, Min(1)] private int optionsPerLevel = 3;
    [SerializeField] private bool pauseGameWhileChoosing = true;


    [Header("Upgrade Rarity")]
    [SerializeField, Min(0f)] private float commonRarityWeight = 59f;
    [SerializeField, Min(0f)] private float uncommonRarityWeight = 25f;
    [SerializeField, Min(0f)] private float rareRarityWeight = 12f;
    [SerializeField, Min(0f)] private float epicRarityWeight = 3f;
    [SerializeField, Min(0f)] private float legendaryRarityWeight = 1f;

    public event Action<List<UpgradeOption>> OnShowUpgradeUI;
        public event Action OnHideUpgradeUI;
    public event Action UtilityChargesChanged;

    private readonly Queue<UpgradeRequest> pendingRequests = new Queue<UpgradeRequest>();
    private readonly List<UpgradeOption> currentOptions = new List<UpgradeOption>();
    private bool isShowingUpgrade;
        private int currentOptionLimit = 3;
    private int rerollCharges;
    private int skipCharges;
    private int removeCharges;

    public IReadOnlyList<UpgradeOption> CurrentOptions => currentOptions;
    public bool IsShowingUpgrade => isShowingUpgrade;
    public bool IsChestReward { get; private set; }
    public string CurrentTitle { get; private set; } = "LÊN CẤP";

private void Awake()
    {
        ResetUtilityCharges();
        if (upgradeUIPrefab != null && FindFirstObjectByType<UpgradeUI>() == null)
            Instantiate(upgradeUIPrefab);

        if (xpSystem == null)
            xpSystem = GetComponentInChildren<XPSystem>(true);
        if (xpSystem == null)
            xpSystem = FindFirstObjectByType<XPSystem>();
        if (weaponController == null)
            weaponController = GetComponentInChildren<WeaponController>(true);
        if (weaponController == null)
            weaponController = FindFirstObjectByType<WeaponController>();
        if (tomeInventory == null)
            tomeInventory = GetComponentInChildren<PlayerTomeInventory>(true);
        if (tomeInventory == null)
            tomeInventory = FindFirstObjectByType<PlayerTomeInventory>();
        if (itemInventory == null)
            itemInventory = GetComponentInChildren<PlayerItemInventory>(true);
        if (itemInventory == null)
            itemInventory = FindFirstObjectByType<PlayerItemInventory>();

        if (allWeapons == null || allWeapons.Length == 0)
            allWeapons = Resources.LoadAll<WeaponData>("Weapons");
        if (allTomes == null || allTomes.Length == 0)
            allTomes = Resources.LoadAll<TomeData>("Tomes");
        if (allItems == null || allItems.Length == 0)
            allItems = Resources.LoadAll<ItemData>("Items");

        if (xpSystem == null)
            Debug.LogError("[UpgradeManager] Cannot find XPSystem in the active scene.", this);
        if (weaponController == null)
            Debug.LogError("[UpgradeManager] Cannot find WeaponController in the active scene.", this);
        if (itemInventory == null)
            Debug.LogWarning("[UpgradeManager] Cannot find PlayerItemInventory; chest rewards will be unavailable.", this);
    }

    private void OnEnable()
    {
        if (xpSystem != null)
            xpSystem.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (xpSystem != null)
            xpSystem.OnLevelUp -= HandleLevelUp;

        if (isShowingUpgrade && pauseGameWhileChoosing)
            Time.timeScale = 1f;
    }

private void HandleLevelUp(int newLevel)
    {
        pendingRequests.Enqueue(new UpgradeRequest(
            optionsPerLevel,
            "LÊN CẤP",
            false));
        if (!isShowingUpgrade)
            ShowNextUpgrade();
    }

public bool RequestChestReward(int optionCount = 3, string title = "RƯƠNG PHẦN THƯỞNG")
    {
        if (!CanRequestChestReward(optionCount))
            return false;

        pendingRequests.Enqueue(new UpgradeRequest(
            Mathf.Max(1, optionCount),
            string.IsNullOrWhiteSpace(title) ? "RƯƠNG PHẦN THƯỞNG" : title,
            true));

        if (!isShowingUpgrade)
            ShowNextUpgrade();

        return isShowingUpgrade || pendingRequests.Count > 0;
    }

public bool CanRequestChestReward(int optionCount = 3)
    {
        return itemInventory != null && GetEligibleChestItems().Count > 0;
    }


    private void ShowNextUpgrade()
    {
        if (pendingRequests.Count == 0)
        {
            CloseUpgradeSession();
            return;
        }

        UpgradeRequest request = pendingRequests.Dequeue();
        currentOptionLimit = request.optionCount;
        CurrentTitle = request.title;
        IsChestReward = request.isChestReward;
        BuildCurrentOptions(currentOptionLimit, request.isChestReward);

        if (currentOptions.Count == 0)
        {
            ShowNextUpgrade();
            return;
        }

        isShowingUpgrade = true;
        if (pauseGameWhileChoosing)
            Time.timeScale = 0f;

        if (OnShowUpgradeUI == null)
        {
            Debug.LogWarning("[UpgradeManager] Reward options generated before UI subscription; keeping them pending.");
            return;
        }

        OnShowUpgradeUI.Invoke(new List<UpgradeOption>(currentOptions));
    }

    public bool SelectOption(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= currentOptions.Count)
            return false;

        return SelectOption(currentOptions[optionIndex]);
    }

    public bool SelectOption(UpgradeOption option)
    {
        if (!isShowingUpgrade || option == null || !currentOptions.Contains(option))
            return false;

        bool applied;
        if (option.IsWeapon)
            applied = weaponController != null
                && weaponController.AddOrUpgradeWeapon(
                    option.weapon,
                    option.RarityMultiplier);
        else if (option.IsTome)
            applied = tomeInventory != null
                && tomeInventory.AddOrUpgradeTome(
                    option.tome,
                    option.RarityMultiplier);
        else if (option.IsItem)
            applied = itemInventory != null && itemInventory.AddOrUpgradeItem(option.item);
        else
            applied = false;

        if (!applied)
            return false;

        FinishCurrentChoice();
        return true;
    }

public bool SkipUpgrade()
    {
        if (!CanUseSkip)
            return false;

        skipCharges--;
        NotifyUtilityChargesChanged();
        FinishCurrentChoice();
        return true;
    }

public bool RerollOptions()
    {
        if (!CanUseReroll)
            return false;

        BuildCurrentOptions(currentOptionLimit, false);
        if (currentOptions.Count == 0)
            return false;

        rerollCharges--;
        NotifyUtilityChargesChanged();
        OnShowUpgradeUI?.Invoke(new List<UpgradeOption>(currentOptions));
        return true;
    }

public List<UpgradeOption> PreviewAvailableOptions()
    {
        BuildCurrentOptions(optionsPerLevel, false);
        return new List<UpgradeOption>(currentOptions);
    }

    private void FinishCurrentChoice()
    {
        OnHideUpgradeUI?.Invoke();
        isShowingUpgrade = false;
        currentOptions.Clear();
        ShowNextUpgrade();
    }

private void CloseUpgradeSession()
    {
        isShowingUpgrade = false;
        currentOptions.Clear();
        currentOptionLimit = optionsPerLevel;
        CurrentTitle = "LÊN CẤP";
        IsChestReward = false;
        if (pauseGameWhileChoosing)
            Time.timeScale = 1f;
    }

    private void BuildCurrentOptions(int maximumOptions, bool chestReward)
    {
        currentOptions.Clear();
        if (chestReward)
        {
            BuildChestItemOptions(maximumOptions);
            return;
        }

        WeaponInventory inventory = weaponController != null ? weaponController.Inventory : null;
        if (inventory != null)
        {
            foreach (WeaponBehaviour equipped in inventory.EquippedWeapons)
            {
                if (equipped != null && equipped.data != null && !equipped.IsMaxLevel)
                    currentOptions.Add(UpgradeOption.CreateWeaponLevelUp(equipped));
            }

            if (!inventory.IsFull && allWeapons != null)
            {
                foreach (WeaponData weapon in allWeapons)
                {
                    if (weapon != null && !inventory.HasWeapon(weapon))
                        currentOptions.Add(UpgradeOption.CreateNewWeapon(
                            weapon,
                            inventory.playerStats));
                }
            }
        }

        if (tomeInventory != null)
        {
            foreach (TomeLevelState state in tomeInventory.OwnedTomes)
            {
                if (state != null && state.tome != null && state.level < state.tome.maxLevel)
                {
                    currentOptions.Add(UpgradeOption.CreateTomeLevelUp(
                        state.tome,
                        state.level,
                        state.extraBonus));
                }
            }

            if (!tomeInventory.IsFull && allTomes != null)
            {
                foreach (TomeData tome in allTomes)
                {
                    if (tome != null && tomeInventory.GetLevel(tome) == 0)
                        currentOptions.Add(UpgradeOption.CreateNewTome(tome));
                }
            }
        }

        for (int i = 0; i < currentOptions.Count; i++)
            currentOptions[i].ApplyRarity(RollUpgradeRarity());

        Shuffle(currentOptions);
        int validMaximum = Mathf.Max(1, maximumOptions);
        if (currentOptions.Count > validMaximum)
            currentOptions.RemoveRange(validMaximum, currentOptions.Count - validMaximum);
    }

    private WeaponRarity RollUpgradeRarity()
    {
        float common = Mathf.Max(0f, commonRarityWeight);
        float uncommon = Mathf.Max(0f, uncommonRarityWeight);
        float rare = Mathf.Max(0f, rareRarityWeight);
        float epic = Mathf.Max(0f, epicRarityWeight);
        float legendary = Mathf.Max(0f, legendaryRarityWeight);
        float total = common + uncommon + rare + epic + legendary;

        if (total <= 0f)
            return WeaponRarity.Common;

        float roll = UnityEngine.Random.value * total;
        if ((roll -= common) < 0f)
            return WeaponRarity.Common;
        if ((roll -= uncommon) < 0f)
            return WeaponRarity.Uncommon;
        if ((roll -= rare) < 0f)
            return WeaponRarity.Rare;
        if ((roll -= epic) < 0f)
            return WeaponRarity.Epic;
        return WeaponRarity.Legendary;
    }


    private void BuildChestItemOptions(int maximumOptions)
    {
        List<ItemData> pool = GetEligibleChestItems();
        int optionCount = Mathf.Min(Mathf.Max(1, maximumOptions), pool.Count);

        for (int i = 0; i < optionCount; i++)
        {
            ItemData item = PickWeightedItem(pool);
            if (item == null)
                break;

            pool.Remove(item);
            int currentStackCount = itemInventory != null ? itemInventory.GetStackCount(item) : 0;
            currentOptions.Add(currentStackCount > 0
                ? UpgradeOption.CreateItemStack(item, currentStackCount)
                : UpgradeOption.CreateNewItem(item));
        }
    }

    private List<ItemData> GetEligibleChestItems()
    {
        List<ItemData> eligible = new List<ItemData>();
        if (allItems == null)
            return eligible;

        for (int i = 0; i < allItems.Length; i++)
        {
            ItemData item = allItems[i];
            if (item != null && item.chestWeight > 0f)
                eligible.Add(item);
        }

        return eligible;
    }

    private static ItemData PickWeightedItem(IList<ItemData> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += Mathf.Max(0f, pool[i] != null ? pool[i].chestWeight : 0f);

        if (totalWeight <= 0f)
            return pool[UnityEngine.Random.Range(0, pool.Count)];

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < pool.Count; i++)
        {
            ItemData item = pool[i];
            roll -= item != null ? Mathf.Max(0f, item.chestWeight) : 0f;
            if (roll <= 0f)
                return item;
        }

        return pool[pool.Count - 1];
    }

private readonly struct UpgradeRequest
    {
        public readonly int optionCount;
        public readonly string title;
        public readonly bool isChestReward;

        public UpgradeRequest(int optionCount, string title, bool isChestReward)
        {
            this.optionCount = Mathf.Max(1, optionCount);
            this.title = title;
            this.isChestReward = isChestReward;
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }


public bool RemoveOption()
    {
        if (!CanUseRemove)
            return false;

        currentOptions.RemoveAt(currentOptions.Count - 1);
        removeCharges--;
        NotifyUtilityChargesChanged();
        OnShowUpgradeUI?.Invoke(new List<UpgradeOption>(currentOptions));
        return true;
    }


private void NotifyUtilityChargesChanged()
    {
        UtilityChargesChanged?.Invoke();
    }


private void ResetUtilityCharges()
    {
        rerollCharges = ShopUI.GetUtilityCharges(2);
        skipCharges = ShopUI.GetUtilityCharges(3);
        removeCharges = ShopUI.GetUtilityCharges(4);
        UtilityChargesChanged?.Invoke();
    }


public int RemainingRerollCharges => rerollCharges;

    public int RemainingSkipCharges => skipCharges;

    public int RemainingRemoveCharges => removeCharges;

    public bool CanUseReroll =>
        isShowingUpgrade && !IsChestReward && rerollCharges > 0;

    public bool CanUseSkip =>
        isShowingUpgrade && !IsChestReward && skipCharges > 0;

    public bool CanUseRemove =>
        isShowingUpgrade && !IsChestReward && removeCharges > 0 && currentOptions.Count > 1;
}
