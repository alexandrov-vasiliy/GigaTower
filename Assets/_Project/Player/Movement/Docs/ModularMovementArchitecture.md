# Modular First-Person Movement Architecture

This document describes the intended modular direction for the first-person player movement code in `Assets/_Project/Player/Movement`.

The goal is to keep the controller easy to reuse in other projects: sprinting, jumping, ladder climbing, stamina costs, feedback, and future movement features should be removable or addable by changing components on the player prefab, not by rewriting the core controller.

## Folder Layout

`Movement` is split by responsibility so reusable gameplay rules, core control flow, and presentation adapters do not blur together.

- `Core`: the movement coordinator, input adapter, and ordinary ground movement.
- `Abilities`: optional player movement abilities that can be added or removed from a prefab.
- `Abilities/Ladders`: ladder-specific ability code and ladder world volumes.
- `Shared`: small shared contracts or enums used by multiple movement modules.
- `Presentation`: camera, audio, FEEL, UI-adjacent, and feedback adapters that observe movement state.
- `Docs`: movement architecture notes and implementation guides.

When adding a new movement feature, put its gameplay rule component under `Abilities` unless it is a core dependency for every controller. Put feature-specific scene/world helpers in a subfolder under that ability area, and keep visual or audio reactions in `Presentation`.

## Core Idea

`FirstPersonMovement` should be the frame coordinator, not the owner of every movement rule.

It should:

- read the current `PlayerMovementInput` state;
- ask optional ability components what they want to contribute this frame;
- choose whether the player is in ground movement or a special movement mode such as ladder climbing;
- call `CharacterController.Move` exactly once for the selected movement path;
- keep transitions between movement modules explicit.

It should not:

- own stamina costs for every feature;
- know detailed sprint, jump, ladder, crouch, dash, or other ability rules;
- require optional features through `[RequireComponent]`;
- let multiple components move the same `CharacterController` in the same frame.

## Responsibilities

### `FirstPersonMovement`

Owns the movement frame flow and `CharacterController.Move`.

Expected responsibilities:

- gather input from `PlayerMovementInput`;
- query optional abilities such as `SprintAbility`, `JumpAbility`, and `LadderClimbing`;
- pass simple results into movement calculators;
- apply the final velocity/displacement through `CharacterController`;
- stop or reset modules during transitions, disabling, and knockback.

Dependencies that are fundamental to the controller may stay required, such as `CharacterController` and ground movement. Optional gameplay features should be resolved with `TryGetComponent` or serialized references and handled when missing.

### `GroundMovement`

Calculates ordinary 3D ground movement.

Expected responsibilities:

- convert move input into orientation-relative direction;
- apply base move speed;
- apply an externally supplied speed multiplier;
- own gravity and vertical velocity;
- own external velocity/displacement such as knockback or ladder exit velocity.

`GroundMovement` should not decide whether sprinting, stamina spending, or jumping is allowed. It should receive those decisions from abilities.

### `PlayerMovementInput`

Adapts Unity Input System data into gameplay-friendly state.

Expected responsibilities:

- expose normalized move input;
- expose held actions such as sprint;
- expose one-shot actions such as jump through consume methods.

Gameplay components should read this adapter instead of reading `InputAction` directly.

### `PlayerStamina`

Owns the stamina resource and stamina events.

Expected responsibilities:

- track current and maximum stamina;
- expose spending and draining methods;
- handle regeneration and exhaustion recovery;
- notify UI and gameplay listeners.

Movement abilities may use stamina, but the core movement coordinator should not require stamina to exist.

### Ability Components

Ability components own optional movement rules.

Examples:

- `SprintAbility` decides whether sprint is active and returns a speed multiplier.
- `JumpAbility` decides whether a jump request is allowed and pays any jump cost.
- `LadderClimbing` owns ladder trigger overlap, climb state, climb velocity, and ladder exit velocity.

An ability may depend on optional resources such as `PlayerStamina`, but it should define what happens when that resource is missing.

## Missing Optional Dependencies

Optional dependencies should have explicit behavior.

For abilities that can use stamina, prefer a serialized policy:

```csharp
public enum MissingStaminaPolicy
{
    AllowForFree,
    DisableAbility
}
```

Use `AllowForFree` when the ability should work in projects without stamina. Use `DisableAbility` when missing stamina means the prefab is intentionally not allowed to use that ability.

Avoid hidden behavior where an ability silently half-works because a dependency is missing.

## Suggested First Refactor

Use small steps so the full current player prefab keeps the same feel after each step.

