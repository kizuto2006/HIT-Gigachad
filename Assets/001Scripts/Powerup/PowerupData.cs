using UnityEngine;

[CreateAssetMenu(fileName = "Powerup", menuName = "Data/Powerups/Powerup")]
public sealed class PowerupData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;
    public PowerupType powerupType;

    [Header("Effect")]
    [Min(0f)] public float magnitude = 0.3f;
    [Min(0f)] public float duration = 8f;
    [Min(1)] public int charges = 1;
    public PowerupStackPolicy stackPolicy = PowerupStackPolicy.RefreshDuration;
    [Min(1)] public int maxCharges = 3;

    [Header("Drop")]
    [Min(0.01f)] public float dropWeight = 1f;
    public bool canDropFromNormalEnemy = true;
    public bool canDropFromElite = true;
    public bool canDropFromBoss = true;

    [Header("Presentation")]
    public Color tint = Color.white;

    public float GetScaledDuration(float multiplier)
    {
        return Mathf.Max(0f, duration * Mathf.Max(1f, multiplier));
    }

    public float GetScaledMagnitude(float multiplier)
    {
        return Mathf.Max(0f, magnitude * Mathf.Max(1f, multiplier));
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;
    }
}
