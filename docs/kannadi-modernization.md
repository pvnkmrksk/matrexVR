# Kannadi / Swarm modernization

The integration branch is `codex/kannadi-modernize`. It starts from `origin/currentlocustvr` (`60a7121`, 2025-11-18) and incorporates the full history of `origin/JuliusTree` (`1205fe3`, 2026-09-09). A merge keeps both histories rather than duplicating their shared ancestors with hundreds of cherry-picks. JuliusTree supplies shared infrastructure and unrelated experiments; locust assets and behavior come from currentlocustvr. The existing NatureStarterKit2 importer fix (`db13b13`) is cherry-picked separately.

## What is retained and integrated

- Restored the historical `Grey_505050` material and its original GUID, fixing missing material references that prevented JuliusTree's two-tree Choice controller from starting.
- JuliusTree's system configuration, ZMQ fresh-pose checks, replay, Choice/DynamicSequence experiments, and data logging, carried forward with the user's Unity 6000.3.16f1 upgrade and package changes.
- Locust models, swarm and band prefabs, Kannadi scene, per-rig initial poses, numeric tracking gains, self/peer viewing, and mouse-controlled overhead overview.
- Unity 6 TextMesh Pro essential resources are upgraded from the installed uGUI package while preserving asset GUIDs. Settings and fonts match the new importer version, so opening TMP text no longer asks for the old resource import.
- A single FPS counter persists at the top right across scenes and remains visible when the overview is hidden. It measures frame rate without changing VSync or frame limits.
- Kannadi is the sole mirror scene. The separate Matrix scene, wrapper, and prefab variants have been removed.
- `Swarm` and `Kannadi` are enabled in build settings. The branch's default sequence runs Swarm for 20 seconds, then Kannadi for 1000 seconds. Entrainment uses 256 locusts per rig over a 200 × 200 cm area, `mu: 0`, `kappa: 100000`, and speed 2 cm/s. This doubles the earlier population while keeping the arena size. At this kappa the existing spawner uses the exact requested heading. The former JuliusTree default is saved as `Assets/StreamingAssets/Kannadi/sequenceConfig_JuliusTree.json`.
- Old locust JSON and generation scripts are preserved in `docs/legacy-locust-inputs`. They are historical inputs, not validated modern Choice configurations. Their paths and schemas may need migration before reuse.

## Hardware configuration and startup

Open `/Users/pavan/src/ledpanelVR` as a Unity project and start `Assets/Scenes/ControlScene.unity` as usual. Use Unity 6000.3.16f1, matching the upgraded project version.

Copy `Assets/StreamingAssets/Kannadi/system_config.example.json` to the ignored local `Assets/StreamingAssets/system_config.json`, or retain your existing hardware file and add its `overheadCamera` section. Set the actual sphere diameters in **centimeters**, FicTrac addresses/ports, LED panel pixel dimensions, panel rows/columns, and display order for VR1–VR4. The example uses a 5 cm sphere and ports 9872–9875 as editable starting values, not a calibration.

The hardware file is applied to the existing `ViewportSetter`, `ZmqListener`, and `ClosedLoop` components on all four swarm/mirror rigs. Scene parameters determine the experiment, not the hardware calibration. For this checkout, the user-selected `system_config_VR1.json` is copied to the ignored `system_config.json`: four rigs, `RBLF`, 2.6 cm spheres, and display 0. The prior primary-checkout changes were saved in the Git stash named `Before switching primary checkout to Kannadi integration 2026-09-28` before switching branches.

### Camera allow-list

`displayOrder` is authoritative in every scene using `ViewportSetter`, including Swarm, Kannadi, Choice, and DynamicSequence. `RBLF` enables only those four cameras, in that order; Up and Down remain disabled even when serialized as enabled. Listed inactive cameras are activated. Empty, missing, or null orders disable all cameras on that rig. A missing rig entry also disables its cameras; it never borrows VR1's settings. Unavailable target displays disable stimulus cameras rather than routing them elsewhere.

