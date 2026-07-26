using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Scene-local pools for projectile prefabs and their short-lived impact effects.
/// A separate pool is maintained for each prefab.
/// </summary>
public sealed class ProjectilePool : MonoBehaviour
{
    private const int DefaultProjectileCapacity = 16;
    private const int MaxProjectilePoolSize = 256;
    private const int DefaultEffectCapacity = 16;
    private const int MaxEffectPoolSize = 512;

    private static ProjectilePool instance;

    private readonly Dictionary<GameObject, ObjectPool<Projectile>> projectilePools =
        new Dictionary<GameObject, ObjectPool<Projectile>>();
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> effectPools =
        new Dictionary<GameObject, ObjectPool<GameObject>>();
    private readonly List<ActiveEffect> activeEffects =
        new List<ActiveEffect>(DefaultEffectCapacity);

    private struct ActiveEffect
    {
        public GameObject Effect;
        public ObjectPool<GameObject> OwnerPool;
        public float ReleaseTime;
    }

    public static Projectile Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        ProjectilePool poolManager = GetOrCreateInstance();
        return poolManager.SpawnProjectile(prefab, position, rotation);
    }

    public static void SpawnEffect(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float lifetime)
    {
        if (prefab == null)
            return;

        ProjectilePool poolManager = GetOrCreateInstance();
        poolManager.SpawnEffectInternal(
            prefab,
            position,
            rotation,
            Mathf.Max(0.01f, lifetime));
    }

    private static ProjectilePool GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<ProjectilePool>();
        if (instance != null)
            return instance;

        GameObject poolObject = new GameObject("[ProjectilePool]");
        instance = poolObject.AddComponent<ProjectilePool>();
        return instance;
    }

    private Projectile SpawnProjectile(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        ObjectPool<Projectile> pool = GetOrCreateProjectilePool(prefab);
        Projectile projectile = pool.Get();
        projectile.PrepareForSpawn(position, rotation);
        return projectile;
    }

    private ObjectPool<Projectile> GetOrCreateProjectilePool(GameObject prefab)
    {
        if (projectilePools.TryGetValue(
            prefab,
            out ObjectPool<Projectile> existingPool))
        {
            return existingPool;
        }

        ObjectPool<Projectile> pool = null;
        pool = new ObjectPool<Projectile>(
            createFunc: () => CreateProjectile(prefab, pool),
            actionOnGet: null,
            actionOnRelease: projectile => projectile.ReturnToPool(),
            actionOnDestroy: projectile => Destroy(projectile.gameObject),
            collectionCheck: false,
            defaultCapacity: DefaultProjectileCapacity,
            maxSize: MaxProjectilePoolSize);

        projectilePools.Add(prefab, pool);
        return pool;
    }

    private Projectile CreateProjectile(
        GameObject prefab,
        ObjectPool<Projectile> ownerPool)
    {
        GameObject projectileObject = Instantiate(prefab, transform);
        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<Projectile>();

        projectile.SetPoolRelease(ownerPool.Release);
        projectileObject.SetActive(false);
        return projectile;
    }

    private void SpawnEffectInternal(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float lifetime)
    {
        ObjectPool<GameObject> pool = GetOrCreateEffectPool(prefab);
        GameObject effect = pool.Get();
        effect.transform.SetPositionAndRotation(position, rotation);
        effect.SetActive(true);
        RestartParticles(effect);

        activeEffects.Add(new ActiveEffect
        {
            Effect = effect,
            OwnerPool = pool,
            ReleaseTime = Time.time + lifetime
        });
    }

    private ObjectPool<GameObject> GetOrCreateEffectPool(GameObject prefab)
    {
        if (effectPools.TryGetValue(
            prefab,
            out ObjectPool<GameObject> existingPool))
        {
            return existingPool;
        }

        ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject effect = Instantiate(prefab, transform);
                effect.SetActive(false);
                return effect;
            },
            actionOnGet: null,
            actionOnRelease: effect =>
            {
                StopParticles(effect);
                effect.SetActive(false);
            },
            actionOnDestroy: effect => Destroy(effect),
            collectionCheck: false,
            defaultCapacity: DefaultEffectCapacity,
            maxSize: MaxEffectPoolSize);

        effectPools.Add(prefab, pool);
        return pool;
    }

    private void Update()
    {
        float currentTime = Time.time;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect activeEffect = activeEffects[i];
            if (activeEffect.Effect != null &&
                currentTime < activeEffect.ReleaseTime)
            {
                continue;
            }

            int lastIndex = activeEffects.Count - 1;
            activeEffects[i] = activeEffects[lastIndex];
            activeEffects.RemoveAt(lastIndex);

            if (activeEffect.Effect != null)
                activeEffect.OwnerPool.Release(activeEffect.Effect);
        }
    }

    private static void RestartParticles(GameObject target)
    {
        ParticleSystem[] particles =
            target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }

    private static void StopParticles(GameObject target)
    {
        ParticleSystem[] particles =
            target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        activeEffects.Clear();

        foreach (ObjectPool<Projectile> pool in projectilePools.Values)
            pool.Clear();
        foreach (ObjectPool<GameObject> pool in effectPools.Values)
            pool.Clear();

        projectilePools.Clear();
        effectPools.Clear();

        if (instance == this)
            instance = null;
    }
}
