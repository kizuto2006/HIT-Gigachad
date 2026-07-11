using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Data/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("── Thông tin nhân vật ──")]
    public string characterName;
    [TextArea(2, 5)] public string description;

    [Header("── Survival ──")]
    public float baseHp = 100f;
    public float baseShield = 0f;

    [Header("── Offense ──")]
    public float baseAtk = 10f;
    public float baseProjSpeed = 20f;
    public int baseProjCount = 1;

    [Header("── Mobility ──")]
    public float baseSpeed = 10f;
    public float jumpHeight = 2.5f;

    [Header("── Defense ──")]
    [Tooltip("Tỷ lệ giảm damage nhận vào (0.35 = giảm 35%)")]
    public float baseDef = 0f;
}
