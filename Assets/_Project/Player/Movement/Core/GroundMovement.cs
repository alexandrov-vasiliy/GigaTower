using UnityEngine;

/// <summary>
/// Stateful ground-motion calculator used by FirstPersonMovement. CalculateVelocity combines orientation-relative input, an externally supplied speed multiplier, gravity, jumping, and decaying external velocity; ResetVerticalVelocity, SetExternalVelocity, and ApplyExternalDisplacement support transitions from movement abilities while keeping CharacterController ownership in FirstPersonMovement.
/// </summary>
public sealed class GroundMovement : MonoBehaviour
{
    [SerializeField] private Transform orientation;
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    private float verticalVelocity;
    private Vector3 externalVelocity;
    private float externalVelocityDecaySpeed;

    private void Awake()
    {
        if (orientation == null)
            orientation = transform;
    }

    public Vector3 CalculateVelocity(
        Vector2 moveInput,
        bool jumpRequested,
        float speedMultiplier,
        bool isGrounded,
        float deltaTime)
    {
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * deltaTime;

        if (jumpRequested && isGrounded)
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));

        float currentMoveSpeed = moveSpeed * Mathf.Max(0f, speedMultiplier);

        Vector3 velocity = GetMoveDirection(moveInput) * currentMoveSpeed;
        velocity += Vector3.up * verticalVelocity;
        velocity += externalVelocity;

        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, GetExternalVelocityDecaySpeed() * deltaTime);
        return velocity;
    }

    public Vector3 GetMoveDirection(Vector2 moveInput)
    {
        Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(orientation.right, Vector3.up).normalized;
        return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
    }

    public void ResetVerticalVelocity()
    {
        verticalVelocity = 0f;
    }

    public void SetExternalVelocity(Vector3 velocity)
    {
        verticalVelocity = velocity.y;
        externalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        externalVelocityDecaySpeed = moveSpeed;
    }

    public void ApplyExternalDisplacement(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0f || distance <= 0f)
            return;

        float safeDuration = Mathf.Max(0.01f, duration);
        float initialSpeed = 2f * distance / safeDuration;
        externalVelocity = direction.normalized * initialSpeed;
        externalVelocityDecaySpeed = initialSpeed / safeDuration;
    }

    private float GetExternalVelocityDecaySpeed()
    {
        return externalVelocityDecaySpeed > 0f ? externalVelocityDecaySpeed : moveSpeed;
    }
}
