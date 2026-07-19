using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private PlayerTomeInventory tomeInventory;
    [SerializeField] private GameObject upgradeUIPrefab;

    [Header("Weapon Pool")]
    [SerializeField] private WeaponData[] allWeapons;
    [SerializeField] private TomeData[] allTomes;

    [Header("Settings")]
    [SerializeField, Min(1)] private int optionsPerLevel = 3;
    [SerializeField] private bool pauseGameWhileChoosing = true;

    public event Action<List<UpgradeOption>> OnShowUpgradeUI;
    public event Action OnHideUpgradeUI;

    private readonly Queue<int> pendingLevelUps = new Queue<int>();
    private readonly List<UpgradeOption> currentOptions = new List<UpgradeOption>();
    private bool isShowingUpgrade;

    public IReadOnlyList<UpgradeOption> CurrentOptions => currentOptions;
    public bool IsShowingUpgrade => isShowingUpgrade;

    private void Awake()
    {
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
        if (allWeapons == null || allWeapons.Length == 0)
            allWeapons = Resources.LoadAll<WeaponData>("Weapons");
        if (allTomes == null || allTomes.Length == 0)
            allTomes = Resources.LoadAll<TomeData>("Tomes");

        if (xpSystem == null)
            Debug.LogError("[UpgradeManager] Cannot find XPSystem in the active scene.", this);
        if (weaponController == null)
            Debug.LogError("[UpgradeManager] Cannot find WeaponController in the active scene.", this);
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
        pendingLevelUps.Enqueue(newLevel);
        if (!isShowingUpgrade)
            ShowNextUpgrade();
    }

    private void ShowNextUpgrade()
    {
        if (pendingLevelUps.Count == 0)
        {
            CloseUpgradeSession();
            return;
        }

        pendingLevelUps.Dequeue();
        BuildCurrentOptions();

        if (currentOptions.Count == 0)
        {
            ShowNextUpgrade();
            return;
        }

        if (OnShowUpgradeUI == null)
        {
            Debug.LogWarning("[UpgradeManager] Level-up options were generated, but no UI is listening yet.");
            ShowNextUpgrade();
            return;
        }

        isShowingUpgrade = true;
        if (pauseGameWhileChoosing)
            Time.timeScale = 0f;

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

        bool applied = option.IsWeapon
            ? weaponController != null && weaponController.AddOrUpgradeWeapon(option.weapon)
            : tomeInventory != null && tomeInventory.AddOrUpgradeTome(option.tome);

        if (!applied)
            return false;

        FinishCurrentChoice();
        return true;
    }

    public void SkipUpgrade()
    {
        if (!isShowingUpgrade)
            return;

        FinishCurrentChoice();
    }

    public bool RerollOptions()
    {
        if (!isShowingUpgrade)
            return false;

        BuildCurrentOptions();
        if (currentOptions.Count == 0)
            return false;

        OnShowUpgradeUI?.Invoke(new List<UpgradeOption>(currentOptions));
        return true;
    }

    public List<UpgradeOption> PreviewAvailableOptions()
    {
        BuildCurrentOptions();
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
        if (pauseGameWhileChoosing)
            Time.timeScale = 1f;
    }

    private void BuildCurrentOptions()
    {
        currentOptions.Clear();
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
                        currentOptions.Add(UpgradeOption.CreateNewWeapon(weapon, inventory.playerStats));
                }
            }
        }

        if (tomeInventory != null)
        {
            foreach (TomeLevelState state in tomeInventory.OwnedTomes)
            {
                if (state != null && state.tome != null && state.level < state.tome.maxLevel)
                    currentOptions.Add(UpgradeOption.CreateTomeLevelUp(state.tome, state.level));
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

        Shuffle(currentOptions);
        if (currentOptions.Count > optionsPerLevel)
            currentOptions.RemoveRange(optionsPerLevel, currentOptions.Count - optionsPerLevel);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