Panel size, row, column, horizontal/vertical direction, and target display come from system config. Layouts refresh on config reload and window resize. Invalid and duplicate letters are ignored without consuming panel slots. Camera visibility masks remain the experiment's responsibility; they do not override the hardware allow-list. The operator overview is separate from the stimulus cameras.

## Experiment parameters

`Assets/StreamingAssets/Kannadi/multiplayer.json` is the runnable example. Sequence parameters can be inline or specify `configFile`; inline values override file values, and an inline `vrConfigs` array replaces the file's array.

| Field | Behavior |
| --- | --- |
| `numberOfRings` | Positive: `1 + 3r(r+1)` replicas per tracked animal. Zero: center only. Negative: no replicas. Self-view omits its own center. |
| `hexRadius` | Positive radius in world centimeters. Legacy `spacing` is accepted when `hexRadius` is absent. Kannadi grid spacing is retained. |
| `kannadiTilePrefab` | Name in `Assets/Resources/KannadiPrefabCatalog.asset`; supports individual locusts, bands, and registered choice shapes. The catalog has explicit references for standalone builds. |
| `boundaryLengthX`, `boundaryLengthZ` | Independent rectangular periodic dimensions, centered at world origin. Wrapping preserves overshoot. |
| `periodicBoundary` | Default `true`; set `false` for unbounded movement. |
| `closedLoopPosition` | Boolean or nonnegative numeric gain; translation is scaled by this and the calibrated sphere radius. |
| `closedLoopOrientation` | Boolean or nonnegative numeric value. In locust scenes this preserves the old convergence behavior: maximum turn rate is value × 360 degrees/second toward the aligned FicTrac heading. Zero disables turning. It is not an angular multiplier. |
| `vrConfigs` | Entries with `vrIndex` 1–4, optional `initialPosition`, `initialRotation` in degrees, `watchIndex`, and per-rig gain overrides. |
| `watchIndex` | Omitted: see the other tracked animals' layers. 1–4: see only that animal's replicas, while retaining the shared floor/background. Own index: see own replicas except the center. |
| `animateOnMove` | Boolean animation gate, enabled in the supplied Swarm and Kannadi configs. `false` restores continuous animation. |
| `animationSpeedThreshold` | Default `0.5` world units/second (cm/s). Walking animation runs only **above** the threshold. Measures planar translation of the root, not animated bones; rotation in place does not count. Legacy `animationNoiseThreshold` is accepted when the new field is absent. |

Both Swarm and Kannadi support `reloadScene: false` sequence steps. Reconfiguration replaces old agents instead of accumulating populations. Bands mirror the tracked rig; their members do not continue autonomous walking. Clone height comes from the selected prefab rather than a hard-coded offset.

For animation gating, Kannadi replicas and band members follow the tracked rig's translation; autonomous Swarm locusts use their own translation. Periodic wrapping is accounted for so crossing an arena boundary does not create a false speed spike. You can set either field inline in a sequence step or in its referenced experiment file:

```json
"animateOnMove": true,
"animationSpeedThreshold": 0.5
```

Both locust scenes use the existing `grass_05` dry green/brown grass texture through a dedicated unlit ground material. Its tile spans 20 world units (20 cm), providing ground optic flow independently of light placement.

## Operator overview

The overview sees all tracked animals and all virtual animal layers. Each active scene rig with a `ClosedLoop` component and a `VR<number>` name gets a colored heading arrow and just its number (`1`, `2`, etc.). Marker discovery runs twice per second and supports IDs beyond four; it never creates placeholder animals. Arrows are drawn in the operator UI, remain 14 pixels long as the camera zooms, and cannot appear in stimulus cameras. Arrows stay at the exact projected positions of the real VR game objects. Overlapping animals keep overlapping arrows and labels; there is no collision avoidance, displacement, or connecting leader line. This also applies in Swarm, where virtual locusts remain part of the scene image rather than receiving extra tracked-animal arrows.

