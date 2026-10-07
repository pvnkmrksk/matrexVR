# Configuration reference

This reference describes the experiment configuration code, tested with Unity **6000.3.16f1**. Start with the complete JSON files in [StreamingAssets/Templates](../Assets/StreamingAssets/Templates). They contain editable example values; the tables below distinguish these from values used when a field is omitted. JSON does not allow comments. Keep notes in a separate text file; do not invent JSON keys, because most loaders silently ignore unknown keys.

## Files, precedence, and running an experiment

1. Use the complete `Templates/system_config.template.json`, `Templates/experiment.template.json` and `Templates/sequence.template.json` as a matched Kinefly flight starting set. Set actual hardware addresses and displays. The local `system_config.json` is Git-ignored and is not replaced by cleanup.
2. For other paradigms, choose a complete file from [Templates](../Assets/StreamingAssets/Templates/README.md); use [Examples](../Assets/StreamingAssets/Examples/README.md) for variations. All references are relative to StreamingAssets. Dynamic Choice uses `parameters.design`; other experiment controllers use `parameters.configFile`.
3. Open `Assets/Scenes/ControlScene.unity`, enter Play, enter metadata, and start the sequence. Sex is visible for each animal, with Unspecified as the fresh default. Saved Female/Male values are restored on reload. Focus Game for hotkeys.
4. The active Selwyn sequence and all files in `Kannadi/` are preserved. The old collection is under `Archive/Legacy/`, with original bytes and a checksum manifest. If an experiment reference is missing at its current path, the loader checks that archive, so unchanged Kannadi sequences can still resolve their old Choice dependency.

System config owns physical camera layout and sphere calibration. Sequence config owns scene order and duration. Experiment configs own stimuli and tracked motion. Kannadi/Swarm merge inline `parameters` over `configFile` (arrays replace, rather than append), while autopilot remains owned by the referenced file. Choice/Optomotor read their referenced file without merging inline stimulus fields. Dynamic Choice uses `parameters.design`, not `configFile`.

The recovered historical Adaminaby image/config can also run through the current six-camera Choice scene: use `Examples/Sequences/adaminaby-historical.example.json`. See [its provenance and run instructions](adaminaby-historical-scene.md), including the original screenshot's projection limitations.

Autopilot is controlled by the individual experiment file referenced by `parameters.configFile`, so the sequence selects a complete movement configuration without duplicating its fields.

A default in a C# class is not necessarily a scene default: serialized prefab values override inspector defaults, and omitted fields on an in-scene step can retain state. Use explicit values for reproducibility. A template does not add rigs, scene assets, or hardware automatically.

## Units and coordinate system

Unity coordinates are **left-handed**: +X right, +Y up, +Z forward. In the current walking experiments, **one world unit is one centimeter**. Distances and translation speeds use world units and world units/second (cm, cm/s here); do not multiply by 100 in configs. Imported terrain, models, or flight branches may have a different authored scale; verify their scale before claiming physical centimeters.

