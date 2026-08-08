using UnityEngine;

public sealed class BorgarPickup : MonoBehaviour
{
    public const float DefaultHealAmount = 20f;
    [Header("Pickup")]
    [SerializeField, Min(0.1f)] private float magnetRange = 1f;
    [SerializeField, Min(0.1f)] private float magnetSpeed = 10f;
    [SerializeField, Min(0.05f)] private float pickupRange = 0.3f;
    [SerializeField, Min(0f)] private float pickupDelay = 0.25f;
    [SerializeField, Min(1f)] private float lifetime = 30f;

    [Header("Visual")]
    [SerializeField, Min(0.01f)] private float visualScale = 0.75f;
    [SerializeField] private float groundHoverHeight = 0.2f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float rotateSpeed = 90f;

    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private ItemData borgarItem;
    private Transform visualTransform;
    private Vector3 basePosition;
    private float timer;
    private float effectiveMagnetRange;
    private float pickupDelayRemaining;
    private bool collected;
    private bool isMagneting;

    public static BorgarPickup Spawn(Vector3 position)
    {
        GameObject pickupObject = new GameObject("BorgarPickup");
        pickupObject.transform.position = position;

        BorgarPickup pickup = pickupObject.AddComponent<BorgarPickup>();
        pickup.basePosition = pickup.FindLandingPosition(position);
        pickup.transform.position = pickup.basePosition;
        return pickup;
    }

    private void Awake()
    {
        borgarItem = Resources.Load<ItemData>("Items/Borgar");
        CreateCollider();
        CreateVisual();
    }

    private void OnEnable()
    {
        collected = false;
        isMagneting = false;
        timer = 0f;
        pickupDelayRemaining = Mathf.Max(0f, pickupDelay);
        ResolvePlayer();
        basePosition = FindLandingPosition(transform.position);
        transform.position = basePosition;
    }

    private void CreateCollider()
    {
        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.45f;
    }

    private void CreateVisual()
    {
        GameObject visualObject = new GameObject("BorgarVisual");
        visualTransform = visualObject.transform;
        visualTransform.SetParent(transform, false);
        visualTransform.localScale = Vector3.one * visualScale;

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = borgarItem != null ? borgarItem.icon : null;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null && playerHealth != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        playerTransform = player.transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = player.GetComponentInChildren<PlayerHealth>(true);


        float playerPickupRange = playerHealth != null && playerHealth.stats != null
            ? playerHealth.stats.FinalPickupRange
            : 0f;
        effectiveMagnetRange = Mathf.Max(magnetRange, playerPickupRange);
    }

    private void Update()
    {
        if (collected)
            return;

        timer += Time.deltaTime;
        pickupDelayRemaining = Mathf.Max(0f, pickupDelayRemaining - Time.deltaTime);
        if (timer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (visualTransform != null)
        {
            if (Camera.main != null)
                visualTransform.rotation = Camera.main.transform.rotation;
            visualTransform.Rotate(Vector3.forward, rotateSpeed * Time.deltaTime, Space.Self);
        }

        ResolvePlayer();
        if (!isMagneting)
        {
            Vector3 bobPosition = basePosition;
            bobPosition.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = bobPosition;
        }

        if (playerTransform == null)
            return;

        if (pickupDelayRemaining > 0f)
            return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= pickupRange)
        {
            Collect();
            return;
        }

        if (distance <= effectiveMagnetRange)
        {
            isMagneting = true;
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            float range = Mathf.Max(0.01f, effectiveMagnetRange);
            float speed = magnetSpeed * (1f + (effectiveMagnetRange - distance) / range);
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    private void Collect()
    {
        if (collected)
            return;

        ResolvePlayer();
        if (playerHealth == null)
            return;

        collected = true;
        playerHealth.Heal(DefaultHealAmount);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || pickupDelayRemaining > 0f)
            return;

        PlayerHealth targetHealth = other.GetComponentInParent<PlayerHealth>();
        if (targetHealth != null)
        {
            playerHealth = targetHealth;
            playerTransform = targetHealth.transform;
            Collect();
        }
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
            RaycastHit hit;
            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out hit,
                20f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
        }

        return new Vector3(spawnPosition.x, groundY + groundHoverHeight, spawnPosition.z);
    }
}
