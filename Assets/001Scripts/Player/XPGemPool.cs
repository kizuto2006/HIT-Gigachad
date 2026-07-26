using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Scene-local pool for XP gems. A separate pool is created for each gem prefab.
/// </summary>
public sealed class XPGemPool : MonoBehaviour
{
    private const int DefaultCapacity = 64;
    private const int MaxPoolSize = 2048;

    private static XPGemPool instance;

    private readonly Dictionary<GameObject, ObjectPool<XPGem>> pools =
        new Dictionary<GameObject, ObjectPool<XPGem>>();

    public static XPGem Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        XPGemPool poolManager = GetOrCreateInstance();
        return poolManager.SpawnInternal(prefab, position, rotation);
    }

    private static XPGemPool GetOrCreateInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<XPGemPool>();
        if (instance != null)
        {
            return instance;
        }

        GameObject poolObject = new GameObject("[XPGemPool]");
        instance = poolObject.AddComponent<XPGemPool>();
        return instance;
    }

    private XPGem SpawnInternal(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        ObjectPool<XPGem> pool = GetOrCreatePool(prefab);
        XPGem gem = pool.Get();
        gem.transform.SetPositionAndRotation(position, rotation);
        gem.gameObject.SetActive(true);
        return gem;
    }

    private ObjectPool<XPGem> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<XPGem> existingPool))
        {
            return existingPool;
        }

        ObjectPool<XPGem> pool = null;
        pool = new ObjectPool<XPGem>(
            createFunc: () => CreateGem(prefab, pool),
            actionOnGet: null,
            actionOnRelease: gem => gem.gameObject.SetActive(false),
            actionOnDestroy: gem => Destroy(gem.gameObject),
            collectionCheck: false,
            defaultCapacity: DefaultCapacity,
            maxSize: MaxPoolSize);

        pools.Add(prefab, pool);
        Prewarm(pool);
        return pool;
    }

    private XPGem CreateGem(GameObject prefab, ObjectPool<XPGem> ownerPool)
    {
        GameObject instanceObject = Instantiate(prefab, transform);
        XPGem gem = instanceObject.GetComponent<XPGem>();
        if (gem == null)
        {
            Destroy(instanceObject);
            throw new MissingComponentException(
                string.Format("XP gem prefab '{0}' must contain an XPGem component on its root.", prefab.name));
        }

        gem.SetPoolRelease(ownerPool.Release);
        instanceObject.SetActive(false);
        return gem;
    }

    private static void Prewarm(ObjectPool<XPGem> pool)
    {
        XPGem[] gems = new XPGem[DefaultCapacity];
        for (int i = 0; i < gems.Length; i++)
        {
            gems[i] = pool.Get();
        }

        for (int i = 0; i < gems.Length; i++)
        {
            pool.Release(gems[i]);
        }
    }

    private void OnDestroy()
    {
        foreach (ObjectPool<XPGem> pool in pools.Values)
        {
            pool.Clear();
        }

        pools.Clear();
        if (instance == this)
        {
            instance = null;
        }
    }
}