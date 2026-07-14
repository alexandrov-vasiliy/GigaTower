using UnityEngine;

/// <summary>
/// First-person interaction coordinator for the player. It consumes PlayerInteractionInput, raycasts from the configured origin or active camera, finds IInteractable components on hit colliders or parents, and forwards interaction requests without owning target behavior.
/// </summary>
public sealed class FirstPersonInteractor : MonoBehaviour
{
    [SerializeField] private PlayerInteractionInput interactionInput;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Transform rayOrigin;
    [SerializeField, Min(0.01f)] private float interactionDistance = 2.5f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    public IInteractable CurrentTarget { get; private set; }
    public RaycastHit CurrentHit { get; private set; }

    private void Awake()
    {
        if (interactionInput == null)
            interactionInput = GetComponentInChildren<PlayerInteractionInput>();

        if (interactionCamera == null)
            interactionCamera = Camera.main;
    }

    private void Update()
    {
        UpdateCurrentTarget();

        if (interactionInput == null)
            return;

        if (!interactionInput.ConsumeInteractRequest())
            return;

        if (CurrentTarget == null)
            return;

        if (CurrentTarget.CanInteract(gameObject))
            CurrentTarget.Interact(gameObject);
    }

    private void UpdateCurrentTarget()
    {
        CurrentTarget = null;
        CurrentHit = default;

        Transform origin = GetRayOrigin();
        if (origin == null)
            return;

        Ray ray = new(origin.position, origin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, triggerInteraction))
            return;

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null)
            return;

        CurrentHit = hit;
        CurrentTarget = interactable;
    }

    private Transform GetRayOrigin()
    {
        if (rayOrigin != null)
            return rayOrigin;

        if (interactionCamera != null)
            return interactionCamera.transform;

        interactionCamera = Camera.main;
        return interactionCamera != null ? interactionCamera.transform : transform;
    }
}
