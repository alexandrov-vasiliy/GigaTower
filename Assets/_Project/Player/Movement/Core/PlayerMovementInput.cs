using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System adapter for player locomotion. PlayerInput callbacks update normalized movement and queue jump input, Update polls the named Sprint action, and ConsumeJumpRequest provides one-shot jump semantics to FirstPersonMovement.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerMovementInput : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool SprintRequested { get; private set; }

    private bool jumpRequested;
    private PlayerInput _playerInput;

    private InputAction _sprintAction;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _sprintAction = _playerInput.actions["Sprint"];
    }
    
    private void Update()
    {
        SprintRequested = _sprintAction.IsPressed();
    }

    public void OnMove(InputValue value)
    {
        MoveInput = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            jumpRequested = true;
    }
    

    public bool ConsumeJumpRequest()
    {
        bool wasRequested = jumpRequested;
        jumpRequested = false;
        return wasRequested;
    }

    private void OnDisable()
    {
        MoveInput = Vector2.zero;
        SprintRequested = false;
        jumpRequested = false;
    }
}
