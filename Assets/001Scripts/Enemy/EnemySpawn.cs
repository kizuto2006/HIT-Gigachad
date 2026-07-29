using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;


public class EnemySpawn : MonoBehaviour
{
    [System.Serializable]
    public sealed class EnemySpawnType
    {
        public GameObject prefab;
        [Min(0f)] public float earlyWeight = 1f;
        [Min(0f)] public float lateWeight = 1f;
    }
    [Header("References")]
    [Tooltip("Các enemy bổ sung sẽ được spawn ngẫu nhiên cùng enemyPrefab.")]
    public GameObject[] additionalEnemyPrefabs;
    public GameObject enemyPrefab;
    public Transform playerTransform;

    [Header("Enemy Mix Over Time")]
    [Tooltip("Weighted enemy selection. Falls back to the legacy prefab fields when empty.")]
    public EnemySpawnType[] enemyTypes;
    [Min(0f)] public float enemyMixRampStart = 60f;
    [Min(0.1f)] public float enemyMixTransitionDuration = 30f;

    [Header("Spawn Area")]
    [Min(0f)] public float minSpawnRadius = 15f;
    [Min(0.1f)] public float spawnRadius = 25f;
    [Tooltip("Bán kính rải quái quanh tâm của một đàn.")]
    [Min(0f)] public float groupSpreadRadius = 2.5f;
    [Tooltip("Tỷ lệ một đàn được ưu tiên spawn trong cung phía trước hướng nhìn của Player.")]
    [Range(0f, 1f)] public float frontSpawnChance = 0.75f;
    [Tooltip("Nửa góc của cung spawn phía trước. 65 nghĩa là đàn có thể lệch tối đa 65 độ sang mỗi bên.")]
    [Range(0f, 180f)] public float frontSpawnHalfAngle = 65f;
    [Tooltip("Only colliders on these layers can be used as spawn ground.")]
    [SerializeField] private LayerMask groundMask = 1 << 7;

    [Header("Group Size Every 30 Seconds")]
    [Min(1)] public int startingMinGroupSize = 1;
    [Min(1)] public int startingMaxGroupSize = 2;
    [Min(0.1f)] public float groupSizeStepSeconds = 30f;
    [Min(0)] public int groupSizeIncreasePerStep = 1;
    [Min(1)] public int maximumGroupSize = 10;

    [Header("Spawn Timing")]
    [Tooltip("Time between enemy groups.")]
    [Min(0.05f)] public float groupInterval = 2.25f;
    [Tooltip("Maximum active enemies allowed at once.")]
    [Min(1)] public int maxActiveEnemies = 300;

    [Header("Pool")]
    [Tooltip("Số enemy được tạo sẵn khi bắt đầu scene.")]
    [Min(0)] public int initialPoolSize = 100;

    [Header("Spawn Emergence")]
    [Tooltip("Độ sâu quái bắt đầu ở dưới mặt đất.")]
    [Min(0f)] public float emergenceDepth = 2f;
    [Tooltip("Thời gian để quái trồi hoàn toàn lên mặt đất.")]
    [Min(0.05f)] public float emergenceDuration = 0.8f;
    [Tooltip("Độ trễ giữa từng con trong cùng một đàn.")]
    [Min(0f)] public float emergenceStagger = 0.08f;

    [Header("Mini Bosses")]
    [Tooltip("Tỷ lệ mỗi enemy trong đàn trở thành mini boss.")]
    [Range(0f, 1f)] public float miniBossChance = 0.12f;
    [Min(1f)] public float miniBossScaleMultiplier = 1.5f;
    [Min(1f)] public float miniBossHpMultiplier = 3f;
    [Min(1f)] public float miniBossDamageMultiplier = 1.5f;

    [Header("Organization")]
    public Transform enemyContainer;

    [Header("Thống kê (Chỉ xem)")]
    public int activeEnemyCount;
    [SerializeField] private float elapsedTime;
    [SerializeField] private int currentMinGroupSize;
    [SerializeField] private int currentMaxGroupSize;


    private readonly Dictionary<GameObject, ObjectPool<GameObject>> poolByPrefab =
        new Dictionary<GameObject, ObjectPool<GameObject>>();
    private readonly List<ObjectPool<GameObject>> enemyPools = new List<ObjectPool<GameObject>>();
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> poolByEnemy =
        new Dictionary<GameObject, ObjectPool<GameObject>>();
    private Coroutine spawnRoutine;

