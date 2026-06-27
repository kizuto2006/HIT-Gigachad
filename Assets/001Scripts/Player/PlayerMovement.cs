using UnityEngine;

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
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
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
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        inputDirection = new Vector3(x, 0f, z).normalized;

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