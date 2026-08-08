using UnityEngine;

[DisallowMultipleComponent]
public sealed class PowerupPickup : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField, Min(0.1f)] private float magnetRange = 1.25f;
    [SerializeField, Min(0.1f)] private float magnetSpeed = 11f;
    [SerializeField, Min(0.05f)] private float pickupRange = 0.35f;
    [SerializeField, Min(0f)] private float pickupDelay = 0.25f;
    [SerializeField, Min(1f)] private float lifetime = 35f;

    [Header("Visual")]
    // Powerup textures are 1024 px at 100 PPU. This matches Borgar's
    // 32 px at 32 PPU with its 0.75 visual scale.
    [SerializeField, Min(0.01f)] private float visualScale = 0.0732422f;
    [SerializeField] private float groundHoverHeight = 0.22f;
    [SerializeField] private float bobAmplitude = 0.14f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private float rotateSpeed = 105f;

    [SerializeField] private PowerupData powerupData;
    private Transform playerTransform;
    private PlayerPowerupController playerPowerups;
    private Transform visualTransform;
    private Vector3 basePosition;
    private float timer;
    private float effectiveMagnetRange;
    private float pickupDelayRemaining;
    private bool collected;
    private bool isMagneting;

    public PowerupData Data => powerupData;

    public static PowerupPickup Spawn(Vector3 position, PowerupData data)
    {
        if (data == null)
            return null;

        GameObject pickupObject = new GameObject("PowerupPickup_" + data.powerupType);
        PowerupPickup pickup = pickupObject.AddComponent<PowerupPickup>();
        pickup.Initialize(data);
        pickup.basePosition = pickup.FindLandingPosition(position);
        pickup.transform.position = pickup.basePosition;
        return pickup;
    }

    private void Awake()
    {
        CreateCollider();
        CreateVisual();
        ApplyDataVisual();
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

    private void Initialize(PowerupData data)
    {
        powerupData = data;

        if (visualTransform == null)
            CreateVisual();

        ApplyDataVisual();
    }

    private void ApplyDataVisual()
    {
        if (visualTransform == null)
            return;

        SpriteRenderer renderer = visualTransform.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.sprite = powerupData != null ? powerupData.icon : null;

        if (powerupData == null)
            return;

        PowerupVfxController.AttachPickupAura(
            visualTransform,
            powerupData.tint);
    }

    private void CreateCollider()
    {
        if (GetComponent<SphereCollider>() != null)
            return;

        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.5f;
    }

    private void CreateVisual()
    {
        if (visualTransform != null)
            return;

        Transform existingVisual = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<SpriteRenderer>() != null)
            {
                existingVisual = child;
                break;
            }
        }

        if (existingVisual != null)
        {
            visualTransform = existingVisual;
            visualTransform.localScale = Vector3.one * visualScale;

            SpriteRenderer existingRenderer = existingVisual.GetComponent<SpriteRenderer>();
            if (existingRenderer == null)
                existingRenderer = existingVisual.gameObject.AddComponent<SpriteRenderer>();

            existingRenderer.color = Color.white;
            existingRenderer.sortingOrder = 12;
            return;
        }

        GameObject visualObject = new GameObject("PowerupVisual");
        visualTransform = visualObject.transform;
        visualTransform.SetParent(transform, false);
        visualTransform.localScale = Vector3.one * visualScale;

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.color = Color.white;
        renderer.sortingOrder = 12;
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null && playerPowerups != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        playerTransform = player.transform;
        playerPowerups = PlayerPowerupController.FindFor(playerTransform);

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
            health = player.GetComponentInChildren<PlayerHealth>(true);

        float playerPickupRange = health != null && health.stats != null
            ? health.stats.FinalPickupRange
            : 0f;
        float powerupMagnetMultiplier = PlayerPowerupController.GetPickupRangeMultiplierFor(playerTransform);
        effectiveMagnetRange = Mathf.Max(
            magnetRange,
            playerPickupRange * powerupMagnetMultiplier);
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

            visualTransform.Rotate(
                Vector3.forward,
                rotateSpeed * Time.deltaTime,
                Space.Self);
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
        if (playerPowerups == null || powerupData == null)
            return;

        if (!playerPowerups.TryApply(powerupData))
            return;

        collected = true;
        PowerupVfxController.PlayPickupBurst(transform.position, powerupData.tint);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || pickupDelayRemaining > 0f)
            return;

        PlayerPowerupController target = other.GetComponentInParent<PlayerPowerupController>();
        if (target != null)
        {
            playerPowerups = target;
            playerTransform = target.transform;
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

        return new Vector3(
            spawnPosition.x,
            groundY + groundHoverHeight,
            spawnPosition.z);
    }
}
