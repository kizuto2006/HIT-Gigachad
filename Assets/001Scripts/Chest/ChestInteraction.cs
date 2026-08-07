using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChestInteraction : MonoBehaviour
{
    private const int StartingOpenCost = 35;
    private const int OpenCostIncrease = 35;
    private static int nextOpenCost = StartingOpenCost;
    private static int progressionSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCostProgression()
    {
        nextOpenCost = StartingOpenCost;
        progressionSceneHandle = -1;
    }

    [Header("Chest")]
    [SerializeField] private ChestAnimator chestAnimator;
    [SerializeField] private Transform rewardSpawnPoint;
    [SerializeField] private GameObject idleVfxPrefab;
    [SerializeField] private GameObject openVfxPrefab;

    [Header("Interaction")]
    [SerializeField, Min(0)] private int openCost = 35;
    [SerializeField, Min(0.5f)] private float interactionRadius = 3.2f;
    [SerializeField, Min(0f)] private float rewardDelay = 0.55f;
    [SerializeField, Min(1)] private int rewardOptionCount = 3;

    [Header("Prompt")]
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 2.35f, 0f);

    private Transform player;
    private PlayerCurrency currency;
    private UpgradeManager upgradeManager;
    private GameObject promptRoot;
    private TMP_Text promptText;
    private TMP_Text statusText;
    private GameObject idleVfxInstance;
    private Camera mainCamera;
    private bool isOpen;
    private bool isFreeReward;
    private Coroutine statusRoutine;

    public bool IsOpen => isOpen;
    public int OpenCost => openCost;

    private void Awake()
    {
        EnsureCostProgressionForCurrentScene();
        if (!isFreeReward)
            openCost = nextOpenCost;

        if (chestAnimator == null)
            chestAnimator = GetComponent<ChestAnimator>();
        if (rewardSpawnPoint == null)
            rewardSpawnPoint = transform.Find("RewardSpawnPoint");

        BuildPrompt();

        if (idleVfxPrefab != null)
        {
            idleVfxInstance = Instantiate(
                idleVfxPrefab,
                rewardSpawnPoint != null ? rewardSpawnPoint : transform);
            idleVfxInstance.name = "Chest Idle Glow";
        }
    }

    private void Start()
    {
        ResolvePlayerReferences();
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (isOpen)
            return;

        if (player == null)
            ResolvePlayerReferences();
        if (player == null)
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        bool isInRange = offset.sqrMagnitude <= interactionRadius * interactionRadius;
        SetPromptVisible(isInRange);

        if (isInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryInteract();
    }

    private void LateUpdate()
    {
        if (promptRoot == null || !promptRoot.activeSelf)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            promptRoot.transform.rotation = mainCamera.transform.rotation;
    }

    public bool TryInteract()
    {
        if (isOpen)
            return false;

        ResolvePlayerReferences();
        if (upgradeManager == null || (openCost > 0 && currency == null))
        {
            ShowTemporaryStatus(
                "Không tìm thấy hệ thống phần thưởng",
                new Color(1f, 0.35f, 0.25f));
            return false;
        }

        if (upgradeManager.IsShowingUpgrade)
        {
            ShowTemporaryStatus(
                "Hãy chọn phần thưởng hiện tại trước",
                new Color(1f, 0.8f, 0.25f));
            return false;
        }

        if (!upgradeManager.CanRequestChestReward(rewardOptionCount))
        {
            ShowTemporaryStatus(
                "Rương chưa có item để trao",
                new Color(1f, 0.35f, 0.25f));
            return false;
        }

        if (openCost > 0 && !currency.TrySpend(openCost))
        {
            int missingGold = Mathf.Max(0, openCost - currency.Gold);
            ShowTemporaryStatus(
                $"Thiếu {missingGold} vàng",
                new Color(1f, 0.3f, 0.2f));
            return false;
        }

        isOpen = true;
        AdvanceCostProgression();
        SetPromptVisible(false);
        chestAnimator?.Open();

        if (idleVfxInstance != null)
            idleVfxInstance.SetActive(false);
        if (openVfxPrefab != null)
        {
            Transform spawnPoint = rewardSpawnPoint != null ? rewardSpawnPoint : transform;
            GameObject effect = Instantiate(openVfxPrefab, spawnPoint.position, Quaternion.identity);
            effect.name = "Chest Reward Effect";
        }

        StartCoroutine(ShowRewardAfterDelay());
        return true;
    }

    public void ConfigureFreeReward()
    {
        isFreeReward = true;
        SetOpenCost(0);
    }

    private void EnsureCostProgressionForCurrentScene()
    {
        int sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (progressionSceneHandle == sceneHandle)
            return;

        progressionSceneHandle = sceneHandle;
        nextOpenCost = StartingOpenCost;
    }

    private void AdvanceCostProgression()
    {
        if (isFreeReward)
            return;

        nextOpenCost = nextOpenCost >= int.MaxValue - OpenCostIncrease
            ? int.MaxValue
            : nextOpenCost + OpenCostIncrease;

        ChestInteraction[] chests = UnityEngine.Object.FindObjectsByType<ChestInteraction>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < chests.Length; i++)
        {
            ChestInteraction chest = chests[i];
            if (chest == null ||
                chest == this ||
                chest.isOpen ||
                chest.isFreeReward ||
                chest.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            chest.SetOpenCost(nextOpenCost);
        }
    }

    private void SetOpenCost(int cost)
    {
        openCost = Mathf.Max(0, cost);
        RefreshPromptCost();
    }

    private void RefreshPromptCost()
    {
        if (promptText == null)
            return;

        int lastSpace = promptText.text.LastIndexOf(' ');
        if (lastSpace >= 0)
        {
            promptText.text =
                promptText.text.Substring(0, lastSpace + 1) + openCost;
        }
    }





    private IEnumerator ShowRewardAfterDelay()
    {
        if (rewardDelay > 0f)
            yield return new WaitForSecondsRealtime(rewardDelay);

        if (upgradeManager != null && !upgradeManager.RequestChestReward(
                rewardOptionCount,
                "RƯƠNG PHẦN THƯỞNG"))
        {
            ShowTemporaryStatus(
                "Không thể tạo phần thưởng",
                new Color(1f, 0.35f, 0.25f));
        }
    }

    private void ResolvePlayerReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
            return;

        if (currency == null)
            currency = player.GetComponent<PlayerCurrency>();
        if (upgradeManager == null)
            upgradeManager = player.GetComponent<UpgradeManager>();
    }

    private void BuildPrompt()
    {
        promptRoot = new GameObject(
            "Chest Interaction Prompt",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        promptRoot.transform.SetParent(transform, false);
        promptRoot.transform.localPosition = promptLocalPosition;
        promptRoot.transform.localScale = Vector3.one * 0.005f;

        Canvas canvas = promptRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;

        RectTransform canvasRect = promptRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(390f, 116f);

        GameObject panelObject = CreateUIObject("Prompt Background", promptRoot.transform);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.025f, 0.03f, 0.04f, 0.94f);
        panel.raycastTarget = false;
        Stretch(panelObject.GetComponent<RectTransform>());

        GameObject accentObject = CreateUIObject("Prompt Accent", panelObject.transform);
        Image accent = accentObject.AddComponent<Image>();
        accent.color = new Color(1f, 0.72f, 0.08f, 1f);
        accent.raycastTarget = false;
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(8f, 0f);

        GameObject textObject = CreateUIObject("Prompt Text", panelObject.transform);
        promptText = textObject.AddComponent<TextMeshProUGUI>();
        promptText.font = TMP_Settings.defaultFontAsset;
        promptText.fontSize = 30f;
        promptText.fontStyle = FontStyles.Bold;
        promptText.color = Color.white;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.raycastTarget = false;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.38f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        promptText.text = $"[E]  MỞ RƯƠNG     {openCost}";

        if (coinIcon != null)
        {
            GameObject iconObject = CreateUIObject("Coin Icon", panelObject.transform);
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = coinIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0.69f);
            iconRect.anchorMax = new Vector2(1f, 0.69f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-18f, 0f);
            iconRect.sizeDelta = new Vector2(38f, 38f);
        }

        GameObject statusObject = CreateUIObject("Status Text", panelObject.transform);
        statusText = statusObject.AddComponent<TextMeshProUGUI>();
        statusText.font = TMP_Settings.defaultFontAsset;
        statusText.fontSize = 20f;
        statusText.color = new Color(1f, 0.78f, 0.16f, 1f);
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.raycastTarget = false;
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0.38f);
        statusRect.offsetMin = new Vector2(12f, 4f);
        statusRect.offsetMax = new Vector2(-12f, 0f);
        statusText.text = "NHẬN 1 NÂNG CẤP NGẪU NHIÊN";
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }

    private void ShowTemporaryStatus(string message, Color color)
    {
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(ShowStatusRoutine(message, color));
    }

    private IEnumerator ShowStatusRoutine(string message, Color color)
    {
        if (statusText == null)
            yield break;

        string originalText = statusText.text;
        Color originalColor = statusText.color;
        statusText.text = message;
        statusText.color = color;
        yield return new WaitForSecondsRealtime(1.25f);
        statusText.text = "NHẬN ITEM NGẪU NHIÊN";
        statusText.color = originalColor;
        statusRoutine = null;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
