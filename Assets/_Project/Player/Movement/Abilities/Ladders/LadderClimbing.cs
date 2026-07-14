using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional ladder movement ability that tracks overlapping Ladder triggers and owns the active climbing state. FirstPersonMovement calls TryStartClimbing, CalculateVelocity, ShouldStopClimbing, and StopClimbing to enter, drive, validate, and exit ladder movement, including jump-off velocity.
/// </summary>
public sealed class LadderClimbing : MonoBehaviour
{
    [SerializeField, Min(0f)] private float climbSpeed = 3f;
    [SerializeField, Range(-1f, 1f)] private float approachDotThreshold = 0.35f;
    [SerializeField, Min(0f)] private float minimumInput = 0.1f;
    [SerializeField, Min(0f)] private float jumpOffSpeed = 3f;
    [SerializeField, Min(0f)] private float jumpOffUpSpeed = 2f;

    private readonly List<Ladder> availableLadders = new();
    private Ladder activeLadder;

    public bool IsClimbing => activeLadder != null;

    public bool TryStartClimbing(
        Vector3 playerPosition,
        Vector2 moveInput,
        Vector3 moveDirection)
    {
        if (IsClimbing || moveInput.y <= minimumInput)
            return false;

        RemoveUnavailableLadders();

        Ladder bestLadder = null;
        float bestApproach = approachDotThreshold;

        foreach (Ladder ladder in availableLadders)
        {
            if (!ladder.ContainsHeight(playerPosition))
                continue;

            float approach = Vector3.Dot(moveDirection, -ladder.Normal);
            if (approach <= bestApproach)
                continue;

            bestApproach = approach;
            bestLadder = ladder;
        }

        activeLadder = bestLadder;
        return IsClimbing;
    }

    public Vector3 CalculateVelocity(Vector2 moveInput, float speedMultiplier)
    {
        float currentClimbSpeed = climbSpeed * Mathf.Max(0f, speedMultiplier);

        return IsClimbing
            ? activeLadder.Up * (moveInput.y * currentClimbSpeed)
            : Vector3.zero;
    }

    public bool ShouldStopClimbing(Vector3 playerPosition, CollisionFlags collisionFlags)
    {
        if (!IsClimbing)
            return true;

        bool reachedVerticalObstacle = (collisionFlags & CollisionFlags.Above) != 0;
        if (activeLadder.ContainsHeight(playerPosition) && !reachedVerticalObstacle)
            return false;

        StopClimbing(false);
        return true;
    }

    public Vector3 StopClimbing(bool jumpOff)
    {
        if (!IsClimbing)
            return Vector3.zero;

        Vector3 exitVelocity = jumpOff
            ? activeLadder.Normal * jumpOffSpeed + Vector3.up * jumpOffUpSpeed
            : Vector3.zero;

        activeLadder = null;
        return exitVelocity;
    }

    private void OnTriggerEnter(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();
        if (ladder != null && !availableLadders.Contains(ladder))
            availableLadders.Add(ladder);
    }

    private void OnTriggerExit(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();
        if (ladder == null)
            return;

        availableLadders.Remove(ladder);

        if (activeLadder == ladder)
            activeLadder = null;
    }

    private void RemoveUnavailableLadders()
    {
        availableLadders.RemoveAll(ladder => ladder == null || !ladder.isActiveAndEnabled);
    }
}
