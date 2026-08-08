using UnityEngine;

/// <summary>
/// Hệ thống XP & Level. Gắn trên Player.
/// Khi đủ XP → level up → broadcast event OnLevelUp.
/// Từ level 5, XP yêu cầu tăng nhanh hơn để giữ độ khó về sau.
/// </summary>
public class XPSystem : MonoBehaviour
{
    [Header("── XP Settings ──")]
    [Tooltip("XP hiện tại.")]
    [SerializeField] private int currentXP;
    [Tooltip("Level hiện tại.")]
    [SerializeField] private int currentLevel = 1;
    private float fractionalXP;

    [Header("── Scaling ──")]
    [Tooltip("XP cơ bản để level up level 1.")]
    public int baseXPRequired = 10;
    [Tooltip("XP thêm mỗi level.")]
    public int xpPerLevel = 5;
    [Tooltip("Level bắt đầu áp dụng phần XP độ khó bổ sung.")]
    [Min(1)] public int difficultyScalingStartLevel = 5;
    [Tooltip("XP cộng dồn thêm cho mỗi level kể từ mốc độ khó.")]
    [Min(0)] public int bonusXPPerLevel = 10;

    /// <summary>
    /// Event broadcast when the player receives a positive amount of XP.
    /// </summary>
    public event System.Action<int> OnXPReceived;

    /// <summary>
    /// Event broadcast khi player level up. Parameter = level mới.
    /// </summary>
    public event System.Action<int> OnLevelUp;

    /// <summary>
    /// Event broadcast khi XP thay đổi. Parameters = (currentXP, xpToNextLevel).
    /// </summary>
    public event System.Action<int, int> OnXPChanged;

    public int CurrentXP => currentXP;
    public int CurrentLevel => currentLevel;

    /// <summary>
    /// XP cần để đạt level tiếp theo.
    /// </summary>
    public int XPToNextLevel
    {
        get
        {
            int difficultyLevels = Mathf.Max(0, currentLevel - difficultyScalingStartLevel + 1);
            return baseXPRequired + currentLevel * xpPerLevel + difficultyLevels * bonusXPPerLevel;
        }
    }

    /// <summary>
    /// Tỷ lệ XP hiện tại / XP cần (0..1). Dùng cho XP bar UI.
    /// </summary>
    public float XPProgress => (float)currentXP / XPToNextLevel;

    /// <summary>
    /// Thêm XP. Tự động level up nếu đủ (có thể level up nhiều lần liên tiếp).
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        OnXPReceived?.Invoke(amount);
        currentXP += amount;
        OnXPChanged?.Invoke(currentXP, XPToNextLevel);

        while (currentXP >= XPToNextLevel)
        {
            currentXP -= XPToNextLevel;
            currentLevel++;

            Debug.Log($"[XPSystem] Level Up! Level {currentLevel}");
            OnLevelUp?.Invoke(currentLevel);
            OnXPChanged?.Invoke(currentXP, XPToNextLevel);
        }
    }

    public void AddXP(float amount)
    {
        if (amount <= 0f)
            return;

        float totalXP = amount + fractionalXP;
        int wholeXP = Mathf.FloorToInt(totalXP);
        fractionalXP = totalXP - wholeXP;

        if (wholeXP > 0)
            AddXP(wholeXP);
    }

    private void OnValidate()
    {
        baseXPRequired = Mathf.Max(1, baseXPRequired);
        xpPerLevel = Mathf.Max(0, xpPerLevel);
        difficultyScalingStartLevel = Mathf.Max(1, difficultyScalingStartLevel);
        bonusXPPerLevel = Mathf.Max(0, bonusXPPerLevel);
    }
}
