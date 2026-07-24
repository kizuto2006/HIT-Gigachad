using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class EnemyMeshFlipbookManager : MonoBehaviour
{
    private static readonly List<EnemyMeshFlipbookAnimator> Animators =
        new List<EnemyMeshFlipbookAnimator>(512);
    private static readonly HashSet<EnemyMeshFlipbookAnimator> KnownAnimators =
        new HashSet<EnemyMeshFlipbookAnimator>();

    private static EnemyMeshFlipbookManager instance;

    [SerializeField, Range(10f, 60f)] private float managerTickRate = 30f;

    private float nextTickTime;
    private int cleanupCountdown = 300;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Animators.Clear();
        KnownAnimators.Clear();
        instance = null;
    }

    internal static void Register(EnemyMeshFlipbookAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        EnsureInstance();
        if (KnownAnimators.Add(animator))
        {
            Animators.Add(animator);
        }
    }

    internal static void Unregister(EnemyMeshFlipbookAnimator animator)
    {
        // Keep pooled instances in the compact manager list. isActiveAndEnabled
        // prevents updates while an enemy is returned to the pool.
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("~EnemyMeshFlipbookManager");
        instance = managerObject.AddComponent<EnemyMeshFlipbookManager>();
        DontDestroyOnLoad(managerObject);
    }

    private void Update()
    {
        float now = Time.time;
        if (now < nextTickTime)
        {
            return;
        }

        nextTickTime = now + 1f / Mathf.Max(10f, managerTickRate);
        for (int i = 0; i < Animators.Count; i++)
        {
            EnemyMeshFlipbookAnimator animator = Animators[i];
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Tick(now);
            }
        }

        cleanupCountdown--;
        if (cleanupCountdown <= 0)
        {
            CleanupDestroyedAnimators();
            cleanupCountdown = 300;
        }
    }

    private static void CleanupDestroyedAnimators()
    {
        for (int i = Animators.Count - 1; i >= 0; i--)
        {
            if (Animators[i] != null)
            {
                continue;
            }

            Animators.RemoveAt(i);
        }

        KnownAnimators.RemoveWhere(animator => animator == null);
    }
}
