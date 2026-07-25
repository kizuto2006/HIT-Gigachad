using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    [Header("References")]
    [SerializeField] private FlowFieldManager flowFieldManager;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    public float runSpeed = 5f;
    public float separationRadius = 0.4f;
    public float separationForce = 1.5f;

    [Header("AI LOD Distances")]
    [Min(0f)] public float nearDistance = 10f;
    [Min(0f)] public float middleDistance = 25f;

    [Header("Direction Update Intervals")]
    [Min(0.01f)] public float nearDirectionInterval = 0.05f;
    [Min(0.01f)] public float middleDirectionInterval = 0.12f;
    [Min(0.01f)] public float farDirectionInterval = 0.35f;

    [Header("Environment Update Intervals")]
    [Min(0.01f)] public float nearEnvironmentInterval = 0.08f;
    [Min(0.01f)] public float middleEnvironmentInterval = 0.18f;
    [Min(0.01f)] public float farEnvironmentInterval = 0.5f;
    [Tooltip("Ngân sách raycast dùng chung cho toàn bộ enemy trong một frame.")]
    [Min(3)] public int maxRaycastsPerFrame = 48;

    [Header("Runtime Statistics")]
    [SerializeField] private int activeEnemyCount;
    [SerializeField] private int nativeCapacity;
    [SerializeField] private int raycastsUsedLastFrame;

    public readonly List<EnemyAI> activeEnemies = new List<EnemyAI>(512);
    public TransformAccessArray transformAccessArray;

    private readonly Dictionary<EnemyAI, int> enemyIndices = new Dictionary<EnemyAI, int>(512);
    private NativeArray<float3> positions;
    private NativeArray<float3> moveDirections;
    private NativeArray<float3> newPositions;
    private NativeArray<byte> separationEnabled;
    private NativeParallelMultiHashMap<int, int> spatialHash;
    private JobHandle movementHandle;
    private bool movementScheduled;

    private void Awake()
    {
        Instance = this;
        transformAccessArray = new TransformAccessArray(128);
        ResolveReferences();
    }

    private void Update()
    {
        // Bình thường Job của frame trước đã hoàn tất ở LateUpdate. Guard này bảo vệ
        // khi script execution order hoặc pooling thay đổi danh sách giữa frame.
        CompleteScheduledMovement();

        int count = activeEnemies.Count;
        activeEnemyCount = count;
        if (count == 0)
        {
            raycastsUsedLastFrame = 0;
            return;
        }

        ResolveReferences();
        UpdateAgentsByLod();
        EnsureNativeCapacity(count);
        PrepareMovementData(count);
        ScheduleMovement(count);
    }

    private void LateUpdate()
    {
        // Cho worker thread chạy song song với phần Update còn lại của frame.
        CompleteScheduledMovement();
    }

    private void OnDestroy()
    {
        CompleteScheduledMovement();

        if (transformAccessArray.isCreated)
        {
            transformAccessArray.Dispose();
        }

        DisposeNativeData();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        CompleteScheduledMovement();
    }

    public void RegisterEnemy(EnemyAI enemy)
    {
        if (enemy == null || enemyIndices.ContainsKey(enemy))
        {
            return;
        }

        CompleteScheduledMovement();
        int index = activeEnemies.Count;
        activeEnemies.Add(enemy);
        enemyIndices.Add(enemy, index);
        transformAccessArray.Add(enemy.transform);
        activeEnemyCount = activeEnemies.Count;
    }

    public void UnregisterEnemy(EnemyAI enemy)
    {
        if (enemy == null || !enemyIndices.TryGetValue(enemy, out int index))
        {
            return;
        }

        CompleteScheduledMovement();

        int lastIndex = activeEnemies.Count - 1;
        EnemyAI lastEnemy = activeEnemies[lastIndex];

        if (index != lastIndex)
        {
            activeEnemies[index] = lastEnemy;
            enemyIndices[lastEnemy] = index;
        }

        activeEnemies.RemoveAt(lastIndex);
        enemyIndices.Remove(enemy);
        transformAccessArray.RemoveAtSwapBack(index);
        activeEnemyCount = activeEnemies.Count;
    }

    private void ResolveReferences()
    {
        if (flowFieldManager == null)
        {
            flowFieldManager = FlowFieldManager.Instance;
        }

        if (playerTransform == null && flowFieldManager != null)
        {
            playerTransform = flowFieldManager.playerTransform;
        }
    }

    private void UpdateAgentsByLod()
    {
        if (playerTransform == null)
        {
            raycastsUsedLastFrame = 0;
            return;
        }

        int raycastsUsed = 0;
        int carryOverBudget = 0;
        float nearSqr = nearDistance * nearDistance;
        float middleSqr = middleDistance * middleDistance;

        // Mỗi tier có phần ngân sách tối thiểu để quái xa không bị bỏ đói.
        // Phần không dùng của tier gần được chuyển tiếp cho tier sau.
        for (int tier = 0; tier < 3; tier++)
        {
            int tierBaseBudget = GetTierRaycastBudget(tier);
            int tierRemainingBudget = tierBaseBudget + carryOverBudget;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyAI enemy = activeEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float sqrDistance = enemy.SqrDistanceTo(playerTransform.position);
                if (!BelongsToTier(sqrDistance, nearSqr, middleSqr, tier))
                {
                    continue;
                }

                GetTierIntervals(tier, out float directionInterval, out float environmentInterval);
                float now = Time.time;

                if (enemy.IsDirectionUpdateDue(now))
                {
                    bool allowLineOfSight = tier < 2 && tierRemainingBudget > 0;
                    int used = enemy.RefreshMovementDirection(
                        playerTransform,
                        flowFieldManager,
                        directionInterval,
                        allowLineOfSight);
                    tierRemainingBudget -= used;
                    raycastsUsed += used;
                }

                bool allowEnvironment = enemy.IsEnvironmentCheckDue(now) && tierRemainingBudget >= 2;
                int environmentRaycasts = enemy.UpdateEnvironment(environmentInterval, allowEnvironment);
                tierRemainingBudget -= environmentRaycasts;
                raycastsUsed += environmentRaycasts;
                tierRemainingBudget = Mathf.Max(0, tierRemainingBudget);
            }

            carryOverBudget = tierRemainingBudget;
        }

        raycastsUsedLastFrame = raycastsUsed;
    }

    private int GetTierRaycastBudget(int tier)
    {
        switch (tier)
        {
            case 0:
                return Mathf.Max(1, Mathf.RoundToInt(maxRaycastsPerFrame * 0.6f));
            case 1:
                return Mathf.Max(1, Mathf.RoundToInt(maxRaycastsPerFrame * 0.3f));
            default:
                return Mathf.Max(1, maxRaycastsPerFrame -
                    Mathf.RoundToInt(maxRaycastsPerFrame * 0.6f) -
                    Mathf.RoundToInt(maxRaycastsPerFrame * 0.3f));
        }
    }

    private static bool BelongsToTier(float sqrDistance, float nearSqr, float middleSqr, int tier)
    {
        switch (tier)
        {
            case 0:
                return sqrDistance <= nearSqr;
            case 1:
                return sqrDistance > nearSqr && sqrDistance <= middleSqr;
            default:
                return sqrDistance > middleSqr;
        }
    }

    private void GetTierIntervals(int tier, out float directionInterval, out float environmentInterval)
    {
        switch (tier)
        {
            case 0:
                directionInterval = nearDirectionInterval;
                environmentInterval = nearEnvironmentInterval;
                break;
            case 1:
                directionInterval = middleDirectionInterval;
                environmentInterval = middleEnvironmentInterval;
                break;
            default:
                directionInterval = farDirectionInterval;
                environmentInterval = farEnvironmentInterval;
                break;
        }
    }

    private void EnsureNativeCapacity(int requiredCount)
    {
        if (nativeCapacity >= requiredCount && positions.IsCreated)
        {
            return;
        }

        CompleteScheduledMovement();
        DisposeNativeData();

        nativeCapacity = Mathf.NextPowerOfTwo(Mathf.Max(128, requiredCount));
        positions = new NativeArray<float3>(nativeCapacity, Allocator.Persistent);
        moveDirections = new NativeArray<float3>(nativeCapacity, Allocator.Persistent);
        newPositions = new NativeArray<float3>(nativeCapacity, Allocator.Persistent);
        separationEnabled = new NativeArray<byte>(nativeCapacity, Allocator.Persistent);
        spatialHash = new NativeParallelMultiHashMap<int, int>(nativeCapacity, Allocator.Persistent);
    }

    private void PrepareMovementData(int count)
    {
        spatialHash.Clear();
        float cellSize = Mathf.Max(0.1f, separationRadius);
        float separationLodSqr = middleDistance * middleDistance;
        Vector3 playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            EnemyAI enemy = activeEnemies[i];
            float3 position = enemy.transform.position;
            positions[i] = position;
            moveDirections[i] = enemy.CachedMovementDirection;
            separationEnabled[i] = (byte)(playerTransform != null &&
                ((Vector3)position - playerPosition).sqrMagnitude <= separationLodSqr ? 1 : 0);

            int2 cell = CalculateCell(position, cellSize);
            spatialHash.Add(HashCell(cell), i);
        }
    }

    private void ScheduleMovement(int count)
    {
        EnemyUpdateJob movementJob = new EnemyUpdateJob
        {
            positions = positions,
            moveDirections = moveDirections,
            separationEnabled = separationEnabled,
            spatialHash = spatialHash,
            agentCount = count,
            cellSize = Mathf.Max(0.1f, separationRadius),
            deltaTime = Time.deltaTime,
            runSpeed = runSpeed,
            separationRadius = separationRadius,
            separationForce = separationForce,
            newPositions = newPositions
        };

        movementHandle = movementJob.Schedule(count, 64);
        movementScheduled = true;
    }

    private void CompleteScheduledMovement()
    {
        if (!movementScheduled)
        {
            return;
        }

        movementHandle.Complete();

        EnemyMoveJob moveJob = new EnemyMoveJob
        {
            newPositions = newPositions,
            moveDirections = moveDirections,
            deltaTime = Time.deltaTime
        };

        JobHandle transformHandle = moveJob.Schedule(transformAccessArray);
        transformHandle.Complete();

        movementScheduled = false;
    }

    private void DisposeNativeData()
    {
        if (positions.IsCreated) positions.Dispose();
        if (moveDirections.IsCreated) moveDirections.Dispose();
        if (newPositions.IsCreated) newPositions.Dispose();
        if (separationEnabled.IsCreated) separationEnabled.Dispose();
        if (spatialHash.IsCreated) spatialHash.Dispose();
        nativeCapacity = 0;
    }

    private static int2 CalculateCell(float3 position, float cellSize)
    {
        return new int2(
            (int)math.floor(position.x / cellSize),
            (int)math.floor(position.z / cellSize));
    }

    private static int HashCell(int2 cell)
    {
        unchecked
        {
            return (cell.x * 73856093) ^ (cell.y * 19349663);
        }
    }

    private void OnValidate()
    {
        nearDistance = Mathf.Max(0f, nearDistance);
        middleDistance = Mathf.Max(nearDistance, middleDistance);
        maxRaycastsPerFrame = Mathf.Max(3, maxRaycastsPerFrame);
        separationRadius = Mathf.Max(0.01f, separationRadius);
    }
}

