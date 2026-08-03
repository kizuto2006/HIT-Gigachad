using System;
using UnityEngine;

/// <summary>
/// XP Gem — drop bởi enemy khi chết.
/// Player chạm vào → thu XP. Có magnet effect: tự bay về player khi đủ gần.
/// </summary>
public class XPGem : MonoBehaviour
{
    [Header("── XP Settings ──")]
    [Tooltip("Lượng XP gem này cho khi thu.")]
    public int xpAmount = 1;

    [Header("── Magnet Settings ──")]
    [Tooltip("Khoảng cách để gem bắt đầu bay về player.")]
    public float magnetRange = 1f;

    [Tooltip("Tốc độ bay về player.")]
    public float magnetSpeed = 10f;

    [Tooltip("Khoảng cách để thu XP (pickup).")]
    public float pickupRange = 0.25f;

    [Header("── Visual ──")]
    [Tooltip("Thời gian tồn tại tối đa trước khi tự hủy (giây).")]
    public float lifetime = 30f;

    [Header("Drop Animation")]
    [Tooltip("Độ cao gem bắt đầu rơi so với điểm đáp.")]
    [Min(0f)] public float dropHeight = 1.5f;
    [Tooltip("Thời gian gem rơi xuống đất.")]
    [Min(0.05f)] public float dropDuration = 0.45f;
    [Tooltip("Độ cao nảy nhẹ sau khi chạm đất.")]
    [Min(0f)] public float bounceHeight = 0.12f;
    [Tooltip("Thời gian của lần nảy sau khi chạm đất.")]
    [Min(0f)] public float bounceDuration = 0.18f;
    [Tooltip("Khoảng cách gem lơ lửng trên mặt đất.")]
    [Min(0f)] public float groundHoverHeight = 0.16f;

    [Tooltip("Chiều cao của gem so với chiều cao Player. 0.1 = bằng 1/10 Player.")]
    [Range(0.01f, 1f)]
    public float playerHeightRatio = 0.1f;

    [Tooltip("Tốc độ quay (visual effect).")]
    public float rotateSpeed = 90f;

    [Tooltip("Biên độ lên xuống (bobbing).")]
    public float bobAmplitude = 0.15f;

    [Tooltip("Tốc độ bobbing.")]
    public float bobSpeed = 2f;

    private Transform playerTransform;
    private XPSystem xpSystem;
    private PlayerBaseStats playerStats;
    private bool collected;
    private float timer;
    private Vector3 basePosition;
    private bool isMagneting;
    private bool isDropping;
    private float dropTimer;
    private Vector3 dropStartPosition;
    private Vector3 originalLocalScale;
    private Action<XPGem> releaseToPool;

    private void Awake()
    {
        originalLocalScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    internal void SetPoolRelease(Action<XPGem> releaseAction)
    {
        releaseToPool = releaseAction;
    }

    private void ResetForSpawn()
    {
        collected = false;
        timer = 0f;
        isMagneting = false;
        isDropping = false;
        dropTimer = 0f;
        playerTransform = null;
        xpSystem = null;
        playerStats = null;
        transform.localScale = originalLocalScale;
        // Tìm player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            xpSystem = player.GetComponent<XPSystem>();
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerStats = playerHealth.stats;

            if (playerStats != null)
                magnetRange = playerStats.FinalPickupRange;

            ScaleRelativeToPlayer(player);
        }

        basePosition = FindLandingPosition(transform.position);
        dropStartPosition = basePosition + Vector3.up * dropHeight;
        transform.position = dropStartPosition;
        isDropping = true;
    }

    private void ScaleRelativeToPlayer(GameObject player)
    {
        float playerHeight = 0f;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            playerHeight = controller.height * Mathf.Abs(controller.transform.lossyScale.y);
        }

        if (playerHeight <= 0.001f)
        {
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>();
            if (playerRenderers.Length > 0)
            {
                Bounds playerBounds = playerRenderers[0].bounds;
                for (int i = 1; i < playerRenderers.Length; i++)
                {
                    playerBounds.Encapsulate(playerRenderers[i].bounds);
                }

                playerHeight = playerBounds.size.y;
            }
        }

        Renderer gemRenderer = GetComponentInChildren<Renderer>();
        if (playerHeight <= 0.001f || gemRenderer == null || gemRenderer.bounds.size.y <= 0.001f)
        {
            return;
        }

        float targetHeight = playerHeight * Mathf.Clamp(playerHeightRatio, 0.01f, 1f);
        float uniformScale = targetHeight / gemRenderer.bounds.size.y;
        transform.localScale *= uniformScale;
    }

    void Update()
    {
        if (collected) return;

        // Lifetime
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Despawn();
            return;
        }

        // Visual: quay + bobbing
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        if (isDropping)
        {
            UpdateDropAnimation();
            return;
        }

        if (!isMagneting)
        {
            Vector3 pos = basePosition;
            pos.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = pos;
        }

        if (playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Pickup
        if (distToPlayer <= pickupRange)
        {
            Collect();
            return;
        }

        // Magnet: bay về player khi đủ gần
        if (distToPlayer <= magnetRange)
        {
            isMagneting = true;
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            float speed = magnetSpeed * (1f + (magnetRange - distToPlayer) / magnetRange); // Tăng tốc khi gần
            transform.position += dir * speed * Time.deltaTime;
        }
    }

    private void UpdateDropAnimation()
    {
        dropTimer += Time.deltaTime;

        if (dropTimer < dropDuration)
        {
            float progress = Mathf.Clamp01(dropTimer / Mathf.Max(0.05f, dropDuration));
            float acceleratedProgress = progress * progress;
            transform.position = Vector3.LerpUnclamped(dropStartPosition, basePosition, acceleratedProgress);
            return;
        }

        float bounceTime = dropTimer - dropDuration;
        if (bounceDuration > 0f && bounceTime < bounceDuration)
        {
            float bounceProgress = Mathf.Clamp01(bounceTime / bounceDuration);
            float bounceOffset = Mathf.Sin(bounceProgress * Mathf.PI) * bounceHeight;
            transform.position = basePosition + Vector3.up * bounceOffset;
            return;
        }

        transform.position = basePosition;
        isDropping = false;
    }

    private Vector3 FindLandingPosition(Vector3 spawnPosition)
    {
        float groundY = spawnPosition.y;

        if (Terrain.activeTerrain != null)
        {
            Terrain terrain = Terrain.activeTerrain;
            groundY = terrain.SampleHeight(spawnPosition) + terrain.transform.position.y;
        }
        else
        {
            Vector3 rayOrigin = spawnPosition + Vector3.up * 3f;
            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                20f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
        }

        return new Vector3(spawnPosition.x, groundY + groundHoverHeight, spawnPosition.z);
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        if (xpSystem != null)
        {
            float multiplier = playerStats != null ? playerStats.FinalExperienceMultiplier : 1f;
            xpSystem.AddXP(Mathf.Max(0f, xpAmount * multiplier));
        }

        // TODO: Thêm VFX/SFX thu gem ở đây
        Despawn();
    }

    private void Despawn()
    {
        if (releaseToPool != null)
        {
            releaseToPool(this);
            return;
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || isDropping) return;
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }
}
