using UnityEngine;

/// <summary>
/// Optional movement ability that turns sprint input into a speed multiplier for FirstPersonMovement. It can spend PlayerStamina when available, or follow its missing-stamina policy so the same controller prefab can support free sprinting or no sprinting in projects without stamina.
/// </summary>
public sealed class SprintAbility : MonoBehaviour
{
    [SerializeField, Min(1f)] private float speedMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float inputThreshold = 0.01f;
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private MissingStaminaPolicy missingStaminaPolicy = MissingStaminaPolicy.AllowForFree;

    private void Awake()
    {
        if (stamina == null)
            TryGetComponent(out stamina);
    }

    public float GetGroundSpeedMultiplier(Vector2 moveInput, bool sprintRequested, float deltaTime)
    {
        return CanSprint(sprintRequested, moveInput.sqrMagnitude > inputThreshold * inputThreshold, deltaTime)
            ? speedMultiplier
            : 1f;
    }

    public float GetVerticalSpeedMultiplier(Vector2 moveInput, bool sprintRequested, float deltaTime)
    {
        return CanSprint(sprintRequested, Mathf.Abs(moveInput.y) > inputThreshold, deltaTime)
            ? speedMultiplier
            : 1f;
    }

    private bool CanSprint(bool sprintRequested, bool hasMovementInput, float deltaTime)
    {
        if (!sprintRequested || !hasMovementInput)
            return false;

        if (stamina == null)
            return missingStaminaPolicy == MissingStaminaPolicy.AllowForFree;

        return stamina.TryDrainSprint(deltaTime);
    }
}
