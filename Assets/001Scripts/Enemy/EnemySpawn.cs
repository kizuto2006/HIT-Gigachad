using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;


public class EnemySpawn : MonoBehaviour
{
    private const float MinimumAllowedSpawnRadius = 3f;

    [System.Serializable]
    public sealed class EnemySpawnType
    {
        public GameObject prefab;
        [Min(0f)] public float earlyWeight = 1f;
        [Min(0f)] public float midWeight = 1f;
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
    [Min(0f)] public float enemyMixRampStart = 120f;
    [Min(0.1f)] public float enemyMixTransitionDuration = 30f;
    [Min(0f)] public float enemyMixLateRampStart = 240f;

    [Header("Spawn Area")]
    [Min(MinimumAllowedSpawnRadius)] public float minSpawnRadius = MinimumAllowedSpawnRadius;
    [Min(0.1f)] public float spawnRadius = 25f;
    [Tooltip("Bán kính rải quái quanh tâm của một đàn.")]
    [Min(0f)] public float groupSpreadRadius = 2.5f;
    [Tooltip("Tỷ lệ một đàn được ưu tiên spawn trong cung phía trước hướng nhìn của Player.")]
    [Range(0f, 1f)] public float frontSpawnChance = 0.75f;
    [Tooltip("Nửa góc của cung spawn phía trước. 65 nghĩa là đàn có thể lệch tối đa 65 độ sang mỗi bên.")]
    [Range(0f, 180f)] public float frontSpawnHalfAngle = 65f;
    [Tooltip("Only colliders on these layers can be used as spawn ground.")]
    [SerializeField] private LayerMask groundMask = 1 << 7;

    [Header("Normal Group Size Progression")]
    [Min(1)] public int startingMinGroupSize = 1;
    [Min(1)] public int startingMaxGroupSize = 2;
    [Min(0.1f)] public float groupSizeStepSeconds = 15f;
    [Min(0)] public int groupSizeIncreasePerStep = 1;
    [Min(1)] public int maximumGroupSize = 6;
    [Tooltip("Normal spawn groups are reduced by this multiplier during the opening period. Raid group sizes are unaffected.")]
    [Range(0.01f, 1f)] public float openingGroupSizeMultiplier = 0.5f;
    [Tooltip("Duration of the reduced normal spawn groups. Elite enemies cannot appear during this period.")]
    [Min(0f)] public float openingSpawnDuration = 60f;

    [Header("Spawn Timing")]
    [Tooltip("Time between enemy groups.")]
    [Min(0.05f)] public float groupInterval = 2.25f;
    [Tooltip("Duration from the start of the run where normal groups spawn more frequently.")]
    [Min(0f)] public float earlySpawnBoostDuration = 120f;
    [Tooltip("Multiplier applied to groupInterval during the early spawn boost. 0.75 means 25% less waiting between groups.")]
    [Range(0.1f, 1f)] public float earlySpawnIntervalMultiplier = 0.75f;
    [Tooltip("Maximum active enemies allowed at once.")]
    [Min(1)] public int maxActiveEnemies = 300;

    [Header("Raid Waves")]
    [Tooltip("Time between the start of each raid wave.")]
    [Min(1f)] public float raidInterval = 240f;
    [Tooltip("How long each raid wave lasts.")]
    [Min(0.1f)] public float raidDuration = 30f;
    [Tooltip("Time between raid bursts.")]
    [Min(0.1f)] public float raidBurstInterval = 2f;
    [Min(1)] public int raidMinGroupSize = 10;
    [Min(1)] public int raidMaxGroupSize = 12;

    [Header("Pool")]
    [Tooltip("Số enemy được tạo sẵn khi bắt đầu scene.")]
    [Min(0)] public int initialPoolSize = 100;

