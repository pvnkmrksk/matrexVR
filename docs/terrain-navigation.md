# Config-driven terrain experiments

Run the existing `navrug_rr`, `navrug_rs`, `navrug_sr`, and `navrug_ss` scenes through `ControlScene` and the normal sequence controller. There are no terrain-condition GUI controls. All experiment settings come from JSON.

**One Unity unit = one centimeter.** The default camera-rig clearance is **1 cm vertically above the navigation surface**, not 0.01 units and not a distance along the surface normal.

The active `sequenceConfig.json` currently runs the full 16-mode sweep with `autoStart: true`: open **ControlScene** and press **Play**. Each mode lasts 30 seconds; the eight-minute cycle repeats. Set top-level `autoStart` to `false` to return to manual sequence startup.

## Four ready-to-run conditions

The first scene suffix letter describes **looks**, the second describes **feels**: `r` = rough, `s` = smooth. “Feels” here means the camera-rig trajectory and tilt, not a force-feedback actuator.

| Condition | Scene | Choice config in `Assets/StreamingAssets/` | Rig behavior |
| --- | --- | --- | --- |
| Looks rough, feels rough | `navrug_rr` | `choice_navrug_looks_rough_feels_rough.json` | Follows rough ground height and normal |
| Looks rough, feels smooth | `navrug_rs` | `choice_navrug_looks_rough_feels_smooth.json` | Follows a hidden flat surface; stays level |
| Looks smooth, feels rough | `navrug_sr` | `choice_navrug_looks_smooth_feels_rough.json` | Flat visible terrain; follows hidden rough height and normal |
| Looks smooth, feels smooth | `navrug_ss` | `choice_navrug_looks_smooth_feels_smooth.json` | Flat visible and navigation surfaces; stays level |

The four conditions are listed in **`sequenceConfig_terrainNavigation.json`**, 30 seconds each, sequential order, looping enabled. Each entry names its choice config explicitly; the scene name is not the only source of the condition settings.

### Run the four-condition sequence

1. Back up your current `Assets/StreamingAssets/sequenceConfig.json`.
2. Copy `Assets/StreamingAssets/sequenceConfig_terrainNavigation.json` to `Assets/StreamingAssets/sequenceConfig.json`.
3. Open `Assets/Scenes/ControlScene.unity`, enter Play mode, and use the existing **Start** sequence control.
4. Use the normal FicTrac input. Keyboard controls, where enabled on the existing rigs, remain arrow keys for translation and A/D for yaw.

Starting a `navrug` scene directly does **not** load a named choice config; use the sequence path above for reproducible conditions. The previous active sequence is saved as `sequenceConfig_before_terrain_*.json`. Automated tests do not overwrite the active config.

## How the files fit together

`sequenceConfig.json` selects the scene, duration and choice file. The choice file supplies the condition's world and movement settings plus the existing choice-scene objects, starting pose, background and closed-loop flags.

An entry for **looks rough, feels smooth**:

```json
{
  "sceneName": "navrug_rs",
  "duration": 30,
  "reloadScene": true,
  "parameters": {
    "configFile": "choice_navrug_looks_rough_feels_smooth.json"
  }
}
```

The referenced choice file is a complete runnable file:

```json
{
  "objects": [],
  "closedLoopOrientation": true,
  "closedLoopPosition": true,
  "initialPosition": { "x": 0, "y": 1, "z": 0 },
  "initialRotation": { "x": 0, "y": 0, "z": 0 },
  "randomInitialRotation": false,
  "backgroundColor": { "r": 0.8, "g": 0.8, "b": 0.8, "a": 1 },
  "terrainWorld": {
    "flatAppearance": false,
    "flatNavigation": true,
    "flatHeightCm": 0.16480498
  },
  "terrainNavigation": {
    "heightMode": "AboveGround",
    "orientationMode": "HeightOnly",
    "heightAboveGroundCm": 1,
    "absoluteHeightCm": 1.16480498,
    "normalSampleRadiusCm": 0.5,
    "alignmentSpeed": 12
  }
}
```

`objects: []` means no additional choice targets; the forest is supplied by the scene. Add objects using the existing choice-config schema when needed. Terrain following determines final Y after the initial pose is applied.

## Height and orientation are independent

| Field under `terrainNavigation` | Values / units | Meaning |
| --- | --- | --- |
| `heightMode` | `AboveGround`, `Absolute` | Navigation ground Y + clearance, or fixed world Y |
| `heightAboveGroundCm` | cm; default 1 | Vertical clearance in `AboveGround` mode |
| `absoluteHeightCm` | world Y in cm | Fixed elevation in `Absolute` mode; no automatic capture |
| `orientationMode` | `HeightOnly`, `PerpendicularToNormal` | Level with yaw steering, or rig up aligned to navigation normal |
| `normalSampleRadiusCm` | cm; default 0.5 | Radius for averaging the local ground normal |
| `alignmentSpeed` | inverse seconds; default 12 | Exponential tilt response; 0 snaps immediately |

