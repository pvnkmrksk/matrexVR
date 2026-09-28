# Kannadi / Swarm modernization

The integration branch is `codex/kannadi-modernize`. It starts from `origin/currentlocustvr` (`60a7121`, 2025-11-18) and incorporates the full history of `origin/JuliusTree` (`1205fe3`, 2026-09-09). A merge keeps both histories rather than duplicating their shared ancestors with hundreds of cherry-picks. JuliusTree supplies shared infrastructure and unrelated experiments; locust assets and behavior come from currentlocustvr. The existing NatureStarterKit2 importer fix (`db13b13`) is cherry-picked separately.

## What is retained and integrated

- Restored the historical `Grey_505050` material and its original GUID, fixing missing material references that prevented JuliusTree's two-tree Choice controller from starting.
- JuliusTree's system configuration, ZMQ fresh-pose checks, replay, Choice/DynamicSequence experiments, data logging, packages, and Unity 2022.3.58f1 project version.
- Locust models, swarm and band prefabs, Kannadi scene, per-rig initial poses, numeric tracking gains, self/peer viewing, and mouse-controlled overhead overview.
- Kannadi is the sole mirror scene. The separate Matrix scene, wrapper, and prefab variants have been removed.
- `Swarm` and `Kannadi` are enabled in build settings. The branch's default sequence runs Swarm for 10 seconds, then Kannadi. The former JuliusTree default is saved as `Assets/StreamingAssets/Kannadi/sequenceConfig_JuliusTree.json`.
- Old locust JSON and generation scripts are preserved in `docs/legacy-locust-inputs`. They are historical inputs, not validated modern Choice configurations. Their paths and schemas may need migration before reuse.

## Hardware configuration and startup

Open `/Users/pavan/src/ledpanelVR` as a Unity project and start `Assets/Scenes/ControlScene.unity` as usual. Use Unity 2022.3.58f1 for the unchanged project version.

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
| `animateOnMove`, `animationNoiseThreshold` | Locust animation movement threshold, retaining the old setting. |

Both Swarm and Kannadi support `reloadScene: false` sequence steps. Reconfiguration replaces old agents instead of accumulating populations. Bands mirror the tracked rig; their members do not continue autonomous walking. Clone height comes from the selected prefab rather than a hard-coded offset.

## Operator overview

The overview sees all tracked animals and all virtual animal layers. Colored markers identify the four tracked rigs and are excluded from the animal-facing cameras. It is created in Swarm and Kannadi and destroyed with the scene.

In `system_config.json`, `overheadCamera` supports:

```json
"overheadCamera": {
  "enabled": true,
  "targetDisplay": -1,
  "x": 0.68,
  "y": 0.02,
  "width": 0.3,
  "height": 0.3,
  "resolution": 512
}
```

Coordinates are normalized with the origin at the bottom left; the default places the complete panel in the bottom-right area. `targetDisplay: -1` follows the system display; a nonnegative value selects an operator display. Adjust this rectangle to the unused area of your actual LED layout. The overview never changes the LED camera viewports.

Within the image, right-drag orbits, middle-drag pans, and the wheel zooms. **Reset view** restores the overhead framing; **Show / hide** toggles the image. Gestures started outside the image do not control this camera. The orbit transition from straight down is continuous.

## Logging

Kannadi writes one compressed clone CSV per rig, including positions, orientations, ring count, and radius. Filenames distinguish rapid scene reloads, and floating-point data uses invariant decimal formatting. Swarm retains its population logger. Referenced experiment JSON files in subdirectories are copied into the run log along with modern system/sequence records.

## Validation

Run the dependency/build-registration check with:

```sh
python3 tools/validate_kannadi_assets.py
```

`Assets/Editor/KannadiValidation.cs` is a Unity batch regression runner. **Use a disposable project copy** with `KANNADI_VALIDATION_COPY` at its root, `system_config_VR1.json` copied to `system_config.json`, and the branch's default sequence. It creates synthetic session data and disables live ZMQ input. Invoke Unity with `-batchmode -projectPath <copy> -executeMethod KannadiValidation.Run -logFile <log>`; do not pass `-quit`, because the runner exits after asynchronous Play Mode checks.

It covers strict camera inclusion, omitted/null orders, missing rigs, horizontal/vertical pixel placement, runtime configuration reloads, inactive cameras, missing displays, in-scene sequence changes, system settings on four rigs, Swarm → Kannadi → Choice → JuliusTree two-tree → dynamic sequence transitions, fresh/missing/stale tracking behavior, numeric translation, visibility masks, zero/negative rings, rectangular wrapping, prefab materials, band following, camera orbit, CSV creation, and overview cleanup. Results are written to `kannadi-validation.json`, with a rendered overview in `kannadi-overview.png`.

The recorded run passed **693 runtime assertions** with zero runtime failures. The static check passed all **84 Swarm/Kannadi scene/catalog dependency files**. See `kannadi-validation-results.json` and `kannadi-overview.png` alongside this document.

This machine only has Unity 6000.3.16f1. Validation uses a disposable Unity 6 copy with its installed package cache. The small TextMesh Pro UV compatibility fixes are included in the branch and retain compatibility with older UV types; the package and project versions remain those of JuliusTree. Unity's unrelated editor search-cache exception is recorded separately in the test report. Validation on Unity 2022.3 and on the physical four-animal LED/FicTrac system remains necessary; synthetic tracking does not validate calibration, network timing, or actual monitor mapping.
