using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private const float GlobalEnemySpeedMultiplier = 2f / 3f;

    [Header("Climbing")]
    public float climbSpeed = 4f;

    [Header("Knockback")]
    [SerializeField, Min(0f)] private float knockbackDamping = 8f;

    private bool isClimbing;
    private Cell currentCell;
    private bool hasLineOfSight;
    private bool isGrounded = true;
    private Vector3 knockbackVelocity;
    private Vector3 cachedMovementDirection;
    private float nextDirectionUpdateTime;
    private float nextEnvironmentCheckTime;

    private EnemyHealth enemyHealth;
    private bool isMovementLocked;

    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    public Vector3 CachedMovementDirection => cachedMovementDirection;

    public float MoveSpeed
    {
        get
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            return enemyHealth != null && enemyHealth.data != null
                ? enemyHealth.MovementSpeed * GlobalEnemySpeedMultiplier
                : 0f;
        }
    }
    public bool IsMovementLocked => isMovementLocked;
    public float SqrDistanceTo(Vector3 position) => (transform.position - position).sqrMagnitude;

    private void OnEnable()
    {
        isClimbing = false;
        isGrounded = true;
        hasLineOfSight = false;
        knockbackVelocity = Vector3.zero;
        cachedMovementDirection = Vector3.zero;
        nextDirectionUpdateTime = 0f;
        nextEnvironmentCheckTime = 0f;
        isMovementLocked = false;

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(this);
        }
    }

    private void Start()
    {
        // A scene-placed enemy can enable before EnemyManager.Awake runs.
        // RegisterEnemy is idempotent, so this is also safe for pooled enemies.
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(this);
        }
    }

    private void OnDisable()
    {
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(this);
        }

        if (currentCell != null)
        {
            currentCell.enemiesInThisCell.Remove(transform);
            currentCell = null;
        }
    }

    public bool IsDirectionUpdateDue(float currentTime)
    {
        return currentTime >= nextDirectionUpdateTime;
    }

    public bool IsEnvironmentCheckDue(float currentTime)
    {
        return currentTime >= nextEnvironmentCheckTime;
    }

    /// <summary>
    /// Boss skills use this to prevent the shared movement job from sliding the enemy
    /// during telegraph, attack and recovery windows.
    /// </summary>
    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        cachedMovementDirection = Vector3.zero;

        if (!locked)
        {
            nextDirectionUpdateTime = 0f;
        }
    }

    /// <summary>
    /// Cập nhật hướng AI ở tần suất thấp hơn frame rate và lưu lại để Job sử dụng.
    /// Trả về số raycast đã dùng trong lần cập nhật này.
    /// </summary>
    public int RefreshMovementDirection(
        Transform player,
        FlowFieldManager flowField,
        float updateInterval,
        bool allowLineOfSightRaycast)
    {
        nextDirectionUpdateTime = Time.time + JitterInterval(updateInterval);

        if (isMovementLocked || isClimbing || player == null || flowField == null)
        {
            cachedMovementDirection = Vector3.zero;
            return 0;
        }

        currentCell = flowField.GetCellFromWorldPos(transform.position);
        Vector3 directionToPlayer = player.position - transform.position;
        directionToPlayer.y = 0f;
        float sqrDistance = directionToPlayer.sqrMagnitude;
        int raycastsUsed = 0;

        if (allowLineOfSightRaycast && sqrDistance > 0.0001f)
        {
            hasLineOfSight = !Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                directionToPlayer.normalized,
                Mathf.Sqrt(sqrDistance),
                flowField.obstacleLayer);
            raycastsUsed = 1;
        }

        if (sqrDistance < 1.2f * 1.2f)
        {
            cachedMovementDirection = Vector3.zero;
        }
        else if (hasLineOfSight)
        {
            cachedMovementDirection = directionToPlayer.normalized;
        }
        else if (currentCell != null && currentCell.bestDirection.sqrMagnitude > 0.0001f)
        {
            cachedMovementDirection = new Vector3(
                currentCell.bestDirection.x,
                0f,
                currentCell.bestDirection.z).normalized;
        }
        else
        {
            // Ngoài flow field hoặc ô chưa có hướng: vẫn đuổi trực tiếp để không bị mất phương hướng.
            cachedMovementDirection = directionToPlayer.normalized;
        }

        return raycastsUsed;
    }

    /// <summary>
    /// Áp dụng knockback/trọng lực mỗi frame. Các raycast môi trường chỉ chạy khi
    /// EnemyManager cấp ngân sách và đến thời điểm cập nhật của tier hiện tại.
    /// </summary>
    public int UpdateEnvironment(float updateInterval, bool allowRaycasts)
    {
        ApplyKnockbackVelocity();

        if (isClimbing)
        {
            return ClimbLogic(updateInterval, allowRaycasts);
        }

        int raycastsUsed = 0;
        if (allowRaycasts && IsEnvironmentCheckDue(Time.time))
        {
            nextEnvironmentCheckTime = Time.time + JitterInterval(updateInterval);
            raycastsUsed += UpdateGroundState();
            raycastsUsed += CheckForClimbObstacle();
        }

        if (!isGrounded)
        {
            transform.position += Vector3.down * 15f * Time.deltaTime;
        }

        return raycastsUsed;
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || force <= 0f)
        {
            return;
        }

        knockbackVelocity += direction.normalized * force;
    }

    private int UpdateGroundState()
    {
        Vector3 rayOriginDown = transform.position + Vector3.up * 10f;
        if (TryGetGroundHit(rayOriginDown, out RaycastHit groundHit))
        {
            float correctY = groundHit.point.y;

            if (transform.position.y < correctY ||
                (transform.position.y > correctY && transform.position.y <= correctY + 0.6f))
            {
                transform.position = new Vector3(transform.position.x, correctY, transform.position.z);
                isGrounded = true;
            }
            else if (transform.position.y > correctY + 0.6f)
            {
                isGrounded = false;
            }
            else
            {
                isGrounded = true;
            }
        }
        else
        {
            isGrounded = false;
        }

        return 1;
    }

    private int CheckForClimbObstacle()
    {
        FlowFieldManager flowField = FlowFieldManager.Instance;
        if (flowField == null)
        {
            return 0;
        }

        Vector3 rayOriginForward = transform.position + Vector3.up * 0.5f - transform.forward * 0.5f;
        if (Physics.Raycast(rayOriginForward, transform.forward, out RaycastHit hit, 1.5f, flowField.obstacleLayer))
        {
            isClimbing = true;
            cachedMovementDirection = Vector3.zero;
            transform.position = new Vector3(hit.point.x, transform.position.y, hit.point.z) - transform.forward * 0.4f;
        }

        return 1;
    }

    private void ApplyKnockbackVelocity()
    {
        if (knockbackVelocity.sqrMagnitude < 0.0001f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }

        transform.position += knockbackVelocity * Time.deltaTime;
        float damping = 1f - Mathf.Exp(-knockbackDamping * Time.deltaTime);
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, damping);
    }

    private bool TryGetGroundHit(Vector3 origin, out RaycastHit closestHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            GroundHits,
            20f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        closestHit = default;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<EnemyAI>() != null || hit.collider.CompareTag("Player"))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundGround = true;
            }
        }

        return foundGround;
    }

    private int ClimbLogic(float updateInterval, bool allowRaycast)
    {
        transform.position += Vector3.up * climbSpeed * Time.deltaTime;

        if (!allowRaycast || !IsEnvironmentCheckDue(Time.time))
        {
            return 0;
        }

        nextEnvironmentCheckTime = Time.time + JitterInterval(updateInterval);
        FlowFieldManager flowField = FlowFieldManager.Instance;
        if (flowField == null)
        {
            isClimbing = false;
            return 0;
        }

        Vector3 bottomRayOrigin = transform.position + Vector3.up * 0.1f - transform.forward * 0.5f;
        if (!Physics.Raycast(bottomRayOrigin, transform.forward, 1.5f, flowField.obstacleLayer))
        {
            transform.position += transform.forward * 0.8f + Vector3.up * 0.2f;
            isClimbing = false;
            nextDirectionUpdateTime = 0f;
        }

        return 1;
    }

    private static float JitterInterval(float interval)
    {
        return Mathf.Max(0.01f, interval) * Random.Range(0.85f, 1.15f);
    }
}
