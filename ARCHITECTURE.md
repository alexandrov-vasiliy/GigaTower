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
- Embedded editor package: `Packages/com.texturelab.editor` contains Texture Lab, a Unity 6 GPU texture-processing tool. Its product plan remains at `Assets/TextureLab/Plan.md`.

## Gameplay map

### Bootstrap and scene

- `Assets/_Project/Main.cs` — currently an empty scene `MonoBehaviour`; no bootstrap flow is implemented yet.
- `Assets/_Project/Player/PlayerSpawner.cs` — idempotently instantiates one configured player prefab at a spawn transform.
- `Assets/_Project/Player/Player.prefab` — main player composition.
- `Assets/_Project/Player/FPCamera.prefab` — first-person camera composition.

### RomanLevel camera look

- `Shaders/Prototype/RomanDream.shadergraph` and `RomanDream.mat` — native URP Fullscreen Shader Graph for aspect-correct pixel sampling, perceptual colour quantization, pixel dithering, and animated monochrome film grain. The material exposes pixel height/amount, colour steps, dither, grain, grain frame rate, and effect blend.
- `Shaders/Prototype/RomanDreamVolume.asset` — RomanLevel's own Neutral tonemapping, bloom, colour adjustment, white balance, split toning, and vignette profile, assigned to its existing Global Volume. Neutral preserves readable details in the level's dim corridors.
- `Shaders/Prototype/RomanDreamRenderer.asset` — native Full Screen Pass after post-processing, selected only by the `Player/HandsCamera` instance in `Scenes/RomanLevel.unity`. Main Camera draws the world without post-processing; the final overlay camera grades the combined world/hands and runs the graph once. Camera overrides remain in the scene, not the shared Player prefab. PC/Mobile pipeline assets register this renderer at index 1 while keeping their original renderer at default index 0. `Shaders/Prototype/README.md` documents tuning and restoring the original camera settings.
- `Shaders/Prototype/RomanTorchLight.cs` — attached only to `Point Light Torch` in RomanLevel; binds that light's position/range and adjustable brightness steps, light pixel height, and effect strength before rendering cameras in its scene. Its native point light retains color, intensity, range, and hard shadows (512 px per face).
- `Shaders/Prototype/RomanTorchSurface.shadergraph`, `RomanTorchWater.shadergraph`, and `RomanTorchLighting.hlsl` — URP Lit graphs with a Custom Function that replaces only the designated torch's direct lighting contribution with pixelated brightness bands. Other lights and native indirect lighting remain smooth. `TorchMaterials/` contains scene-only copies of the existing Lit materials and RomanWater; receiver assignments are RomanLevel instance overrides, preserving original materials/prefabs. The water graph retains the original waves/refraction/foam. This prototype targets the project's Forward+ / Forward per-pixel lighting, one designated non-coincident realtime torch, and metallic Lit surfaces without clear coat or baked shadow masks; the camera post-effect remains independent.

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
- `Env/Fog/source/FogVolume.shader` — depth-clipped raymarched fog with per-material Glow Enabled, HDR Glow Color, and Glow Intensity controls. Emission is weighted by integrated opacity, preserving empty space and edge fades; it is off by default. RomanLevel's `roof animated fog` uses `Shaders/Water/Zowell_Water.shadergraph`, which also exposes an optional Glow group.
- `Env/RomanLevel/Water/` — the current `RomanLevel` water and shaft waterfall, authored with native Shader Graph nodes. `RomanWater.shadergraph` handles depth color, vertex waves, surface normals, refraction, and shore foam; `RomanWaterfall.shadergraph` pans a Voronoi flow texture through separately animated Voronoi UV distortion and blends into the pool; `RomanWaterTraces.shadergraph` adds expanding, dissolving impact rings to `tracesWater`. All noise textures reference existing `Env/SharedTextures/noises_512x512/512x512` assets (Perlin_08 and Voronoi_01). The graphs compensate for these textures' existing sRGB import settings without changing shared imports.
- Water, waterfall, foam traces, and the `RomanTorchWater` variant expose a native Shader Graph Glow group: toggle, HDR tint, and intensity (0–20). Existing `_WaterGlow` / `_FoamGlow` references and values remain compatible. The toggle gates only self-emission, preserving refraction and the torch correction. The existing Bloom Volume controls the optional screen-space halo separately.
- The water bundle uses only its existing `RomanLevelWaterLayer.fbx`, `WaterFallMesh.fbx`, and `WaterTracerMesh.fbx`; the last mesh also supplies the lower pool. Projection is independent of the supplied FBX UVs. Materials expose tuning directly; matching wave settings keep the pool and traces coordinated. Refraction and contact foam use the existing URP depth/opaque textures.
- `Env/RomanLevel/Water/RomanWaterfallVFX.prefab` — a VisualEffect using `RomanWaterfallImpact.vfx`: three native VFX Graph systems for droplets, splash crown, and soft mist, with Shader Graph outputs using `RomanWaterSpray.shadergraph`. Blackboard properties expose rates, tint, and shared noise; the graph prewarms for three seconds. The existing pipe splash (`waterfall (1)`) also uses this graph with lower rates at the upper water surface. No runtime controller is required. `README.md` documents tuning and tutorial references. Earlier `Shaders/Water` and `Zowell_*`/`waterfall.vfx` assets remain legacy content; the old shaft `waterfall` scene object is disabled. The replaced handwritten Roman water shaders, generated noise, and ParticleSystem materials have been removed.
- `Env/Water/SteamSprayVolume.cs` and `Shaders/Particles/SteamSpray.shader` — reusable `RomanLevel` box-volume steam/spray particles; object scale changes only the emission volume while the component owns density, motion, texture, color, fading, and turbulence.
- `Env/Sky/Mesh/SkyRotator.cs` — looping DOTween sky rotation.
- `Env` also contains environment models, materials, textures, trees, fog shaders, and sky prefabs used by the scene.

