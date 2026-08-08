using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button backButton;

    [Header("Shop Items")]
    [SerializeField] private Button[] itemButtons;
    [SerializeField] private Image[] itemBackgrounds;
    [SerializeField] private Outline[] itemBorders;
    [SerializeField] private Image[] itemIcons;

    [Header("Silver Balance")]
    [SerializeField] private Image silverIcon;
    [SerializeField] private TextMeshProUGUI silverAmountText;

    [Header("Selected Item Info")]
    [SerializeField] private TextMeshProUGUI selectedIconText;
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TextMeshProUGUI infoTitle;
    [SerializeField] private TextMeshProUGUI infoDescription;
    [SerializeField] private TextMeshProUGUI infoPrice;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("UI-only Actions")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button refundButton;

    private static readonly ShopItemData[] Items =
    {
        new("W+", "WEAPON SLOT", "Unlock one additional slot for carrying a weapon.", 24),
        new("T+", "TOME SLOT", "Unlock one additional slot for carrying a tome.", 24),
        new("R", "REROLL", "Gain one extra reroll for upgrade selections.", 9),
        new("S", "SKIP", "Gain one extra skip for upgrade selections.", 9),
        new("X", "REMOVE", "Gain one extra remove charge for upgrade selections.", 9)
    };

    private readonly Color normalCardColor = Color.black;
    private readonly Color selectedCardColor = new Color(0.31f, 0.31f, 0.31f, 1f);
    private readonly Color normalBorderColor = new Color(0.02f, 0.02f, 0.02f, 1f);
    private readonly Color selectedBorderColor = new Color(0.78f, 0.58f, 0.25f, 1f);

    private StartUI owner;
    private PlayerCurrency currency;
    private int selectedIndex;
    private bool listenersRegistered;
    private bool persistentListenerRegistered;
    private bool purchaseStateLoaded;
    private readonly int[] purchaseCounts = new int[5];
    private TextMeshProUGUI[] itemPriceTexts;

    private const float PriceIncreaseMultiplier = 4f;
    
    private static readonly int[] MaxPurchaseCounts = { 2, 2, int.MaxValue, int.MaxValue, int.MaxValue };
    private const string PurchaseCountKeyPrefix = "Gigachad.Shop.PurchaseCount.";

    public void Initialize(StartUI startUI)
    {
        owner = startUI;
        LoadPurchaseState();
        CacheItemPriceTexts();
        RegisterPersistentListener();
        ResolveCurrency();
        UpdatePriceTexts();
        RegisterListeners();
    }

    public void ConfigureSceneReferences(
        GameObject panel,
        Button back,
        Button[] buttons,
        Image[] backgrounds,
        Outline[] borders,
        Image[] icons,
        TextMeshProUGUI selectedIcon,
        Image selectedIconGraphic,
        Image silverIconGraphic,
        TextMeshProUGUI silverAmount,
        TextMeshProUGUI title,
        TextMeshProUGUI description,
        TextMeshProUGUI price,
        TextMeshProUGUI feedback,
        Button buy,
        Button refund)
    {
        shopPanel = panel;
        backButton = back;
        itemButtons = buttons;
        itemBackgrounds = backgrounds;
        itemBorders = borders;
        itemIcons = icons;
        selectedIconText = selectedIcon;
        selectedIconImage = selectedIconGraphic;
        silverIcon = silverIconGraphic;
        silverAmountText = silverAmount;
        infoTitle = title;
        infoDescription = description;
        infoPrice = price;
        feedbackText = feedback;
        buyButton = buy;
        refundButton = refund;
        LoadPurchaseState();
        CacheItemPriceTexts();
        UpdatePriceTexts();
    }

    public void Open()
    {
        ResolveCurrency();
        RegisterListeners();
        if(shopPanel != null)
        {
            shopPanel.SetActive(true);
            shopPanel.transform.SetAsLastSibling();
        }

        SelectItem(selectedIndex);
    }

    private void OnDestroy()
    {
        if(currency != null)
            currency.CoinsChanged -= RefreshSilver;

        if(persistentListenerRegistered)
            PlayerCurrency.PersistentCoinsChanged -= RefreshSilver;
    }

    private void Update()
    {
        if(currency != PlayerCurrency.Instance)
            ResolveCurrency();
    }

    public void Close()
    {
        if(shopPanel != null)
            shopPanel.SetActive(false);

        if(owner != null)
            owner.SetActiveStartPanel(true);
    }

    private void ResolveCurrency()
    {
        LoadPurchaseState();
        RegisterPersistentListener();
        PlayerCurrency current = PlayerCurrency.Instance;
        if(current == null)
            current = FindFirstObjectByType<PlayerCurrency>();

        if(currency != current)
        {
            if(currency != null)
                currency.CoinsChanged -= RefreshSilver;

            currency = current;
            if(currency != null)
                currency.CoinsChanged += RefreshSilver;
        }

        RefreshSilver(currency != null ? currency.Coins : PlayerCurrency.PersistentCoins);
    }

    private void RefreshSilver(int coins)
    {
        if(silverAmountText != null)
            silverAmountText.text = Mathf.Max(0, coins).ToString();
    }
    private void RegisterListeners()
    {
        if(listenersRegistered)
            return;

        if(backButton != null)
            backButton.onClick.AddListener(Close);

        if(itemButtons != null)
        {
            for(int i = 0; i < itemButtons.Length; i++)
            {
                if(itemButtons[i] == null)
                    continue;

                int capturedIndex = i;
                itemButtons[i].onClick.AddListener(() => SelectItem(capturedIndex));
            }
        }

        if(buyButton != null)
            buyButton.onClick.AddListener(BuySelectedItem);

        if(refundButton != null)
            refundButton.onClick.AddListener(() => ShowUiOnlyFeedback("REFUND IS NOT AVAILABLE YET"));

        listenersRegistered = true;
    }

    private void SelectItem(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, Items.Length - 1);
        ShopItemData item = Items[selectedIndex];
        bool purchaseLimitReached = IsPurchaseLimitReached(selectedIndex);
        int currentPrice = GetCurrentPrice(selectedIndex);

        if(itemBackgrounds != null && itemBorders != null)
        {
            int count = Mathf.Min(itemBackgrounds.Length, itemBorders.Length);
            for(int i = 0; i < count; i++)
            {
                bool selected = i == selectedIndex;
                if(itemBackgrounds[i] != null)
                    itemBackgrounds[i].color = selected ? selectedCardColor : normalCardColor;
                if(itemBorders[i] != null)
                {
                    itemBorders[i].effectColor = selected ? selectedBorderColor : normalBorderColor;
                    itemBorders[i].effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(3f, -3f);
                }
            }
        }

        if(selectedIconText != null)
            selectedIconText.text = item.Icon;
        if(selectedIconImage != null && itemIcons != null && selectedIndex < itemIcons.Length)
            selectedIconImage.sprite = itemIcons[selectedIndex] != null ? itemIcons[selectedIndex].sprite : null;
        if(infoTitle != null)
            infoTitle.text = item.Title;
        if(infoDescription != null)
            infoDescription.text = item.Description;
        if(infoPrice != null)
            infoPrice.text = purchaseLimitReached
                ? "MAX PURCHASED"
                : "PRICE  " + currentPrice + "     OWNED  --";
        if(feedbackText != null)
            feedbackText.text = purchaseLimitReached
                ? "THIS UPGRADE HAS REACHED ITS PURCHASE LIMIT"
                : "SELECT AN UPGRADE, THEN CHOOSE BUY OR REFUND";

        UpdatePriceTexts();
        UpdatePurchaseButtonState();
    }

    private void ShowUiOnlyFeedback(string message)
    {
        if(feedbackText != null)
            feedbackText.text = message;
    }

    private readonly struct ShopItemData
    {
        public ShopItemData(string icon, string title, string description, int basePrice)
        {
            Icon = icon;
            Title = title;
            Description = description;
            BasePrice = basePrice;
        }

        public string Icon { get; }
        public string Title { get; }
        public string Description { get; }
        public int BasePrice { get; }
    }


    private void BuySelectedItem()
    {
        ResolveCurrency();
        if(IsPurchaseLimitReached(selectedIndex))
        {
            ShowUiOnlyFeedback("THIS UPGRADE HAS REACHED ITS PURCHASE LIMIT");
            UpdatePurchaseButtonState();
            return;
        }

        int price = GetCurrentPrice(selectedIndex);
        bool purchased = currency != null
            ? currency.TrySpend(price)
            : PlayerCurrency.TrySpendPersistentCoins(price);

        if(!purchased)
        {
            ShowUiOnlyFeedback("NOT ENOUGH COINS - NEED " + price);
            return;
        }

        purchaseCounts[selectedIndex]++;
        SavePurchaseState();
        UpdatePriceTexts();
        if(IsPurchaseLimitReached(selectedIndex))
        {
            if(infoPrice != null)
                infoPrice.text = "MAX PURCHASED";
            ShowUiOnlyFeedback("PURCHASED - MAXIMUM 2 TIMES");
        }
        else
        {
            int nextPrice = GetCurrentPrice(selectedIndex);
            if(infoPrice != null)
                infoPrice.text = "PRICE  " + nextPrice + "     OWNED  --";
            ShowUiOnlyFeedback("PURCHASED - NEXT PRICE " + nextPrice);
        }
        UpdatePurchaseButtonState();
    }


    private void UpdatePriceTexts()
    {
        if(itemPriceTexts == null)
            return;

        int count = Mathf.Min(itemPriceTexts.Length, Items.Length);
        for(int i = 0; i < count; i++)
        {
            if(itemPriceTexts[i] != null)
                itemPriceTexts[i].text = IsPurchaseLimitReached(i)
                    ? "MAX"
                    : GetCurrentPrice(i).ToString();
        }
    }


    private void CacheItemPriceTexts()
    {
        if(itemButtons == null)
        {
            itemPriceTexts = null;
            return;
        }

        itemPriceTexts = new TextMeshProUGUI[itemButtons.Length];
        for(int i = 0; i < itemButtons.Length; i++)
        {
            if(itemButtons[i] == null)
                continue;

            Transform amount = itemButtons[i].transform.Find("Price/Amount");
            if(amount != null)
                itemPriceTexts[i] = amount.GetComponent<TextMeshProUGUI>();
        }
    }


    private int GetCurrentPrice(int index)
    {
        LoadPurchaseState();
        index = Mathf.Clamp(index, 0, Items.Length - 1);
        int price = Mathf.Max(1, Items[index].BasePrice);
        for(int i = 0; i < purchaseCounts[index]; i++)
        {
            int nextPrice = Mathf.CeilToInt(price * PriceIncreaseMultiplier);
            price = Mathf.Max(price + 1, nextPrice);
        }

        return price;
    }


    private void SavePurchaseState()
    {
        for(int i = 0; i < purchaseCounts.Length; i++)
        {
            purchaseCounts[i] = Mathf.Clamp(purchaseCounts[i], 0, GetMaxPurchaseCount(i));
            PlayerPrefs.SetInt(PurchaseCountKeyPrefix + i, purchaseCounts[i]);
        }

        PlayerPrefs.Save();
    }


    private void LoadPurchaseState()
    {
        if(purchaseStateLoaded)
            return;

        for(int i = 0; i < purchaseCounts.Length; i++)
            purchaseCounts[i] = Mathf.Clamp(PlayerPrefs.GetInt(PurchaseCountKeyPrefix + i, 0), 0, GetMaxPurchaseCount(i));

        purchaseStateLoaded = true;
    }


    private void RegisterPersistentListener()
    {
        if(persistentListenerRegistered)
            return;

        PlayerCurrency.PersistentCoinsChanged += RefreshSilver;
        persistentListenerRegistered = true;
    }


    private void UpdatePurchaseButtonState()
    {
        if(buyButton != null)
            buyButton.interactable = !IsPurchaseLimitReached(selectedIndex);
    }


    private bool IsPurchaseLimitReached(int index)
    {
        return purchaseCounts[Mathf.Clamp(index, 0, purchaseCounts.Length - 1)] >= GetMaxPurchaseCount(index);
    }


    private int GetMaxPurchaseCount(int index)
    {
        index = Mathf.Clamp(index, 0, MaxPurchaseCounts.Length - 1);
        return MaxPurchaseCounts[index];
    }


    public static int GetUnlockedSlotCount(bool weaponSlot, int startingSlots = 2, int maximumSlots = 4)
    {
        int baseSlots = Mathf.Clamp(startingSlots, 1, maximumSlots);
        int itemIndex = weaponSlot ? 0 : 1;
        return Mathf.Clamp(baseSlots + GetPurchaseCount(itemIndex), baseSlots, maximumSlots);
    }


    public static int GetPurchaseCount(int index)
    {
        index = Mathf.Clamp(index, 0, MaxPurchaseCounts.Length - 1);
        return Mathf.Clamp(PlayerPrefs.GetInt(PurchaseCountKeyPrefix + index, 0), 0, MaxPurchaseCounts[index]);
    }


public static int GetUtilityCharges(int itemIndex)
    {
        return Mathf.Max(1, 1 + GetPurchaseCount(itemIndex));
    }
}
