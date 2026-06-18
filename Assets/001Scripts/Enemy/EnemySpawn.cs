using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawn : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform playerTransform;

    [Header("Spawn Settings")]
    public float spawnRadius = 25f;
    public int enemiesPerSecond = 10;

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

        // Lấy một hướng ngẫu nhiên 360 độ
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        // Lấy khoảng cách ngẫu nhiên từ 15m đến 25m
        float randomDist = Random.Range(15f, spawnRadius);

        Vector3 spawnPos = new Vector3(
            playerTransform.position.x + randomDir.x * randomDist,
            1.5f,
            playerTransform.position.z + randomDir.y * randomDist
        );

        enemy.transform.position = spawnPos;
    }

    public void ReturnEnemyToPool(GameObject enemy)
    {
        enemyPool.Release(enemy);
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