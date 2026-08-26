using UnityEngine;

/// <summary>
/// Distance-based footstep trigger for the first-person player. It observes the CharacterController after movement and forwards each completed step to the entity's surface feedback player.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SurfaceFeedbackPlayer))]
public sealed class FirstPersonFootstepFeedbackPlayer : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float stepDistance = 1.8f;
    [SerializeField, Min(0f)] private float minimumHorizontalSpeed = 0.1f;
    [SerializeField, Range(0f, 1f)] private float firstStepDistanceRatio = 0.35f;

    private CharacterController characterController;
    private SurfaceFeedbackPlayer feedbackPlayer;
    private readonly DistanceStepFeedbackCycle stepCycle = new();

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        feedbackPlayer = GetComponent<SurfaceFeedbackPlayer>();
        ResetStepCycle();
    }

    private void LateUpdate()
    {
        if (characterController == null)
            return;

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up);
        bool shouldCountSteps = characterController.isGrounded
                                && horizontalVelocity.sqrMagnitude >= minimumHorizontalSpeed * minimumHorizontalSpeed;

        if (!shouldCountSteps)
        {
            ResetStepCycle();
            return;
        }

        if (!stepCycle.Tick(horizontalVelocity.magnitude * Time.deltaTime, stepDistance))
            return;

        feedbackPlayer.PlayFootstep();
    }

    private void OnDisable()
    {
        ResetStepCycle();
    }

    private void ResetStepCycle()
    {
        stepCycle.Reset(stepDistance, firstStepDistanceRatio);
    }

}
