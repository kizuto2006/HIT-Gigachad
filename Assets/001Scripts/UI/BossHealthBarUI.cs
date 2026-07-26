using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-of-screen boss HUD presenter. The layout is authored in the scene while
/// this component owns boss discovery, health animation and visibility.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarUI : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private EnemyHealth bossHealth;
    [SerializeField] private string bossDisplayName = "STONE GOLEM";

    [Header("UI")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image delayedHealthFill;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField, Min(1f)] private float healthAnimationSpeed = 14f;
    [SerializeField, Min(0.1f)] private float delayedHealthSpeed = 1.8f;
    [SerializeField, Min(0.1f)] private float visibilitySpeed = 6f;

    private float maximumHealth = 1f;
    private float displayedHealth = 1f;
    private float delayedHealth = 1f;

    private void Start()
    {
        ResolveBoss();
        RefreshBossBinding(true);
    }

    private void Update()
    {
        if (bossHealth == null)
        {
            ResolveBoss();
            RefreshBossBinding(true);
        }

        bool shouldShow = bossHealth != null && bossHealth.gameObject.activeInHierarchy;
        AnimateVisibility(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        float currentHealth = Mathf.Clamp(bossHealth.currentHp, 0f, maximumHealth);
        float targetProgress = maximumHealth > 0f ? currentHealth / maximumHealth : 0f;
        float deltaTime = Time.unscaledDeltaTime;

        displayedHealth = SmoothTowards(displayedHealth, targetProgress, healthAnimationSpeed, deltaTime);
        delayedHealth = delayedHealth > displayedHealth
            ? Mathf.MoveTowards(delayedHealth, displayedHealth, delayedHealthSpeed * deltaTime)
            : displayedHealth;

        SetFill(healthFill, displayedHealth);
        SetFill(delayedHealthFill, delayedHealth);

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth):N0}  /  {Mathf.CeilToInt(maximumHealth):N0}";
        }
    }

    public void Configure(
        EnemyHealth targetBoss,
        Image mainFill,
        Image damageFill,
        TMP_Text nameLabel,
        TMP_Text hpLabel,
        CanvasGroup group,
        string displayName)
    {
        bossHealth = targetBoss;
        healthFill = mainFill;
        delayedHealthFill = damageFill;
        bossNameText = nameLabel;
        healthText = hpLabel;
        canvasGroup = group;
        bossDisplayName = string.IsNullOrWhiteSpace(displayName) ? "BOSS" : displayName;
    }

    private void ResolveBoss()
    {
        StoneGolemSandBurstAttack stoneGolem = FindFirstObjectByType<StoneGolemSandBurstAttack>();
        if (stoneGolem != null)
        {
            bossHealth = stoneGolem.GetComponent<EnemyHealth>();
        }
    }

    private void RefreshBossBinding(bool immediate)
    {
        maximumHealth = CalculateMaximumHealth(bossHealth);
        float progress = bossHealth != null
            ? Mathf.Clamp01(bossHealth.currentHp / maximumHealth)
            : 0f;

        if (immediate)
        {
            displayedHealth = progress;
            delayedHealth = progress;
            SetFill(healthFill, progress);
            SetFill(delayedHealthFill, progress);
        }

        if (bossNameText != null)
        {
            bossNameText.text = bossDisplayName;
        }

        if (healthText != null && bossHealth != null)
        {
            healthText.text = $"{Mathf.CeilToInt(bossHealth.currentHp):N0}  /  {Mathf.CeilToInt(maximumHealth):N0}";
        }
    }

    private void AnimateVisibility(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        float targetAlpha = visible ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            visibilitySpeed * Time.unscaledDeltaTime);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static float CalculateMaximumHealth(EnemyHealth health)
    {
        if (health == null || health.data == null)
        {
            return 1f;
        }

        float multiplier = 1f;
        switch (health.data.size)
        {
            case EnemySize.Small:
                multiplier = 0.6f;
                break;
            case EnemySize.Large:
                multiplier = 2f;
                break;
        }

        return Mathf.Max(1f, health.data.hp * multiplier);
    }

    private static float SmoothTowards(float current, float target, float speed, float deltaTime)
    {
        float blend = 1f - Mathf.Exp(-speed * deltaTime);
        return Mathf.Lerp(current, target, blend);
    }

    private static void SetFill(Image image, float progress)
    {
        if (image != null)
        {
            progress = Mathf.Clamp01(progress);
            image.fillAmount = progress;

            // Resize the hierarchy as well as setting fillAmount so decorative
            // children (the highlight strip) follow the remaining health width.
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(progress, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
