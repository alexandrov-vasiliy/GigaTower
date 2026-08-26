using UnityEngine;

/// <summary>Forwards successful first-person ground jumps to the entity surface feedback player.</summary>
[RequireComponent(typeof(FirstPersonMovement))]
[RequireComponent(typeof(SurfaceFeedbackPlayer))]
public sealed class FirstPersonJumpFeedbackPlayer : MonoBehaviour
{
    private FirstPersonMovement movement;
    private SurfaceFeedbackPlayer feedbackPlayer;

    private void Awake()
    {
        movement = GetComponent<FirstPersonMovement>();
        feedbackPlayer = GetComponent<SurfaceFeedbackPlayer>();
    }

    private void OnEnable() => movement.GroundJumped += feedbackPlayer.PlayJumpTakeoff;
    private void OnDisable() => movement.GroundJumped -= feedbackPlayer.PlayJumpTakeoff;
}
