using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineInputAxisController))]
public sealed class AdaptiveCameraLookFilter : MonoBehaviour
{
    [Header("Adaptive Look Filtering")]
    [SerializeField] private bool filterInput = true;
    [SerializeField, Min(0f)] private float mouseNoiseThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float gamepadDeadzone = 0.12f;
    [SerializeField, Min(0f)] private float smallInputSmoothing = 0.06f;
    [SerializeField, Min(0f)] private float mouseLargeInputThreshold = 4f;
    [SerializeField, Range(0f, 1f)] private float gamepadLargeInputThreshold = 0.35f;
    [SerializeField, Min(0f)] private float largeInputSmoothing = 0.012f;

    [Header("Camera Shake")]
    [SerializeField] private bool allowCameraShake = false;

    private CinemachineInputAxisController inputAxisController;
    private CinemachineBasicMultiChannelPerlin cameraNoise;
    private InputAction cachedAction;
    private Vector2 filteredInput;
    private int lastProcessedFrame = -1;

    public bool AllowCameraShake => allowCameraShake;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (inputAxisController != null)
        {
            inputAxisController.ReadControlValueOverride = ReadFilteredInput;
        }

        ResetFilter();
        ApplyShakePolicy();
    }

    private void OnDisable()
    {
        if (inputAxisController != null)
        {
            inputAxisController.ReadControlValueOverride = null;
        }

        ResetFilter();
    }

    private void OnValidate()
    {
        mouseNoiseThreshold = Mathf.Max(0f, mouseNoiseThreshold);
        gamepadDeadzone = Mathf.Clamp01(gamepadDeadzone);
        smallInputSmoothing = Mathf.Max(0f, smallInputSmoothing);
        mouseLargeInputThreshold = Mathf.Max(mouseNoiseThreshold, mouseLargeInputThreshold);
        gamepadLargeInputThreshold = Mathf.Max(gamepadDeadzone, gamepadLargeInputThreshold);
        largeInputSmoothing = Mathf.Max(0f, largeInputSmoothing);
    }

    public void SetCameraShakeEnabled(bool enabled)
    {
        allowCameraShake = enabled;
        ApplyShakePolicy();
    }

    public void ResetFilter()
    {
        cachedAction = null;
        filteredInput = Vector2.zero;
        lastProcessedFrame = -1;
    }

    private void CacheReferences()
    {
        if (inputAxisController == null)
        {
            inputAxisController = GetComponent<CinemachineInputAxisController>();
        }

        if (cameraNoise == null)
        {
            cameraNoise = GetComponent<CinemachineBasicMultiChannelPerlin>();
        }
    }

    private float ReadFilteredInput(
        InputAction action,
        IInputAxisOwner.AxisDescriptor.Hints hint,
        Object context,
        CinemachineInputAxisController.Reader.ControlValueReader defaultReader)
    {
        if (!filterInput || action == null || action.activeValueType != typeof(Vector2))
        {
            return ReadDefaultInput(action, hint, context, defaultReader);
        }

        if (cachedAction != action || lastProcessedFrame != Time.frameCount)
        {
            cachedAction = action;
            lastProcessedFrame = Time.frameCount;
            filteredInput = ProcessInput(action);
        }

        return hint == IInputAxisOwner.AxisDescriptor.Hints.Y
            ? filteredInput.y
            : filteredInput.x;
    }

    private float ReadDefaultInput(
        InputAction action,
        IInputAxisOwner.AxisDescriptor.Hints hint,
        Object context,
        CinemachineInputAxisController.Reader.ControlValueReader defaultReader)
    {
        return defaultReader != null
            ? defaultReader(action, hint, context, defaultReader)
            : 0f;
    }

    private Vector2 ProcessInput(InputAction action)
    {
        Vector2 rawInput = action.ReadValue<Vector2>();
        bool isGamepadInput = action.activeControl != null
            && action.activeControl.device is Gamepad;

        if (isGamepadInput)
        {
            rawInput = ApplyGamepadDeadzone(rawInput);
        }
        else if (rawInput.magnitude <= mouseNoiseThreshold)
        {
            rawInput = Vector2.zero;
        }

        float inputMagnitude = rawInput.magnitude;
        float largeInputThreshold = isGamepadInput
            ? gamepadLargeInputThreshold
            : mouseLargeInputThreshold;

        if (inputMagnitude >= largeInputThreshold)
        {
            return rawInput;
        }

        float smoothingTime = smallInputSmoothing;
        if (smoothingTime <= 0f)
        {
            return rawInput;
        }

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
        float blend = 1f - Mathf.Exp(-deltaTime / smoothingTime);
        return Vector2.Lerp(filteredInput, rawInput, blend);
    }

    private Vector2 ApplyGamepadDeadzone(Vector2 rawInput)
    {
        float magnitude = rawInput.magnitude;
        if (magnitude <= gamepadDeadzone)
        {
            return Vector2.zero;
        }

        float remappedMagnitude = Mathf.InverseLerp(gamepadDeadzone, 1f, Mathf.Min(1f, magnitude));
        return rawInput.normalized * remappedMagnitude;
    }

    private void ApplyShakePolicy()
    {
        if (cameraNoise != null)
        {
            cameraNoise.enabled = allowCameraShake;
        }
    }
}
