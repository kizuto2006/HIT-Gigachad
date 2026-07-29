using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies centralized distance LOD to the repeated desert props. Renderers are
/// grouped into spatial chunks so only a small portion is evaluated each frame.
/// Colliders and gameplay objects are never disabled.
/// </summary>
public sealed class EnvironmentRenderOptimizer : MonoBehaviour
{
    private const float ChunkSize = 24f;
    private const int ChunksPerFrame = 8;

    private static EnvironmentRenderOptimizer instance;

    private readonly List<RenderChunk> chunks = new List<RenderChunk>(128);
    private Camera targetCamera;
    private int nextChunkIndex;
    private Coroutine rebuildRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject optimizerObject = new GameObject("Environment Render Optimizer");
        DontDestroyOnLoad(optimizerObject);
        instance = optimizerObject.AddComponent<EnvironmentRenderOptimizer>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QueueRebuild();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
            rebuildRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (chunks.Count == 0)
        {
            return;
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        int count = Mathf.Min(ChunksPerFrame, chunks.Count);
        for (int i = 0; i < count; i++)
        {
            if (nextChunkIndex >= chunks.Count)
            {
                nextChunkIndex = 0;
            }

            chunks[nextChunkIndex].UpdateRenderers(cameraPosition);
            nextChunkIndex++;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueRebuild();
    }

    private void QueueRebuild()
    {
        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
        }

        rebuildRoutine = StartCoroutine(RebuildAfterSceneLoad());
    }

    private IEnumerator RebuildAfterSceneLoad()
    {
        yield return null;
        RebuildChunks();
        rebuildRoutine = null;
    }

    private void RebuildChunks()
    {
        chunks.Clear();
        nextChunkIndex = 0;
        targetCamera = Camera.main;

        Dictionary<ChunkKey, RenderChunk> chunkByKey =
            new Dictionary<ChunkKey, RenderChunk>(128);
        EnvironmentChunkBakeState[] bakeStates =
            FindObjectsByType<EnvironmentChunkBakeState>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null ||
                IsBakedSourceRenderer(renderer, bakeStates) ||
                !TryGetDistances(
                    renderer.gameObject.name,
                    out PropCategory category,
                    out float shadowDistance,
                    out float cullDistance))
            {
                continue;
            }

            Vector3 position = renderer.transform.position;
            ChunkKey key = new ChunkKey(
                category,
                Mathf.FloorToInt(position.x / ChunkSize),
                Mathf.FloorToInt(position.z / ChunkSize));

            if (!chunkByKey.TryGetValue(key, out RenderChunk chunk))
            {
                Vector3 center = new Vector3(
                    (key.x + 0.5f) * ChunkSize,
                    position.y,
                    (key.z + 0.5f) * ChunkSize);
                chunk = new RenderChunk(center);
                chunkByKey.Add(key, chunk);
                chunks.Add(chunk);
            }