    private void Start()
    {
        if (!HasAnyEnemyPrefab() || playerTransform == null)
        {
            Debug.LogError("[EnemySpawn] Cần gán ít nhất một Enemy Prefab và Player Transform.", this);
            enabled = false;
            return;
        }

        CreatePools();
        PrewarmPool();
        spawnRoutine = StartCoroutine(SpawnGroups());
    }

    private void OnEnable()
    {
        if (enemyPools.Count > 0 && spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnGroups());
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        if (enemyPools.Count > 0)
        {
            activeEnemyCount = GetActiveEnemyCount();
        }

        UpdateCurrentGroupSize();
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnGroups()
    {
        yield return new WaitForSeconds(1f);

        while (true)
        {
            SpawnGroup();
            yield return new WaitForSeconds(groupInterval);
        }
    }

    private void SpawnGroup()
    {
        int availableSlots = maxActiveEnemies - GetActiveEnemyCount();
        if (availableSlots <= 0)
        {
            return;
        }

        UpdateCurrentGroupSize();
        int groupSize = Random.Range(currentMinGroupSize, currentMaxGroupSize + 1);
        groupSize = Mathf.Min(groupSize, availableSlots);

        Vector3 spawnDirection = GetSpawnDirection();
        float distance = Random.Range(minSpawnRadius, spawnRadius);
        Vector3 groupCenter = playerTransform.position + spawnDirection * distance;

        for (int i = 0; i < groupSize; i++)
        {
            Vector2 offset = Random.insideUnitCircle * groupSpreadRadius;
            Vector3 spawnPosition = groupCenter + new Vector3(offset.x, 0f, offset.y);
            spawnPosition.y = GetGroundHeight(spawnPosition);

            ObjectPool<GameObject> selectedPool = SelectEnemyPool();
            GameObject enemy = selectedPool.Get();
            EnemyMiniBoss miniBoss = enemy.GetComponent<EnemyMiniBoss>();
            if (miniBoss != null)
            {
                miniBoss.Configure(
                    Random.value < miniBossChance,
                    miniBossScaleMultiplier,
                    miniBossHpMultiplier,
                    miniBossDamageMultiplier);
            }

            EnemySpawnEmergence emergence = enemy.GetComponent<EnemySpawnEmergence>();
            if (emergence == null)
            {
                emergence = enemy.AddComponent<EnemySpawnEmergence>();
            }

            emergence.Prepare(
                spawnPosition,
                emergenceDepth,
                emergenceDuration,
                i * emergenceStagger
            );
            enemy.SetActive(true);
        }
    }

    private Vector3 GetSpawnDirection()
    {
        if (Random.value <= frontSpawnChance)
        {
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.001f)
            {
                playerForward = Vector3.forward;
            }

            float angle = Random.Range(-frontSpawnHalfAngle, frontSpawnHalfAngle);
            return Quaternion.Euler(0f, angle, 0f) * playerForward.normalized;
        }

        Vector2 randomDirection = Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude < 0.001f)
        {
            randomDirection = Vector2.right;
        }

        randomDirection.Normalize();
        return new Vector3(randomDirection.x, 0f, randomDirection.y);
    }


