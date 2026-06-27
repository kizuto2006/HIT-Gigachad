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

    // Danh sách lưu toàn bộ quái vật đang sống
    public List<EnemyAI> activeEnemies = new List<EnemyAI>(5000);
    public TransformAccessArray transformAccessArray;

    [Header("Settings")]
    public float runSpeed = 5f;
    public float separationRadius = 0.4f;
    public float separationForce = 1.5f;

    void Awake()
    {
        Instance = this;
        transformAccessArray = new TransformAccessArray(5000);
    }

    void OnDestroy()
    {
        if (transformAccessArray.isCreated)
            transformAccessArray.Dispose();
    }

    public void RegisterEnemy(EnemyAI enemy)
    {
        activeEnemies.Add(enemy);
        transformAccessArray.Add(enemy.transform);
    }

    public void UnregisterEnemy(EnemyAI enemy)
    {
        int index = activeEnemies.IndexOf(enemy);
        if (index >= 0)
        {
            // Để giữ đồng bộ giữa danh sách và mảng Transform, phải hoán đổi phần tử cuối cùng
            activeEnemies[index] = activeEnemies[activeEnemies.Count - 1];
            activeEnemies.RemoveAt(activeEnemies.Count - 1);
            transformAccessArray.RemoveAtSwapBack(index);
        }
    }

    void Update()
    {
        int count = activeEnemies.Count;
        if (count == 0) return;

        NativeArray<float3> positions = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<float3> moveDirs = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<float3> newPositions = new NativeArray<float3>(count, Allocator.TempJob);

        for (int i = 0; i < count; i++)
        {
            positions[i] = activeEnemies[i].transform.position;
            moveDirs[i] = activeEnemies[i].GetMovementDirection(); 
        }

        // Job 1: Tính toán hướng di chuyển và xô đẩy
        EnemyUpdateJob job = new EnemyUpdateJob
        {
            positions = positions,
            moveDirs = moveDirs,
            deltaTime = Time.deltaTime,
            runSpeed = runSpeed,
            separationRadius = separationRadius,
            separationForce = separationForce,
            newPositions = newPositions
        };

        JobHandle handle = job.Schedule(count, 64);

        // Job 2: Cập nhật Transform trực tiếp trên đa luồng (Worker Threads)
        EnemyMoveJob moveJob = new EnemyMoveJob
        {
            newPositions = newPositions,
            moveDirs = moveDirs,
            deltaTime = Time.deltaTime
        };

        // Lên lịch Job 2 chạy NGAY SAU KHI Job 1 hoàn thành
        JobHandle moveHandle = moveJob.Schedule(transformAccessArray, handle);
        
        // Đợi tất cả các Job chạy xong
        moveHandle.Complete();

        // Xử lý nốt thao tác Raycast leo tường trên Main Thread
        for (int i = 0; i < count; i++)
        {
            activeEnemies[i].ApplyRaycasts();
        }

        positions.Dispose();
        moveDirs.Dispose();
        newPositions.Dispose();
    }
}

[BurstCompile]
public struct EnemyUpdateJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> moveDirs;
    public float deltaTime;
    public float runSpeed;
    public float separationRadius;
    public float separationForce;

    public NativeArray<float3> newPositions;

    public void Execute(int index)
    {
        float3 myPos = positions[index];
        float3 dir = moveDirs[index];

        float3 separationMove = float3.zero;
        int pushCount = 0;

        for (int i = 0; i < positions.Length; i++)
        {
            if (i == index) continue;

            float3 diff = myPos - positions[i];
            float sqrDist = math.lengthsq(diff); 

            if (sqrDist < separationRadius * separationRadius && sqrDist > 0.001f)
            {
                float dist = math.sqrt(sqrDist);
                separationMove += (diff / dist) * separationForce;
                pushCount++;
                if (pushCount >= 3) break; 
            }
        }

        float3 finalMove = (dir * runSpeed) + separationMove;
        newPositions[index] = myPos + (finalMove * deltaTime);
    }
}

[BurstCompile]
public struct EnemyMoveJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> newPositions;
    [ReadOnly] public NativeArray<float3> moveDirs;
    public float deltaTime;

    public void Execute(int index, TransformAccess transform)
    {
        transform.position = newPositions[index];

        float3 dir = moveDirs[index];
        if (math.lengthsq(dir) > 0.01f)
        {
            quaternion currentRot = transform.rotation;
            quaternion targetRot = quaternion.LookRotationSafe(dir, new float3(0, 1, 0));
            transform.rotation = math.slerp(currentRot, targetRot, deltaTime * 15f);
        }
    }
}