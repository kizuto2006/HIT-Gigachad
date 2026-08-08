using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject chestPrefab;
    [SerializeField, Min(1)] private int chestCount = 100;
    [SerializeField, Min(1f)] private Vector2 mapBoundsMin = new Vector2(-270f, -270f);
    [SerializeField] private Vector2 mapBoundsMax = new Vector2(270f, 270f);
        [SerializeField, Min(1f)] private float minimumSpacing = 14f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 18f;
    [SerializeField, Min(1f)] private float raycastOriginHeight = 160f;
    [SerializeField, Min(1f)] private float raycastDistance = 400f;
    [SerializeField, Min(1)] private int maxPlacementAttempts = 12000;
    [SerializeField] private int randomSeed = 20260806;
    [SerializeField] private bool randomizeSeed;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.65f;

    private readonly List<Vector3> spawnedPositions = new List<Vector3>();
    private System.Random placementRandom;

    public int SpawnedChestCount => spawnedPositions.Count;

private IEnumerator Start()
    {
        if (chestPrefab == null)
        {
            Debug.LogError("[ChestSpawner] Missing chest prefab.", this);
            yield break;
        }

        GameObject player = null;
        while (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                yield return null;
        }

        SpawnChests(player.transform.position);
    }

private void SpawnChests(Vector3 playerPosition)
    {
        spawnedPositions.Clear();
        int targetCount = Mathf.Max(1, chestCount);
        int attemptLimit = Mathf.Max(targetCount, maxPlacementAttempts);
        int seed = randomizeSeed ? System.Environment.TickCount : randomSeed;
        placementRandom = new System.Random(seed);

        for (int attempt = 0; attempt < attemptLimit && spawnedPositions.Count < targetCount; attempt++)
        {
            if (!TryFindSpawnPoint(playerPosition, out Vector3 position))
                continue;

            Quaternion rotation = Quaternion.Euler(
                0f,
                (float)placementRandom.NextDouble() * 360f,
                0f);
            GameObject chest = Instantiate(chestPrefab, position, rotation, transform);
            chest.name = $"Runtime Chest {spawnedPositions.Count + 1:000}";
            spawnedPositions.Add(position);
        }

        if (spawnedPositions.Count < targetCount)
        {
            Debug.LogWarning(
                $"[ChestSpawner] Placed {spawnedPositions.Count}/{targetCount} chests. " +
                "Check map bounds, ground layers, and spacing.",
                this);
        }
        else
        {
            Debug.Log($"[ChestSpawner] Placed {spawnedPositions.Count} chests across the map.", this);
        }
    }

private bool TryFindSpawnPoint(Vector3 playerPosition, out Vector3 position)
    {
        if (placementRandom == null)
            placementRandom = new System.Random(randomSeed);

        Vector2 minBounds = new Vector2(
            Mathf.Min(mapBoundsMin.x, mapBoundsMax.x),
            Mathf.Min(mapBoundsMin.y, mapBoundsMax.y));
        Vector2 maxBounds = new Vector2(
            Mathf.Max(mapBoundsMin.x, mapBoundsMax.x),
            Mathf.Max(mapBoundsMin.y, mapBoundsMax.y));
        float spacingSquared = Mathf.Max(1f, minimumSpacing * minimumSpacing);
        float playerDistanceSquared = Mathf.Max(0f, minimumDistanceFromPlayer * minimumDistanceFromPlayer);

        Vector3 candidate = new Vector3(
            Mathf.Lerp(minBounds.x, maxBounds.x, (float)placementRandom.NextDouble()),
            0f,
            Mathf.Lerp(minBounds.y, maxBounds.y, (float)placementRandom.NextDouble()));

        Vector3 playerOffset = candidate - playerPosition;
        playerOffset.y = 0f;
        if (playerOffset.sqrMagnitude < playerDistanceSquared)
        {
            position = default;
            return false;
        }

        Vector3 rayOrigin = candidate + Vector3.up * raycastOriginHeight;
        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            position = default;
            return false;
        }

        if (hit.normal.y < minimumGroundNormalY)
        {
            position = default;
            return false;
        }

        for (int i = 0; i < spawnedPositions.Count; i++)
        {
            Vector3 separation = hit.point - spawnedPositions[i];
            separation.y = 0f;
            if (separation.sqrMagnitude < spacingSquared)
            {
                position = default;
                return false;
            }
        }

        position = hit.point;
        return true;
    }
}
