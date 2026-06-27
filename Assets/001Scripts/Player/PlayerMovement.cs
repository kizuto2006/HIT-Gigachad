using UnityEngine;

public class PlayerSimpleMovement : MonoBehaviour
{
    [Header("Components")]
    public CharacterController controller;
    private Transform cam;

    // --- 1. THÊM BIẾN CHỨA ANIMATOR ---
    private Animator anim;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float gravity = -25f;
    public float jumpHeight = 2.5f;

    [Header("Rotation Settings")]
    public float turnSmoothTime = 0.05f;
    private float turnSmoothVelocity;

    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        // --- 2. TỰ ĐỘNG TÌM ANIMATOR Ở OBJECT CON (Megachadd) ---
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(x, 0f, z).normalized;

        // --- 3. BÁO CÁO TỐC ĐỘ CHO ANIMATOR ---
        // direction.magnitude sẽ bằng 0 khi đứng im, và bằng 1 khi bấm phím di chuyển
        if (anim != null)
        {
            anim.SetFloat("Speed", direction.magnitude);
        }

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // --- 4. KÍCH HOẠT HOẠT ẢNH NHẢY ---
            if (anim != null)
            {
                anim.SetTrigger("Jump");
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}