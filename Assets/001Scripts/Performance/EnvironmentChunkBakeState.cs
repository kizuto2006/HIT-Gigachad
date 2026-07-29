using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the source renderers replaced by editor-baked environment chunks.
/// Source objects and colliders remain active; only their renderers are disabled.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnvironmentChunkBakeState : MonoBehaviour
{
    [SerializeField] private MeshRenderer[] sourceRenderers;
    [SerializeField] private Transform generatedRoot;

    private HashSet<MeshRenderer> sourceRendererSet;

    public MeshRenderer[] SourceRenderers => sourceRenderers;
    public Transform GeneratedRoot => generatedRoot;

    public void Configure(
        MeshRenderer[] renderers,
        Transform chunkRoot)
    {
        sourceRenderers = renderers;
        generatedRoot = chunkRoot;
        sourceRendererSet = null;
    }

    public bool IsSourceRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (sourceRendererSet == null)
        {
            sourceRendererSet = new HashSet<MeshRenderer>();
            if (sourceRenderers != null)
            {
                for (int i = 0; i < sourceRenderers.Length; i++)
                {
                    MeshRenderer sourceRenderer = sourceRenderers[i];
                    if (sourceRenderer != null)
                    {
                        sourceRendererSet.Add(sourceRenderer);
                    }
                }
            }
        }

        return sourceRendererSet.Contains(renderer);
    }
}
