using System;
using UnityEngine;

[Serializable]
public sealed class PowerupRuntimeState
{
    public PowerupData data;
    public float remainingDuration;
    public float totalDuration;
    public int charges;

    public bool IsTimed => data != null && data.duration > 0f;
    public float NormalizedTime => totalDuration <= 0f
        ? 1f
        : Mathf.Clamp01(remainingDuration / totalDuration);
}
