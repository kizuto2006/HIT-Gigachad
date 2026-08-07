using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCurrency : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingGold;

    public int Gold { get; private set; }

    private void Awake()
    {
        Gold = Mathf.Max(0, startingGold);
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Gold < amount)
            return false;

        Gold -= amount;
        return true;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        Gold = Gold > int.MaxValue - amount
            ? int.MaxValue
            : Gold + amount;
    }

    private void OnValidate()
    {
        startingGold = Mathf.Max(0, startingGold);
    }
}
