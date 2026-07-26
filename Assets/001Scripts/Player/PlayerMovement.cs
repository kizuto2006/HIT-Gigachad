using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSimpleMovement : MonoBehaviour
{
    [Header("── References ──")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private PlayerBaseStats playerStats;
    [Tooltip("Chỉ dùng khi PlayerBaseStats chưa được gán. Không phải nguồn tốc độ gameplay chính.")]
    [SerializeField, Min(0f)] private float fallbackMoveSpeed = 5f;
    [Tooltip("Chỉ dùng khi PlayerBaseStats chưa được gán. Không phải nguồn jump gameplay chính.")]
    [SerializeField, Min(0f)] private float fallbackJumpHeight = 1.8f;

    [Header("── Ground Momentum ──")]
    [Tooltip("Gia tốc khi chạy cùng hướng hoặc bắt đầu di chuyển.")]
    [SerializeField, Min(0f)] private float groundAcceleration = 50f;
    [Tooltip("Ma sát giảm tốc khi thả input.")]
    [SerializeField, Min(0f)] private float groundDeceleration = 24f;
    [Tooltip("Lực phanh khi cua gấp hoặc đổi hướng.")]
    [SerializeField, Min(0f)] private float groundBraking = 55f;
    [Tooltip("Tốc độ tối đa mà hướng velocity có thể xoay trên mặt đất (độ/giây).")]
    [SerializeField, Min(0f)] private float groundTurnSpeed = 720f;
    [Tooltip("Nhân lực phanh khi input gần ngược với momentum hiện tại.")]
    [SerializeField, Min(1f)] private float reverseBrakingMultiplier = 1.4f;
    [Tooltip("Mức mất tốc khi cua. 0 không mất tốc, 1 dùng toàn bộ groundBraking theo góc cua.")]
    [SerializeField, Range(0f, 1f)] private float turnSpeedLoss = 0.1f;

    [Header("── Air Momentum ──")]
    [Tooltip("Gia tốc rất nhỏ khi input cùng hướng momentum.")]
    [SerializeField, Min(0f)] private float airForwardAcceleration = 0.5f;
    [Tooltip("Gia tốc ngang cực nhỏ khi input lệch khỏi momentum.")]
    [SerializeField, Min(0f)] private float airLateralAcceleration = 0.35f;
    [Tooltip("Lực giảm nhẹ momentum khi input ngược hướng trên không.")]
    [SerializeField, Min(0f)] private float airCounterMomentumBraking = 1.5f;
    [Tooltip("Tỷ lệ ảnh hưởng tối đa của input ngang lên quỹ đạo trên không.")]
    [SerializeField, Range(0f, 1f)] private float maximumAirDirectionInfluence = 0.05f;

    [Header("── Jump Horizontal Momentum ──")]
    [Tooltip("Tỷ lệ momentum ngang giữ lại khi nhảy thường. Đặt 1 để cú nhảy đầu tiên không làm giảm vận tốc.")]
    [SerializeField, Range(0f, 1f)] private float normalJumpMomentumRetention = 1f;
    [Tooltip("Lượng vận tốc ngang cộng thêm cho cú nhảy thường khi player đang di chuyển.")]
    [SerializeField, Min(0f)] private float normalJumpSpeedBoost = 0.75f;
    [Tooltip("Giới hạn tốc độ ngang của cú nhảy thường theo tỷ lệ CurrentMoveSpeed.")]
    [SerializeField, Min(1f)] private float normalJumpMaxSpeedMultiplier = 1.1f;
    [Tooltip("Tỷ lệ momentum ngang giữ lại khi bunny-hop. Đặt 1 để giữ nguyên vận tốc trước khi cộng boost.")]
    [SerializeField, Range(0f, 1f)] private float bunnyHopMomentumRetention = 1f;

    [Header("── Bunny Hop ──")]
    [Tooltip("Cho phép bunny-hop thông qua jump buffer gần thời điểm landing.")]
    [SerializeField] private bool enableBunnyHop = true;
    [Tooltip("Khoảng thời gian trước hoặc sau landing được tính là bunny-hop.")]
    [SerializeField, Min(0f)] private float bunnyHopWindow = 0.12f;
    [Tooltip("Lượng vận tốc ngang cộng thêm khi bunny-hop thành công.")]
    [SerializeField, Min(0f)] private float bunnyHopSpeedBoost = 0.75f;
    [Tooltip("Giới hạn bunny speed theo tỷ lệ CurrentMoveSpeed.")]
    [SerializeField, Min(1f)] private float bunnyHopMaxSpeedMultiplier = 1.15f;
    [Tooltip("Thời gian tối thiểu phải airborne trước khi landing có thể bunny-hop.")]
    [SerializeField, Min(0f)] private float minimumAirTimeForBunnyHop = 0.1f;

    [Header("── Vertical Movement ──")]
    [SerializeField] private float gravity = -20f;
    [Tooltip("Bonus jump theo tỷ lệ tốc độ hiện tại. Mặc định 0 để jump chỉ do stats quyết định.")]
    [SerializeField, Min(0f)] private float speedJumpBonusMultiplier = 0f;
    [Tooltip("Vận tốc nhỏ hướng xuống giúp CharacterController bám mặt đất.")]
    [SerializeField, Min(0f)] private float groundedStickForce = 2f;

    [Header("── Rotation ──")]
    [SerializeField, Min(0f)] private float groundRotationSpeed = 720f;
    [SerializeField, Min(0f)] private float airRotationSpeed = 540f;
    [SerializeField, Min(0f)] private float minimumRotationSpeed = 0.1f;

    [Header("Slope Alignment")]
    [Tooltip("Nghiêng riêng model theo bề mặt; CharacterController và camera vẫn thẳng đứng.")]
    [SerializeField] private bool alignVisualToGround = true;
    [SerializeField, Min(0f)] private float slopeAlignmentSpeed = 12f;
    [SerializeField, Range(0f, 60f)] private float maxVisualSlopeAngle = 45f;
    [SerializeField, Min(0f)] private float visualGroundSnapDistance = 0.75f;

    [Header("── Jump Assist ──")]
    [Tooltip("Thời gian cho phép nhảy sau khi rời mặt đất.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.15f;
    [Tooltip("Thời gian ghi nhớ input nhảy trước khi chạm đất.")]
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
    [Tooltip("Thời gian ở trên không trước khi bật trạng thái Falling.")]
    [SerializeField, Min(0f)] private float fallTimeThreshold = 0.3f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private Transform cameraTransform;
    private Animator animator;
    private Transform visualRoot;
    private Quaternion visualBaseLocalRotation;
    private readonly RaycastHit[] slopeHits = new RaycastHit[16];
    private Vector2 moveInput;
    private Vector3 horizontalVelocity;
    private Vector3 desiredFacingDirection;
    private float verticalVelocity;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float airTimeCounter;
    private float lastCompletedAirTime;
    private float timeSinceLanded = float.PositiveInfinity;
    private float jumpPressAge = float.PositiveInfinity;
    private bool isGrounded;
    private bool wasGrounded;
    private bool justLanded;
    private bool bunnyHopConsumedForLanding = true;
    private bool jumpPressed;
    private bool hasSpeedParam;
    private bool hasIsGroundedParam;
    private bool hasIsFallingParam;
    private bool hasVelocityYParam;
    private bool hasJumpParam;

    private float CurrentMoveSpeed => playerStats != null
        ? Mathf.Max(0f, playerStats.FinalSpeed)
        : fallbackMoveSpeed;

    private float CurrentJumpHeight => playerStats != null
        ? Mathf.Max(0f, playerStats.FinalJumpHeight)
        : fallbackJumpHeight;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (playerStats == null)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                playerStats = health.stats;
            }
        }

        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        animator = GetComponentInChildren<Animator>();
        visualRoot = animator != null ? animator.transform : null;
        visualBaseLocalRotation = visualRoot != null
            ? visualRoot.localRotation
            : Quaternion.identity;
        CacheAnimatorParameters();

        if (controller == null)
        {
            Debug.LogError("[PlayerMovement] CharacterController chưa được gán.", this);
            enabled = false;
            return;
        }

        if (playerStats == null)
        {
            Debug.LogWarning("[PlayerMovement] PlayerBaseStats chưa được gán; đang dùng fallback speed/jump.", this);
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("[PlayerMovement] Không tìm thấy Main Camera; input sẽ dùng hướng world-space.", this);
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateGroundState(deltaTime);
        UpdateJumpTimers(deltaTime);

        Vector3 desiredDirection = GetCameraRelativeInput();
        if (isGrounded)
        {
            UpdateGroundVelocity(desiredDirection, deltaTime);
        }
        else
        {
            UpdateAirVelocity(desiredDirection, deltaTime);
        }

        TryJump(desiredDirection);
        ApplyGravity(deltaTime);
        UpdateRotation(desiredDirection, deltaTime);

        Vector3 frameVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(frameVelocity * deltaTime);

        UpdateAnimator();
    }

    private void LateUpdate()
    {
        // Animator evaluates after Update and can overwrite the model root.
        // Apply the terrain tilt last while leaving the gameplay root upright.
        UpdateVisualSlopeAlignment(Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        horizontalVelocity += new Vector3(force.x, 0f, force.z);
        
        if (force.y > 0f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, force.y);
            isGrounded = false;
        }
    }

    /// <summary>
    /// Moves the player with an enemy that is maintaining contact. Unlike knockback,
    /// this does not add momentum, so the player stops being pushed as soon as contact ends.
    /// </summary>
    public void ApplyContactPush(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (controller == null || direction.sqrMagnitude < 0.0001f || speed <= 0f)
        {
            return;
        }

        controller.Move(direction.normalized * speed * Time.fixedDeltaTime);
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            int hash = parameter.nameHash;
            if (hash == SpeedHash) hasSpeedParam = true;
            else if (hash == IsGroundedHash) hasIsGroundedParam = true;
            else if (hash == IsFallingHash) hasIsFallingParam = true;
            else if (hash == VelocityYHash) hasVelocityYParam = true;
            else if (hash == JumpHash) hasJumpParam = true;
        }
    }

    private void UpdateGroundState(float deltaTime)
    {
        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        justLanded = isGrounded && !wasGrounded;

        if (isGrounded)
        {
            if (justLanded)
            {
                lastCompletedAirTime = airTimeCounter;
                timeSinceLanded = 0f;
                bunnyHopConsumedForLanding = false;
            }
            else
            {
                timeSinceLanded += deltaTime;
            }

            airTimeCounter = 0f;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }
        }
        else
        {
            airTimeCounter += deltaTime;
            timeSinceLanded = float.PositiveInfinity;
        }
    }

    private void UpdateJumpTimers(float deltaTime)
    {
        coyoteTimeCounter = isGrounded
            ? coyoteTime
            : Mathf.Max(0f, coyoteTimeCounter - deltaTime);

        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
            jumpPressAge = 0f;
            jumpPressed = false;
        }
        else
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - deltaTime);
            jumpPressAge += deltaTime;
        }
    }

    private Vector3 GetCameraRelativeInput()
    {
        if (moveInput.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
    }

    private void UpdateGroundVelocity(Vector3 desiredDirection, float deltaTime)
    {
        float currentSpeed = horizontalVelocity.magnitude;
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            float newSpeed = Mathf.MoveTowards(currentSpeed, 0f, groundDeceleration * deltaTime);
            horizontalVelocity = currentSpeed > 0.0001f
                ? horizontalVelocity * (newSpeed / currentSpeed)
                : Vector3.zero;
            return;
        }

        float targetSpeed = CurrentMoveSpeed * desiredDirection.magnitude;
        if (currentSpeed < 0.0001f)
        {
            horizontalVelocity = Vector3.MoveTowards(
                Vector3.zero,
                desiredDirection.normalized * targetSpeed,
                groundAcceleration * deltaTime);
            return;
        }

        Vector3 currentDirection = horizontalVelocity / currentSpeed;
        Vector3 targetDirection = desiredDirection.normalized;
        float directionDot = Vector3.Dot(currentDirection, targetDirection);

        if (directionDot < -0.25f)
        {
            float reverseBrake = groundBraking * reverseBrakingMultiplier;
            float newSpeed = Mathf.MoveTowards(currentSpeed, 0f, reverseBrake * deltaTime);
            horizontalVelocity = newSpeed > 0f ? currentDirection * newSpeed : Vector3.zero;
            return;
        }

        Vector3 turnedDirection = Vector3.RotateTowards(
            currentDirection,
            targetDirection,
            groundTurnSpeed * Mathf.Deg2Rad * deltaTime,
            0f).normalized;

        float turnAmount = 1f - Mathf.Clamp01(directionDot);
        float acceleratedSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            groundAcceleration * deltaTime);
        float speedAfterTurn = Mathf.MoveTowards(
            acceleratedSpeed,
            0f,
            groundBraking * turnSpeedLoss * turnAmount * deltaTime);

        horizontalVelocity = turnedDirection * speedAfterTurn;
    }

    private void UpdateAirVelocity(Vector3 desiredDirection, float deltaTime)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float currentSpeed = horizontalVelocity.magnitude;
        Vector3 targetDirection = desiredDirection.normalized;

        if (currentSpeed < 0.0001f)
        {
            horizontalVelocity += targetDirection
                * airLateralAcceleration
                * maximumAirDirectionInfluence
                * deltaTime;
            return;
        }

        Vector3 currentDirection = horizontalVelocity / currentSpeed;
        float directionDot = Vector3.Dot(currentDirection, targetDirection);

        if (directionDot < 0f)
        {
            float newSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                airCounterMomentumBraking * -directionDot * deltaTime);
            horizontalVelocity = currentDirection * newSpeed;
            return;
        }

        float speedCap = Mathf.Max(currentSpeed, CurrentMoveSpeed);
        float forwardSpeed = Mathf.Min(
            currentSpeed + airForwardAcceleration * directionDot * deltaTime,
            speedCap);
        Vector3 updatedVelocity = currentDirection * forwardSpeed;

        Vector3 lateralDirection = targetDirection - currentDirection * directionDot;
        if (lateralDirection.sqrMagnitude > 0.0001f)
        {
            updatedVelocity += lateralDirection.normalized
                * airLateralAcceleration
                * maximumAirDirectionInfluence
                * deltaTime;
        }

        horizontalVelocity = Vector3.ClampMagnitude(updatedVelocity, speedCap);
    }

    private void TryJump(Vector3 desiredDirection)
    {
        if (jumpBufferCounter <= 0f || coyoteTimeCounter <= 0f)
        {
            return;
        }

        float finalSpeed = CurrentMoveSpeed;
        float normalizedSpeed = finalSpeed > 0f
            ? Mathf.Clamp01(horizontalVelocity.magnitude / finalSpeed)
            : 0f;
        float jumpHeight = CurrentJumpHeight * (1f + speedJumpBonusMultiplier * normalizedSpeed);

        bool isBunnyHop = IsBunnyHopEligible(desiredDirection);

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        if (isBunnyHop)
        {
            ApplyBunnyHopBoost(desiredDirection);
        }
        else
        {
            ApplyNormalJumpBoost(desiredDirection);
        }

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        jumpPressAge = float.PositiveInfinity;
        if (isGrounded)
        {
            bunnyHopConsumedForLanding = true;
        }
        isGrounded = false;

        if (animator != null && hasJumpParam)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    private bool IsBunnyHopEligible(Vector3 desiredDirection)
    {
        if (!enableBunnyHop
            || !isGrounded
            || bunnyHopConsumedForLanding
            || lastCompletedAirTime < minimumAirTimeForBunnyHop
            || timeSinceLanded > bunnyHopWindow
            || jumpPressAge > bunnyHopWindow)
        {
            return false;
        }

        return desiredDirection.sqrMagnitude >= 0.0001f
            || horizontalVelocity.sqrMagnitude >= minimumRotationSpeed * minimumRotationSpeed;
    }

    private void ApplyNormalJumpBoost(Vector3 desiredDirection)
    {
        horizontalVelocity *= normalJumpMomentumRetention;

        Vector3 boostDirection = desiredDirection.sqrMagnitude >= 0.0001f
            ? desiredDirection.normalized
            : horizontalVelocity.normalized;

        if (boostDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        horizontalVelocity += boostDirection * normalJumpSpeedBoost;
        float maxJumpSpeed = CurrentMoveSpeed * normalJumpMaxSpeedMultiplier;
        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxJumpSpeed);
    }

    private void ApplyBunnyHopBoost(Vector3 desiredDirection)
    {
        horizontalVelocity *= bunnyHopMomentumRetention;

        Vector3 boostDirection = desiredDirection.sqrMagnitude >= 0.0001f
            ? desiredDirection.normalized
            : horizontalVelocity.normalized;

        if (boostDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        horizontalVelocity += boostDirection * bunnyHopSpeedBoost;
        float maxBunnySpeed = CurrentMoveSpeed * bunnyHopMaxSpeedMultiplier;
        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxBunnySpeed);
    }

    private void ApplyGravity(float deltaTime)
    {
        verticalVelocity += gravity * deltaTime;
    }

    private void UpdateRotation(Vector3 desiredDirection, float deltaTime)
    {
        Vector3 facingDirection = Vector3.zero;
        if (desiredDirection.sqrMagnitude >= 0.0001f)
        {
            desiredFacingDirection = desiredDirection.normalized;
            facingDirection = desiredFacingDirection;
        }
        else if (isGrounded
            && horizontalVelocity.sqrMagnitude >= minimumRotationSpeed * minimumRotationSpeed)
        {
            facingDirection = horizontalVelocity.normalized;
        }

        if (facingDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        float rotationSpeed = isGrounded ? groundRotationSpeed : airRotationSpeed;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * deltaTime);
    }

    private void UpdateVisualSlopeAlignment(float deltaTime)
    {
        if (visualRoot == null)
            return;

        Vector3 surfaceNormal = Vector3.up;
        if (alignVisualToGround)
            TryGetGroundNormal(out surfaceNormal);

        float slopeAngle = Vector3.Angle(Vector3.up, surfaceNormal);
        if (slopeAngle > maxVisualSlopeAngle && slopeAngle > 0.001f)
        {
            surfaceNormal = Vector3.Slerp(
                Vector3.up,
                surfaceNormal,
                maxVisualSlopeAngle / slopeAngle).normalized;
        }

        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        Quaternion targetRotation = slopeRotation * transform.rotation * visualBaseLocalRotation;
        float blend = 1f - Mathf.Exp(-slopeAlignmentSpeed * deltaTime);
        visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, blend);
    }

    private bool TryGetGroundNormal(out Vector3 groundNormal)
    {
        Vector3 origin = transform.position + Vector3.up * 2f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            slopeHits,
            8f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        float closestGroundY = float.NegativeInfinity;
        groundNormal = Vector3.up;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = slopeHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestGroundY = hit.point.y;
                groundNormal = hit.normal.normalized;
                foundGround = true;
            }
        }
        if (!foundGround)
            return false;

        float footClearance = controller.bounds.min.y - closestGroundY;
        if (footClearance > visualGroundSnapDistance)
        {
            groundNormal = Vector3.up;
            return false;
        }

        return true;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        float finalSpeed = CurrentMoveSpeed;
        float speedPercent = finalSpeed > 0f
            ? Mathf.Clamp01(horizontalVelocity.magnitude / finalSpeed)
            : 0f;

        if (hasSpeedParam) animator.SetFloat(SpeedHash, speedPercent);
        if (hasIsGroundedParam) animator.SetBool(IsGroundedHash, isGrounded);
        if (hasIsFallingParam) animator.SetBool(IsFallingHash, airTimeCounter >= fallTimeThreshold);
        if (hasVelocityYParam) animator.SetFloat(VelocityYHash, verticalVelocity);
    }
}
