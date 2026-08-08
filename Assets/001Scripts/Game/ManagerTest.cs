using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ManagerTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawn enemySpawn;
    [SerializeField] private TimedBossSpawner bossSpawner;
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Test Values")]
    [Min(1f)]
    [SerializeField] private float fastForwardTimeScale = 10f;
    [Min(1)]
    [SerializeField] private int levelsPerAction = 1;
    [Min(1)]
    [SerializeField] private int enemiesPerWave = 10;
    [Min(1f)]
    [SerializeField] private float killDamage = 1000000f;
    [Tooltip("Tên một phần của boss spawner sẽ được chọn nếu bossSpawner chưa được gán.")]
    [SerializeField] private string bossSpawnerNameContains = "StoneGolemBoss";
    [Min(0)]
    [SerializeField] private int fallbackBossSpawnerIndex;
    [SerializeField] private bool spawnAllBossesWhenReferenceMissing;

    [Header("Controls")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private bool resetTimeScaleOnDisable = true;

    private bool isFastForwarding;
    public bool IsFastForwarding => isFastForwarding;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!enableHotkeys || !Application.isPlaying || Keyboard.current == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard.f5Key.wasPressedThisFrame)
            ToggleFastForward();
        if (keyboard.f6Key.wasPressedThisFrame)
            SpawnBoss();
        if (keyboard.f7Key.wasPressedThisFrame)
            LevelUp();
        if (keyboard.f8Key.wasPressedThisFrame)
            SpawnEnemies();
        if (keyboard.f9Key.wasPressedThisFrame)
            HealPlayer();
        if (keyboard.f10Key.wasPressedThisFrame)
            KillAllEnemies();
        if (keyboard.f11Key.wasPressedThisFrame)
            ResetTimeScale();
    }

    [ContextMenu("Test/Toggle Fast Forward")]
    public void ToggleFastForward()
    {
        if (isFastForwarding)
        {
            ResetTimeScale();
            return;
        }

        Time.timeScale = Mathf.Max(1f, fastForwardTimeScale);
        isFastForwarding = true;
        Debug.Log($"[ManagerTest] Fast-forward x{Time.timeScale:0.##}.");
    }

    [ContextMenu("Test/Reset Time Scale")]
    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
        isFastForwarding = false;
    }

    [ContextMenu("Test/Spawn Boss")]
    public void SpawnBoss()
    {
        ResolveReferences();

        if (bossSpawner != null)
        {
            bossSpawner.SpawnBoss();
            return;
        }

        TimedBossSpawner[] spawners =
            FindObjectsByType<TimedBossSpawner>(FindObjectsSortMode.None);
        if (spawners.Length == 0)
        {
            Debug.LogWarning("[ManagerTest] Không tìm thấy TimedBossSpawner.", this);
            return;
        }

        if (spawnAllBossesWhenReferenceMissing)
        {
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null)
                    spawners[i].SpawnBoss();
            }

            return;
        }

        TimedBossSpawner selectedSpawner = FindPreferredBossSpawner(spawners);
        selectedSpawner?.SpawnBoss();
    }

    [ContextMenu("Test/Level Up")]
    public void LevelUp()
    {
        ResolveReferences();

        if (xpSystem == null)
        {
            Debug.LogWarning("[ManagerTest] Không tìm thấy XPSystem.", this);
            return;
        }

        int levelUps = Mathf.Max(1, levelsPerAction);
        for (int i = 0; i < levelUps; i++)
        {
            int requiredXP = Mathf.Max(1, xpSystem.XPToNextLevel - xpSystem.CurrentXP);
            xpSystem.AddXP(requiredXP);
        }

        Debug.Log($"[ManagerTest] Đã tăng {levelUps} level. Level hiện tại: {xpSystem.CurrentLevel}.", this);
    }

    [ContextMenu("Test/Spawn Enemy Wave")]
    public void SpawnEnemies()
    {
        ResolveReferences();

        if (enemySpawn == null)
        {
            Debug.LogWarning("[ManagerTest] Không tìm thấy EnemySpawn.", this);
            return;
        }

        int spawned = enemySpawn.SpawnTestGroup(enemiesPerWave, true);
        Debug.Log($"[ManagerTest] Đã spawn thêm {spawned}/{Mathf.Max(1, enemiesPerWave)} enemy.", this);
    }

    [ContextMenu("Test/Heal Player")]
    public void HealPlayer()
    {
        ResolveReferences();

        if (playerHealth == null)
        {
            Debug.LogWarning("[ManagerTest] Không tìm thấy PlayerHealth.", this);
            return;
        }

        playerHealth.HealToFull();
    }

    [ContextMenu("Test/Kill All Enemies")]
    public void KillAllEnemies()
    {
        int affectedEnemies = 0;
        for (int i = EnemyHealth.ActiveEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = EnemyHealth.ActiveEnemies[i];
            if (enemy == null || !enemy.CanBeTargeted)
                continue;

            enemy.TakeDamage(killDamage);
            affectedEnemies++;
        }

        Debug.Log($"[ManagerTest] Đã đánh dấu chết {affectedEnemies} enemy.", this);
    }

    private void ResolveReferences()
    {
        if (enemySpawn == null)
            enemySpawn = FindFirstObjectByType<EnemySpawn>();
        if (xpSystem == null)
            xpSystem = FindFirstObjectByType<XPSystem>();
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private TimedBossSpawner FindPreferredBossSpawner(TimedBossSpawner[] spawners)
    {
        if (!string.IsNullOrWhiteSpace(bossSpawnerNameContains))
        {
            for (int i = 0; i < spawners.Length; i++)
            {
                TimedBossSpawner candidate = spawners[i];
                if (candidate != null &&
                    candidate.gameObject.name.IndexOf(
                        bossSpawnerNameContains,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }
        }

        int index = Mathf.Clamp(fallbackBossSpawnerIndex, 0, spawners.Length - 1);
        return spawners[index];
    }

    private void OnDisable()
    {
        if (resetTimeScaleOnDisable && isFastForwarding)
        {
            ResetTimeScale();
        }
    }

    private void OnValidate()
    {
        fastForwardTimeScale = Mathf.Max(1f, fastForwardTimeScale);
        levelsPerAction = Mathf.Max(1, levelsPerAction);
        enemiesPerWave = Mathf.Max(1, enemiesPerWave);
        killDamage = Mathf.Max(1f, killDamage);
        fallbackBossSpawnerIndex = Mathf.Max(0, fallbackBossSpawnerIndex);
    }
}
