using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PlayerSimpleMovement : MonoBehaviour
{
    [Header("── References ──")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private PlayerBaseStats playerStats;
    [Tooltip("Chỉ dùng khi PlayerBaseStats chưa được gán. Không phải nguồn tốc độ gameplay chính.")]
    [SerializeField, Min(0f)] private float fallbackMoveSpeed = 5f;
    [Tooltip("Chỉ dùng khi PlayerBaseStats chưa được gán. Không phải nguồn jump gameplay chính.")]
    [SerializeField, Min(0f)] private float fallbackJumpHeight = 1.8f;

    [SerializeField] private bool useFixedSpawnPosition = true;
    [SerializeField] private Vector3 fixedSpawnPosition = Vector3.zero;
    [SerializeField, Min(0f)] private float spawnEdgeMargin = 50f;
    [SerializeField] private Vector2 mapBoundsMin = new Vector2(-270f, -270f);
    [SerializeField] private Vector2 mapBoundsMax = new Vector2(270f, 270f);
    [SerializeField] private LayerMask spawnGroundLayers = ~0;
    [SerializeField, Min(1f)] private float spawnProbeHeight = 160f;
    [SerializeField, Min(1f)] private float spawnProbeDistance = 400f;

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
    [Tooltip("Mức phanh khi đổi sang hướng gần ngược để vẫn giữ quỹ đạo cong thay vì dừng gấp.")]
    [SerializeField, Range(0f, 1f)] private float reverseMomentumBrakeMultiplier = 0.35f;

    [Tooltip("Mức mất tốc khi cua. 0 không mất tốc, 1 dùng toàn bộ groundBraking theo góc cua.")]
    [SerializeField, Range(0f, 1f)] private float turnSpeedLoss = 0.05f;

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
    [SerializeField, Min(0f)] private float normalJumpSpeedBoost = 1.25f;
    [Tooltip("Giới hạn tốc độ ngang của cú nhảy thường theo tỷ lệ CurrentMoveSpeed.")]
    [SerializeField, Min(1f)] private float normalJumpMaxSpeedMultiplier = 1.4f;
    [Tooltip("Tỷ lệ momentum ngang giữ lại khi bunny-hop. Đặt 1 để giữ nguyên vận tốc trước khi cộng boost.")]
    [SerializeField, Range(0f, 1f)] private float bunnyHopMomentumRetention = 1f;

    [Header("── Bunny Hop ──")]
    [Tooltip("Cho phép bunny-hop thông qua jump buffer gần thời điểm landing.")]
    [SerializeField] private bool enableBunnyHop = true;
    [Tooltip("Hold jump to automatically take off again on landing.")]
    [SerializeField] private bool allowHeldBunnyHop = false;
    [Tooltip("Khoảng thời gian trước hoặc sau landing được tính là bunny-hop.")]
    [SerializeField, Min(0f)] private float bunnyHopWindow = 0.2f;
    [Tooltip("Lượng vận tốc ngang cộng thêm khi bunny-hop thành công.")]
    [SerializeField, Min(0f)] private float bunnyHopSpeedBoost = 0.45f;
    [Tooltip("Minimum speed of the first chained bunny hop relative to CurrentMoveSpeed.")]
    [SerializeField, Min(1f)] private float bunnyHopStartSpeedMultiplier = 1.55f;
    [Tooltip("Additional speed-cap multiplier granted by each consecutive bunny hop.")]
    [SerializeField, Min(0f)] private float bunnyHopSpeedStepMultiplier = 0.15f;
    [Tooltip("Giới hạn bunny speed theo tỷ lệ CurrentMoveSpeed.")]
    [SerializeField, Min(1f)] private float bunnyHopMaxSpeedMultiplier = 1.85f;
    [Tooltip("Time grounded before the accumulated bunny-hop chain resets.")]
    [SerializeField, Min(0f)] private float bunnyHopChainResetDelay = 0.3f;
    [Tooltip("Tốc độ xoay momentum theo input khi đang bunny-hop (độ/giây).")]
    [SerializeField, Min(0f)] private float bunnyHopAirTurnSpeed = 300f;
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
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.2f;
    [Tooltip("Thời gian ở trên không trước khi bật trạng thái Falling.")]
    [SerializeField, Min(0f)] private float fallTimeThreshold = 0.3f;

    [Header("Jump VFX")]
    [Tooltip("Emit a sand-dust burst at the player's feet on takeoff.")]
    [SerializeField] private bool enableJumpParticles = true;
    [Tooltip("Particle count for a normal jump.")]
    [SerializeField, Min(1)] private int normalJumpParticleCount = 12;
    [Tooltip("Base particle count for a bunny hop.")]
    [SerializeField, Min(1)] private int bunnyHopBaseParticleCount = 16;
    [Tooltip("Extra particles for each consecutive bunny hop.")]
    [SerializeField, Min(0)] private int bunnyHopParticlesPerChain = 4;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static Material sharedJumpParticleMaterial;

    private Transform cameraTransform;
    private Animator animator;
    private Transform visualRoot;
    private Quaternion visualBaseLocalRotation;
    private readonly RaycastHit[] slopeHits = new RaycastHit[16];
    private readonly RaycastHit[] spawnGroundHits = new RaycastHit[32];
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
    private int bunnyHopChain;
    private bool isGrounded;
    private bool wasGrounded;
    private bool justLanded;
    private bool bunnyHopConsumedForLanding = true;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool hasSpeedParam;
    private bool hasIsGroundedParam;
    private bool hasIsFallingParam;
    private bool hasVelocityYParam;
    private bool hasJumpParam;
    private ParticleSystem jumpParticles;
    private float CurrentMoveSpeed => Mathf.Max(
        0f,
        (playerStats != null ? playerStats.FinalSpeed : fallbackMoveSpeed) *
        PlayerPowerupController.GetMoveSpeedMultiplierFor(transform));
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
        CreateJumpParticles();

        if (controller == null)
        {
            Debug.LogError("[PlayerMovement] CharacterController chưa được gán.", this);
            enabled = false;
            return;
        }

        if (useFixedSpawnPosition)
            PlaceAtFixedSpawnPosition();

        if (playerStats == null)
        {
            Debug.LogWarning("[PlayerMovement] PlayerBaseStats chưa được gán; đang dùng fallback speed/jump.", this);
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("[PlayerMovement] Không tìm thấy Main Camera; input sẽ dùng hướng world-space.", this);
        }
    }

    private void PlaceAtFixedSpawnPosition()
    {
        Vector3 spawnPosition = fixedSpawnPosition;
        float minX = Mathf.Min(mapBoundsMin.x, mapBoundsMax.x);
        float maxX = Mathf.Max(mapBoundsMin.x, mapBoundsMax.x);
        float minZ = Mathf.Min(mapBoundsMin.y, mapBoundsMax.y);
        float maxZ = Mathf.Max(mapBoundsMin.y, mapBoundsMax.y);
        float marginX = Mathf.Min(spawnEdgeMargin, (maxX - minX) * 0.5f);
        float marginZ = Mathf.Min(spawnEdgeMargin, (maxZ - minZ) * 0.5f);

        spawnPosition.x = Mathf.Clamp(spawnPosition.x, minX + marginX, maxX - marginX);
        spawnPosition.z = Mathf.Clamp(spawnPosition.z, minZ + marginZ, maxZ - marginZ);

        if (TryGetSpawnGroundHeight(spawnPosition, out float groundY))
            spawnPosition.y = groundY + GetControllerGroundOffset();
        else
            spawnPosition.y += GetControllerGroundOffset();

        bool wasControllerEnabled = controller.enabled;
        if (wasControllerEnabled)
            controller.enabled = false;

        transform.position = spawnPosition;

        if (wasControllerEnabled)
            controller.enabled = true;
    }

    private bool TryGetSpawnGroundHeight(Vector3 position, out float groundY)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            bool insideTerrain = position.x >= terrainPosition.x
                && position.x <= terrainPosition.x + terrainSize.x
                && position.z >= terrainPosition.z
                && position.z <= terrainPosition.z + terrainSize.z;

            if (insideTerrain)
            {
                groundY = terrain.SampleHeight(position) + terrainPosition.y;
                return true;
            }
        }

        Vector3 rayOrigin = new Vector3(position.x, spawnProbeHeight, position.z);
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            spawnGroundHits,
            spawnProbeDistance,
            spawnGroundLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        groundY = position.y;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = spawnGroundHits[i];
            if (hit.collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            groundY = hit.point.y;
            foundGround = true;
        }

        return foundGround;
    }

    private float GetControllerGroundOffset()
    {
        return controller != null
            ? Mathf.Max(0f, controller.height * 0.5f - controller.center.y)
            : 0f;
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
        bool preserveLandingMomentum = IsBunnyHopEligible(desiredDirection);
        if (isGrounded && !preserveLandingMomentum)
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
            jumpHeld = true;
            jumpPressed = true;
        }
        else if (context.canceled)
        {
            jumpHeld = false;
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        PlayerPowerupController powerups = PlayerPowerupController.FindFor(transform);
        if (powerups != null && powerups.IsInvulnerable)
            return;

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
        PlayerPowerupController powerups = PlayerPowerupController.FindFor(transform);
        if (powerups != null && powerups.IsInvulnerable)
            return;

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
                if (timeSinceLanded > bunnyHopChainResetDelay)
                {
                    bunnyHopChain = 0;
                }
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

        if (enableBunnyHop && allowHeldBunnyHop && jumpHeld && justLanded)
        {
            jumpBufferCounter = Mathf.Max(jumpBufferCounter, bunnyHopWindow);
            jumpPressAge = 0f;
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
        float turnRadians = groundTurnSpeed * Mathf.Deg2Rad * deltaTime;

        if (directionDot < -0.25f)
        {
            Vector3 reverseTurnedDirection = Vector3.RotateTowards(
                currentDirection,
                targetDirection,
                turnRadians,
                0f).normalized;
            float reverseBrake = groundBraking
                * reverseBrakingMultiplier
                * reverseMomentumBrakeMultiplier;
            // Brake the old momentum while the velocity direction turns. Using
            // targetSpeed here would preserve full speed when currentSpeed is
            // already equal to targetSpeed, making an opposite input feel like
            // a wide, delayed arc instead of a responsive turn.
            float newSpeed = Mathf.MoveTowards(currentSpeed, 0f, reverseBrake * deltaTime);
            horizontalVelocity = reverseTurnedDirection * newSpeed;
            return;
        }

        Vector3 turnedDirection = Vector3.RotateTowards(
            currentDirection,
            targetDirection,
            turnRadians,
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

        if (!isGrounded && bunnyHopChain > 0)
        {
            Vector3 steeredDirection = Vector3.RotateTowards(
                currentDirection,
                targetDirection,
                bunnyHopAirTurnSpeed * Mathf.Deg2Rad * deltaTime,
                0f).normalized;
            horizontalVelocity = steeredDirection * currentSpeed;
            return;
        }
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
            bunnyHopChain++;
            ApplyBunnyHopBoost(desiredDirection);
        }
        else
        {
            bunnyHopChain = 0;
            ApplyNormalJumpBoost(desiredDirection);
        }

        PlayJumpParticles(isBunnyHop);
        SoundEffectsAudioManager.Instance?.PlayJumpSound();

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

        return desiredDirection.sqrMagnitude >= 0.0001f;
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
        float chainSpeedMultiplier = Mathf.Min(
            bunnyHopStartSpeedMultiplier
                + Mathf.Max(0, bunnyHopChain - 1) * bunnyHopSpeedStepMultiplier,
            bunnyHopMaxSpeedMultiplier);
        float chainTargetSpeed = CurrentMoveSpeed * chainSpeedMultiplier;
        float maxBunnySpeed = CurrentMoveSpeed * bunnyHopMaxSpeedMultiplier;
        float targetSpeed = Mathf.Min(
            Mathf.Max(horizontalVelocity.magnitude, chainTargetSpeed),
            maxBunnySpeed);
        horizontalVelocity = horizontalVelocity.normalized * targetSpeed;
    }

    private void CreateJumpParticles()
    {
        if (!enableJumpParticles || jumpParticles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("Player Jump Dust");
        particleObject.transform.SetParent(transform, false);
        jumpParticles = particleObject.AddComponent<ParticleSystem>();
        jumpParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = jumpParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        main.cullingMode = ParticleSystemCullingMode.Automatic;
        main.gravityModifier = 0.65f;

        ParticleSystem.EmissionModule emission = jumpParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = jumpParticles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = jumpParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = jumpParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.2f)));

        ParticleSystemRenderer particleRenderer = jumpParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.allowOcclusionWhenDynamic = false;
        Material particleMaterial = GetOrCreateJumpParticleMaterial();
        if (particleMaterial != null)
        {
            particleRenderer.sharedMaterial = particleMaterial;
        }
    }

    private void PlayJumpParticles(bool isBunnyHop)
    {
        if (!enableJumpParticles)
        {
            return;
        }

        if (jumpParticles == null)
        {
            CreateJumpParticles();
        }

        if (jumpParticles == null)
        {
            return;
        }

        int chainStrength = isBunnyHop ? Mathf.Clamp(bunnyHopChain, 1, 3) : 0;
        int particleCount = isBunnyHop
            ? bunnyHopBaseParticleCount + bunnyHopParticlesPerChain * chainStrength
            : normalJumpParticleCount;
        Vector3 footPosition = new Vector3(
            transform.position.x,
            controller != null ? controller.bounds.min.y + 0.04f : transform.position.y + 0.04f,
            transform.position.z);

        Vector3 movementDirection = horizontalVelocity.sqrMagnitude > 0.001f
            ? horizontalVelocity.normalized
            : transform.forward;
        Color baseColor = Color.white;

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle;
            Vector3 radialDirection = new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (radialDirection.sqrMagnitude < 0.001f)
            {
                radialDirection = transform.right;
            }
            radialDirection.Normalize();

            float spreadRadius = Random.Range(0.05f, isBunnyHop ? 0.28f : 0.2f);
            float outwardSpeed = Random.Range(
                isBunnyHop ? 2.2f : 1.35f,
                isBunnyHop ? 3.6f + chainStrength * 0.3f : 2.45f);
            float upwardSpeed = Random.Range(0.35f, isBunnyHop ? 1.25f : 0.9f);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = footPosition + radialDirection * spreadRadius,
                velocity = radialDirection * outwardSpeed
                    - movementDirection * (isBunnyHop ? 0.8f : 0.35f)
                    + Vector3.up * upwardSpeed,
                startLifetime = Random.Range(0.42f, isBunnyHop ? 0.78f : 0.65f),
                startSize = Random.Range(
                    isBunnyHop ? 0.2f : 0.14f,
                    isBunnyHop ? 0.4f + chainStrength * 0.035f : 0.3f),
                startColor = baseColor
            };
            jumpParticles.Emit(emit, 1);
        }
    }

    private static Material GetOrCreateJumpParticleMaterial()
    {
        if (sharedJumpParticleMaterial != null)
        {
            return sharedJumpParticleMaterial;
        }

        Shader shader = Resources.Load<Shader>("Shaders/GoldenSandParticle");
        if (shader == null)
        {
            shader = Shader.Find("Custom/Gigachad/Golden Sand Particle");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
        if (shader == null)
        {
            return null;
        }

        sharedJumpParticleMaterial = new Material(shader)
        {
            name = "Shared Runtime Player Jump Dust Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (sharedJumpParticleMaterial.HasProperty("_Softness"))
        {
            sharedJumpParticleMaterial.SetFloat("_Softness", 0.3f);
        }
        return sharedJumpParticleMaterial;
    }

    private void ApplyGravity(float deltaTime)
    {
        verticalVelocity += gravity * deltaTime;
    }

    private void UpdateRotation(Vector3 desiredDirection, float deltaTime)
    {
        Vector3 facingDirection = horizontalVelocity;
        float minimumRotationSpeedSquared = minimumRotationSpeed * minimumRotationSpeed;

        if (facingDirection.sqrMagnitude >= minimumRotationSpeedSquared)
        {
            desiredFacingDirection = facingDirection.normalized;
        }
        else if (desiredDirection.sqrMagnitude >= 0.0001f)
        {
            desiredFacingDirection = desiredDirection.normalized;
            facingDirection = desiredFacingDirection;
        }
        else
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
