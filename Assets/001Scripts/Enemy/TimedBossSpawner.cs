using UnityEngine;

/// <summary>
/// Spawns one boss after the configured amount of gameplay time.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimedBossSpawner : MonoBehaviour
{
    private const float MinimumAllowedSpawnDistance = 3f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private string spawnedBossName = "StoneGolemBoss";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float spawnDelay = 180f;

    [Header("Placement")]
    [SerializeField, Min(MinimumAllowedSpawnDistance)] private float spawnDistance = 18f;
    [SerializeField, Min(1f)] private float groundProbeHeight = 30f;
    [SerializeField, Min(1f)] private float groundProbeDistance = 80f;
    [SerializeField] private LayerMask groundMask = 1 << 7;

    [Header("Spawn Emergence")]
    [SerializeField, Min(0f)] private float emergenceDepth = 2f;
    [SerializeField, Min(0.05f)] private float emergenceDuration = 0.8f;

    private float elapsedTime;
    private bool hasSpawned;
    private bool musicDuckActive;

    public GameObject SpawnedBoss { get; private set; }

    private void Awake()
    {
        spawnDistance = Mathf.Max(MinimumAllowedSpawnDistance, spawnDistance);
        ResolvePlayer();
    }

    private void Update()
    {
        if (hasSpawned)
        {
            if (SpawnedBoss == null || !SpawnedBoss.activeInHierarchy)
                ReleaseMusicDuck();
            return;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= spawnDelay)
        {
            SpawnBoss();
        }
    }

    public void Configure(GameObject prefab, Transform playerTransform, float delaySeconds, float distance)
    {
        bossPrefab = prefab;
        player = playerTransform;
        spawnDelay = Mathf.Max(0f, delaySeconds);
        spawnDistance = Mathf.Max(MinimumAllowedSpawnDistance, distance);
    }

    [ContextMenu("Spawn Boss Now")]
    public void SpawnBoss()
    {
        if (hasSpawned)
        {
            return;
        }

        ResolvePlayer();
        if (bossPrefab == null || player == null)
        {
            Debug.LogError("[TimedBossSpawner] Boss prefab hoặc Player chưa được gán.", this);
            return;
        }

        Vector3 spawnPosition = FindSpawnPosition();
        Vector3 lookDirection = player.position - spawnPosition;
        lookDirection.y = 0f;
        Quaternion rotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : Quaternion.identity;

        SpawnedBoss = Instantiate(bossPrefab, spawnPosition, rotation);
        string bossName = string.IsNullOrWhiteSpace(spawnedBossName)
            ? bossPrefab.name
            : spawnedBossName.Trim();
        SpawnedBoss.name = bossName;

        EnemySpawnEmergence emergence = SpawnedBoss.GetComponent<EnemySpawnEmergence>();
        if (emergence == null)
        {
            emergence = SpawnedBoss.AddComponent<EnemySpawnEmergence>();
        }

        emergence.Prepare(spawnPosition, emergenceDepth, emergenceDuration, 0f);
        hasSpawned = true;
        SoundEffectsAudioManager.Instance?.PlayBossAppearSound();
        RequestMusicDuck();

        Debug.Log($"[TimedBossSpawner] {bossName} xuất hiện tại {elapsedTime:F1}s.", this);
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

    private void OnDestroy()
    {
        ReleaseMusicDuck();
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
        }
    }

    private Vector3 FindSpawnPosition()
    {
        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        // Prefer a point in front of the player so the boss entrance is visible.
        Vector3 candidate = player.position + forward.normalized * spawnDistance;
        Vector3 rayOrigin = candidate + Vector3.up * groundProbeHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            candidate.y = hit.point.y;
        }
        else
        {
            candidate.y = player.position.y;
        }

        return candidate;
    }

    private void OnValidate()
    {
        spawnDelay = Mathf.Max(0f, spawnDelay);
        spawnDistance = Mathf.Max(MinimumAllowedSpawnDistance, spawnDistance);
        groundProbeHeight = Mathf.Max(1f, groundProbeHeight);
        groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
        emergenceDepth = Mathf.Max(0f, emergenceDepth);
        emergenceDuration = Mathf.Max(0.05f, emergenceDuration);
    }
}