[BurstCompile]
public struct EnemyUpdateJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> moveDirections;
    [ReadOnly] public NativeArray<byte> separationEnabled;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialHash;
    public int agentCount;
    public float cellSize;
    public float deltaTime;
    public float runSpeed;
    public float separationRadius;
    public float separationForce;
    public NativeArray<float3> newPositions;

    public void Execute(int index)
    {
        float3 currentPosition = positions[index];
        float3 separationMove = float3.zero;

        if (separationEnabled[index] != 0)
        {
            int2 currentCell = CalculateCell(currentPosition, cellSize);
            float separationRadiusSqr = separationRadius * separationRadius;
            int neighborCount = 0;

            for (int x = -1; x <= 1 && neighborCount < 3; x++)
            {
                for (int z = -1; z <= 1 && neighborCount < 3; z++)
                {
                    int key = HashCell(currentCell + new int2(x, z));
                    if (!spatialHash.TryGetFirstValue(key, out int otherIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (otherIndex == index || otherIndex < 0 || otherIndex >= agentCount)
                        {
                            continue;
                        }

                        float3 difference = currentPosition - positions[otherIndex];
                        difference.y = 0f;
                        float sqrDistance = math.lengthsq(difference);

                        if (sqrDistance < separationRadiusSqr && sqrDistance > 0.001f)
                        {
                            separationMove += math.normalizesafe(difference) * separationForce;
                            neighborCount++;
                        }
                    }
                    while (neighborCount < 3 && spatialHash.TryGetNextValue(out otherIndex, ref iterator));
                }
            }
        }

        float3 finalMovement = moveDirections[index] * runSpeed + separationMove;
        newPositions[index] = currentPosition + finalMovement * deltaTime;
    }

    private static int2 CalculateCell(float3 position, float size)
    {
        return new int2(
            (int)math.floor(position.x / size),
            (int)math.floor(position.z / size));
    }

    private static int HashCell(int2 cell)
    {
        unchecked
        {
            return (cell.x * 73856093) ^ (cell.y * 19349663);
        }
    }
}

[BurstCompile]
public struct EnemyMoveJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> newPositions;
    [ReadOnly] public NativeArray<float3> moveDirections;
    public float deltaTime;

    public void Execute(int index, TransformAccess transform)
    {
        transform.position = newPositions[index];

        float3 direction = moveDirections[index];
        if (math.lengthsq(direction) > 0.01f)
        {
            quaternion currentRotation = transform.rotation;
            quaternion targetRotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
            transform.rotation = math.slerp(currentRotation, targetRotation, deltaTime * 15f);
        }
    }
}
