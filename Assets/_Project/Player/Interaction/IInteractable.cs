using UnityEngine;

/// <summary>
/// Contract for world objects that can react to the first-person interaction module. Implementers own their interaction rules and side effects while FirstPersonInteractor only discovers a target and forwards the interactor GameObject.
/// </summary>
public interface IInteractable
{
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
