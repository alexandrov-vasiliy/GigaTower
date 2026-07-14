using UnityEngine;

/// <summary>
/// Reusable distance accumulator for footstep-style feedback components. It owns only the step timing state: callers supply traveled distance and current tuning, then play their own MMF_Player or other feedback when Tick returns true. FirstPersonFootstepFeedbackPlayer and enemy movement feedback use this to share the same first-step delay and per-distance cadence while keeping their movement-source dependencies separate.
/// </summary>
public sealed class DistanceStepFeedbackCycle
{
    private float distanceUntilNextStep;

    public void Reset(float stepDistance, float firstStepDistanceRatio)
    {
        distanceUntilNextStep = NormalizeStepDistance(stepDistance) * Mathf.Clamp01(firstStepDistanceRatio);
    }

    public bool Tick(float distanceDelta, float stepDistance)
    {
        distanceUntilNextStep -= Mathf.Max(0f, distanceDelta);
        if (distanceUntilNextStep > 0f)
            return false;

        distanceUntilNextStep += NormalizeStepDistance(stepDistance);
        return true;
    }

    private static float NormalizeStepDistance(float stepDistance)
    {
        return Mathf.Max(0.01f, stepDistance);
    }
}
