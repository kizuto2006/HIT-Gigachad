using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps projectile trails alive briefly after gameplay has finished with the
/// projectile, so pooling does not cut the trail off on the impact frame.
/// </summary>
public sealed class ProjectileTrailVFX : MonoBehaviour
{

private void Awake()
    {
        if (projectileVisualRoot != null)
        {
            visualBaseScale =
                projectileVisualRoot.transform.localScale;
        }
        else
        {
            visualBaseScale = Vector3.one;
        }
    }

    [SerializeField] private GameObject projectileVisualRoot;
    [SerializeField] private TrailRenderer[] trails;
    [SerializeField] private ParticleSystem[] motionParticles;
    [SerializeField, Min(0f)] private float releaseDelay = 0.16f;


    private Vector3 visualBaseScale;
    private Coroutine releaseRoutine;

    public void PrepareForSpawn()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }


        if (projectileVisualRoot != null)
            projectileVisualRoot.transform.localScale = visualBaseScale;
        if (projectileVisualRoot != null)
            projectileVisualRoot.SetActive(true);

        if (trails != null)
        {
            foreach (TrailRenderer trail in trails)
            {
                if (trail == null)
                    continue;

                trail.Clear();
                trail.emitting = true;
            }
        }

        if (motionParticles == null)
            return;

        foreach (ParticleSystem particles in motionParticles)
        {
            if (particles == null)
                continue;

            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }
    }

    public bool TryBeginRelease(Action onComplete)
    {
        if (!isActiveAndEnabled || releaseDelay <= 0f || !HasTrail())
            return false;

        if (releaseRoutine != null)
            return true;



        if (trails != null)
        {
            foreach (TrailRenderer trail in trails)
            {
                if (trail != null)
                    trail.emitting = false;
            }
        }

        if (motionParticles != null)
        {
            foreach (ParticleSystem particles in motionParticles)
            {
                if (particles != null)
                {
                    particles.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        releaseRoutine = StartCoroutine(
            CompleteReleaseAfterDelay(onComplete));
        return true;
    }

public void ResetForPool()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        if (projectileVisualRoot != null)
        {
            projectileVisualRoot.transform.localScale = visualBaseScale;
            projectileVisualRoot.SetActive(false);
        }

        if (trails == null)
            return;

        foreach (TrailRenderer trail in trails)
        {
            if (trail == null)
                continue;

            trail.emitting = false;
            trail.Clear();
        }
    }

private IEnumerator CompleteReleaseAfterDelay(Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < releaseDelay)
        {
            elapsed += Time.deltaTime;
            if (projectileVisualRoot != null)
            {
                float remainingScale =
                    1f - Mathf.Clamp01(elapsed / releaseDelay);
                projectileVisualRoot.transform.localScale =
                    visualBaseScale * remainingScale;
            }

            yield return null;
        }

        if (projectileVisualRoot != null)
        {
            projectileVisualRoot.SetActive(false);
            projectileVisualRoot.transform.localScale = visualBaseScale;
        }

        releaseRoutine = null;
        if (trails != null)
        {
            foreach (TrailRenderer trail in trails)
            {
                if (trail != null)
                    trail.Clear();
            }
        }

        onComplete?.Invoke();
    }

    private bool HasTrail()
    {
        if (trails == null)
            return false;

        foreach (TrailRenderer trail in trails)
        {
            if (trail != null)
                return true;
        }

        return false;
    }
}
