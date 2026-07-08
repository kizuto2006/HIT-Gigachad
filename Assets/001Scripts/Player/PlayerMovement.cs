using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSimpleMovement : MonoBehaviour
{
    [Header("Components")]
    public CharacterController controller;
    private Transform cam;

    private Animator anim;

    [Header("Movement Settings (Acceleration)")]
    public float minMoveSpeed = 2f;
    public float maxMoveSpeed = 10f;
    public float accelerationTime = 1.0f;
    public float decelerationTime = 0.2f;
    private float currentSpeed;
    private float speedVelocity;

    [Header("Air Momentum & Jump")]
    public float gravity = -25f;
    public float baseJumpHeight = 2.5f;
    public float maxBonusJumpHeight = 1.5f;
    [Tooltip("Độ linh hoạt khi điều hướng trên không (1 = dễ như dưới đất, 0 = không thể bẻ lái)")]
    [Range(0f, 1f)] public float airControlMultiplier = 0.5f;
    private Vector3 currentMoveVelocity;

    [Header("Rotation Settings")]
    public float turnSmoothTime = 0.05f;
    private float turnSmoothVelocity;

    [Header("Jump Assist")]
    [Tooltip("Thời gian cho phép nhảy sau khi rời mặt đất")]
    public float coyoteTime = 0.15f;
    [Tooltip("Thời gian ghi nhớ input nhảy trước khi chạm đất")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Thời gian ở trên không trước khi chuyển sang trạng thái rơi (Falling)")]
    public float fallTimeThreshold = 0.3f;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float airTimeCounter;

    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 inputDirection;

    // Input System: lưu giá trị input từ callback
    private Vector2 moveInput;
    private bool jumpPressed;

    private bool hasIsFallingParam;
    private bool hasVelocityYParam;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        anim = GetComponentInChildren<Animator>();
        CheckAnimatorParameters();
    }

    private void CheckAnimatorParameters()
    {
        if (anim != null)
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == "IsFalling") hasIsFallingParam = true;
                if (param.name == "VelocityY") hasVelocityYParam = true;
            }
        }
    }

    void Update()
    {
        GatherInput();
        HandleCoyoteTime();
        HandleJumpBuffer();
    }

    void FixedUpdate()
    {
        HandleGroundCheck();
        HandleMovementAndRotation();
        HandleJump();
        ApplyGravity();
    }

    // =============================================
    // INPUT SYSTEM CALLBACKS (PlayerInput - Invoke Unity Events)
    // Kéo thả trong Inspector của PlayerInput component
    // =============================================

    /// <summary>
    /// Gán vào PlayerInput > Events > Player > Move
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// Gán vào PlayerInput > Events > Player > Jump
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
        }
    }

    // =============================================
    // MOVEMENT LOGIC (giữ nguyên logic cũ)
    // =============================================

    /// <summary>
    /// Kiểm tra nhân vật có đang chạm đất không và reset velocity.y
    /// </summary>
    private void HandleGroundCheck()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded)
        {
            if (velocity.y < 0)
            {
                velocity.y = -2f;
            }
            airTimeCounter = 0f;
        }
        else
        {
            airTimeCounter += Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// Coyote Time: cho phép nhảy trong khoảng thời gian ngắn sau khi rời mặt đất
    /// </summary>
    private void HandleCoyoteTime()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Jump Buffer: ghi nhớ input nhảy trước khi chạm đất
    /// </summary>
    private void HandleJumpBuffer()
    {
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
            jumpPressed = false; // reset flag sau khi đã ghi nhận
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Thu thập input di chuyển và cập nhật animation
    /// </summary>
    private void GatherInput()
    {
        // Đọc từ biến moveInput (được cập nhật bởi OnMove callback)
        inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (anim != null)
        {
            // Truyền currentSpeed / maxMoveSpeed (0 đến 1) để blend animation mượt hơn
            float speedPercent = maxMoveSpeed > 0f ? currentSpeed / maxMoveSpeed : 0f;
            anim.SetFloat("Speed", speedPercent);
            anim.SetBool("IsGrounded", isGrounded);
            
            // Xử lý animation rơi (Falling)
            if (hasIsFallingParam)
            {
                anim.SetBool("IsFalling", airTimeCounter >= fallTimeThreshold);
            }
            if (hasVelocityYParam)
            {
                anim.SetFloat("VelocityY", velocity.y); // Hỗ trợ thêm cho Blend Tree nếu cần
            }
        }
    }

    /// <summary>
    /// Xử lý di chuyển và xoay nhân vật theo hướng camera
    /// </summary>
    private void HandleMovementAndRotation()
    {
        if (inputDirection.magnitude >= 0.1f)
        {
            // Xoay nhân vật theo hướng di chuyển (cả trên đất và trên không)
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 targetMoveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (isGrounded)
            {
                // Dưới đất: Tăng tốc bình thường
                currentSpeed = Mathf.SmoothDamp(currentSpeed, maxMoveSpeed, ref speedVelocity, accelerationTime);
                currentMoveVelocity = targetMoveDir.normalized * currentSpeed;
            }
            else
            {
                // Trên không: Vẫn cho phép lấy thêm tốc độ (nếu nhảy tại chỗ rồi mới bấm đi tới)
                currentSpeed = Mathf.SmoothDamp(currentSpeed, maxMoveSpeed, ref speedVelocity, accelerationTime);
                Vector3 targetAirVelocity = targetMoveDir.normalized * currentSpeed;

                // Dùng Lerp để bẻ lái mượt mà trên không dựa vào airControlMultiplier
                float lerpSpeed = Mathf.Lerp(1f, 15f, airControlMultiplier);
                currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, targetAirVelocity, lerpSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            if (isGrounded)
            {
                // Dưới đất không input: Giảm tốc dần về 0
                currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref speedVelocity, decelerationTime);
                if (currentSpeed < 0.1f) currentSpeed = 0f;

                if (currentMoveVelocity.magnitude > 0.1f)
                {
                    currentMoveVelocity = currentMoveVelocity.normalized * currentSpeed;
                }
                else
                {
                    currentMoveVelocity = Vector3.zero;
                }
            }
            else
            {
                // Trên không không input: Bảo toàn đà trượt (quán tính)
                // (Giữ nguyên currentMoveVelocity)
            }
        }

        controller.Move(currentMoveVelocity * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Xử lý nhảy với coyote time và jump buffer
    /// </summary>
    private void HandleJump()
    {
        if (jumpBufferCounter <= 0f || coyoteTimeCounter <= 0f) return;

        // Cơ chế nhảy phụ thuộc vào tốc độ: Tốc độ càng cao, nhảy càng cao (và bay xa nhờ quán tính currentMoveVelocity)
        float speedRatio = maxMoveSpeed > 0f ? Mathf.Clamp01(currentSpeed / maxMoveSpeed) : 0f;
        float actualJumpHeight = baseJumpHeight + (maxBonusJumpHeight * speedRatio);

        velocity.y = Mathf.Sqrt(actualJumpHeight * -2f * gravity);

        if (anim != null)
        {
            anim.SetTrigger("Jump");
        }

        // Reset cả hai để tránh nhảy nhiều lần
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    /// <summary>
    /// Áp dụng trọng lực và di chuyển theo trục Y
    /// </summary>
    private void ApplyGravity()
    {
        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }
}