using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Landing feedback trigger for the first-person player. It observes the required CharacterController after FirstPersonMovement has applied movement, tracks airborne time, downward speed, and drop height, then calls the configured MMF_Player.PlayFeedbacks when the player lands from a jump or fall that passes the configured thresholds.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonLandingFeedbackPlayer : MonoBehaviour
{
    [SerializeField] private MMF_Player landingFeedback;
    [SerializeField, Min(0f)] private float minimumAirTime = 0.15f;
    [SerializeField, Min(0f)] private float minimumDownwardSpeed = 2f;
    [SerializeField, Min(0f)] private float minimumFallDistance = 0.25f;
    [SerializeField] private bool playAtPlayerPosition = true;

    private CharacterController characterController;
    private bool wasGrounded;
    private float airborneTime;
    private float highestAirborneY;
    private float fastestDownwardSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        wasGrounded = characterController != null && characterController.isGrounded;
        ResetAirborneState();
    }

    private void LateUpdate()
    {
        if (characterController == null)
            return;

        bool isGrounded = characterController.isGrounded;

        if (!isGrounded)
        {
            TrackAirborneState();
            wasGrounded = false;
            return;
        }

        if (!wasGrounded && ShouldPlayLandingFeedback())
            PlayLanding();

        ResetAirborneState();
        wasGrounded = true;
    }

    private void OnDisable()
    {
        ResetAirborneState();
        wasGrounded = false;
    }

    private void TrackAirborneState()
    {
        if (wasGrounded)
        {
            airborneTime = 0f;
            highestAirborneY = transform.position.y;
            fastestDownwardSpeed = 0f;
        }

        airborneTime += Time.deltaTime;
        highestAirborneY = Mathf.Max(highestAirborneY, transform.position.y);
        fastestDownwardSpeed = Mathf.Max(fastestDownwardSpeed, -characterController.velocity.y);
    }

    private bool ShouldPlayLandingFeedback()
    {
        if (landingFeedback == null || airborneTime < minimumAirTime)
            return false;

        float fallDistance = Mathf.Max(0f, highestAirborneY - transform.position.y);
        return fastestDownwardSpeed >= minimumDownwardSpeed
               || fallDistance >= minimumFallDistance;
    }

    private void ResetAirborneState()
    {
        airborneTime = 0f;
        highestAirborneY = transform.position.y;
        fastestDownwardSpeed = 0f;
    }

    private void PlayLanding()
    {
        if (playAtPlayerPosition)
            landingFeedback.PlayFeedbacks(transform.position);
        else
            landingFeedback.PlayFeedbacks();
    }
}
