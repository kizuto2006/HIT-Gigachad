using UnityEngine;

/// <summary>
/// Kích thước enemy — quyết định scale và HP multiplier.
/// </summary>
public enum EnemySize
{
    Small,
    Medium,
    Large
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("── Base Stats ──")]
    public float hp = 50f;
    public float atk = 5f;
    public float speed = 3f;
    public float armor = 0f;

    [Header("── Size ──")]
    public EnemySize size = EnemySize.Medium;

    [Header("── Rewards ──")]
    [Tooltip("Lượng XP enemy drop khi chết.")]
    public int xpReward = 5;
}
