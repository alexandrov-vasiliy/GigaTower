using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Applies a smoothed Cinemachine Dutch angle from horizontal PlayerMovementInput to communicate strafing motion. It resolves a child CinemachineCamera when needed and restores the original lens angle when disabled.
/// </summary>
public sealed class MovementCameraTilt : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField, Min(0f)] private float maxTiltAngle = 2.5f;
    [SerializeField, Min(0.01f)] private float tiltSmoothTime = 0.15f;

   [SerializeField]  private PlayerMovementInput movementInput;
    private float initialDutch;
    private float currentDutch;
    private float tiltVelocity;

    private void Awake()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (cinemachineCamera == null)
            return;

        initialDutch = cinemachineCamera.Lens.Dutch;
        currentDutch = initialDutch;
    }

    private void Update()
    {
        if (cinemachineCamera == null)
            return;
        if(movementInput == null)
            return;
        
        
        float targetDutch = initialDutch - movementInput.MoveInput.x * maxTiltAngle;
        currentDutch = Mathf.SmoothDampAngle(
            currentDutch,
            targetDutch,
            ref tiltVelocity,
            tiltSmoothTime);

        cinemachineCamera.Lens.Dutch = currentDutch;
    }

    private void OnDisable()
    {
        if (cinemachineCamera != null)
            cinemachineCamera.Lens.Dutch = initialDutch;
    }
}