Euler angles are degrees: X pitch, Y yaw, Z roll. Positive yaw takes `(0,0,1)` to `(1,0,0)` at +90°. Viewed from above with +Z up on the page and +X right, this is clockwise. Euler log values wrap into 0–360°; unwrap yaw before differentiating. “Clockwise” for pitch/roll depends on viewing direction; use the axis and sign, not an unspecified viewpoint. See [Unity's rotation and orientation reference](https://docs.unity3d.com/6000.3/Documentation/Manual/QuaternionAndEulerRotationsInUnity.html).

Polar positions use `x = radius × sin(angle)`, `z = radius × cos(angle)`, `y = height`, with angle in degrees. Thus 0° is +Z and +90° is +X. Colors are normalized 0–1 channels unless given as hex; they are stimulus values, not calibrated physical luminance. Time/duration fields use seconds, display indices are zero-based, rig numbers are one-based.

## System configuration

Templates: `Templates/system_config.template.json` for this branch’s Bogong flight setup; `Examples/System/fictrac-four-cameras.example.json` or `fictrac-six-cameras.example.json` for walking. Existing Kinefly configs and single-frame JSON publishers require no migration. Select `"closedLoopMode": "Kinefly"` per rig; **Tab** shows the effective mode and input endpoint.

| Field | Omitted default | Meaning / useful example |
|---|---|---|
| `targetFrameRate` | 120 | Positive desired FPS, or -1 for uncapped desktop rendering. Effective when vSyncCount=0. This is a target, not a guaranteed achieved rate. |
| `vSyncCount` | 0 | 0 uses the FPS target; 1–4 synchronize to every Nth display refresh and override the target on desktop. |
| `targetDisplay` | 1 | Zero-based physical display; template uses 0. Applies globally to every rig. Per-rig `targetDisplay` is overwritten. The legacy `-display N` startup flag affects initial activation; use JSON targetDisplay for reliable experiment camera mapping. |
| `configs` | none | Array of per-rig entries. Use one entry for every rig that should render. |
| `overheadCamera` | defaults below | Operator overview in Swarm/Kannadi; independent of animal camera letter order. |
| `configs[].vrId` | `VR1` | Exact rig identifier, e.g. VR1–VR4. Duplicate IDs overwrite earlier entries; use unique IDs. |
| `sphereDiameter` | 1 | Walking path ball diameter in cm; template 2.6. Historical Bogong/Kinefly retains the rig prefab diameter, as in the reference branch. |
| `closedLoopMode` | `FicTrac` | Historical enum (also accepts 0/1/2): `FicTrac`, `Kinefly`, `Tirbala`. Set `Kinefly` for wing-angle input. See [gain compatibility](bogong-gain-compatibility.md) for the reference branch's unusual FicTrac label and the separate walking path. |
| `ledPanelWidth`, `ledPanelHeight` | 128, 128 | Each camera viewport size in pixels. |
| `startRow`, `startCol` | 0, 0 | Zero-based panel cells, measured from the top-left. |
| `horizontal` | true | Advance through columns when true, rows when false. |
| `displayOrder` | empty | Camera allow-list and order. `RBLF` means Right, Back, Left, Front. Other letters: U=Up, D=Down. Missing letters disable the corresponding cameras even if enabled in the scene. Empty order or missing rig config renders none of its stimulus cameras. |
| `zmqAddress` | `localhost` | Host running the pose publisher; no `tcp://` prefix. |
| `zmqPort` | 9872 | TCP port; template uses 9871–9874. |
| `targetDisplay` inside rig | 1, then overwritten | Use the top-level value. |
| `manualControls` | null | Optional per-rig operator movement settings below. Omission preserves prefab tuning. |

The base VR prefab and its standard, Swarm, Kannadi and Cube variants now contain all six camera faces, including Down. Use `displayOrder: "DRBLFU"` for all six. `Examples/System/fictrac-six-cameras.example.json` provides four non-overlapping rows of six 128×128 panels (768×512 total) on display 0. Copy it to the local `system_config.json` and edit tracker addresses for your hardware. Up/Down are reactivated when requested and use Front's origin, clip planes and visibility mask. Existing `RBLF` configurations still enable only four faces. See [six-camera-rig.md](six-camera-rig.md).

Viewport slot `i`: x=(startCol + i if horizontal)×width; y=screenHeight−(startRow+1 + i if vertical)×height. Ensure these rectangles fit the physical display. An unavailable display disables its cameras. Configuring VR10 does not accidentally match VR1. Configured letters do not create a camera absent from the prefab. Camera disabling does not delete the rig or stop its sensor logging.

The top-right HUD shows measured FPS and either the requested target or VSync mode. No viewport component changes frame timing. For refresh-synchronized pacing use `vSyncCount:1`; for a numeric software cap use `vSyncCount:0`. Desktop software caps can still have uneven frame pacing; [Unity explains the target/VSync interaction](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings-vSyncCount.html).

### Manual controls (inside each rig)

| Field | Block default | Unit / meaning |
|---|---|---|
| `enabled` | true | Enable keyboard/gamepad manual movement for this rig. Reset and tracking toggles still work. |
| `translateSpeed` | 10 | cm/s for full arrow-key/stick input. |
| `rotateSpeed` | 50 | degrees/s for full yaw input. |
| `maxTranslateSpeed` | 100 | cm/s cap for operator speed adjustment. |
| `maxRotateSpeed` | 300 | degrees/s cap. |
| `allowVerticalTranslation` | false | Allow C/Z vertical motion. |
| `allowPitchAndRoll` | false | Allow W/S pitch and Q/E roll. |

### Overview

| `overheadCamera` field | Default | Meaning / useful range |
|---|---|---|
| `enabled` | true | Create the overview. |
| `targetDisplay` | -1 | Follow VR1's configured display; otherwise a zero-based display. |
| `x`, `y` | 0.58, 0.02 | Normalized bottom-left of panel. |
| `width`, `height` | 0.4, 0.4 | Fraction of display width/height. |
| `resolution` | 512 | Square render texture pixels; 512 is a practical starting point. |
| `markerSizePixels` | 14 | Fixed screen-size arrow, visible while zooming out. |
| `trailDurationSeconds` | 60 | Fading history; use 30 for shorter trails. |
| `trailSampleInterval` | 0.1 | Seconds between samples. |
| `trailWidthPixels` | 1.5 | Screen-space trail width. |
| `trailBreakDistance` | 50 | World-unit jump that breaks a trail at reset/wrap. |

Numbered arrows track active `VR<number>` roots and their headings. Overlapping animals keep overlapping labels. Right drag orbits, middle drag pans, wheel zooms, Reset view returns overhead. Hide disables both overview cameras and tracking updates and releases the render texture; showing starts a fresh trail. FPS remains visible. The overview can discover additional numbered rigs, but Kannadi's mirror routing currently supports only VR1–VR4.

## Sequence configuration

Template: `sequence.template.json`.

| Field | Default | Meaning |
|---|---|---|
| `randomise` | false | Shuffle outer scene execution order. |
| `loop` | true | Repeat outer sequence; templates explicitly use false. |
| `autoStart` | false | Start the sequence when Play begins in ControlScene. Returning with Escape does not auto-start again. Canonical templates explicitly use false. |
| `sequences` | none | Required nonempty array of scene steps. |
| `sceneName` | none | Exact enabled scene name, e.g. Swarm, Kannadi, Choice, Optomotor, Choice_desync. No Matrix scene exists. |
| `duration` | 0 | Outer step time, seconds; use a positive duration. |
| `gain` | 1 | Historical Bogong/Kinefly signed yaw gain, applied on scene load and in-place trial transitions. Zero disables yaw response; negative reverses it. It does not replace the separate walking angular multiplier. |
| `reloadScene` | true | When false and the scene stays the same, call its in-scene sequencer. Use true for Dynamic Choice: its AdvanceStep is intentionally empty. |
| `parameters` | null | Controller-specific dictionary. |
| `parameters.configFile` | none | Choice, Optomotor, Kannadi or Swarm file relative to StreamingAssets. |
| `parameters.design` | `dynamicSequenceDesign.json` | Dynamic Choice design file. |

Outer duration always limits time spent in a scene, including an inner looping optomotor/dynamic sequence. Top-level step `gain` from Bogong is restored and defaults to 1 exactly as in `BogongAustralia`; it is applied even when the same scene advances without reloading.

## Movement settings in individual experiment files

Kannadi, Swarm, Choice, and migration config files accept these fields. The sequence only references the file through `parameters.configFile`.

| Field | Omitted default | Meaning |
|---|---|---|
| `autopilotEnabled` | false | Keep moving forward while retaining lateral, reverse, and yaw steering. Ctrl+Space toggles the active mode. |
| `autopilotSpeed` | 10 | Constant forward speed in world units/second (cm/s for the walking setup). |

## Choice and migration

Templates: `choice.template.json`, `choice-band.template.json`, `migration.template.json`. Migration is a Choice configuration with wind/AGL settings, not a new scene or an alternate tracking protocol.

| Top-level field | Omitted default | Meaning / example |
|---|---|---|
| `objects` | empty array | Omitted, null or `[]` means environment-only flight/migration; sky and movement settings still apply. |
| `closedLoopPosition`, `closedLoopOrientation` | false | Boolean FicTrac translation/yaw switches. Use true for walking. |
| `initialPosition` | (0,0,0) | World x/y/z, cm. Template ground-eye height 0.5; migration example 100. |
| `initialRotation` | (0,0,0) | Euler x/y/z degrees. |
| `randomInitialRotation` | false | Random starting yaw; Choice shares that sampled yaw across its rigs. |
| `backgroundColor` | null | `{r,g,b,a}` (0–1); null keeps existing camera background. |
| `skyboxPath` | null | Panorama path under StreamingAssets; takes precedence over `nightSky`. Uses Choice's legacy image loader. |
| `nightSky` | null | Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when `skyboxPath` is empty. Omitted/disabled restores the scene background. |
| `windSpeed` | 0 | Nonnegative world units/s, independent of FicTrac position gain. Zero disables drift. |
| `windDirection` | 0 | Degrees wind comes **FROM**: 0 from +Z, 90 from +X. Motion is opposite. |
| `aglHeight` | 0 | Height above ground in world units. Zero disables height regulation. Examples: 10, 100, 1000. |
| `groundLayerMask` | 1 | Physics bit mask (1 = Default layer), not a layer index. Select only ground/terrain layers. |

`groundLayerMask` tells the migration AGL raycast which Unity physics layers count as ground. It is a bit mask: layer `n` contributes `1 << n`, so `1` selects the built-in **Default** layer, while a custom layer number 8 would use `256`. In the Inspector, use the layer mask picker; in JSON, add the corresponding bit values together. The raycast goes downward at the animal's X/Z position. If the mask excludes the terrain or collider, no ground is found and the current Y position is preserved. Exclude locusts, trees, and other movable objects so AGL cannot lock onto an animal.

Wind/AGL support is adapted from `origin/BogongAustralia` and `origin/smrmah-optomotor-updated`. AGL samples a matching Unity Terrain at the current X/Z; otherwise it raycasts ground colliders. It includes the terrain's world offset. No ground hit preserves current Y. Wind/AGL continue without FicTrac packets, and turning both to zero disables this work. Use a ground-only mask to avoid sampling trees/animals. Each new Choice config resets these optional settings, including in-scene transitions.

A Choice terrain scene can use `nightSky` without spawning objects. Start it through ControlScene so the sequence's `parameters.configFile` is loaded; with `autoStart: false`, press Start after entering Play mode. Choice uses boolean tracking switches (`true`/`false`). Swarm fields such as `spawnVolumeSize`, `mu` and `bogongVisual` do not spawn agents in a Choice scene.

Exact historical inputs are in [config-history](config-history). They reference `Choice_Bogong` and `Photosphere/LSM.png`, which are not guaranteed here. The runnable examples use the existing Choice environment. SmrMah examples are provided with their branch identity; no remote branch actually named Deathhead was found. The Bogong Kinefly yaw-rate path and its sequence gain are restored; see [gain compatibility](bogong-gain-compatibility.md).

### Choice object fields

Each entry uses prefab/material names registered in that scene controller's Inspector arrays; an arbitrary filename does not automatically load a prefab. Unknown prefab names are skipped. For a given stimulus, irrelevant component-specific fields have no effect.

| Field | Default if omitted | Meaning / reasonable example |
|---|---|---|
| `type` | null | Required prefab name, e.g. `tree01` or `LocustBand_black`. |
| `position` | null | World zero if absent; position object described below. |
| `scale` | null | Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use `{x:1,y:1,z:1}` for unit scale. |
| `rotation` | null | Nullable Euler x/y/z degrees; all three present overrides `mu`. |
| `material` | null | Registered material name; empty keeps prefab material. |
| `flip` | false | Negate X scale. |
| `speed` | 0 | cm/s on LocustMover/band movement components; example 2. |
| `mu` | 0 | Mean/explicit yaw in degrees. |
| `visualAngleDegrees` | 0 | Angular size for prefabs with ScaleWithDistance; example 10. |
| `meanBlueA`, `meanBlueB` | 0, 0 | Blue-channel means for supported color-drift stimuli; normalized 0–1. |
| `switchInterval` | 0 | Color switching period, seconds; set positive for switching. |
| `numberOfInstances` | 0 | Band population; example 32. |
| `spawnLengthX`, `spawnLengthZ` | 0, 0 | Band spawn extent in world units; example 100 each. |
| `gridType` | 0 | 0 Hexagonal, 1 Manhattan, 2 Random. |
| `kappa` | 0 | Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned. |
| `visibleOffDuration`, `visibleOnDuration` | 0, 0 | Band invisibility/visibility durations in seconds; example 0/10. |
| `boundaryLengthX`, `boundaryLengthZ` | 0, 0 | Periodic arena lengths in world units; example 200 each. |
| `lockBoundaryWithAnimalPosition` | false | Translate band boundary with tracked animal. |
| `lockAgentWithAnimalPosition` | false | Translate agents with their parent animal. |
| `prioritizeNumbers` | false | Prefer requested count over geometric grid filling. |
| `hexRadius` | 0 | Band grid spacing parameter in world units; example 10. |
| `sectionLengthX`, `sectionLengthZ` | 0, 0 | Band grid section dimensions; example 100 each. |
| `rotationAngle` | 0 | Band layout rotation in degrees. |

`position.radius`, `.angle`, `.height` default to zero. Supply all Cartesian `position.x/y/z` to override polar positioning; these three default to null. Partial Cartesian coordinates fall back to the polar position. Explicit rotation likewise needs all three values. The full Choice template includes nullable alternatives for reference; don't set both conventions unless the override is intentional.

## Swarm, Kannadi, and kinematics

Swarm/Bogong and Kannadi accept optional `nightSky` settings and a top-level `skyboxPath` override. See [night-sky.md](night-sky.md) for location, local date/time, 30-minute updates, physical north alignment, and saved images/IDs. The Bogong template enables this feature; omission preserves the scene background.

Templates: `swarm.template.json`, `kannadi.template.json`, `kinematic.template.json`. The kinematic template uses the **Kannadi parser** and a single tile per rig (`numberOfRings:0`); there is no separate Kinematic scene or magic kinematic filename.

| Shared movement field | Default | Meaning |
|---|---|---|
| `closedLoopPosition` | null → 1 | Nonnegative translation multiplier. Booleans remain accepted: true=1, false=0. |
| `closedLoopOrientation` | null → 1 | Nonnegative **angular multiplier**: 1 means one world degree per sensor degree; 0 disables turning, 0.5 halves yaw, 2 doubles it. No turn-speed cap or smoothing. |
| `animateOnMove` | false | Enable leg-animation gating; templates set true. |
| `animationSpeedThreshold` | 0.5 | cm/s planar translation; animate only strictly above threshold. Rotation alone does not animate. |
| `autopilotEnabled` | false | Constant forward motion for walking/Swarm/Kannadi templates. Migration/flying templates set this to true. Ctrl+Space toggles it during a run. |
| `autopilotSpeed` | 10 | Constant forward speed in world units/second; cm/s in the current walking scale. |
| `vrConfigs` | null | Optional per-rig pose/gain overrides below. |
| `animationNoiseThreshold` | legacy alias | Used only when `animationSpeedThreshold` is absent. Prefer the canonical name. |

The Swarm/Kannadi walking path uses **unit gain by default for both translation and yaw**. Explicit numeric fields override those defaults; per-rig values take precedence over global values. Walking gain operates on displacement/angle, independent of render FPS. The former locust turn-speed interpretation has been removed. Walking yaw differences use the shortest signed angle across 0/360; the input must not turn more than 180 degrees between consumed samples to avoid angular aliasing.

Kinefly explicitly selected in the system config uses the historical raw-radian yaw-rate path instead. The numeric Swarm/Kannadi gains only act as enabled/disabled switches there, and the sequence's `gain` controls Kinefly yaw. Ordinary Choice follows the historical Bogong tracking path.

`vrConfigs[]`: `vrIndex` is required (1–4, unique); `initialPosition` and `initialRotation` are optional `{x,y,z}` world position/Euler degrees and otherwise keep current pose. Nullable `closedLoopPosition`/`closedLoopOrientation` override global gains. `watchIndex` is optional 1–4: in Kannadi it selects that animal's replica layer; omit/null to see the other animals. Self-view excludes its own central tile. Swarm does not use `watchIndex`.

| Kannadi field | Omitted behavior | Meaning |
|---|---|---|
| `numberOfRings` | Keep scene/current value (3 initially) | Hex rings: 0=one tile, 1=7, 3=37 per rig. Negative disables replicas. Maximum 100. |
| `hexRadius` | Keep scene/current value (10 initially) | Positive grid spacing in world units. |
| `spacing` | null | Legacy alias, overridden by hexRadius. |
| `kannadiTilePrefab` | Keep scene prefab | Catalog name, e.g. SimulatedLocust or LocustBand_black. Catalog is a build-safe Resources asset. |
| `periodicBoundary` | true | Wrap animal and replicas in X/Z. |
| `boundaryLengthX`, `boundaryLengthZ` | 200, 200 | Positive world-unit arena extents, centered at zero. |
| `backgroundColor` | null | Optional normalized RGBA background. |

Kannadi replicas use the tracked source's translation speed for animation. Wrapped displacement is measured across the short edge; teleport-sized trail jumps break rather than draw across the whole arena. Bands mirror their source and do not independently walk away.

| Swarm field | Omitted behavior | Meaning / template |
|---|---|---|
| `numberOfLocusts` | Keep scene/current value (128 per rig in shipped scene) | Population **per rig**; template 256 means 1024 total. |
| `density` | null | Optional agents per world-unit area (2D) or volume (3D). Used only when `numberOfLocusts` is omitted; count is rounded to the nearest whole agent. |
| `spawnAreaSize` | Keep scene/current value (200) | Square spawn extent in world units. |
| `mu` | Keep scene/current value (0) | Mean heading degrees; 0=+Z. |
| `kappa` | Keep scene/current value (10000) | Von Mises concentration; >=10000 uses exactly mu, 0 is uniform. Template 100000. |
| `locustSpeed` | Keep scene/current value (2) | cm/s; template 2. |
| `dimension` | `2D` | `2D` preserves the historical X/Z swarm. `3D` samples a volume and can wrap Y. |
| `spawnCenter` | Spawner X/Z and legacy Y | Optional `{x,y,z}` center of the spawn volume. In 2D only X/Z are used. |
| `spawnVolumeSize` | A square; 3D uses a cube | Positive `{x,y,z}` volume dimensions. Omit for `spawnAreaSize` in every dimension. |
| `muElevation` | 0 | Unity X pitch in degrees: negative tilts upward, positive downward; ignored in 2D. |
| `kappaElevation` | 10000 | 3D elevation concentration; 0 is uniform, >=10000 is fixed. |
| `boundaryHeight` | 0 | 3D periodic Y extent. Required only when `wrapY` is true; otherwise Y is not wrapped. |
| `wrapY` | false | Enable periodic Y wrapping for a 3D swarm. |
| `agentVisual` | `ScenePrefab` | Existing prefab, or `Bogong` for an opaque, unlit circular patch. |
| `bogongVisual` | World-size circle, 0.5 units, dark gray | `sizeMode` is `World` (diameter `size`) or `Angular` (diameter `angularSizeDegrees`, 0–180 exclusive). Both draw a camera-facing circular patch of solid `color` RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. `flickerFrequencyHz` 0 disables flicker; `flickerDutyCycle` is the visible fraction. |

Bogong angular size is resolved separately for each rendering camera before culling, so an overhead view or another rig cannot change the panel stimulus size. Movement heading is unchanged by the camera-facing patch. For a 90° square perspective camera, center-screen diameter in pixels is `panelWidth × tan(angularSizeDegrees / 2)` (angle in degrees). A 2° dot is about **1.12 pixels across at 64×64**, or **2.23 pixels across at 128×128**; filled pixel area differs from diameter. The 64-pixel center has about 1.79° per pixel, with a finer angular pitch toward the edges. Filled-pixel counts depend on position and subpixel alignment; very small dots can fall between pixel centers. There is no minimum-pixel clamp or brightness blending. The active `system_config.json` currently requests 128×128 panels; changing the physical panel resolution requires matching that config.

The historical 2D swarm keeps its scene-owned boundary manager and prefab assignments. A 3D swarm uses `boundaryLengthX`, `boundaryLengthZ`, and `boundaryHeight` from the individual file when `wrapY` is enabled. Kannadi's ring/prefab/watch fields do not configure Swarm geometry. Swarm parameters rebuild the population on each in-scene step. Density is count divided by spawn area squared in 2D, or by the volume in 3D. The active sequence files reference individual Swarm config files; older inline sequence fields remain accepted as a compatibility fallback.

## Optomotor

Template: `optomotor.template.json`. `Examples/Optomotor/frequency-speed-sweep.example.json` preserves the frequency/speed sweep from `origin/Optomotor`. The Optomotor scene is enabled in the build list.

| Field | Default | Meaning |
|---|---|---|
| `loop` | false | Repeat stimulus list internally. |
| `stimuli` | empty array | Required nonempty stimulus list. |
| `stimuli[].duration` | 10 | Seconds per stimulus. |
| `speed` | 20 | Drum angular speed, degrees/s. Use nonnegative values and `clockwise` for sign. |
| `clockwise` | true | Positive rotation around the selected local axis when true; negative when false. |
| `rotationAxis` | `Yaw` | Case-sensitive Yaw=Y, Pitch=X, Roll=Z; unknown values fall back to Yaw. |
| `frequency` | 4 | Cycles per complete revolution, not Hz. A 20°/s, 4-cycle stimulus has temporal frequency 20×4/360 Hz. |
| `contrast` | 0.5 | 0–1; at >=0.99 the texture becomes discrete rather than sinusoidal. |
| `dutyCycle` | 0.5 | Fraction in color2 for discrete grating; used at contrast >=0.99. |
| `color1`, `color2` | `#000000`, `#FFFFFF` | Hex grating endpoints. |
| `closedLoopOrientation`, `closedLoopPosition` | false, false | Boolean tracking switches for the animal. |

The outer sequence duration and internal sum of stimulus durations are distinct. Use enough outer time for the desired stimulus list. R also resets drum orientation; Space or backslash pauses/resumes drum rotation. These debug keys are not a replacement for logged experimental timing.

## Dynamic Choice design (Choice_desync)

Template: `dynamic-choice.template.json`, loaded via `parameters.design`. This schema is different from ordinary Choice. The controller maintains an internal sequence for each rig and shuffles each repetition. Outer sequence time ends the scene. Its Inspector `loopSequence` defaults true and controls repetition after exhausting the list; there is no JSON `loop` field here.

| Design field | Default | Meaning |
|---|---|---|
| `seed` | -1 | Shuffle seed; negative uses time. Example 42 for initial reproducibility; later controller loops reseed using time. |
| `repetitions` | 1 | Number of shuffled repetitions before reaching the end. |
| `steps` | null | Array of steps. |
| `intertrial` | null | Optional step inserted before each trial. Same schema as a step. |
| `adaptiveDecision` | null | Optional adaptive staircase below. |
| `sync` | true | Legacy serialized field; currently not consulted by execution. Do not infer rig synchronization from it. |
| `size`, `boxSize` at design root | 1.5, null | Legacy fields not consulted by trigger construction; use trigger fields. |

| Step field | Default | Meaning |
|---|---|---|
| `name` | empty | Logged step label; keep names unique and avoid CSV delimiters. |
| `trigger` | null | Null advances immediately; explicit time or area trigger is recommended. |
| `objects` | null | Array of dynamic objects below. |
| `camera` | null | Array of `{vrId,clearFlags,bgColor}`. clearFlags defaults SolidColor; bgColor is [r,g,b,a] (alpha optional). Changes clear/background, never system camera order. |
| `skybox` | null | Material name loaded by Resources.Load<Material>. This differs from Choice's panorama file path. |
| `closedLoopPosition`, `closedLoopOrientation` | false | Per-step tracking switches. |
| `initialPosition`, `initialRotation` | (0,0,0) | World position/Euler degrees applied on each rig's step entry. |
| `randomInitialRotation` | false | Random yaw on reset. |
| `swapAfterSeconds` | 0 | Positive delay before swapping object colors; 0 disables. |
| `resetVR` | null | Legacy serialized list; currently resets the executing rig regardless of this list. |

| Trigger field | Default | Meaning |
|---|---|---|
| `type` | null | `time` waits seconds; use `area` for collider-based trials. Other values take the area path. |
| `seconds` | 0 | Time trigger duration. |
| `areaTag` | null | Tag assigned to trigger objects (fallback Untagged). |
| `vrId` | null | Rig allowed to trigger; null/`any` means the executing rig. |
| `shape` | `box` | `box` or `cylinder` (capsule collider). |
| `size` | 1.5 | Default box size in world units, with minimum 0.5. |
| `boxSize` | null | Explicit [x,y,z] trigger dimensions overriding size. |
| `radius`, `height` | 0, 0 | Cylinder dimensions in world units; controller applies fallback/minimum geometry. |
| `timeoutSeconds` | 0 | Positive timeout, otherwise wait indefinitely for area event. |
| `triggerOnExit` | false | Use exit instead of entry. |
| `advanceOnTrigger` | true | false resets the rig on contact and keeps waiting for timeout. |

| Dynamic object field | Default | Meaning |
|---|---|---|
| `type` | null | Registered prefab name (fallback alias `prefab`). |
| `polar` | null | `{radius,angle,height}` as above, defaults zero for each scalar. Takes precedence over Cartesian `pos`. |
| `pos` | null | Legacy [x,y,z], fallback zero. |
| `scale` | null | `{x,y,z}` multipliers; null preserves prefab scale. |
| `material` | null | Registered name (alias `mat`). |
| `color`, `swapColor` | null | Normalized [r,g,b,a]; RGB allowed. Explicit swapColor sets swap target. Otherwise first two colors exchange. |
| `flip` | false | Negate X scale. |
| `visualAngleDegrees` | 0 | ScaleWithDistance angular width. |
| `randomInitialRotation` | false | Random object yaw. |
| `mu` | 0 | Object yaw in degrees when not random. |
| `rot` | null | Legacy serialized array, currently ignored. Use mu for yaw. |
| `role` | null | `black` or `gray` for adaptive decisions. |

`adaptiveDecision`: `enabled=false`, `startGray=0.5` (normalized intensity), `grayStep=0.05` (intensity change), `controlEvery=5` (trial count), `noBarControlSeconds=20` (seconds). When enabled, the controller generates adaptive trial/control steps and logs the starting gray/side. The ordinary steps list is not used as the adaptive schedule.

## Replay and historical configuration limits

Replay uses the Control UI and ReplayController inspector fields, not a standalone JSON motion config. Select a run directory and its data files. The environment loader can use a saved dynamic design, including step names, object names, polar positions, scales, materials, and camera backgrounds. Operator bindings are in [Unity 6 controls](unity6-controls.md); data requirements are in [data formats](data-formats.md).

Older files in `StreamingAssets/Archive/Legacy` and `Assets/StreamingAssets/Archive/LegacyLocustInputs` are retained research inputs, not the authoritative template library. Read the modern parser before reusing an old parameter: a silently ignored `rot`, `sync`, or unsupported tracking setting will not produce the intended experiment.

## Heading-relative presentations, telemetry and status

Experiment files can contain `preStimulus`, `stimulus` and `postStimulus`, each using its scene schema plus `enabled` and `durationSeconds`. Flat files remain compatible. Heading reference is measured during visible pre-stimulus conditions, with an optional start offset; spawners opt into it separately with `useHeadingReference` (default false). Position and rotation resets are independently configurable, both default true. Choice also accepts an embedded `swarm` and `uniformSkyColor`. See [experiment phases](experiment-phases.md) for timing, schema, supported controllers and the active eight-minute Selwyn example, and [telemetry](heading-reference-telemetry.md) for wire fields.

System config accepts `telemetry` (defaults: enabled, loopback port 9880, 20 Hz, topic `matrex.telemetry.v1`) and `statusOverlay` (visible on display 0). The same reference documents every field. **Tab** restores the bottom-right panel; errors reopen it automatically.
