using UnityEngine;

/// <summary>
/// Landing trigger for the first-person player. It tracks airborne time, downward speed, and drop height, then forwards qualifying physical contacts to the entity's surface feedback player.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SurfaceFeedbackPlayer))]
public sealed class FirstPersonLandingFeedbackPlayer : MonoBehaviour
{
    [SerializeField, Min(0f)] private float minimumAirTime = 0.15f;
    [SerializeField, Min(0f)] private float minimumDownwardSpeed = 2f;
    [SerializeField, Min(0f)] private float minimumFallDistance = 0.25f;

    private CharacterController characterController;
    private SurfaceFeedbackPlayer feedbackPlayer;
    private bool wasGrounded;
    private float airborneTime;
    private float highestAirborneY;
    private float fastestDownwardSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        feedbackPlayer = GetComponent<SurfaceFeedbackPlayer>();
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
            feedbackPlayer.PlayLanding(fastestDownwardSpeed);

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
        if (airborneTime < minimumAirTime)
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

}
