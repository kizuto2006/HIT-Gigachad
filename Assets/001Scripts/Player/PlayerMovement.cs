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
    [SerializeField, Min(0f)] private float groundAcceleration = 30f;
    [Tooltip("Ma sát giảm tốc khi thả input.")]
    [SerializeField, Min(0f)] private float groundDeceleration = 15f;
    [Tooltip("Lực phanh khi cua gấp hoặc đổi hướng.")]
    [SerializeField, Min(0f)] private float groundBraking = 38f;
    [Tooltip("Tốc độ tối đa mà hướng velocity có thể xoay trên mặt đất (độ/giây).")]
    [SerializeField, Min(0f)] private float groundTurnSpeed = 270f;
    [Tooltip("Nhân lực phanh khi input gần ngược với momentum hiện tại.")]
    [SerializeField, Min(1f)] private float reverseBrakingMultiplier = 1.6f;
    [Tooltip("Mức mất tốc khi cua. 0 không mất tốc, 1 dùng toàn bộ groundBraking theo góc cua.")]
    [SerializeField, Range(0f, 1f)] private float turnSpeedLoss = 0.45f;

    [Header("── Air Momentum ──")]
    [Tooltip("Gia tốc rất nhỏ được phép thêm khi đang trên không.")]
    [SerializeField, Min(0f)] private float airAcceleration = 3f;
    [Tooltip("Tốc độ tối đa mà hướng velocity có thể xoay trên không (độ/giây).")]
    [SerializeField, Min(0f)] private float airTurnSpeed = 55f;
    [Tooltip("Lực giảm momentum khi giữ input ngược hướng trên không.")]
    [SerializeField, Min(0f)] private float airBraking = 2f;
    [Tooltip("Giới hạn air speed so với FinalSpeed.")]
    [SerializeField, Min(1f)] private float maxAirSpeedMultiplier = 1.1f;

    [Header("── Vertical Movement ──")]
    [SerializeField] private float gravity = -20f;
    [Tooltip("Bonus jump theo tỷ lệ tốc độ hiện tại. Mặc định 0 để jump chỉ do stats quyết định.")]
    [SerializeField, Min(0f)] private float speedJumpBonusMultiplier = 0f;
    [Tooltip("Vận tốc nhỏ hướng xuống giúp CharacterController bám mặt đất.")]
    [SerializeField, Min(0f)] private float groundedStickForce = 2f;

    [Header("── Rotation ──")]
    [SerializeField, Min(0f)] private float groundRotationSpeed = 540f;
    [SerializeField, Min(0f)] private float airRotationSpeed = 180f;
    [SerializeField, Min(0f)] private float minimumRotationSpeed = 0.1f;

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
    private Vector2 moveInput;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float airTimeCounter;
    private bool isGrounded;
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

        TryJump();
        ApplyGravity(deltaTime);
        UpdateRotation(deltaTime);

        Vector3 frameVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(frameVelocity * deltaTime);

        UpdateAnimator();
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
        isGrounded = controller.isGrounded;

        if (isGrounded)
        {
            airTimeCounter = 0f;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -groundedStickForce;
            }
        }
        else
        {
            airTimeCounter += deltaTime;
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
            jumpPressed = false;
        }
        else
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - deltaTime);
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
        float maxAirSpeed = CurrentMoveSpeed * maxAirSpeedMultiplier;
        Vector3 targetDirection = desiredDirection.normalized;

        if (currentSpeed < 0.0001f)
        {
            horizontalVelocity = Vector3.MoveTowards(
                Vector3.zero,
                targetDirection * maxAirSpeed,
                airAcceleration * deltaTime);
            return;
        }

        Vector3 currentDirection = horizontalVelocity / currentSpeed;
        float directionDot = Vector3.Dot(currentDirection, targetDirection);
        Vector3 turnedDirection = Vector3.RotateTowards(
            currentDirection,
            targetDirection,
            airTurnSpeed * Mathf.Deg2Rad * deltaTime,
            0f).normalized;

        float newSpeed = currentSpeed;
        if (directionDot > 0f)
        {
            newSpeed = Mathf.MoveTowards(
                currentSpeed,
                maxAirSpeed,
                airAcceleration * directionDot * deltaTime);
        }
        else
        {
            newSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                airBraking * -directionDot * deltaTime);
        }

        horizontalVelocity = turnedDirection * Mathf.Min(newSpeed, maxAirSpeed);
    }

    private void TryJump()
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

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        isGrounded = false;

        if (animator != null && hasJumpParam)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    private void ApplyGravity(float deltaTime)
    {
        verticalVelocity += gravity * deltaTime;
    }

    private void UpdateRotation(float deltaTime)
    {
        if (horizontalVelocity.sqrMagnitude < minimumRotationSpeed * minimumRotationSpeed)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
        float rotationSpeed = isGrounded ? groundRotationSpeed : airRotationSpeed;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * deltaTime);
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
