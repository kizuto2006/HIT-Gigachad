using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawn : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform playerTransform;

    [Header("Spawn Settings")]
    [Min(0f)] public float minSpawnRadius = 15f;
    public float spawnRadius = 25f;
    public int enemiesPerSecond = 10;
    [Tooltip("Tỷ lệ enemy được ưu tiên spawn trong cung phía trước hướng nhìn của Player.")]
    [Range(0f, 1f)] public float frontSpawnChance = 0.75f;
    [Tooltip("Nửa góc của cung spawn phía trước.")]
    [Range(0f, 180f)] public float frontSpawnHalfAngle = 65f;

    [Header("Organization")]
    public Transform enemyContainer;

    [Header("Thống kê (Chỉ xem)")]
    // Biến này sẽ hiển thị trong Inspector để bạn theo dõi
    public int activeEnemyCount = 0;

    private ObjectPool<GameObject> enemyPool;

    void Start()
    {
        enemyPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(enemyPrefab, enemyContainer),
            actionOnGet: (enemy) => enemy.SetActive(true),
            actionOnRelease: (enemy) => enemy.SetActive(false),
            actionOnDestroy: (enemy) => Destroy(enemy),
            collectionCheck: false,
            defaultCapacity: 1000,
            maxSize: 5000
        );

        // Gọi hàm spawn liên tục
        InvokeRepeating(nameof(SpawnEnemy), 1f, 1f / enemiesPerSecond);
    }

    void Update()
    {
        // Liên tục cập nhật số lượng quái đang hoạt động vào biến để hiển thị
        if (enemyPool != null)
        {
            activeEnemyCount = enemyPool.CountActive;
        }
    }

    void SpawnEnemy()
    {
        GameObject enemy = enemyPool.Get();

        Vector3 spawnDirection = GetSpawnDirection();
        float randomDist = Random.Range(minSpawnRadius, spawnRadius);

        Vector3 spawnPos = new Vector3(
            playerTransform.position.x + spawnDirection.x * randomDist,
            1.5f,
            playerTransform.position.z + spawnDirection.z * randomDist
        );

        enemy.transform.position = spawnPos;
    }

    private Vector3 GetSpawnDirection()
    {
        if (Random.value <= frontSpawnChance)
        {
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.001f)
                playerForward = Vector3.forward;

            float angle = Random.Range(-frontSpawnHalfAngle, frontSpawnHalfAngle);
            return Quaternion.Euler(0f, angle, 0f) * playerForward.normalized;
        }

        Vector2 randomDirection = Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude < 0.001f)
            randomDirection = Vector2.right;

        randomDirection.Normalize();
        return new Vector3(randomDirection.x, 0f, randomDirection.y);
    }
    public void ReturnEnemyToPool(GameObject enemy)
    {
        enemyPool.Release(enemy);
    }

    private void OnValidate()
    {
        minSpawnRadius = Mathf.Max(0f, minSpawnRadius);
        spawnRadius = Mathf.Max(minSpawnRadius, spawnRadius);
        frontSpawnChance = Mathf.Clamp01(frontSpawnChance);
        frontSpawnHalfAngle = Mathf.Clamp(frontSpawnHalfAngle, 0f, 180f);
        enemiesPerSecond = Mathf.Max(1, enemiesPerSecond);
    }
    // --- HÀM VẼ GIAO DIỆN NHANH LÊN MÀN HÌNH GAME ---
    void OnGUI()
    {
        // Tạo một kiểu chữ to, màu vàng
        GUIStyle style = new GUIStyle();
        style.fontSize = 30;
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        // In dòng chữ ra góc trên cùng bên trái màn hình Game
        GUI.Label(new Rect(20, 20, 400, 50), "Số lượng Quái vật: " + activeEnemyCount, style);
    }
}