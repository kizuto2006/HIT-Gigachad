using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawn : MonoBehaviour
{
    [Header("References")]
    public GameObject enemyPrefab;
    public Transform playerTransform;

    [Header("Spawn Area")]
    [Min(0f)] public float minSpawnRadius = 15f;
    [Min(0.1f)] public float spawnRadius = 25f;
    [Tooltip("Bán kính rải quái quanh tâm của một đàn.")]
    [Min(0f)] public float groupSpreadRadius = 2.5f;

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

    private ObjectPool<GameObject> enemyPool;
    private Coroutine spawnRoutine;

    private void Start()
    {
        if (enemyPrefab == null || playerTransform == null)
        {
            Debug.LogError("[EnemySpawn] Cần gán Enemy Prefab và Player Transform.", this);
            enabled = false;
            return;
        }

        enemyPool = new ObjectPool<GameObject>(
            createFunc: CreatePooledEnemy,
            actionOnGet: _ => { },
            actionOnRelease: enemy => enemy.SetActive(false),
            actionOnDestroy: enemy => Destroy(enemy),
            collectionCheck: false,
            defaultCapacity: 100,
            maxSize: 1000
        );

        PrewarmPool();
        spawnRoutine = StartCoroutine(SpawnGroups());
    }

    private void OnEnable()
    {
        if (enemyPool != null && spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnGroups());
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        if (enemyPool != null)
        {
            activeEnemyCount = enemyPool.CountActive;
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
        int availableSlots = maxActiveEnemies - enemyPool.CountActive;
        if (availableSlots <= 0)
        {
            return;
        }

        UpdateCurrentGroupSize();
        int groupSize = Random.Range(currentMinGroupSize, currentMaxGroupSize + 1);
        groupSize = Mathf.Min(groupSize, availableSlots);

        Vector2 direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        float distance = Random.Range(minSpawnRadius, spawnRadius);
        Vector3 groupCenter = playerTransform.position + new Vector3(
            direction.x * distance,
            0f,
            direction.y * distance
        );

        for (int i = 0; i < groupSize; i++)
        {
            Vector2 offset = Random.insideUnitCircle * groupSpreadRadius;
            Vector3 spawnPosition = groupCenter + new Vector3(offset.x, 0f, offset.y);
            spawnPosition.y = GetGroundHeight(spawnPosition);

            GameObject enemy = enemyPool.Get();
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

    private GameObject CreatePooledEnemy()
    {
        GameObject enemy = Instantiate(enemyPrefab, enemyContainer);
        enemy.SetActive(false);
        return enemy;
    }

    private void PrewarmPool()
    {
        int count = Mathf.Clamp(initialPoolSize, 0, 1000);
        if (count == 0)
        {
            return;
        }

        GameObject[] prewarmedEnemies = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            prewarmedEnemies[i] = enemyPool.Get();
        }

        for (int i = 0; i < count; i++)
        {
            enemyPool.Release(prewarmedEnemies[i]);
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
        if (enemyPool == null || enemy == null)
        {
            return;
        }

        enemyPool.Release(enemy);
    }

    private void OnValidate()
    {
        minSpawnRadius = Mathf.Max(0f, minSpawnRadius);
        spawnRadius = Mathf.Max(minSpawnRadius, spawnRadius);
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