private GameObject CreatePooledEnemy(GameObject prefab, ObjectPool<GameObject> ownerPool)
    {
        GameObject enemy = Instantiate(prefab, enemyContainer);
        enemy.SetActive(false);
        poolByEnemy[enemy] = ownerPool;

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.SetSpawner(this);

            if (enemy.GetComponent<EnemyMiniBoss>() == null)
            {
                enemy.AddComponent<EnemyMiniBoss>();
            }
        }

        return enemy;
    }

    private void CreatePools()
    {
        enemyPools.Clear();
        poolByPrefab.Clear();
        poolByEnemy.Clear();

        List<GameObject> prefabs = GetUniqueEnemyPrefabs();
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            ObjectPool<GameObject> pool = null;
            pool = new ObjectPool<GameObject>(
                createFunc: () => CreatePooledEnemy(prefab, pool),
                actionOnGet: _ => { },
                actionOnRelease: enemy => enemy.SetActive(false),
                actionOnDestroy: enemy =>
                {
                    poolByEnemy.Remove(enemy);
                    Destroy(enemy);
                },
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, initialPoolSize / prefabs.Count),
                maxSize: 1000
            );
            enemyPools.Add(pool);
            poolByPrefab[prefab] = pool;
        }
    }

    private bool HasAnyEnemyPrefab()
    {
        return GetUniqueEnemyPrefabs().Count > 0;
    }

    private List<GameObject> GetUniqueEnemyPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();

        if (enemyTypes != null)
        {
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                EnemySpawnType spawnType = enemyTypes[i];
                if (spawnType != null)
                {
                    AddUniquePrefab(prefabs, spawnType.prefab);
                }
            }
        }

        if (prefabs.Count == 0)
        {
            AddUniquePrefab(prefabs, enemyPrefab);

            if (additionalEnemyPrefabs != null)
            {
                for (int i = 0; i < additionalEnemyPrefabs.Length; i++)
                {
                    AddUniquePrefab(prefabs, additionalEnemyPrefabs[i]);
                }
            }
        }

        return prefabs;
    }

    private ObjectPool<GameObject> SelectEnemyPool()
    {
        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            return enemyPools[Random.Range(0, enemyPools.Count)];
        }

        float mixProgress = Mathf.InverseLerp(
            enemyMixRampStart,
            enemyMixRampStart + enemyMixTransitionDuration,
            elapsedTime);
        float totalWeight = 0f;

        for (int i = 0; i < enemyTypes.Length; i++)
        {
            EnemySpawnType spawnType = enemyTypes[i];
            if (spawnType != null && spawnType.prefab != null && poolByPrefab.ContainsKey(spawnType.prefab))
            {
                totalWeight += Mathf.Lerp(spawnType.earlyWeight, spawnType.lateWeight, mixProgress);
            }
        }

        if (totalWeight <= 0f)
        {
            return enemyPools[Random.Range(0, enemyPools.Count)];
        }

        float selection = Random.value * totalWeight;
        for (int i = 0; i < enemyTypes.Length; i++)
        {
            EnemySpawnType spawnType = enemyTypes[i];
            if (spawnType == null || spawnType.prefab == null || !poolByPrefab.TryGetValue(spawnType.prefab, out ObjectPool<GameObject> pool))
            {
                continue;
            }

            selection -= Mathf.Lerp(spawnType.earlyWeight, spawnType.lateWeight, mixProgress);
            if (selection <= 0f)
            {
                return pool;
            }
        }

        return enemyPools[enemyPools.Count - 1];
    }

    private static void AddUniquePrefab(List<GameObject> prefabs, GameObject prefab)
    {
        if (prefab != null && !prefabs.Contains(prefab))
        {
            prefabs.Add(prefab);
        }
    }

    private int GetActiveEnemyCount()
    {
        int count = 0;
        for (int i = 0; i < enemyPools.Count; i++)
        {
            count += enemyPools[i].CountActive;
        }
        return count;
    }


    private void PrewarmPool()
    {
        int count = Mathf.Clamp(initialPoolSize, 0, 1000);
        if (count == 0)
        {
            return;
        }

        GameObject[] prewarmedEnemies = new GameObject[count];
        ObjectPool<GameObject>[] prewarmedPools = new ObjectPool<GameObject>[count];
        for (int i = 0; i < count; i++)
        {
            ObjectPool<GameObject> pool = enemyPools[i % enemyPools.Count];
            prewarmedPools[i] = pool;
            prewarmedEnemies[i] = pool.Get();
        }

        for (int i = 0; i < count; i++)
        {
            prewarmedPools[i].Release(prewarmedEnemies[i]);
        }
    }

    private void UpdateCurrentGroupSize()
    {
        int step = Mathf.FloorToInt(elapsedTime / Mathf.Max(0.1f, groupSizeStepSeconds));
        int increase = step * Mathf.Max(0, groupSizeIncreasePerStep);
        currentMinGroupSize = Mathf.Min(startingMinGroupSize + increase, maximumGroupSize);
        currentMaxGroupSize = Mathf.Min(startingMaxGroupSize + increase, maximumGroupSize);
        currentMaxGroupSize = Mathf.Max(currentMinGroupSize, currentMaxGroupSize);
    }