1. Make `PlayerStamina` optional for `FirstPersonMovement`.
2. Make `LadderClimbing` optional for `FirstPersonMovement`.
3. Add `SprintAbility` and move sprint stamina drain/speed multiplier logic into it.
4. Change `GroundMovement.CalculateVelocity` to receive a speed multiplier instead of a sprint boolean.
5. Add `JumpAbility` and move jump stamina spending/permission logic into it.
6. Keep `LadderClimbing` as the ladder ability, but let it be removed from the prefab without breaking ground movement.
7. Update class-level XML summaries for every changed or created C# class.
8. Let Unity compile and check the console before relying on the new components.

## Adding a New Movement Ability

Use this checklist when adding a feature such as crouch, dash, slide, swim, wall run, ledge grab, or slow walk.

1. Define the ability's ownership.

   Write down what the new component owns and what it does not own. A good ability owns one feature and its local state. It should not take over the full controller.

2. Decide whether the ability modifies ground movement or replaces the movement mode.

   A modifier changes the normal ground result. Examples: sprint, crouch speed, slow walk.

   A movement mode temporarily replaces ground movement. Examples: ladder climbing, swimming, ledge hanging.

3. Keep `CharacterController.Move` in `FirstPersonMovement`.

   The ability should return intent, velocity, multipliers, permissions, or transition data. It should not move the character directly unless the whole architecture is deliberately changed.

4. Use `PlayerMovementInput` instead of direct input actions.

   If the ability needs a new input, add it to the input adapter first, then consume the adapter state from the ability or coordinator.

5. Make external resources optional when possible.

   If the ability can use stamina, inventory, buffs, or status effects, resolve those dependencies explicitly and define missing-dependency behavior.

6. Avoid hard requirements for removable features.

   Do not add `[RequireComponent]` for an optional feature. Use `[RequireComponent]` only for components without which this component cannot function at all.

7. Keep transitions explicit.

   If the ability starts or stops a movement mode, expose clear methods such as `TryStart`, `CalculateVelocity`, `ShouldStop`, and `Stop`. Return any exit velocity or reset request instead of reaching into other modules.

8. Keep presentation separate.

   Camera tilt, UI, audio, FEEL feedback, particles, and animation should observe gameplay state or public events. They should not contain movement rules.

9. Update the class-level XML summary.

   Every C# class in `Assets/_Project` must have a class-level XML summary. When adding or changing an ability, update the summary so future work can understand the class without reading the whole file.

10. Verify by removing the component.

    A modular ability is not finished until the player still works when the ability component is removed from the prefab. The feature should disappear cleanly; unrelated movement should keep working.

## Ability Shape Examples

### Ground Modifier Ability

Use this shape for abilities that affect normal ground movement without replacing it.

```csharp
public sealed class SprintAbility : MonoBehaviour
{
    public float GetSpeedMultiplier(Vector2 moveInput, bool sprintRequested, float deltaTime)
    {
        return 1f;
    }
}
```

The coordinator asks the ability for a result, then passes that result into `GroundMovement`.

### Movement Mode Ability

Use this shape for abilities that temporarily replace ground movement.

```csharp
public sealed class LadderClimbing : MonoBehaviour
{
    public bool IsClimbing { get; }

    public bool TryStartClimbing(Vector3 playerPosition, Vector2 moveInput, Vector3 moveDirection)
    {
        return false;
    }

    public Vector3 CalculateVelocity(Vector2 moveInput, float speedMultiplier)
    {
        return Vector3.zero;
    }

    public bool ShouldStopClimbing(Vector3 playerPosition, CollisionFlags collisionFlags)
    {
        return true;
    }

    public Vector3 StopClimbing(bool jumpOff)
    {
        return Vector3.zero;
    }
}
```

The coordinator remains responsible for calling `CharacterController.Move` and passing collision results back into the active mode.

## Prefab Composition Examples

### Basic First-Person Controller

- `CharacterController`
- `PlayerMovementInput`
- `FirstPersonMovement`
- `GroundMovement`

### Controller With Jump

- basic controller components
- `JumpAbility`

### Controller With Sprint

- basic controller components
- `SprintAbility`

### Controller With Stamina

- basic controller components
- `PlayerStamina`
- any ability configured to spend stamina
- optional `StaminaView` for UI

### Controller With Ladders

- basic controller components
- `LadderClimbing`
- `Ladder` components on climbable trigger volumes in the scene

## Review Questions Before Merging a New Ability

- Can the player prefab run without this ability component?
- Does only `FirstPersonMovement` move the `CharacterController`?
- Does the ability use `PlayerMovementInput` instead of direct `InputAction` reads?
- Are stamina and other resources optional or clearly required?
- Are transitions into and out of the ability explicit?
- Did the changed C# class summaries get updated?
- Did Unity compile without console errors?
