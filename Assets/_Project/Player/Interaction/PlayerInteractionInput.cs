using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System Send Messages adapter for first-person interaction. PlayerInput calls OnInteract, this component queues a one-shot request, and FirstPersonInteractor consumes it without reading input actions directly.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInteractionInput : MonoBehaviour
{
    private bool interactRequested;

    private void OnDisable()
    {
        interactRequested = false;
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
            interactRequested = true;
    }

    public bool ConsumeInteractRequest()
    {
        bool wasRequested = interactRequested;
        interactRequested = false;
        return wasRequested;
    }
}