private float GetGroundHeight(Vector3 position)
    {
        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
        }

        Vector3 rayOrigin = new Vector3(position.x, 100f, position.z);
        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            200f,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return playerTransform != null ? playerTransform.position.y : 0f;
    }

    public void ReturnEnemyToPool(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        ObjectPool<GameObject> ownerPool;
        if (poolByEnemy.TryGetValue(enemy, out ownerPool))
        {
            ownerPool.Release(enemy);
        }
        else
        {
            Debug.LogWarning("[EnemySpawn] Enemy không thuộc pool của spawner này.", enemy);
            enemy.SetActive(false);
        }
    }

    private void OnValidate()
    {
        minSpawnRadius = Mathf.Max(0f, minSpawnRadius);
        spawnRadius = Mathf.Max(minSpawnRadius, spawnRadius);
        groupSpreadRadius = Mathf.Max(0f, groupSpreadRadius);
        frontSpawnChance = Mathf.Clamp01(frontSpawnChance);
        frontSpawnHalfAngle = Mathf.Clamp(frontSpawnHalfAngle, 0f, 180f);
        startingMaxGroupSize = Mathf.Max(startingMinGroupSize, startingMaxGroupSize);
        groupSizeStepSeconds = Mathf.Max(0.1f, groupSizeStepSeconds);
        groupSizeIncreasePerStep = Mathf.Max(0, groupSizeIncreasePerStep);
        maximumGroupSize = Mathf.Max(startingMaxGroupSize, maximumGroupSize);
        groupInterval = Mathf.Max(0.05f, groupInterval);
        enemyMixRampStart = Mathf.Max(0f, enemyMixRampStart);
        enemyMixTransitionDuration = Mathf.Max(0.1f, enemyMixTransitionDuration);
        maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
        initialPoolSize = Mathf.Clamp(initialPoolSize, 0, 1000);
        emergenceDepth = Mathf.Max(0f, emergenceDepth);
        emergenceDuration = Mathf.Max(0.05f, emergenceDuration);
        emergenceStagger = Mathf.Max(0f, emergenceStagger);
        miniBossChance = Mathf.Clamp01(miniBossChance);
        miniBossScaleMultiplier = Mathf.Max(1f, miniBossScaleMultiplier);
        miniBossHpMultiplier = Mathf.Max(1f, miniBossHpMultiplier);
        miniBossDamageMultiplier = Mathf.Max(1f, miniBossDamageMultiplier);

        if (enemyTypes != null)
        {
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                if (enemyTypes[i] == null)
                {
                    continue;
                }

                enemyTypes[i].earlyWeight = Mathf.Max(0f, enemyTypes[i].earlyWeight);
                enemyTypes[i].lateWeight = Mathf.Max(0f, enemyTypes[i].lateWeight);
            }
        }
    }

}

/// <summary>
/// Trạng thái xuất hiện tạm thời được gắn runtime lên enemy trong pool.
/// Trong lúc trồi lên, enemy không di chuyển, nhận sát thương hoặc va chạm.
/// </summary>
internal sealed class EnemySpawnEmergence : MonoBehaviour
{
    private EnemyAI enemyAI;
    private EnemyHealth enemyHealth;
    private Collider[] colliders;
    private bool[] colliderEnabledStates;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float duration;
    private float delay;
    private bool prepared;
    private Coroutine emergenceRoutine;

    public void Prepare(Vector3 groundPosition, float depth, float riseDuration, float startDelay)
    {
        CacheComponents();

        targetPosition = groundPosition;
        startPosition = groundPosition - Vector3.up * depth;
        duration = Mathf.Max(0.05f, riseDuration);
        delay = Mathf.Max(0f, startDelay);
        prepared = true;

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        if (enemyHealth != null)
        {
            enemyHealth.SetSpawnProtection(true);
            enemyHealth.enabled = false;
        }

        colliderEnabledStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderEnabledStates[i] = colliders[i] != null && colliders[i].enabled;
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        // Keep the spawn rotation (the boss is already facing the player).
        transform.position = startPosition;

        // Pooled enemies are prepared while inactive and start in OnEnable.
        // Dedicated spawners instantiate active objects, so start immediately.
        if (gameObject.activeInHierarchy)
        {
            emergenceRoutine = StartCoroutine(Emerge());
        }
    }

    private void OnEnable()
    {
        if (prepared && emergenceRoutine == null)
        {
            emergenceRoutine = StartCoroutine(Emerge());
        }
    }

    private void OnDisable()
    {
        if (emergenceRoutine != null)
        {
            StopCoroutine(emergenceRoutine);
            emergenceRoutine = null;
        }
    }

    private IEnumerator Emerge()
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, easedProgress);
            yield return null;
        }

        transform.position = targetPosition;
        RestoreGameplayComponents();
        prepared = false;
        emergenceRoutine = null;
    }

    private void CacheComponents()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void RestoreGameplayComponents()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = colliderEnabledStates[i];
            }
        }

        if (enemyHealth != null)
        {
            enemyHealth.SetSpawnProtection(false);
            enemyHealth.enabled = true;
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = true;
        }
    }
}