    [Header("Out-of-Bounds Cleanup")]
    [Tooltip("Enemy sẽ được trả về pool nếu khoảng cách ngang tới Player vượt quá giá trị này (m).")]
    [Min(1f)] public float maxEnemyDistanceFromPlayer = 100f;
    [Tooltip("Enemy sẽ được trả về pool nếu thấp hơn Player quá khoảng cách này (m).")]
    [Min(1f)] public float maxEnemyDropBelowPlayer = 40f;
    [Tooltip("Khoảng thời gian giữa hai lần kiểm tra enemy bị rơi hoặc đi quá xa.")]
    [Min(0.05f)] public float outOfBoundsCheckInterval = 0.25f;

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
    [Min(1f)] public float miniBossSpeedMultiplier = 1.35f;

    [Header("Organization")]
    public Transform enemyContainer;

    [Header("Thống kê (Chỉ xem)")]
    public int activeEnemyCount;
    [SerializeField] private float elapsedTime;
    [SerializeField] private int currentMinGroupSize;
    [SerializeField] private int currentMaxGroupSize;
    [SerializeField] private bool isRaidActive;


    private readonly Dictionary<GameObject, ObjectPool<GameObject>> poolByPrefab =
        new Dictionary<GameObject, ObjectPool<GameObject>>();
    private readonly List<ObjectPool<GameObject>> enemyPools = new List<ObjectPool<GameObject>>();
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> poolByEnemy =
        new Dictionary<GameObject, ObjectPool<GameObject>>();
    private Coroutine spawnRoutine;
    private Coroutine raidRoutine;
    private float nextRaidTime;
    private float nextOutOfBoundsCheckTime;
    private RaidAnnouncementUI raidAnnouncementUI;
    private bool musicDuckActive;
    private readonly List<GameObject> outOfBoundsEnemies = new List<GameObject>(64);

    public bool IsRaidActive => isRaidActive;

