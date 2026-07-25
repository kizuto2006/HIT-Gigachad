using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawn : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Các enemy bổ sung sẽ được spawn ngẫu nhiên cùng enemyPrefab.")]
    public GameObject[] additionalEnemyPrefabs;
    public GameObject enemyPrefab;
    public Transform playerTransform;

    [Header("Spawn Area")]
    [Min(0f)] public float minSpawnRadius = 15f;
    [Min(0.1f)] public float spawnRadius = 25f;
    [Tooltip("Bán kính rải quái quanh tâm của một đàn.")]
    [Min(0f)] public float groupSpreadRadius = 2.5f;
    [Tooltip("Tỷ lệ một đàn được ưu tiên spawn trong cung phía trước hướng nhìn của Player.")]
    [Range(0f, 1f)] public float frontSpawnChance = 0.75f;
    [Tooltip("Nửa góc của cung spawn phía trước. 65 nghĩa là đàn có thể lệch tối đa 65 độ sang mỗi bên.")]
    [Range(0f, 180f)] public float frontSpawnHalfAngle = 65f;

    [Header("Group Size Over Time")]
    [Tooltip("Số quái ít nhất trong một đàn ở đầu trận.")]
    [Min(1)] public int startingMinGroupSize = 1;
    [Tooltip("Số quái nhiều nhất trong một đàn ở đầu trận.")]
    [Min(1)] public int startingMaxGroupSize = 2;
    [Tooltip("Số quái ít nhất trong một đàn khi đạt thời gian tăng tối đa.")]
    [Min(1)] public int finalMinGroupSize = 3;
    [Tooltip("Số quái nhiều nhất trong một đàn khi đạt thời gian tăng tối đa.")]
    [Min(1)] public int finalMaxGroupSize = 5;
    [Tooltip("Sau số phút này, một đàn sẽ đạt kích thước 3-5 con.")]
    [Min(0.1f)] public float minutesToMaxGroupSize = 3f;

    [Header("Spawn Timing")]
    [Tooltip("Khoảng nghỉ giữa hai đàn ở đầu trận.")]
    [Min(0.05f)] public float startingGroupInterval = 2.5f;
    [Tooltip("Khoảng nghỉ giữa hai đàn khi độ khó đã tăng tối đa.")]
    [Min(0.05f)] public float finalGroupInterval = 1.5f;
    [Tooltip("Giới hạn số quái đang hoạt động để tránh quá tải.")]
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

    [Header("Organization")]
    public Transform enemyContainer;

    [Header("Thống kê (Chỉ xem)")]
    public int activeEnemyCount;
    [SerializeField] private float elapsedTime;
    [SerializeField] private int currentMinGroupSize;
    [SerializeField] private int currentMaxGroupSize;

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
            yield return new WaitForSeconds(GetCurrentGroupInterval());
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

            ObjectPool<GameObject> selectedPool = enemyPools[Random.Range(0, enemyPools.Count)];
            GameObject enemy = selectedPool.Get();
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
        return enemy;
    }

    private void CreatePools()
    {
        enemyPools.Clear();
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
        }
    }

    private bool HasAnyEnemyPrefab()
    {
        return GetUniqueEnemyPrefabs().Count > 0;
    }

    private List<GameObject> GetUniqueEnemyPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
        AddUniquePrefab(prefabs, enemyPrefab);

        if (additionalEnemyPrefabs != null)
        {
            for (int i = 0; i < additionalEnemyPrefabs.Length; i++)
            {
                AddUniquePrefab(prefabs, additionalEnemyPrefabs[i]);
            }
        }

        return prefabs;
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
        float progress = GetDifficultyProgress();
        currentMinGroupSize = Mathf.RoundToInt(Mathf.Lerp(startingMinGroupSize, finalMinGroupSize, progress));
        currentMaxGroupSize = Mathf.RoundToInt(Mathf.Lerp(startingMaxGroupSize, finalMaxGroupSize, progress));
        currentMaxGroupSize = Mathf.Max(currentMinGroupSize, currentMaxGroupSize);
    }

    private float GetCurrentGroupInterval()
    {
        return Mathf.Lerp(startingGroupInterval, finalGroupInterval, GetDifficultyProgress());
    }

    private float GetDifficultyProgress()
    {
        float duration = Mathf.Max(0.1f, minutesToMaxGroupSize) * 60f;
        return Mathf.Clamp01(elapsedTime / duration);
    }

    private float GetGroundHeight(Vector3 position)
    {
        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
        }

        if (Physics.Raycast(new Vector3(position.x, 100f, position.z), Vector3.down, out RaycastHit hit, 200f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return 0f;
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
        finalMaxGroupSize = Mathf.Max(finalMinGroupSize, finalMaxGroupSize);
        maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
        initialPoolSize = Mathf.Clamp(initialPoolSize, 0, 1000);
        emergenceDepth = Mathf.Max(0f, emergenceDepth);
        emergenceDuration = Mathf.Max(0.05f, emergenceDuration);
        emergenceStagger = Mathf.Max(0f, emergenceStagger);
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

        transform.SetPositionAndRotation(startPosition, Quaternion.identity);
    }

    private void OnEnable()
    {
        if (prepared)
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
            enemyHealth.enabled = true;
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = true;
        }
    }
}