## Editor tooling

- `Packages/com.texturelab.editor/Editor/UI/TextureLabWindow.cs` — UI Toolkit window opened from `Tools > Texture Lab`; accepts ordinary LDR Default/Sprite textures, previews the result, and owns effect-stack editing, drag reorder, duplication, enable/disable, Undo/Redo, preset Save As/Apply/Overwrite/Duplicate/Rename/Reset, the Variations entry point, and Export. `VariationsWindow` creates a 3×3 grid from one source and nine independent stack copies; selecting a candidate and applying it is one Undo step, while closing the window changes nothing. `TextureLabExportWindow` exposes PNG/JPG, JPG quality, import settings, Point/Bilinear filtering, and three destinations. Built-in presets selected from the object field can be applied or duplicated but cannot be overwritten or renamed; only copies under `Assets` are editable. Its preview workspace provides 1×1 through 8×8 tiling over one shared display texture, click/Space toggle comparison with the original, Fit/25%/50%/100%/200% zoom with scrolling, RGB/R/G/B/Alpha/Luminance display channels, and 512/1024/2048 preview quality.
- `Packages/com.texturelab.editor/Editor/Model/` — serializable Pixelate, Posterize, Levels, Color Adjustments, Color Replace, Palette Quantization, Dither, Noise, Gaussian Blur, Offset, Seam Blend, Channel Mixer, and Dodge / Burn Brush data plus `TextureLabPalette` and versioned `TextureLabPreset` assets. Brush strokes own their UV points and a snapshot of mode, size, hardness, and exposure, while the card holds settings for the next stroke; they are non-randomizable and survive duplication, presets, source replacement, and full-resolution export. Every effect has an Allow Randomize state; Offset and Seam Blend start opted out. Palette Quantization has a non-destructive Color Limit separate from palette extraction. Preset/session transfers always duplicate their `[SerializeReference]` effect data, preventing shared mutable stacks; presets never hold source textures or preview state. Channel Mixer keeps the legacy internal `ChannelRemapEffectData` type name so existing `[SerializeReference]` sessions deserialize safely. A project-local session stored under `Library` keeps the unsaved working stack and preview UI preferences across Editor restarts without entering version control; user palette assets live under `Assets`.
- `Packages/com.texturelab.editor/Editor/Randomization/VariationGenerator.cs` — deterministic seed-driven parameter jitter for safe, opted-in effect fields. It never changes Color Replace colours or palette asset references; Offset/Seam Blend vary only after explicit opt-in.
- `Packages/com.texturelab.editor/Editor/Processing/TextureExporter.cs` — full-resolution GPU export using the existing processor and a final temporary CPU readback. It atomically writes PNG RGBA or JPG RGB, prompts in the UI before JPG alpha loss/overwrite, and imports Assets destinations with either safe Recommended settings or a narrow source-importer inheritance.
- `Packages/com.texturelab.editor/Editor/Presets/Starter/` — nine read-only technical starter presets (PSX Soft/Harsh, Low Color, Retro PC, Dirty Texture, Posterized, Pixel Art, Dreamcast-ish, Dark Horror) embedded in the package. `TextureLabPresetLibrary` can restore any missing starter asset from `Tools > Texture Lab > Create Starter Presets` without overwriting existing files.
- `Packages/com.texturelab.editor/Editor/Processing/TextureProcessor.cs` and `Shaders/` — selectable 512/1024/2048 GPU preview using two reusable effect ping-pong render textures without upscaling above source size; effect data is separate from processors. `ProcessFullResolution` uses source dimensions for export. One additional reusable display render texture handles original downsampling and channel visualization without rerunning the effect stack. `VariationsWindow` runs nine 256px candidates sequentially through its one processor and retains only their thumbnail RTs until regeneration/close. Color Replace supports soft RGB-distance masks, white/value/blue noise keeps deterministic seeds, Gaussian Blur is separable, and the one-pass Channel Mixer provides a signed RGB 3×3 matrix, constants, strength, monochrome mixing, local recipes, and manual normalization. `BrushStrokeRasterizer` independently converts brush strokes to a transient GPU exposure mask; `TextureLabBrushExposure.shader` applies signed exposure while preserving alpha, with repeat or clamp edge handling. `TextureLabSeamless.shader` supplies mathematical-repeat or clamp Offset and independently axis-selectable smooth Seam Blend without another persistent render target. Color effects preserve alpha; Seam Blend changes alpha only through its explicit toggle, and Channel Mixer only in its explicit Alpha Mix mode. Palette matching uses perceptual Oklab distance with up to 64 colors and can use a stack-local Color Limit.
- `Packages/com.texturelab.editor/Editor/Palettes/PaletteExtractor.cs` — deterministic Oklab K-Means extraction from a GPU-downsampled 128px source; it does not require readable source imports. Palette Quantization cards create, assign, edit, reorder, and extract palette assets with Undo support.
- `Packages/com.texturelab.editor/Shaders/TextureLabDither.shader` — independently reorderable Bayer 2x2/4x4/8x8 and self-contained 16x16 blue-noise dithering with strength, pattern scale, deterministic seed/offset, monochrome or RGB channels, and preserved alpha.

## Change checklist

1. Read this map, then inspect the actual affected source, callers, scene/prefab references, and package APIs.
2. Verify Unity MCP is connected to the `GigaTower` instance before project work.
3. Make the smallest root-cause change under project-owned paths; preserve serialized field compatibility and `.meta` files.
4. Refresh/compile and run the smallest relevant Edit Mode or Play Mode check through Unity MCP.
5. Update this RepoMap in the same change if components, ownership, flows, dependencies, scenes, prefabs, or directory boundaries changed.
