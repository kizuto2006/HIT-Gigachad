using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Climbing")]
    public float climbSpeed = 4f;

    private bool isClimbing = false;
    private Cell currentCell;

    private float losTimer = 0f;
    private bool hasLineOfSight = false;
    private float raycastTimer = 0f;
    private bool isGrounded = true;

    private void OnEnable()
    {
        isClimbing = false;
        isGrounded = true;
        // Lệch nhịp tia laser để giảm tải CPU
        losTimer = Random.Range(0f, 0.2f);
        raycastTimer = Random.Range(0f, 0.06f);

        // Báo danh với Quản lý Đa luồng khi vừa sinh ra
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.RegisterEnemy(this);
    }

    private void OnDisable()
    {
        // Gạch tên khỏi Quản lý khi bị thu hồi vào Pool
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.UnregisterEnemy(this);

        // (Tùy chọn) Xóa tên khỏi ô lưới cũ nếu FlowFieldManager vẫn còn dùng
        if (currentCell != null)
            currentCell.enemiesInThisCell.Remove(this.transform);
    }

    // --- HÀM NÀY CUNG CẤP DỮ LIỆU HƯỚNG ĐI CHO MANAGER ---
    public Vector3 GetMovementDirection()
    {
        if (isClimbing) return Vector3.zero;

        Transform player = FlowFieldManager.Instance.playerTransform;
        if (player == null) return Vector3.zero;

        // Cập nhật ô lưới để lấy hướng mũi tên của Flow Field
        currentCell = FlowFieldManager.Instance.GetCellFromWorldPos(transform.position);

        Vector3 dirToPlayer = player.position - transform.position;

        losTimer -= Time.deltaTime;
        if (losTimer <= 0f)
        {
            losTimer = 0.2f;
            hasLineOfSight = !Physics.Raycast(transform.position - Vector3.up * 0.5f, dirToPlayer.normalized, dirToPlayer.magnitude, FlowFieldManager.Instance.obstacleLayer);
        }

        if (dirToPlayer.magnitude < 1.2f) return Vector3.zero;
        else if (hasLineOfSight) return new Vector3(dirToPlayer.x, 0, dirToPlayer.z).normalized;
        else if (currentCell != null) return new Vector3(currentCell.bestDirection.x, 0, currentCell.bestDirection.z).normalized;

        return Vector3.zero;
    }

    // --- HÀM NÀY DO MANAGER GỌI ĐỂ XỬ LÝ LEO TƯỜNG VÀ RƠI ---
    public void ApplyRaycasts()
    {
        if (isClimbing)
        {
            ClimbLogic();
            return;
        }

        // Tối ưu hóa: Chỉ bắn tia Raycast 15 lần/giây
        raycastTimer -= Time.deltaTime;
        if (raycastTimer <= 0f)
        {
            raycastTimer = 0.06f;

            // 1. TRỌNG LỰC (BÁM DÍNH CHỐNG LÚN)
            // Nhấc điểm bắn tia lên ngang ngực (+1.0m) để tia luôn bắt đầu từ trên không khí đâm xuống
            Vector3 rayOriginDown = transform.position + Vector3.up * 1f;

            // Bắn tia dò tìm tọa độ mặt đất (dài 3 mét)
            if (Physics.Raycast(rayOriginDown, Vector3.down, out RaycastHit groundHit, 3f))
            {
                // Độ cao chuẩn = Điểm sàn nhà (groundHit.point.y) + 1.0m (Khoảng cách từ gót chân lên tâm quái)
                float correctY = groundHit.point.y + 1.0f;

                if (transform.position.y < correctY)
                {
                    // LỖI LÚN: Nếu Y hiện tại thấp hơn độ cao chuẩn -> Ép nảy lên mặt đất ngay lập tức!
                    transform.position = new Vector3(transform.position.x, correctY, transform.position.z);
                    isGrounded = true;
                }
                else if (transform.position.y > correctY + 0.1f)
                {
                    // Đang lơ lửng trên không -> Cho rơi tiếp
                    isGrounded = false;
                }
                else
                {
                    // Đang đứng vững trên mặt đất
                    isGrounded = true;
                }
            }
            else
            {
                isGrounded = false; // Rơi xuống vực nếu không thấy đất
            }

            // 2. LEO TƯỜNG (Giữ nguyên như cũ)
            Vector3 rayOriginForward = transform.position - Vector3.up * 0.5f - transform.forward * 0.5f;
            LayerMask wallLayer = FlowFieldManager.Instance.obstacleLayer;
            if (Physics.Raycast(rayOriginForward, transform.forward, out RaycastHit hit, 1.5f, wallLayer))
            {
                isClimbing = true;
                transform.position = new Vector3(hit.point.x, transform.position.y, hit.point.z) - transform.forward * 0.4f;
            }
        }

        // Kéo quái rơi tự do nếu đang lơ lửng
        if (!isGrounded)
        {
            transform.position += Vector3.down * 15f * Time.deltaTime;
        }
    }

    void ClimbLogic()
    {
        transform.position += Vector3.up * climbSpeed * Time.deltaTime;

        Vector3 bottomRayOrigin = transform.position - Vector3.up * 0.9f - transform.forward * 0.5f;
        LayerMask wallLayer = FlowFieldManager.Instance.obstacleLayer;

        if (!Physics.Raycast(bottomRayOrigin, transform.forward, 1.5f, wallLayer))
        {
            transform.position += transform.forward * 0.8f + Vector3.up * 0.2f;
            isClimbing = false;
        }
    }
}