`HeightOnly` is the legacy enum name for **level orientation**, even when combined with `Absolute` height. Yaw always remains steerable. Height correction is immediate so AGL clearance remains exact.

For each visible/navigation surface pair, the four motion combinations are:

| Height | Orientation | Result over rough navigation terrain |
| --- | --- | --- |
| `AboveGround` | `PerpendicularToNormal` | Moves up/down and tilts |
| `AboveGround` | `HeightOnly` | Moves up/down but remains level |
| `Absolute` | `PerpendicularToNormal` | Fixed world height but tilts |
| `Absolute` | `HeightOnly` | Fixed world height and remains level |

**`sequenceConfig_terrainNavigation_allModes.json`** runs all 16 combinations (four surface pairs × four motion modes). It sets `alignmentSpeed: 0` for immediate orientation comparisons. Within each scene it uses `reloadScene: false`, exercising in-place configuration changes. The first entry in each scene group reloads that scene.

A sequence can override only the fields needed for a trial:

```json
{
  "sceneName": "navrug_sr",
  "duration": 30,
  "reloadScene": false,
  "parameters": {
    "configFile": "choice_navrug_looks_smooth_feels_rough.json",
    "terrainNavigation": {
      "heightMode": "Absolute",
      "absoluteHeightCm": 5,
      "orientationMode": "PerpendicularToNormal"
    }
  }
}
```

## Precedence and resets

Navigation fields are merged individually in this order, later values winning:

1. Scene defaults.
2. Optional `terrainNavigation` object in that rig's `system_config.json` entry.
3. The selected choice file's `terrainNavigation` object.
4. Sequence `parameters.terrainNavigation` overrides.

For example, `"terrainNavigation": { "heightAboveGroundCm": 1.5 }` inside a VR system entry sets that rig's baseline clearance. The included choice examples explicitly set 1 cm; remove their clearance field if you want the system baseline to apply instead.

`terrainWorld` is shared by all four rigs: scene defaults → choice file → sequence `parameters.terrainWorld`. Its fields are `flatAppearance`, `flatNavigation`, and `flatHeightCm` (local terrain Y in cm). Every trial resolves from defaults again; settings do not leak from previous trials. This also applies to `reloadScene: false` transitions.

## Surfaces and boundaries

The four scenes' original TerrainData references were missing from this checkout. They now reference the checked-in `NatureStarterKit2/Scene/TerrainData_All.asset`, centered so the origin spawn is inside its 250 × 250 cm footprint. Flat variants are runtime copies; the source asset is not edited. The flat height in the examples matches the forest's height at the origin.

The navigation sampler reads its selected heightmap directly. Trees, props and the visible surface cannot become accidental navigation ground. Movement outside the footprint or into a rough-terrain hole retains the last valid position. Existing periodic boundaries run before the terrain correction.

A mismatched visible and navigation surface can intersect the camera; fixed absolute height can also intersect visible hills. This is inherent in separating the two surfaces. The implementation does not silently adjust the configured height against visible terrain.

## Automated tests on the actual navrug scenes

Close the interactive editor before running another Unity instance on this project. From the repository root:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -executeMethod TerrainNavigationPlayValidation.Run \
  -logFile /tmp/ledpanel-navrug-sequences.log
```

Do **not** add `-quit`: the Play-mode test exits with code 0 on success, 1 on failure. It loads both checked-in sequence examples through `MainController.LoadSequenceConfiguration`, starts the normal sequence controller, and exercises all four actual `navrug` scenes with their four rigs. Only test durations are shortened in memory. It verifies choice defaults, partial sequence overrides, appearance/navigation data, actual LateUpdate height/orientation, scene reloads and in-place transitions. Deterministic XZ movement supplies test input; live FicTrac hardware is not required or validated. Report: `/tmp/ledpanel-terrain-play-validation.txt`.

The separate numerical slope/boundary checks remain available:

```sh
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod TerrainNavigationValidation.Run \
  -logFile /tmp/ledpanel-terrain-unity.log
```

## Recorded validation

Validated with Unity 6000.3.16f1 on macOS on September 21, 2026:

- Actual `navrug_rr`, `navrug_rs`, `navrug_sr`, and `navrug_ss` scenes: 20 conditions (four presets plus the 16-mode sweep), 7,816 rig-pose checks, including in-place transitions and scene reloads. No captured runtime errors in the final run.
- Numerical regression checks: ground clearance, fixed height, normal alignment, heading preservation, bounds, configuration reset and scene references passed.
- Automatic startup: entering Play mode in `ControlScene` loaded the active 16-condition sequence and entered `navrug_rr` without pressing Start.

These checks validate runtime behavior and scene wiring. They do not validate physical FicTrac hardware or the LED-panel display calibration. The Unity/package/TextMesh Pro upgrades already present locally are separate from the terrain implementation commits.

The original follower script metadata GUID is preserved by renaming `AntWalker.cs` to `TerrainOrientationUpdater.cs`. Missing unused choice-prefab references are skipped during initialization. ZMQ socket creation, receive and disposal now stay on the listener thread, preventing the shutdown race observed during repeated scene reloads.
