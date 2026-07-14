using UnityEngine;

/// <summary>
/// Optional movement ability that validates one-shot jump input for FirstPersonMovement. It owns jump permission and stamina spending while leaving jump physics in GroundMovement and ladder exit handling in the movement coordinator.
/// </summary>
public sealed class JumpAbility : MonoBehaviour
{
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private MissingStaminaPolicy missingStaminaPolicy = MissingStaminaPolicy.AllowForFree;

    private void Awake()
    {
        if (stamina == null)
            TryGetComponent(out stamina);
    }

    public bool TryUseJump(bool jumpRequested, bool canJump)
    {
        if (!jumpRequested || !canJump)
            return false;

        if (stamina == null)
            return missingStaminaPolicy == MissingStaminaPolicy.AllowForFree;

        return stamina.TrySpendJump();
    }
}
