using System.Collections;
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
    private Button removeButton;
    private Button skipButton;
    private Button rerollButton;
    private TMP_Text removeButtonLabel;
    private TMP_Text skipButtonLabel;
    private TMP_Text rerollButtonLabel;
    private TMP_Text titleText;
    private UpgradeInventoryStatsView inventoryStatsView;
    private UpgradeBackgroundEffect backgroundEffect;
    private Coroutine cardOpenAnimation;
    private bool subscribed;
    private bool musicDuckActive;

    private void Awake()
    {
        EnsureEventSystem();

        if (upgradePanel == null)
            upgradePanel = gameObject;

        CacheView();
        EnsureBackgroundEffect();
        EnsureInventoryStatsView();
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

    private void Update()
    {
        if (upgradePanel != null && upgradePanel.activeInHierarchy)
            RequestMusicDuck();
        else
            ReleaseMusicDuck();
    }

    private void OnDestroy()
    {
        ReleaseMusicDuck();

        if (subscribed && upgradeManager != null)
        {
            upgradeManager.OnShowUpgradeUI -= ShowUpgrade;
            upgradeManager.OnHideUpgradeUI -= HideUpgrade;
            upgradeManager.UtilityChargesChanged -= RefreshUtilityButtons;
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
        upgradeManager.UtilityChargesChanged += RefreshUtilityButtons;
        subscribed = true;

        if (upgradeManager.IsShowingUpgrade && upgradeManager.CurrentOptions.Count > 0)
            ShowUpgrade(new List<UpgradeOption>(upgradeManager.CurrentOptions));
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

        removeButton = FindButton("RemoveButton");
        skipButton = FindButton("SkipButton");
        rerollButton = FindButton("RerollButton");
        removeButtonLabel = FindDeepChild(removeButton != null ? removeButton.transform : transform, "Label")?.GetComponent<TMP_Text>();
        skipButtonLabel = FindDeepChild(skipButton != null ? skipButton.transform : transform, "Label")?.GetComponent<TMP_Text>();
        rerollButtonLabel = FindDeepChild(rerollButton != null ? rerollButton.transform : transform, "Label")?.GetComponent<TMP_Text>();
        titleText = FindDeepChild(transform, "Title")?.GetComponent<TMP_Text>();

        if (removeButton != null)
        {
            EnableButtonRaycast(removeButton);
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => upgradeManager?.RemoveOption());
        }

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
        RequestMusicDuck();
        bool isChestReward = upgradeManager != null && upgradeManager.IsChestReward;
        if (titleText != null)
            titleText.text = upgradeManager != null ? upgradeManager.CurrentTitle : "REWARD";
        if (skipButton != null)
            skipButton.gameObject.SetActive(!isChestReward);
        if (rerollButton != null)
            rerollButton.gameObject.SetActive(!isChestReward);
        if (removeButton != null)
            removeButton.gameObject.SetActive(!isChestReward);
        RefreshUtilityButtons();
        inventoryStatsView?.RefreshAll();

        for (int i = 0; i < cards.Count; i++)
        {
            bool hasOption = options != null && i < options.Count;
            cards[i].Root.gameObject.SetActive(hasOption);
            if (!hasOption)
                continue;

            int capturedIndex = i;
            cards[i].Bind(options[i], () => Select(capturedIndex));
        }

        inventoryStatsView?.PlayOpenAnimation();
        backgroundEffect?.Play();
        PlayCardOpenAnimation();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EnsureInventoryStatsView()
    {
        inventoryStatsView = GetComponent<UpgradeInventoryStatsView>();
        if (inventoryStatsView == null)
            inventoryStatsView = gameObject.AddComponent<UpgradeInventoryStatsView>();

        WeaponInventory weapons = FindFirstObjectByType<WeaponInventory>();
        PlayerTomeInventory tomes = FindFirstObjectByType<PlayerTomeInventory>();
        PlayerItemInventory items = FindFirstObjectByType<PlayerItemInventory>();
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        TMP_Text fontSource = GetComponentInChildren<TMP_Text>(true);
        inventoryStatsView.Configure(
            weapons,
            tomes,
            items,
            health != null ? health.stats : null,
            fontSource != null ? fontSource.font : null);
    }

    private void EnsureBackgroundEffect()
    {
        backgroundEffect = GetComponent<UpgradeBackgroundEffect>();
        if (backgroundEffect == null)
            backgroundEffect = gameObject.AddComponent<UpgradeBackgroundEffect>();

        Transform dimmedBackground = FindDeepChild(transform, "DimmedBackground");
        backgroundEffect.Configure(dimmedBackground);
    }

    private void HideUpgrade()
    {
        if (cardOpenAnimation != null)
            StopCoroutine(cardOpenAnimation);
        cardOpenAnimation = null;
        backgroundEffect?.Hide();
        ResetCardScales();
        currentOptions = null;
        ClearOptionContent();
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);
        if (rerollButton != null)
            rerollButton.gameObject.SetActive(true);
        if (removeButton != null)
            removeButton.gameObject.SetActive(true);
        upgradePanel.SetActive(false);
        ReleaseMusicDuck();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RequestMusicDuck()
    {
        if (musicDuckActive || MusicAudioManager.Instance == null)
            return;

        MusicAudioManager.Instance.PushMusicDuck();
        musicDuckActive = true;
    }

    private void ReleaseMusicDuck()
    {
        if (!musicDuckActive)
            return;

        if (MusicAudioManager.Instance != null)
            MusicAudioManager.Instance.PopMusicDuck();
        musicDuckActive = false;
    }

    private void PlayCardOpenAnimation()
    {
        if (cardOpenAnimation != null)
            StopCoroutine(cardOpenAnimation);
        cardOpenAnimation = StartCoroutine(AnimateCardsOpen());
    }

    private IEnumerator AnimateCardsOpen()
    {
        const float startScale = 0.72f;
        const float duration = 0.24f;
        const float stagger = 0.045f;
        float elapsed = 0f;

        foreach (OptionCard card in cards)
        {
            if (card.Root.gameObject.activeSelf)
                card.Root.localScale = new Vector3(startScale, startScale, 1f);
        }

        float totalDuration = duration + Mathf.Max(0, cards.Count - 1) * stagger;
        while (elapsed < totalDuration)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                OptionCard card = cards[i];
                if (!card.Root.gameObject.activeSelf)
                    continue;
                float progress = Mathf.Clamp01((elapsed - i * stagger) / duration);
                float scale = Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(progress));
                card.Root.localScale = new Vector3(scale, scale, 1f);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ResetCardScales();
        cardOpenAnimation = null;
    }

    private void ResetCardScales()
    {
        foreach (OptionCard card in cards)
            card.Root.localScale = Vector3.one;
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.70158f;
        float shifted = value - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted
            + overshoot * shifted * shifted;
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
        return UpgradeRarityUtility.GetColor(rarity);
    }

    private sealed class OptionCard
    {
        private static readonly Color CardColor = new Color32(31, 31, 31, 255);
        private static readonly Color NewColor = new Color32(255, 255, 255, 255);
        private static readonly Color OwnedColor = new Color32(29, 238, 117, 255);

        public readonly Transform Root;
        private readonly Button button;
        private readonly Image background;
        private readonly Image cardBorder;

        private readonly Image icon;
        private readonly TMP_Text rarity;
        private readonly TMP_Text itemName;
        private readonly TMP_Text stat;
        private readonly TMP_Text level;

        public OptionCard(Transform root)
        {
            Root = root;
            cardBorder = root.GetComponent<Image>();

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
            Color rarityColor = option.HasRarity
                ? UpgradeRarityUtility.GetColor(option.Rarity)
                : NewColor;
            if (cardBorder != null)
                cardBorder.color = rarityColor;
            if (background != null)
            {
                background.color = option.HasRarity
                    ? UpgradeRarityUtility.GetCardTint(option.Rarity)
                    : CardColor;
            }
            if (icon != null)
            {
                icon.sprite = option.Icon;
                icon.enabled = option.Icon != null;
                icon.preserveAspect = true;
            }
            if (rarity != null)
            {
                rarity.gameObject.SetActive(option.HasRarity);
                rarity.text = option.RarityDisplayName;
                rarity.color = rarityColor;
            }
            if (itemName != null)
                itemName.text = option.DisplayName;
            if (stat != null)
                stat.text = option.GetDisplayDescription();
            if (level != null)
                level.text = option.IsItem
                    ? $"x{option.targetLevel}"
                    : option.isNewItem ? "NEW" : $"LVL {option.CurrentLevel}";

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }

        public void Clear()
        {
            if (cardBorder != null)
                cardBorder.color = new Color32(217, 211, 190, 255);
            if (background != null)
                background.color = CardColor;
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }
            if (rarity != null)
            {
                rarity.gameObject.SetActive(true);
                rarity.text = string.Empty;
            }
            if (itemName != null)
                itemName.text = string.Empty;
            if (stat != null)
                stat.text = string.Empty;
            if (level != null)
                level.text = string.Empty;
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }


private void RefreshUtilityButtons()
    {
        if (upgradeManager == null)
            return;

        SetUtilityButton(
            removeButton,
            removeButtonLabel,
            "REMOVE",
            upgradeManager.RemainingRemoveCharges,
            upgradeManager.CanUseRemove);
        SetUtilityButton(
            skipButton,
            skipButtonLabel,
            "SKIP",
            upgradeManager.RemainingSkipCharges,
            upgradeManager.CanUseSkip);
        SetUtilityButton(
            rerollButton,
            rerollButtonLabel,
            "REROLL",
            upgradeManager.RemainingRerollCharges,
            upgradeManager.CanUseReroll);
    }

    private static void SetUtilityButton(
        Button button,
        TMP_Text label,
        string actionName,
        int remaining,
        bool canUse)
    {
        if (label != null)
            label.text = actionName + "  x" + Mathf.Max(0, remaining);

        if (button != null)
            button.interactable = canUse;
    }
}
