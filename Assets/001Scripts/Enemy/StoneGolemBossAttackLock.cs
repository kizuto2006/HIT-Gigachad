using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoneGolemBossAttackLock : MonoBehaviour
{
    private MonoBehaviour owner;

    public bool IsLocked => owner != null;

    public bool TryAcquire(MonoBehaviour requester)
    {
        if (requester == null)
            return false;

        if (owner != null && owner != requester)
            return false;

        owner = requester;
        return true;
    }

    public void Release(MonoBehaviour requester)
    {
        if (owner == requester)
            owner = null;
    }
}
