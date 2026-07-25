using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Binds the fixed three-card WeaponUpgradePanel prefab to UpgradeManager.
/// The prefab only contains layout; all option text and icons are filled at runtime.
/// </summary>
public class UpgradeUI : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private GameObject upgradePanel;

    private readonly List<OptionCard> cards = new List<OptionCard>(3);
    private List<UpgradeOption> currentOptions;
    private Button skipButton;
    private Button rerollButton;
    private bool subscribed;

    private void Awake()
    {
        EnsureEventSystem();

        if (upgradePanel == null)
            upgradePanel = gameObject;

        CacheView();
        ClearOptionContent();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    private void Start()
    {
        TrySubscribe();
        upgradePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (subscribed && upgradeManager != null)
        {
            upgradeManager.OnShowUpgradeUI -= ShowUpgrade;
            upgradeManager.OnHideUpgradeUI -= HideUpgrade;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;

        if (upgradeManager == null)
            upgradeManager = FindFirstObjectByType<UpgradeManager>();

        if (upgradeManager == null)
        {
            Debug.LogError("[UpgradeUI] Cannot find UpgradeManager in the active scene.", this);
            return;
        }

        upgradeManager.OnShowUpgradeUI += ShowUpgrade;
        upgradeManager.OnHideUpgradeUI += HideUpgrade;
        subscribed = true;
    }

    private void CacheView()
    {
        cards.Clear();
        for (int i = 1; i <= 3; i++)
        {
            Transform cardRoot = FindDeepChild(transform, $"UpgradeChoice_0{i}");
            if (cardRoot != null)
                cards.Add(new OptionCard(cardRoot));
        }

        skipButton = FindButton("SkipButton");
        rerollButton = FindButton("RerollButton");

        if (skipButton != null)
        {
            EnableButtonRaycast(skipButton);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => upgradeManager?.SkipUpgrade());
        }

        if (rerollButton != null)
        {
            EnableButtonRaycast(rerollButton);
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() => upgradeManager?.RerollOptions());
        }
    }

    private void ShowUpgrade(List<UpgradeOption> options)
    {
        currentOptions = options;
        upgradePanel.SetActive(true);

        for (int i = 0; i < cards.Count; i++)
        {
            bool hasOption = options != null && i < options.Count;
            cards[i].Root.gameObject.SetActive(hasOption);
            if (!hasOption)
                continue;

            int capturedIndex = i;
            cards[i].Bind(options[i], () => Select(capturedIndex));
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideUpgrade()
    {
        currentOptions = null;
        ClearOptionContent();
        upgradePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Select(int index)
    {
        if (currentOptions == null || index < 0 || index >= currentOptions.Count)
            return;

        upgradeManager.SelectOption(currentOptions[index]);
    }

    private void ClearOptionContent()
    {
        foreach (OptionCard card in cards)
            card.Clear();
    }

    private Button FindButton(string objectName)
    {
        Transform child = FindDeepChild(transform, objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static void EnableButtonRaycast(Button button)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.raycastTarget = true;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    public static Color GetRarityColor(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Uncommon: return new Color(0.3f, 0.8f, 0.3f);
            case WeaponRarity.Rare: return new Color(0.3f, 0.5f, 1f);
            case WeaponRarity.Epic: return new Color(0.7f, 0.3f, 1f);
            case WeaponRarity.Legendary: return new Color(1f, 0.7f, 0.2f);
            default: return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private sealed class OptionCard
    {
        private static readonly Color CardColor = new Color32(31, 31, 31, 255);
        private static readonly Color NewColor = new Color32(255, 255, 255, 255);
        private static readonly Color OwnedColor = new Color32(29, 238, 117, 255);

        public readonly Transform Root;
        private readonly Button button;
        private readonly Image background;
        private readonly Image icon;
        private readonly TMP_Text rarity;
        private readonly TMP_Text itemName;
        private readonly TMP_Text stat;
        private readonly TMP_Text level;

        public OptionCard(Transform root)
        {
            Root = root;
            button = root.GetComponent<Button>();
            EnableButtonRaycast(button);
            background = FindDeepChild(root, "CardBackground")?.GetComponent<Image>();
            icon = FindDeepChild(root, "Icon")?.GetComponent<Image>();
            rarity = FindDeepChild(root, "RarityText")?.GetComponent<TMP_Text>();
            itemName = FindDeepChild(root, "ItemNameText")?.GetComponent<TMP_Text>();
            stat = FindDeepChild(root, "StatText")?.GetComponent<TMP_Text>();
            level = FindDeepChild(root, "LevelText")?.GetComponent<TMP_Text>();

            Transform oldMarker = FindDeepChild(root, "UpgradeMarker");
            if (oldMarker != null)
                oldMarker.gameObject.SetActive(false);

            if (background != null)
                background.color = CardColor;
        }

        public void Bind(UpgradeOption option, UnityEngine.Events.UnityAction onClick)
        {
            if (background != null)
                background.color = CardColor;
            if (icon != null)
            {
                icon.sprite = option.Icon;
                icon.enabled = option.Icon != null;
                icon.preserveAspect = true;
            }
            if (rarity != null)
            {
                rarity.text = option.isNewItem ? "NEW" : "COMMON";
                rarity.color = option.isNewItem ? NewColor : OwnedColor;
            }
            if (itemName != null)
                itemName.text = option.DisplayName;
            if (stat != null)
                stat.text = option.GetDisplayDescription();
            if (level != null)
                level.text = option.isNewItem ? "NEW" : $"LVL {option.CurrentLevel}";

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }

        public void Clear()
        {
            if (background != null)
                background.color = CardColor;
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }
            if (rarity != null) rarity.text = string.Empty;
            if (itemName != null) itemName.text = string.Empty;
            if (stat != null) stat.text = string.Empty;
            if (level != null) level.text = string.Empty;
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }
}
