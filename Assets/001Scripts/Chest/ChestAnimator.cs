using UnityEngine;

public sealed class ChestAnimator : MonoBehaviour
{
    private static readonly int OpenTriggerHash = Animator.StringToHash("Open");

    [SerializeField] private Animator animator;

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (IsOpen || animator == null)
        {
            return;
        }

        IsOpen = true;
        animator.SetTrigger(OpenTriggerHash);
    }
}