A thin, faint trail shows the most recent 60 seconds and fades continuously with age. Set `trailDurationSeconds` to 30 for a shorter history, or 0 to hide trails. Hiding the overview disables both of its cameras and the tracking/mesh component, and releases the render texture. The header button and FPS readout remain available. Showing it recreates the render target and starts fresh history, avoiding a false line across an unobserved interval. Large jumps (over `trailBreakDistance` world centimeters between samples) break the path instead of drawing across a periodic wrap. History starts afresh when changing scenes. The overview, markers, and history are destroyed with the scene.

In `system_config.json`, `overheadCamera` supports:

```json
"overheadCamera": {
  "enabled": true,
  "targetDisplay": -1,
  "x": 0.58,
  "y": 0.02,
  "width": 0.4,
  "height": 0.4,
  "resolution": 512,
  "markerSizePixels": 14,
  "trailDurationSeconds": 60,
  "trailSampleInterval": 0.1,
  "trailWidthPixels": 1.5,
  "trailBreakDistance": 50
}
```

Coordinates are normalized with the origin at the bottom left; the default places a panel covering 40% of the screen width and height in the bottom-right area (about 78% more area than the previous 30% panel). `targetDisplay: -1` follows the system display; a nonnegative value selects an operator display. Adjust this rectangle to the unused area of your actual LED layout. The overview never changes the LED camera viewports.

Within the image, right-drag orbits, middle-drag pans, and the wheel zooms. **Reset view** restores the overhead framing; **Show / hide** toggles the image. Gestures started outside the image do not control this camera. The orbit transition from straight down is continuous.

## Logging

Kannadi writes one compressed clone CSV per rig, including positions, orientations, ring count, and radius. Filenames distinguish rapid scene reloads, and floating-point data uses invariant decimal formatting. Swarm retains its population logger. Referenced experiment JSON files in subdirectories are copied into the run log along with modern system/sequence records.

## Validation

Run the dependency/build-registration check with:

```sh
python3 tools/validate_kannadi_assets.py
```

`Assets/Editor/KannadiValidation.cs` is a Unity batch regression runner. **Use a disposable project copy** with `KANNADI_VALIDATION_COPY` at its root, `system_config_VR1.json` copied to `system_config.json`, and the branch's default sequence. It creates synthetic session data and disables live ZMQ input. Invoke Unity with `-batchmode -projectPath <copy> -executeMethod KannadiValidation.Run -logFile <log>`; do not pass `-quit`, because the runner exits after asynchronous Play Mode checks.

It covers heading orientation, constant arrow size across tenfold zoom-out, numeric labels, exact overlapping marker positions, hidden rendering and render-target release, persistent FPS, TMP resource compatibility, translation-only animation thresholds and periodic wrapping, dry-grass material references, dynamic discovery including a fifth rig numbered 12, history expiration and fading, discontinuity handling, the 20-second aligned Swarm configuration, strict camera inclusion, omitted/null orders, missing rigs, horizontal/vertical pixel placement, runtime configuration reloads, inactive cameras, missing displays, in-scene sequence changes, system settings on four rigs, Swarm → Kannadi → Choice → JuliusTree two-tree → dynamic sequence transitions, fresh/missing/stale tracking behavior, numeric translation, visibility masks, zero/negative rings, rectangular wrapping, prefab materials, band following, camera orbit, CSV creation, and overview cleanup. Results are written to `kannadi-validation.json`, with a rendered overview in `kannadi-overview.png`.

The recorded run passed **768 runtime assertions** with zero runtime failures. The static check passed all **84 Swarm/Kannadi scene/catalog dependency files**. See `kannadi-validation-results.json` and `kannadi-overview.png` alongside this document.

Validation uses a disposable Unity 6000.3.16f1 copy with the upgraded project's package files. Unity's unrelated editor search-cache exception is recorded separately in the test report. The overview screenshot includes synthetic trajectories to demonstrate the markers and fading; it is not experimental data. Physical LED/FicTrac calibration, network timing, and actual monitor mapping remain untested.
