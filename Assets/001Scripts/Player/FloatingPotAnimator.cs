using UnityEngine;

/// <summary>
/// Animates floating pots with bobbing and rotation effects.
/// Attached automatically by the ForestArenaGenerator.
/// </summary>
public class FloatingPotAnimator : MonoBehaviour
{
    [Header("Floating Settings")]
    public float bobSpeed = 1.5f;
    public float bobHeight = 0.3f;
    public float rotationSpeed = 30f;

    [Header("Glow Pulse")]
    public float pulseSpeed = 2f;
    public float pulseMinIntensity = 1.5f;
    public float pulseMaxIntensity = 3.5f;

    private Vector3 startPosition;
    private float randomOffset;
    private Light pointLight;

    private void Start()
    {
        startPosition = transform.position;
        randomOffset = Random.Range(0f, Mathf.PI * 2f);
        pointLight = GetComponentInChildren<Light>();

        // Randomize speeds slightly for variety
        bobSpeed *= Random.Range(0.8f, 1.2f);
        rotationSpeed *= Random.Range(0.7f, 1.3f);
    }

    private void Update()
    {
        // Bobbing up and down
        float newY = startPosition.y + Mathf.Sin((Time.time + randomOffset) * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);

        // Slow rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Pulse light intensity
        if (pointLight != null)
        {
            float pulse = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity,
                (Mathf.Sin((Time.time + randomOffset) * pulseSpeed) + 1f) * 0.5f);
            pointLight.intensity = pulse;
        }
    }
}
