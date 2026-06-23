using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    // Danh sách lưu toàn bộ quái vật đang sống
    public List<EnemyAI> activeEnemies = new List<EnemyAI>(5000);

    [Header("Settings")]
    public float runSpeed = 5f;
    public float separationRadius = 0.4f;
    public float separationForce = 1.5f;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterEnemy(EnemyAI enemy)
    {
        activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyAI enemy)
    {
        activeEnemies.Remove(enemy);
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

        handle.Complete();

        for (int i = 0; i < count; i++)
        {
            EnemyAI enemy = activeEnemies[i];

            enemy.transform.position = newPositions[i];

            if (math.lengthsq(moveDirs[i]) > 0.01f)
            {
                enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(moveDirs[i]), Time.deltaTime * 15f);
            }

            enemy.ApplyRaycasts();
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