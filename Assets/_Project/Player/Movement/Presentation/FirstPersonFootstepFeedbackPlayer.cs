using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Distance-based footstep feedback trigger for the first-person player. It observes the required CharacterController after FirstPersonMovement has moved it, uses DistanceStepFeedbackCycle to accumulate grounded horizontal travel, and calls the configured MMF_Player.PlayFeedbacks when enough distance has been covered; the MMF_Player is a scene/reference dependency so audio setup stays in FEEL.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonFootstepFeedbackPlayer : MonoBehaviour
{
    [SerializeField] private MMF_Player footstepFeedback;
    [SerializeField, Min(0.01f)] private float stepDistance = 1.8f;
    [SerializeField, Min(0f)] private float minimumHorizontalSpeed = 0.1f;
    [SerializeField, Range(0f, 1f)] private float firstStepDistanceRatio = 0.35f;
    [SerializeField] private bool playAtPlayerPosition = true;

    private CharacterController characterController;
    private readonly DistanceStepFeedbackCycle stepCycle = new();

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ResetStepCycle();
    }

    private void LateUpdate()
    {
        if (characterController == null)
            return;

        if (footstepFeedback == null)
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

        PlayFootstep();
    }

    private void OnDisable()
    {
        ResetStepCycle();
    }

    private void ResetStepCycle()
    {
        stepCycle.Reset(stepDistance, firstStepDistanceRatio);
    }

    private void PlayFootstep()
    {
        if (playAtPlayerPosition)
            footstepFeedback.PlayFeedbacks(transform.position);
        else
            footstepFeedback.PlayFeedbacks();
    }
}
