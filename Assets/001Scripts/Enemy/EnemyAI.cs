using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Movement")]
    public float runSpeed = 5f;
    public float climbSpeed = 4f;

    [Header("Separation (Vật lý toán học)")]
    // Đã giảm thông số mặc định để quái đứng sát nhau hơn, tạo cảm giác bủa vây nghẹt thở
    public float separationRadius = 0.4f;
    public float separationForce = 1.5f;

    [Header("Climbing")]
    public float wallDetectDistance = 0.6f;

    // Các biến dùng cho Line of Sight (Tầm nhìn thẳng)
    private float losTimer = 0f;
    private bool hasLineOfSight = false;

    private bool isClimbing = false;
    private Cell currentCell;

    void Update()
    {
        UpdateSpatialHashing();

        if (isClimbing)
        {
            ClimbLogic();
        }
        else
        {
            RunAndSeparateLogic();
        }
    }

    void UpdateSpatialHashing()
    {
        Cell newCell = FlowFieldManager.Instance.GetCellFromWorldPos(transform.position);
        if (newCell != currentCell)
        {
            if (currentCell != null) currentCell.enemiesInThisCell.Remove(this.transform);
            currentCell = newCell;
            currentCell.enemiesInThisCell.Add(this.transform);
        }
    }

    void RunAndSeparateLogic()
    {
        if (currentCell == null) return;

        Vector3 moveDir = Vector3.zero;
        Transform player = FlowFieldManager.Instance.playerTransform;

        if (player == null) return;

        Vector3 dirToPlayer = player.position - transform.position;

        losTimer -= Time.deltaTime;
        if (losTimer <= 0f)
        {
            losTimer = 0.2f;
            // Đổi thành dấu trừ (-) để hạ tia quét xuống thấp, giúp nó nhìn thấy cả các bức tường nhỏ
            hasLineOfSight = !Physics.Raycast(transform.position - Vector3.up * 0.5f, dirToPlayer.normalized, dirToPlayer.magnitude, FlowFieldManager.Instance.obstacleLayer);
        }

        if (dirToPlayer.magnitude < 1.2f)
        {
            moveDir = Vector3.zero;

        }
        else if (hasLineOfSight)
        {
            moveDir = new Vector3(dirToPlayer.x, 0, dirToPlayer.z).normalized;
        }
        else
        {
            moveDir = new Vector3(currentCell.bestDirection.x, 0, currentCell.bestDirection.z).normalized;
        }

        // 2. TÍNH TOÁN LỰC ĐẨY NHAU (ĐÃ TỐI ƯU HÓA)
        Vector3 separationMove = Vector3.zero;
        int pushCount = 0; // Biến đếm số lượng quái đã tương tác

        foreach (Transform otherEnemy in currentCell.enemiesInThisCell)
        {
            // GIỚI HẠN: Chỉ check tối đa 3 con quái xung quanh để cứu FPS
            if (pushCount >= 3) break;

            if (otherEnemy == this.transform) continue;

            Vector3 diff = transform.position - otherEnemy.position;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist < separationRadius * separationRadius && sqrDist > 0.001f)
            {
                // Thay vì dùng Mathf.Sqrt (rất nặng), ta chia thẳng cho sqrDist hoặc một hằng số
                // Điều này giúp CPU bỏ qua phép tính căn bậc hai phức tạp
                separationMove += diff.normalized * separationForce;
                pushCount++; // Tăng biến đếm lên
            }
        }

        Vector3 finalMove = (moveDir * runSpeed) + separationMove;

        // 3. THÊM TRỌNG LỰC NHÂN TẠO (Đã sửa lỗi lún đất)
        Vector3 rayOriginDown = transform.position;
        if (!Physics.Raycast(rayOriginDown, Vector3.down, 1.05f))
        {
            finalMove += Vector3.down * 15f;
        }

        transform.position += finalMove * Time.deltaTime;

        if (moveDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 15f);

        // 4. KIỂM TRA LEO TƯỜNG (HẠ THẤP CẢM BIẾN XUỐNG ĐẦU GỐI)
        Vector3 rayOriginForward = transform.position - Vector3.up * 0.5f - transform.forward * 0.5f;
        LayerMask wallLayer = FlowFieldManager.Instance.obstacleLayer;

        Debug.DrawRay(rayOriginForward, transform.forward * 1.5f, Color.red);

        if (Physics.Raycast(rayOriginForward, transform.forward, out RaycastHit hit, 1.5f, wallLayer))
        {
            isClimbing = true;
            transform.position = new Vector3(hit.point.x, transform.position.y, hit.point.z) - transform.forward * 0.4f;
        }
    }

    void ClimbLogic()
    {
        transform.position += Vector3.up * climbSpeed * Time.deltaTime;

        Vector3 bottomRayOrigin = transform.position - Vector3.up * 0.9f - transform.forward * 0.5f;
        LayerMask wallLayer = FlowFieldManager.Instance.obstacleLayer;

        Debug.DrawRay(bottomRayOrigin, transform.forward * 1.5f, Color.blue);

        if (!Physics.Raycast(bottomRayOrigin, transform.forward, 1.5f, wallLayer))
        {
            transform.position += transform.forward * 0.8f + Vector3.up * 0.2f;
            isClimbing = false;
        }
    }

    private void OnDisable()
    {
        if (currentCell != null)
        {
            currentCell.enemiesInThisCell.Remove(this.transform);
        }
    }
}