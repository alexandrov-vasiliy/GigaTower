using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Offsets the first-person hands root in LateUpdate using CharacterController velocity. It blends idle hand sway into movement bobbing, and other gameplay systems can adjust its public state multiplier and velocity mode without coupling this component to climbing, attacking, or other optional abilities.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class HandsBobbing : MonoBehaviour
{
    private const float TwoPi = Mathf.PI * 2f;

    [Header("Target")]
    [SerializeField] private Transform handsRoot;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float horizontalAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float verticalAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float frequency = 8f;
    [SerializeField, Min(0.01f)] private float referenceSpeed = 5f;
    [SerializeField, FormerlySerializedAs("minimumSpeed"), Min(0.01f)] private float movementBlendSpeed = 0.25f;
    [SerializeField, Min(0.01f)] private float smoothing = 12f;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleHorizontalAmplitude = 0.008f;
    [SerializeField, Min(0f)] private float idleVerticalAmplitude = 0.004f;
    [SerializeField, Min(0f)] private float idleFrequency = 1.25f;
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 1f;

    [Header("Sprint")]
    [SerializeField, Min(1f)] private float sprintSpeedRatio = 1.5f;
    [SerializeField, Min(1f)] private float sprintIntensityMultiplier = 1.35f;

    private CharacterController characterController;
    private Vector3 initialLocalPosition;
    private Vector3 currentOffset;
    private float phase;

    public float StateIntensityMultiplier { get; set; } = 1f;
    public bool UseVerticalVelocity { get; set; }
    public bool IgnoreGroundedCheck { get; set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (handsRoot == null)
        {
            Debug.LogWarning("HandsBobbing requires a hands root Transform.", this);
            enabled = false;
            return;
        }

        initialLocalPosition = handsRoot.localPosition;
    }

    private void LateUpdate()
    {
        float stateMultiplier = Mathf.Max(0f, StateIntensityMultiplier);
        float movementSpeed = GetMovementSpeed();
        bool canBob = stateMultiplier > 0f
                      && (IgnoreGroundedCheck || characterController.isGrounded);

        Vector3 targetOffset = Vector3.zero;
        if (canBob)
        {
            float speedRatio = movementSpeed / referenceSpeed;
            float movementBlend = Mathf.InverseLerp(0f, movementBlendSpeed, movementSpeed);
            float bobFrequency = Mathf.Lerp(idleFrequency, frequency * Mathf.Max(speedRatio, 0.01f), movementBlend);
            phase = Mathf.Repeat(phase + bobFrequency * Time.deltaTime, TwoPi);

            float sprintBlend = Mathf.InverseLerp(1f, sprintSpeedRatio, speedRatio);
            float sprintMultiplier = Mathf.Lerp(1f, sprintIntensityMultiplier, sprintBlend);
            float movementIntensity = Mathf.Clamp01(speedRatio) * sprintMultiplier;
            float intensity = Mathf.Lerp(idleIntensity, movementIntensity, movementBlend) * stateMultiplier;
            float horizontal = Mathf.Lerp(idleHorizontalAmplitude, horizontalAmplitude, movementBlend);
            float vertical = Mathf.Lerp(idleVerticalAmplitude, verticalAmplitude, movementBlend);

            targetOffset = new Vector3(
                Mathf.Sin(phase) * horizontal,
                -Mathf.Cos(phase * 2f) * vertical,
                0f) * intensity;
        }

        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, blend);
        handsRoot.localPosition = initialLocalPosition + currentOffset;
    }

    private float GetMovementSpeed()
    {
        if (UseVerticalVelocity)
            return Mathf.Abs(characterController.velocity.y);

        return Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude;
    }

    private void OnDisable()
    {
        if (handsRoot != null)
            handsRoot.localPosition = initialLocalPosition;

        currentOffset = Vector3.zero;
        phase = 0f;
    }
}
