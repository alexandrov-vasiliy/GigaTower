using UnityEngine;

/// <summary>
/// Top-level first-person locomotion coordinator and player IKnockbackReceiver implementation. Each frame it consumes PlayerMovementInput, queries optional SprintAbility, JumpAbility, and LadderClimbing modules, then applies the selected movement path through the required CharacterController.
/// Jump input is consumed once per frame; optional ladder exit velocity is forwarded to GroundMovement, and disabling the component forcibly ends active climbing.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(GroundMovement))]
public sealed class FirstPersonMovement : MonoBehaviour
{
    [SerializeField] private PlayerMovementInput movementInput;
    [SerializeField] private SprintAbility sprintAbility;
    [SerializeField] private JumpAbility jumpAbility;
    [SerializeField] private LadderClimbing ladderClimbing;

    private CharacterController characterController;
    private GroundMovement groundMovement;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        groundMovement = GetComponent<GroundMovement>();

        if (movementInput == null)
            TryGetComponent(out movementInput);

        if (sprintAbility == null)
            TryGetComponent(out sprintAbility);

        if (jumpAbility == null)
            TryGetComponent(out jumpAbility);

        if (ladderClimbing == null)
            TryGetComponent(out ladderClimbing);
    }

    private void Update()
    {
        if (movementInput == null)
            return;

        float deltaTime = Time.deltaTime;
        Vector2 moveInput = movementInput.MoveInput;
        bool jumpRequested = movementInput.ConsumeJumpRequest();

        if (ladderClimbing != null && ladderClimbing.IsClimbing)
        {
            if (CanUseJump(jumpRequested, true))
            {
                Vector3 exitVelocity = ladderClimbing.StopClimbing(true);
                groundMovement.SetExternalVelocity(exitVelocity);
                jumpRequested = false;
            }
            else
            {
                float ladderSpeedMultiplier = GetLadderSpeedMultiplier(moveInput, deltaTime);
                Vector3 ladderVelocity = ladderClimbing.CalculateVelocity(
                    moveInput,
                    ladderSpeedMultiplier);
                CollisionFlags collisionFlags = characterController.Move(ladderVelocity * deltaTime);

                if (ladderClimbing.ShouldStopClimbing(transform.position, collisionFlags))
                    groundMovement.ResetVerticalVelocity();

                return;
            }
        }
        else if (ladderClimbing != null && ladderClimbing.TryStartClimbing(
                     transform.position,
                     moveInput,
                     groundMovement.GetMoveDirection(moveInput)))
        {
            groundMovement.ResetVerticalVelocity();
            return;
        }

        bool groundJumpRequested = CanUseJump(jumpRequested, characterController.isGrounded);
        float groundSpeedMultiplier = GetGroundSpeedMultiplier(moveInput, deltaTime);
        Vector3 groundVelocity = groundMovement.CalculateVelocity(
            moveInput,
            groundJumpRequested,
            groundSpeedMultiplier,
            characterController.isGrounded,
            deltaTime);

        characterController.Move(groundVelocity * deltaTime);
    }

    private void OnDisable()
    {
        if (ladderClimbing != null)
            ladderClimbing.StopClimbing(false);
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (groundMovement == null)
            return;

        if (ladderClimbing != null && ladderClimbing.IsClimbing)
            ladderClimbing.StopClimbing(false);

        groundMovement.ApplyExternalDisplacement(direction, distance, duration);
    }

    private bool CanUseJump(bool jumpRequested, bool isGrounded)
    {
        return jumpAbility != null && jumpAbility.TryUseJump(jumpRequested, isGrounded);
    }

    private float GetGroundSpeedMultiplier(Vector2 moveInput, float deltaTime)
    {
        return sprintAbility != null
            ? sprintAbility.GetGroundSpeedMultiplier(moveInput, movementInput.SprintRequested, deltaTime)
            : 1f;
    }

    private float GetLadderSpeedMultiplier(Vector2 moveInput, float deltaTime)
    {
        return sprintAbility != null
            ? sprintAbility.GetVerticalSpeedMultiplier(moveInput, movementInput.SprintRequested, deltaTime)
            : 1f;
    }
}
