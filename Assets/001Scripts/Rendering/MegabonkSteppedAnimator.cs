using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class MegabonkSteppedAnimator : MonoBehaviour
{
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;
    [SerializeField] private Animator targetAnimator;

    private float accumulatedTime;
    private float originalSpeed = 1f;

    public void Configure(float playbackFramesPerSecond)
    {
        framesPerSecond = Mathf.Max(1f, playbackFramesPerSecond);
    }

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    private void OnEnable()
    {
        if (targetAnimator == null)
        {
            return;
        }

        originalSpeed = targetAnimator.speed;
        targetAnimator.speed = 0f;
        accumulatedTime = 0f;
    }

    private void Update()
    {
        if (targetAnimator == null)
        {
            return;
        }

        accumulatedTime += Time.deltaTime;
        float interval = 1f / framesPerSecond;
        if (accumulatedTime < interval)
        {
            return;
        }

        float step = Mathf.Min(accumulatedTime, interval * 2f);
        accumulatedTime -= step;
        targetAnimator.speed = originalSpeed;
        targetAnimator.Update(step);
        targetAnimator.speed = 0f;
    }

    private void OnDisable()
    {
        if (targetAnimator != null)
        {
            targetAnimator.speed = originalSpeed;
        }
    }
}
