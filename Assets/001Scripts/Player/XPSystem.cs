using UnityEngine;

/// <summary>
/// Hệ thống XP & Level. Gắn trên Player.
/// Khi đủ XP → level up → broadcast event OnLevelUp.
/// Công thức XP cần: 10 + currentLevel * 5 (tăng dần theo level).
/// </summary>
public class XPSystem : MonoBehaviour
{
    [Header("── XP Settings ──")]
    [Tooltip("XP hiện tại.")]
    [SerializeField] private int currentXP;
    [Tooltip("Level hiện tại.")]
    [SerializeField] private int currentLevel = 1;

    [Header("── Scaling ──")]
    [Tooltip("XP cơ bản để level up level 1.")]
    public int baseXPRequired = 10;
    [Tooltip("XP thêm mỗi level.")]
    public int xpPerLevel = 5;

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
    public int XPToNextLevel => baseXPRequired + currentLevel * xpPerLevel;

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
}
