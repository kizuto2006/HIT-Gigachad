using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class EnemyHybridLODAnimator : MonoBehaviour
{
    [SerializeField] private AnimationClip animationClip;
    [SerializeField, Min(0f)] private float playbackSpeed = 1f;
    [SerializeField] private bool synchronizeWithGlobalVAT = true;
    [SerializeField] private bool randomizeStartTime;

    private Animator targetAnimator;
    private PlayableGraph playableGraph;
    private AnimationClipPlayable clipPlayable;

    public void Configure(AnimationClip clip, float speed = 1f)
    {
        animationClip = clip;
        playbackSpeed = Mathf.Max(0f, speed);
    }

    private void OnEnable()
    {
        CreateGraph();
    }

    private void OnDisable()
    {
        DestroyGraph();
    }

    private void OnDestroy()
    {
        DestroyGraph();
    }

    private void CreateGraph()
    {
        DestroyGraph();
        if (animationClip == null)
        {
            return;
        }

        targetAnimator = GetComponentInChildren<Animator>(true);
        if (targetAnimator == null)
        {
            return;
        }

        targetAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
        playableGraph = PlayableGraph.Create(name + "_HybridLODAnimation");
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        clipPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetSpeed(playbackSpeed);

        if (animationClip.length > 0f)
        {
            double initialTime = synchronizeWithGlobalVAT
                ? (Time.timeAsDouble * playbackSpeed) % animationClip.length
                : randomizeStartTime
                    ? GetStablePhase() * animationClip.length
                    : 0d;
            clipPlayable.SetTime(initialTime);
        }

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            playableGraph,
            "Hybrid LOD Animation",
            targetAnimator);
        output.SetSourcePlayable(clipPlayable);
        playableGraph.Play();
    }

    private void DestroyGraph()
    {
        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
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
}
