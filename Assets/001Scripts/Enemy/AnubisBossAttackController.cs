using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StoneGolemBossAttackLock))]
public sealed class AnubisBossAttackController : MonoBehaviour
{
    [SerializeField] private AnubisTrackingLaserAttack redLaser;
    [SerializeField] private AnubisBlueLaserRingAttack blueLaser;
    [SerializeField, Min(0f)] private float initialDelay = 1.5f;
    [SerializeField, Min(0.02f)] private float retryDelay = 0.15f;

    private StoneGolemBossAttackLock attackLock;
    private MonoBehaviour lastAttack;
    private float nextDecisionTime;

    private void Awake()
    {
        attackLock = GetComponent<StoneGolemBossAttackLock>();
        if (redLaser == null)
            redLaser = GetComponent<AnubisTrackingLaserAttack>();
        if (blueLaser == null)
            blueLaser = GetComponent<AnubisBlueLaserRingAttack>();
    }

    private void OnEnable()
    {
        nextDecisionTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (Time.time < nextDecisionTime || (attackLock != null && attackLock.IsLocked))
            return;

        bool redReady = redLaser != null && redLaser.CanAttack;
        bool blueReady = blueLaser != null && blueLaser.CanAttack;
        if (!redReady && !blueReady)
        {
            nextDecisionTime = Time.time + retryDelay;
            return;
        }

        bool started;
        if (redReady && blueReady)
        {
            if (lastAttack == redLaser)
                started = TryBlue();
            else if (lastAttack == blueLaser)
                started = TryRed();
            else if (Random.value < 0.5f)
                started = TryRed() || TryBlue();
            else
                started = TryBlue() || TryRed();
        }
        else
        {
            started = redReady ? TryRed() : TryBlue();
        }

        nextDecisionTime = Time.time + (started ? 0.25f : retryDelay);
    }

    private bool TryRed()
    {
        if (redLaser == null || !redLaser.TryStartAttack())
            return false;

        lastAttack = redLaser;
        return true;
    }

    private bool TryBlue()
    {
        if (blueLaser == null || !blueLaser.TryStartAttack())
            return false;

        lastAttack = blueLaser;
        return true;
    }
}
