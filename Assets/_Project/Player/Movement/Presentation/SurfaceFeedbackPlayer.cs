using System;
using System.Collections.Generic;
using System.Diagnostics;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>Plays entity-owned FEEL reactions selected by the detected ground surface.</summary>
[RequireComponent(typeof(SurfaceDetector))]
public sealed class SurfaceFeedbackPlayer : MonoBehaviour
{
    [Serializable]
    private sealed class SurfaceFeedbacks
    {
        public SurfaceType surface;
        public MMF_Player footstep;
        public MMF_Player jumpTakeoff;
        public MMF_Player landing;

        public MMF_Player Get(SurfaceFeedbackEvent feedbackEvent) => feedbackEvent switch
        {
            SurfaceFeedbackEvent.Footstep => footstep,
            SurfaceFeedbackEvent.JumpTakeoff => jumpTakeoff,
            SurfaceFeedbackEvent.Landing => landing,
            _ => null
        };
    }

    private enum SurfaceFeedbackEvent
    {
        Footstep,
        JumpTakeoff,
        Landing
    }

    [SerializeField] private MMF_Player commonFootstepFeedback;
    [SerializeField] private MMF_Player commonJumpTakeoffFeedback;
    [SerializeField] private MMF_Player commonLandingFeedback;
    [SerializeField] private List<SurfaceFeedbacks> surfaces = new();

    private readonly HashSet<string> warnedFallbacks = new();
    private SurfaceDetector detector;

    public float LastLandingImpactSpeed { get; private set; }

    private void Awake() => detector = GetComponent<SurfaceDetector>();

    public void PlayFootstep() => Play(SurfaceFeedbackEvent.Footstep, commonFootstepFeedback);
    public void PlayJumpTakeoff() => Play(SurfaceFeedbackEvent.JumpTakeoff, commonJumpTakeoffFeedback);
    public void PlayLanding(float impactSpeed)
    {
        LastLandingImpactSpeed = impactSpeed;
        Play(SurfaceFeedbackEvent.Landing, commonLandingFeedback);
    }

    private void Play(SurfaceFeedbackEvent feedbackEvent, MMF_Player commonFeedback)
    {
        Vector3 contactPoint = detector.ContactPoint;
        commonFeedback?.PlayFeedbacks(contactPoint);

        SurfaceType detectedType = detector.CurrentType;
        MMF_Player feedback = FindFeedback(detectedType, feedbackEvent);
        if (feedback == null && detectedType != SurfaceType.Earth)
        {
            WarnFallback(detectedType, feedbackEvent);
            feedback = FindFeedback(SurfaceType.Earth, feedbackEvent);
        }

        feedback?.PlayFeedbacks(contactPoint);
    }

    private MMF_Player FindFeedback(SurfaceType type, SurfaceFeedbackEvent feedbackEvent)
    {
        for (int i = 0; i < surfaces.Count; i++)
        {
            if (surfaces[i].surface == type)
                return surfaces[i].Get(feedbackEvent);
        }

        return null;
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void WarnFallback(SurfaceType type, SurfaceFeedbackEvent feedbackEvent)
    {
        string key = $"{type}.{feedbackEvent}";
        if (warnedFallbacks.Add(key))
            UnityEngine.Debug.LogWarning($"{name} has no {feedbackEvent} feedback for {type}; using Earth.", this);
    }
}
