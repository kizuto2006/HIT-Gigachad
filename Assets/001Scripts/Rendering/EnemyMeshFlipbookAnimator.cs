using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class EnemyMeshFlipbookAnimator : MonoBehaviour
{
    [SerializeField] private Mesh[] frames;
    [SerializeField, Min(0.1f)] private float framesPerSecond = 8f;
    [SerializeField, Min(1)] private int phaseBuckets = 4;

    private MeshFilter cachedMeshFilter;
    private int currentFrame = -1;
    private int phaseBucket;

    public int FrameCount => frames != null ? frames.Length : 0;
    public float FramesPerSecond => framesPerSecond;

    public void Configure(Mesh[] bakedFrames, float playbackFramesPerSecond, int bucketCount)
    {
        frames = bakedFrames;
        framesPerSecond = Mathf.Max(0.1f, playbackFramesPerSecond);
        phaseBuckets = Mathf.Max(1, bucketCount);
        ResolvePhaseBucket();
        ApplyFrame(0);
    }

    private void Awake()
    {
        CacheComponents();
        ResolvePhaseBucket();
    }

    private void OnEnable()
    {
        CacheComponents();
        ResolvePhaseBucket();
        EnemyMeshFlipbookManager.Register(this);
    }

    private void OnDisable()
    {
        EnemyMeshFlipbookManager.Unregister(this);
    }

    private void OnValidate()
    {
        framesPerSecond = Mathf.Max(0.1f, framesPerSecond);
        phaseBuckets = Mathf.Max(1, phaseBuckets);
        CacheComponents();
        if (!Application.isPlaying)
        {
            ApplyFrame(0);
        }
    }

    internal void Tick(float globalTime)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        float phaseFrames = phaseBucket * (frames.Length / (float)phaseBuckets);
        int frameIndex = Mathf.FloorToInt(globalTime * framesPerSecond + phaseFrames);
        frameIndex %= frames.Length;
        if (frameIndex < 0)
        {
            frameIndex += frames.Length;
        }

        ApplyFrame(frameIndex);
    }

    private void ApplyFrame(int frameIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        CacheComponents();
        if (cachedMeshFilter == null)
        {
            return;
        }

        int safeFrame = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        if (currentFrame == safeFrame && cachedMeshFilter.sharedMesh == frames[safeFrame])
        {
            return;
        }

        cachedMeshFilter.sharedMesh = frames[safeFrame];
        currentFrame = safeFrame;
    }

    private void CacheComponents()
    {
        if (cachedMeshFilter == null)
        {
            cachedMeshFilter = GetComponent<MeshFilter>();
        }
    }

    private void ResolvePhaseBucket()
    {
        unchecked
        {
            uint hash = (uint)GetInstanceID();
            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            phaseBucket = (int)(hash % (uint)Mathf.Max(1, phaseBuckets));
        }
    }
}