            chunk.Add(new RenderEntry(
                renderer,
                shadowDistance,
                cullDistance));
        }

        if (targetCamera != null)
        {
            Vector3 cameraPosition = targetCamera.transform.position;
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].UpdateRenderers(cameraPosition);
            }
        }
    }

    private static bool IsBakedSourceRenderer(
        MeshRenderer renderer,
        EnvironmentChunkBakeState[] bakeStates)
    {
        for (int i = 0; i < bakeStates.Length; i++)
        {
            EnvironmentChunkBakeState bakeState = bakeStates[i];
            if (bakeState != null &&
                bakeState.IsSourceRenderer(renderer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDistances(
        string objectName,
        out PropCategory category,
        out float shadowDistance,
        out float cullDistance)
    {
        if (objectName.StartsWith("Rock_"))
        {
            category = PropCategory.Rock;
            shadowDistance = 22f;
            cullDistance = 75f;
            return true;
        }

        if (objectName.StartsWith("Cactus_"))
        {
            category = PropCategory.Cactus;
            shadowDistance = 30f;
            cullDistance = 90f;
            return true;
        }

        if (objectName.StartsWith("Tree_"))
        {
            category = PropCategory.Tree;
            shadowDistance = 35f;
            cullDistance = 110f;
            return true;
        }

        if (objectName.StartsWith("Cliff"))
        {
            category = PropCategory.Cliff;
            shadowDistance = 55f;
            cullDistance = 160f;
            return true;
        }

        if (objectName.StartsWith("Decor_"))
        {
            category = PropCategory.Decor;
            shadowDistance = 35f;
            cullDistance = 120f;
            return true;
        }

        category = default;
        shadowDistance = 0f;
        cullDistance = 0f;
        return false;
    }

    private enum PropCategory : byte
    {
        Rock,
        Cactus,
        Tree,
        Cliff,
        Decor
    }

    private readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public readonly PropCategory category;
        public readonly int x;
        public readonly int z;

        public ChunkKey(PropCategory category, int x, int z)
        {
            this.category = category;
            this.x = x;
            this.z = z;
        }

        public bool Equals(ChunkKey other)
        {
            return category == other.category &&
                x == other.x &&
                z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)category;
                hash = (hash * 397) ^ x;
                hash = (hash * 397) ^ z;
                return hash;
            }
        }
    }

    private sealed class RenderChunk
    {
        private const float ChunkRadius = ChunkSize * 0.72f;

        private readonly Vector3 center;
        private readonly List<RenderEntry> entries = new List<RenderEntry>(16);
        private float maximumCullDistance;

        public RenderChunk(Vector3 center)
        {
            this.center = center;
        }

        public void Add(RenderEntry entry)
        {
            entries.Add(entry);
            maximumCullDistance = Mathf.Max(
                maximumCullDistance,
                entry.CullDistance);
        }

        public void UpdateRenderers(Vector3 cameraPosition)
        {
            Vector3 chunkDifference = center - cameraPosition;
            chunkDifference.y = 0f;
            float coarseCullDistance = maximumCullDistance + ChunkRadius;
            bool chunkIsVisible =
                chunkDifference.sqrMagnitude <=
                coarseCullDistance * coarseCullDistance;

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Update(cameraPosition, chunkIsVisible);
            }
        }
    }

    private sealed class RenderEntry
    {
        private readonly MeshRenderer renderer;
        private readonly ShadowCastingMode originalShadowMode;
        private readonly float shadowDistanceSqr;
        private readonly float cullDistanceSqr;
        private bool isVisible;
        private bool castsShadows;

        public float CullDistance { get; }

        public RenderEntry(
            MeshRenderer renderer,
            float shadowDistance,
            float cullDistance)
        {
            this.renderer = renderer;
            originalShadowMode = renderer.shadowCastingMode;
            CullDistance = cullDistance;
            shadowDistanceSqr = shadowDistance * shadowDistance;
            cullDistanceSqr = cullDistance * cullDistance;
            isVisible = renderer.enabled;
            castsShadows =
                originalShadowMode != ShadowCastingMode.Off;
        }

        public void Update(Vector3 cameraPosition, bool chunkIsVisible)
        {
            if (renderer == null)
            {
                return;
            }

            Vector3 difference = renderer.transform.position - cameraPosition;
            float sqrDistance = difference.sqrMagnitude;
            bool shouldBeVisible =
                chunkIsVisible && sqrDistance <= cullDistanceSqr;

            if (isVisible != shouldBeVisible)
            {
                isVisible = shouldBeVisible;
                renderer.enabled = shouldBeVisible;
            }

            if (!shouldBeVisible)
            {
                return;
            }

            bool shouldCastShadows =
                originalShadowMode != ShadowCastingMode.Off &&
                sqrDistance <= shadowDistanceSqr;
            if (castsShadows == shouldCastShadows)
            {
                return;
            }

            castsShadows = shouldCastShadows;
            renderer.shadowCastingMode = shouldCastShadows
                ? originalShadowMode
                : ShadowCastingMode.Off;
        }
    }
}
