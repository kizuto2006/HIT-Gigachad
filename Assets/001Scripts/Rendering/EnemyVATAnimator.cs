using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class EnemyVATAnimator : MonoBehaviour
{
    private static readonly int PhaseOffsetId = Shader.PropertyToID("_VATPhaseOffset");
    private static readonly int PlaybackSpeedId = Shader.PropertyToID("_VATPlaybackSpeed");

    [SerializeField] private bool randomizeStartFrame = true;
    [SerializeField, Range(0f, 1f)] private float phaseOffset;
    [SerializeField, Min(0f)] private float playbackSpeed = 1f;

    private MeshRenderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        ApplyProperties();
    }

    private void OnEnable()
    {
        ApplyProperties();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyProperties();
        }
    }

    private void ApplyProperties()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<MeshRenderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        cachedRenderer.GetPropertyBlock(propertyBlock);

        float resolvedPhase = randomizeStartFrame ? GetStablePhase() : phaseOffset;
        propertyBlock.SetFloat(PhaseOffsetId, resolvedPhase);
        propertyBlock.SetFloat(PlaybackSpeedId, playbackSpeed);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private float GetStablePhase()
    {
        unchecked
        {
            uint hash = (uint)GetInstanceID();
            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            hash *= 0x846ca68b;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / 16777216f;
        }
    }


public void Configure(bool randomStart, float phase, float speed)
    {
        randomizeStartFrame = randomStart;
        phaseOffset = Mathf.Repeat(phase, 1f);
        playbackSpeed = Mathf.Max(0f, speed);
        ApplyProperties();
    }
}
