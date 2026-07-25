using UnityEngine;

/// <summary>
/// Forwards trigger callbacks from a child visual collider to the gameplay component on the enemy root.
/// The Enemy Prefab Creator only adds this when "Use Existing Collider" is selected.
/// </summary>
public sealed class EnemyContactDamageRelay : MonoBehaviour
{
    [SerializeField] private EnemyContactDamage receiver;

    public void SetReceiver(EnemyContactDamage target)
    {
        receiver = target;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (receiver != null)
            receiver.HandleTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (receiver != null)
            receiver.HandleTriggerStay(other);
    }
}
