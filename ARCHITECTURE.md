# GigaTower RepoMap

This is the mandatory first stop for repository work. It maps the current architecture; source files, serialized scenes/prefabs, and Unity MCP remain authoritative. Keep this file synchronized whenever a change affects the map below.

## Runtime and project boundaries

- Unity `6000.4.0f1`, primary platform `StandaloneWindows64`.
- Main scene: `Assets/_Project/Scenes/Game.unity`.
- Project-owned assets and code: `Assets/_Project`.
- Shared input asset: `Assets/InputSystem_Actions.inputactions`.
- Render pipeline assets: `Assets/Settings` (URP PC/Mobile renderers and profiles).
- Third-party/vendor roots: `Assets/Feel` (More Mountains Feel/Nice Vibrations), `Assets/Plugins/Demigiant` (DOTween), `Assets/Thirdparty`, `Assets/UModelerX-Hub`, and `Assets/Gridbox Prototype Materials`. Do not treat these as project-owned code.
- Package inventory: `Packages/manifest.json`; notable packages are Unity MCP, URP, Input System, Cinemachine, AI Navigation, Terrain Tools, Timeline, uGUI, and Test Framework.

## Gameplay map

### Bootstrap and scene

- `Assets/_Project/Main.cs` — currently an empty scene `MonoBehaviour`; no bootstrap flow is implemented yet.
- `Assets/_Project/Player/PlayerSpawner.cs` — idempotently instantiates one configured player prefab at a spawn transform.
- `Assets/_Project/Player/Player.prefab` — main player composition.
- `Assets/_Project/Player/FPCamera.prefab` — first-person camera composition.

### Player movement

Flow: Input System callbacks -> `PlayerMovementInput` -> `FirstPersonMovement` -> ground or ladder path -> `CharacterController.Move`.

- `Player/Movement/Core/PlayerMovementInput.cs` — movement/jump/sprint input adapter.
- `Player/Movement/Core/FirstPersonMovement.cs` — locomotion coordinator and knockback entry point.
- `Player/Movement/Core/GroundMovement.cs` — ground velocity, gravity, jump velocity, and external displacement.
- `Player/Movement/Abilities/SprintAbility.cs` — speed multiplier and stamina drain.
- `Player/Movement/Abilities/JumpAbility.cs` — jump eligibility and stamina cost.
- `Player/Movement/Abilities/Ladders/Ladder.cs` — ladder volume and orientation data.
- `Player/Movement/Abilities/Ladders/LadderClimbing.cs` — ladder discovery, climb state, velocity, and jump-off.
- `Player/Movement/Shared/MissingStaminaPolicy.cs` — behavior when an optional stamina component is absent.

### Stamina and presentation

Flow: movement abilities spend `PlayerStamina` -> stamina events -> `StaminaView` animates UI with DOTween.

- `Player/PlayerStamina.cs` — stamina resource, delayed regeneration, exhaustion lock, and events.
- `Player/StaminaView.cs` — Slider/CanvasGroup presenter and tween ownership.
- `Player/Hands/HandsBobbing.cs` — first-person hand idle/movement/sprint bob driven by controller velocity.
- `Player/Movement/Presentation/DistanceStepFeedbackCycle.cs` — distance-based step cadence.
- `Player/Movement/Presentation/FirstPersonFootstepFeedbackPlayer.cs` — distance-based player footstep event source.
- `Player/Movement/Presentation/FirstPersonJumpFeedbackPlayer.cs` — successful ground-jump event adapter.
- `Player/Movement/Presentation/FirstPersonLandingFeedbackPlayer.cs` — physical landing event source and fall-threshold tracking.
- `Player/Movement/Presentation/SurfaceFeedbackPlayer.cs` — entity-owned `SurfaceType + event` routing to FEEL, with Earth fallback and optional common feedbacks.
- `Player/Movement/Presentation/MovementCameraTilt.cs` — movement camera tilt presentation.

### Interaction

Flow: Input System callback -> `PlayerInteractionInput` one-shot request -> `FirstPersonInteractor` raycast -> target `IInteractable`.

- `Player/Interaction/PlayerInteractionInput.cs` — interaction input adapter.
- `Player/Interaction/FirstPersonInteractor.cs` — target discovery and dispatch.
- `Player/Interaction/IInteractable.cs` — world interaction contract.

### Environment

- `Surfaces/SurfaceType.cs`, `Surface.cs`, and `SurfaceDetector.cs` — semantic Earth/Wood identity and reusable ground-contact detection; world surfaces contain identity only while entities own reactions.
- `Env/Fog/source/FogSimulation.cs` and `FogObstacle.cs` — compute-shader fog density simulation around the player and registered obstacles, with an inspector-authored initial density texture used for Edit Mode preview and runtime initialization.
- `Shaders/Water/waterfall.vfx` and `Zowell_Water.shadergraph` — the `RomanLevel` mesh-particle waterfall, using the `Env/RomanLevel/RustyPipe/waterfallmesh01.fbx` flow mesh and animated distortion, ripples, normals, foam, and transparency.
- `Env/Water/SteamSprayVolume.cs` and `Shaders/Particles/SteamSpray.shader` — reusable `RomanLevel` box-volume steam/spray particles; object scale changes only the emission volume while the component owns density, motion, texture, color, fading, and turbulence.
- `Env/Sky/Mesh/SkyRotator.cs` — looping DOTween sky rotation.
- `Env` also contains environment models, materials, textures, trees, fog shaders, and sky prefabs used by the scene.

## Change checklist

1. Read this map, then inspect the actual affected source, callers, scene/prefab references, and package APIs.
2. Verify Unity MCP is connected to the `GigaTower` instance before project work.
3. Make the smallest root-cause change under project-owned paths; preserve serialized field compatibility and `.meta` files.
4. Refresh/compile and run the smallest relevant Edit Mode or Play Mode check through Unity MCP.
5. Update this RepoMap in the same change if components, ownership, flows, dependencies, scenes, prefabs, or directory boundaries changed.
