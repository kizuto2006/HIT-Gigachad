using UnityEngine;

/// <summary>
/// Keeps camera motion evenly paced on desktop when VSync is disabled.
/// A stable frame cadence is more important for perceived camera smoothness
/// than allowing an uncapped frame rate to fluctuate around the refresh rate.
/// </summary>
public static class StableFramePacing
{
    private const int MaximumTargetFrameRate = 120;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        if (QualitySettings.vSyncCount > 0)
        {
            return;
        }

        int displayRefreshRate = Mathf.RoundToInt(
            (float)Screen.currentResolution.refreshRateRatio.value);
        if (displayRefreshRate <= 0)
        {
            displayRefreshRate = MaximumTargetFrameRate;
        }

        Application.targetFrameRate = Mathf.Min(
            displayRefreshRate,
            MaximumTargetFrameRate);
#endif
    }
}
