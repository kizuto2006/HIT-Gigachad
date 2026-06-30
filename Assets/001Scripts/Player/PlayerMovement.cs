using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSimpleMovement : MonoBehaviour
{
    [Header("Components")]
    public CharacterController controller;
    private Transform cam;

    private Animator anim;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float gravity = -25f;
    public float jumpHeight = 2.5f;

    [Header("Rotation Settings")]
    public float turnSmoothTime = 0.05f;
    private float turnSmoothVelocity;

    [Header("Jump Assist")]
    [Tooltip("Thời gian cho phép nhảy sau khi rời mặt đất")]
    public float coyoteTime = 0.15f;
    [Tooltip("Thời gian ghi nhớ input nhảy trước khi chạm đất")]
    public float jumpBufferTime = 0.15f;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 inputDirection;

    // Input System: lưu giá trị input từ callback
    private Vector2 moveInput;
    private bool jumpPressed;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        anim = GetComponentInChildren<Animator>();
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
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
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
            anim.SetFloat("Speed", inputDirection.magnitude);
            anim.SetBool("IsGrounded", isGrounded);
        }
    }

    /// <summary>
    /// Xử lý di chuyển và xoay nhân vật theo hướng camera
    /// </summary>
    private void HandleMovementAndRotation()
    {
        if (inputDirection.magnitude < 0.1f) return;

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cam.eulerAngles.y;

        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        controller.Move(moveDir.normalized * moveSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Xử lý nhảy với coyote time và jump buffer
    /// </summary>
    private void HandleJump()
    {
        if (jumpBufferCounter <= 0f || coyoteTimeCounter <= 0f) return;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

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