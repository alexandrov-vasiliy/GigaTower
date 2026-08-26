using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>Tracks the nearest non-trigger ground surface beneath a CharacterController.</summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterController))]
public sealed class SurfaceDetector : MonoBehaviour
{
    [SerializeField, Min(0f)] private float probeDistance = 0.3f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private readonly HashSet<Collider> warnedColliders = new();
    private CharacterController characterController;

    public SurfaceType CurrentType { get; private set; } = SurfaceType.Earth;
    public Vector3 ContactPoint { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        Bounds bounds = characterController.bounds;
        float distance = bounds.extents.y + probeDistance;

        if (!Physics.Raycast(bounds.center, Vector3.down, out RaycastHit hit, distance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            CurrentType = SurfaceType.Earth;
            ContactPoint = bounds.center + Vector3.down * bounds.extents.y;
            return;
        }

        ContactPoint = hit.point;
        Surface surface = hit.collider.GetComponentInParent<Surface>();
        CurrentType = surface != null ? surface.Type : SurfaceType.Earth;

        if (surface == null)
            WarnMissingSurface(hit.collider);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void WarnMissingSurface(Collider hitCollider)
    {
        if (warnedColliders.Add(hitCollider))
            UnityEngine.Debug.LogWarning($"{hitCollider.name} has no Surface component; using Earth.", hitCollider);
    }
}