    private void Start()
    {
        minSpawnRadius = Mathf.Max(MinimumAllowedSpawnRadius, minSpawnRadius);
        spawnRadius = Mathf.Max(minSpawnRadius, spawnRadius);

        if (!HasAnyEnemyPrefab() || playerTransform == null)
        {
            Debug.LogError("[EnemySpawn] Cần gán ít nhất một Enemy Prefab và Player Transform.", this);
            enabled = false;
            return;
        }

        CreatePools();
        PrewarmPool();
        raidAnnouncementUI = RaidAnnouncementUI.Create(transform);
        nextRaidTime = raidInterval;
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
        CleanupOutOfBoundsEnemies();

        if (enemyPools.Count > 0)
        {
            activeEnemyCount = GetActiveEnemyCount();
        }

        UpdateCurrentGroupSize();

        if (raidRoutine == null && elapsedTime >= nextRaidTime)
        {
            do
            {
                nextRaidTime += raidInterval;
            }
            while (nextRaidTime <= elapsedTime);

            raidRoutine = StartCoroutine(SpawnRaid());
        }
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (raidRoutine != null)
        {
            StopCoroutine(raidRoutine);
            raidRoutine = null;
        }

        ReleaseMusicDuck();
        isRaidActive = false;
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

    private float GetCurrentGroupInterval()
    {
        float intervalMultiplier = elapsedTime < earlySpawnBoostDuration
            ? earlySpawnIntervalMultiplier
            : 1f;
        return Mathf.Max(0.05f, groupInterval * intervalMultiplier);
    }

    private void SpawnGroup()
    {
        if (isRaidActive)
        {
            return;
        }

        UpdateCurrentGroupSize();
        SpawnGroup(currentMinGroupSize, currentMaxGroupSize, false);
    }

    private IEnumerator SpawnRaid()
    {
        isRaidActive = true;
        RequestMusicDuck();
        if (raidAnnouncementUI == null)
        {
            raidAnnouncementUI = RaidAnnouncementUI.Create(transform);
        }
        raidAnnouncementUI.Show(raidDuration);
        SoundEffectsAudioManager.Instance?.PlayWarningSound();

        float raidEndTime = elapsedTime + raidDuration;

        while (elapsedTime < raidEndTime)
        {
            SpawnGroup(raidMinGroupSize, raidMaxGroupSize, true);
            yield return new WaitForSeconds(raidBurstInterval);
        }

        isRaidActive = false;
        ReleaseMusicDuck();
        raidRoutine = null;
    }

    private void RequestMusicDuck()
    {
        if (musicDuckActive || MusicAudioManager.Instance == null)
            return;

        MusicAudioManager.Instance.PushMusicDuck();
        musicDuckActive = true;
    }

    private void ReleaseMusicDuck()
    {
        if (!musicDuckActive)
            return;

        if (MusicAudioManager.Instance != null)
            MusicAudioManager.Instance.PopMusicDuck();
        musicDuckActive = false;
    }

    private void SpawnGroup(int minGroupSize, int maxGroupSize, bool scatterAroundPlayer)
    {
        int availableSlots = maxActiveEnemies - GetActiveEnemyCount();
        if (availableSlots <= 0)
        {
            return;
        }

        int groupSize = Random.Range(minGroupSize, maxGroupSize + 1);
        groupSize = Mathf.Min(groupSize, availableSlots);

        Vector3 spawnDirection = GetSpawnDirection(true);
        float distance = Random.Range(minSpawnRadius, spawnRadius);
        Vector3 groupCenter = playerTransform.position + spawnDirection * distance;

        for (int i = 0; i < groupSize; i++)
        {
            Vector3 spawnPosition;
            if (scatterAroundPlayer)
            {
                Vector3 raidDirection = GetSpawnDirection(false);
                float raidDistance = Random.Range(minSpawnRadius, spawnRadius);
                spawnPosition = playerTransform.position + raidDirection * raidDistance;
            }
            else
            {
                Vector2 offset = Random.insideUnitCircle * groupSpreadRadius;
                spawnPosition = groupCenter + new Vector3(offset.x, 0f, offset.y);
            }

            spawnPosition = EnforceMinimumSpawnDistance(spawnPosition, spawnDirection);
            spawnPosition.y = GetGroundHeight(spawnPosition);

            ObjectPool<GameObject> selectedPool = SelectEnemyPool();
            GameObject enemy = selectedPool.Get();
            EnemyMiniBoss miniBoss = enemy.GetComponent<EnemyMiniBoss>();
            if (miniBoss != null)
            {
                miniBoss.Configure(
                    elapsedTime >= openingSpawnDuration && Random.value < miniBossChance,
                    miniBossScaleMultiplier,
                    miniBossHpMultiplier,
                    miniBossDamageMultiplier,
                    miniBossSpeedMultiplier);
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

    /// <summary>
    /// Test hook để spawn một nhóm enemy thông qua pool hiện tại.
    /// </summary>
    public int SpawnTestGroup(int groupSize, bool scatterAroundPlayer = true)
    {
        if (enemyPools.Count == 0 || playerTransform == null)
        {
            return 0;
        }

        int countBefore = GetActiveEnemyCount();
        int requestedCount = Mathf.Max(1, groupSize);
        SpawnGroup(requestedCount, requestedCount, scatterAroundPlayer);
        return Mathf.Max(0, GetActiveEnemyCount() - countBefore);
    }

    private Vector3 EnforceMinimumSpawnDistance(Vector3 spawnPosition, Vector3 fallbackDirection)
    {
        Vector3 playerPosition = playerTransform.position;
        Vector3 offset = spawnPosition - playerPosition;
        offset.y = 0f;
        if (offset.sqrMagnitude >= minSpawnRadius * minSpawnRadius)
        {
            return spawnPosition;
        }

        Vector3 direction = offset.sqrMagnitude > 0.001f
            ? offset.normalized
            : fallbackDirection.normalized;
        spawnPosition.x = playerPosition.x + direction.x * minSpawnRadius;
        spawnPosition.z = playerPosition.z + direction.z * minSpawnRadius;
        return spawnPosition;
    }

    private Vector3 GetSpawnDirection(bool preferFront)
    {
        if (preferFront && Random.value <= frontSpawnChance)
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

        float totalWeight = 0f;

        for (int i = 0; i < enemyTypes.Length; i++)
        {
            EnemySpawnType spawnType = enemyTypes[i];
            if (spawnType != null && spawnType.prefab != null && poolByPrefab.ContainsKey(spawnType.prefab))
            {
                totalWeight += GetEnemySpawnWeight(spawnType);
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

            selection -= GetEnemySpawnWeight(spawnType);
            if (selection <= 0f)
            {
                return pool;
            }
        }

        return enemyPools[enemyPools.Count - 1];
    }

    private float GetEnemySpawnWeight(EnemySpawnType spawnType)
    {
        if (elapsedTime < enemyMixLateRampStart)
        {
            float midProgress = Mathf.InverseLerp(
                enemyMixRampStart,
                enemyMixRampStart + enemyMixTransitionDuration,
                elapsedTime);
            return Mathf.Lerp(spawnType.earlyWeight, spawnType.midWeight, midProgress);
        }

        float lateProgress = Mathf.InverseLerp(
            enemyMixLateRampStart,
            enemyMixLateRampStart + enemyMixTransitionDuration,
            elapsedTime);
        return Mathf.Lerp(spawnType.midWeight, spawnType.lateWeight, lateProgress);
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

    private void CleanupOutOfBoundsEnemies()
    {
        if (playerTransform == null || poolByEnemy.Count == 0 ||
            Time.time < nextOutOfBoundsCheckTime)
        {
            return;
        }

        nextOutOfBoundsCheckTime = Time.time + Mathf.Max(0.05f, outOfBoundsCheckInterval);

        Vector3 playerPosition = playerTransform.position;
        float maxDistanceSqr = Mathf.Max(1f, maxEnemyDistanceFromPlayer);
        maxDistanceSqr *= maxDistanceSqr;
        float minimumY = playerPosition.y - Mathf.Max(1f, maxEnemyDropBelowPlayer);

        outOfBoundsEnemies.Clear();
        foreach (GameObject enemy in poolByEnemy.Keys)
        {
            if (enemy == null || !enemy.activeInHierarchy)
            {
                continue;
            }

            EnemySpawnEmergence emergence = enemy.GetComponent<EnemySpawnEmergence>();
            if (emergence != null && emergence.IsEmerging)
            {
                // Không trả enemy về pool khi nó còn đang trồi lên. Nếu làm vậy,
                // các collider/EnemyHealth đang bị tắt trong quá trình trồi có
                // thể không được khôi phục ở lần spawn kế tiếp.
                continue;
            }

            Vector3 horizontalOffset = enemy.transform.position - playerPosition;
            horizontalOffset.y = 0f;
            bool isTooFar = horizontalOffset.sqrMagnitude > maxDistanceSqr;
            bool isTooLow = enemy.transform.position.y < minimumY;

            if (isTooFar || isTooLow)
            {
                outOfBoundsEnemies.Add(enemy);
            }
        }

        for (int i = 0; i < outOfBoundsEnemies.Count; i++)
        {
            GameObject enemy = outOfBoundsEnemies[i];
            if (enemy != null && enemy.activeInHierarchy)
            {
                ReturnEnemyToPool(enemy);
            }
        }

        outOfBoundsEnemies.Clear();
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

        if (elapsedTime < openingSpawnDuration)
        {
            currentMinGroupSize = Mathf.Max(1, Mathf.CeilToInt(currentMinGroupSize * openingGroupSizeMultiplier));
            currentMaxGroupSize = Mathf.Max(1, Mathf.CeilToInt(currentMaxGroupSize * openingGroupSizeMultiplier));
        }

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
        minSpawnRadius = Mathf.Max(MinimumAllowedSpawnRadius, minSpawnRadius);
        spawnRadius = Mathf.Max(minSpawnRadius, spawnRadius);
        groupSpreadRadius = Mathf.Max(0f, groupSpreadRadius);
        frontSpawnChance = Mathf.Clamp01(frontSpawnChance);
        frontSpawnHalfAngle = Mathf.Clamp(frontSpawnHalfAngle, 0f, 180f);
        startingMaxGroupSize = Mathf.Max(startingMinGroupSize, startingMaxGroupSize);
        groupSizeStepSeconds = Mathf.Max(0.1f, groupSizeStepSeconds);
        groupSizeIncreasePerStep = Mathf.Max(0, groupSizeIncreasePerStep);
        maximumGroupSize = Mathf.Max(startingMaxGroupSize, maximumGroupSize);
        openingGroupSizeMultiplier = Mathf.Clamp(openingGroupSizeMultiplier, 0.01f, 1f);
        openingSpawnDuration = Mathf.Max(0f, openingSpawnDuration);
        groupInterval = Mathf.Max(0.05f, groupInterval);
        earlySpawnBoostDuration = Mathf.Max(0f, earlySpawnBoostDuration);
        earlySpawnIntervalMultiplier = Mathf.Clamp(earlySpawnIntervalMultiplier, 0.1f, 1f);
        enemyMixRampStart = Mathf.Max(0f, enemyMixRampStart);
        enemyMixTransitionDuration = Mathf.Max(0.1f, enemyMixTransitionDuration);
        enemyMixLateRampStart = Mathf.Max(
            enemyMixRampStart + enemyMixTransitionDuration,
            enemyMixLateRampStart);
        maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
        raidInterval = Mathf.Max(1f, raidInterval);
        raidDuration = Mathf.Max(0.1f, raidDuration);
        raidBurstInterval = Mathf.Max(0.1f, raidBurstInterval);
        raidMinGroupSize = Mathf.Max(1, raidMinGroupSize);
        raidMaxGroupSize = Mathf.Max(raidMinGroupSize, raidMaxGroupSize);
        initialPoolSize = Mathf.Clamp(initialPoolSize, 0, 1000);
        maxEnemyDistanceFromPlayer = Mathf.Max(1f, maxEnemyDistanceFromPlayer);
        maxEnemyDropBelowPlayer = Mathf.Max(1f, maxEnemyDropBelowPlayer);
        outOfBoundsCheckInterval = Mathf.Max(0.05f, outOfBoundsCheckInterval);
        emergenceDepth = Mathf.Max(0f, emergenceDepth);
        emergenceDuration = Mathf.Max(0.05f, emergenceDuration);
        emergenceStagger = Mathf.Max(0f, emergenceStagger);
        miniBossChance = Mathf.Clamp01(miniBossChance);
        miniBossScaleMultiplier = Mathf.Max(1f, miniBossScaleMultiplier);
        miniBossHpMultiplier = Mathf.Max(1f, miniBossHpMultiplier);
        miniBossDamageMultiplier = Mathf.Max(1f, miniBossDamageMultiplier);
        miniBossSpeedMultiplier = Mathf.Max(1f, miniBossSpeedMultiplier);

        if (enemyTypes != null)
        {
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                if (enemyTypes[i] == null)
                {
                    continue;
                }

                enemyTypes[i].earlyWeight = Mathf.Max(0f, enemyTypes[i].earlyWeight);
                enemyTypes[i].midWeight = Mathf.Max(0f, enemyTypes[i].midWeight);
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

    public bool IsEmerging => prepared || emergenceRoutine != null;

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

/// <summary>
/// Runtime raid banner. It owns a dedicated overlay canvas so the warning stays
/// readable regardless of which HUD canvas is active in the current scene.
/// </summary>
internal sealed class RaidAnnouncementUI : MonoBehaviour
{
    private const float FadeInDuration = 0.75f;
    private const float HoldDuration = 2.4f;
    private const float FadeOutDuration = 0.65f;

    private CanvasGroup canvasGroup;
    private RectTransform bannerRect;
    private TextMeshProUGUI subtitle;
    private TMP_FontAsset displayFont;
    private Material displayFontMaterial;

    private Coroutine animationRoutine;

    public static RaidAnnouncementUI Create(Transform owner)
    {
        RaidAnnouncementUI existing = owner.GetComponentInChildren<RaidAnnouncementUI>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(
            "Raid Announcement UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup),
            typeof(RaidAnnouncementUI));
        root.transform.SetParent(owner, false);
        return root.GetComponent<RaidAnnouncementUI>();
    }

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        ResolveFont();
        canvasGroup.blocksRaycasts = false;

        BuildInterface();
    }

    public void Show(float raidDuration)
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        subtitle.text = $"SURVIVE FOR {Mathf.CeilToInt(raidDuration)} SECONDS";
        animationRoutine = StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        canvasGroup.alpha = 0f;
        bannerRect.localScale = Vector3.one * 0.9f;

        float elapsed = 0f;
        while (elapsed < FadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / FadeInDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            canvasGroup.alpha = eased;
            bannerRect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, eased);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        bannerRect.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(HoldDuration);

        elapsed = 0f;
        while (elapsed < FadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / FadeOutDuration);
            canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        animationRoutine = null;
    }

    private void BuildInterface()
    {
        GameObject bannerObject = new GameObject(
            "Raid Warning Banner",
            typeof(RectTransform),
            typeof(CanvasRenderer));
        bannerObject.transform.SetParent(transform, false);

        bannerRect = bannerObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -125f);
        bannerRect.sizeDelta = new Vector2(640f, 56f);

        TextMeshProUGUI title = CreateLabel(
            "Raid Title",
            bannerRect,
            "RAID INCOMING!",
            68f,
            new Color(1f, 0.2f, 0.08f, 1f),
            0.24f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.04f, 0.38f);
        titleRect.anchorMax = new Vector2(0.96f, 0.96f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        subtitle = CreateLabel(
            "Raid Subtitle",
            bannerRect,
            "SURVIVE FOR 60 SECONDS",
            30f,
            Color.white,
            0.15f);
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.04f, 0.08f);
        subtitleRect.anchorMax = new Vector2(0.96f, 0.43f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;
        subtitle.gameObject.SetActive(false);

        CreateAccentBar("Top Accent", bannerRect, 1f, -5f);
        CreateAccentBar("Bottom Accent", bannerRect, 0f, 5f);
    }

    private TextMeshProUGUI CreateLabel(
        string objectName,
        Transform parent,
        string message,
        float fontSize,
        Color color,
        float outlineWidth)
    {
        GameObject labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = message;
        label.font = displayFont;
        if (displayFontMaterial != null)
            label.fontSharedMaterial = displayFontMaterial;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(20f, fontSize * 0.65f);
        label.fontSizeMax = fontSize;
        label.outlineColor = Color.black;
        label.outlineWidth = outlineWidth;
        label.raycastTarget = false;
        return label;
    }

    private static void CreateAccentBar(
        string objectName,
        Transform parent,
        float anchorY,
        float verticalOffset)
    {
    }


    private void ResolveFont()
    {
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        TMP_FontAsset fallbackFont = null;
        Material fallbackMaterial = null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || candidate.font == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (fallbackFont == null)
            {
                fallbackFont = candidate.font;
                fallbackMaterial = candidate.fontSharedMaterial != null
                    ? candidate.fontSharedMaterial
                    : candidate.font.material;
            }

            if (candidate.font.name == "SVN-Determination Sans SDF")
            {
                displayFont = candidate.font;
                displayFontMaterial = candidate.fontSharedMaterial != null
                    ? candidate.fontSharedMaterial
                    : candidate.font.material;
                break;
            }
        }

        if (displayFont == null)
        {
            displayFont = fallbackFont != null ? fallbackFont : TMP_Settings.defaultFontAsset;
            displayFontMaterial = fallbackMaterial != null
                ? fallbackMaterial
                : displayFont != null ? displayFont.material : null;
        }
    }
}
