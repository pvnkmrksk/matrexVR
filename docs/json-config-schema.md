# JSON Config Schema

This document covers the runtime JSON files used by `MainController`, `ChoiceController`, and `OptomotorSceneController`.

Template files:
- `Assets/StreamingAssets/sequenceConfig_template.json`
- `Assets/StreamingAssets/system_config_template.json`
- `Assets/StreamingAssets/Choice_template_full.json`
- `Assets/StreamingAssets/optomotor_config_template.json`

## Sequence Config

Path used at runtime:
- `Assets/StreamingAssets/sequenceConfig.json`

Top-level fields:
- `randomise`: `bool`
- `loop`: `bool`
- `sequences`: array of sequence items

Sequence item fields:
- `sceneName`: scene to load
- `duration`: seconds to run this step
- `parameters`: free-form dictionary passed to the scene controller
- `reloadScene`: `bool`, default `true`

Typical parameters:
- `configFile`: JSON file in `StreamingAssets` used by the scene controller

Notes:
- `ChoiceController` expects `parameters.configFile`
- `OptomotorSceneController` expects `parameters.configFile`
- If `reloadScene` is `false`, scenes that support in-scene sequencing can advance without reloading

## System Config

Default runtime path:
- `Assets/StreamingAssets/system_config.json`

Top-level fields:
- `targetDisplay`: global target display index
- `configs`: array of per-VR configs

Per-VR config fields:
- `sphereDiameter`: FicTrac sphere diameter in cm
- `ledPanelWidth`: panel width in pixels
- `ledPanelHeight`: panel height in pixels
- `startRow`: panel mapping start row
- `startCol`: panel mapping start column
- `horizontal`: panel ordering flag
- `zmqAddress`: FicTrac or pose source host
- `zmqPort`: FicTrac or pose source port
- `vrId`: identifier such as `VR1`
- `displayOrder`: viewport order string
- `targetDisplay`: class field exists, but runtime currently overrides this from the top-level `targetDisplay`

Notes:
- `MainController` loads the top-level `targetDisplay` first and assigns it to every loaded per-VR config

## Choice Config

Typical runtime path:
- any JSON referenced by `sequenceConfig.json`
- example: `Assets/StreamingAssets/Choice_template_full.json`

Top-level fields:
- `objects`: array of scene objects
- `closedLoopOrientation`: `bool`
- `closedLoopPosition`: `bool`
- `initialPosition`: `{ x, y, z }`
- `initialRotation`: `{ x, y, z }` Euler degrees
- `randomInitialRotation`: `bool`
- `backgroundColor`: `{ r, g, b, a }`
- `skyboxPath`: relative path inside `StreamingAssets`

Per-object fields:
- `type`: prefab name
- `position`: object position settings
- `material`: optional material name
- `scale`: `{ x, y, z }`
- `flip`: mirror by negative X scale
- `speed`: optional `LocustMover.speed`
- `mu`: Y rotation in degrees
- `visualAngleDegrees`: used by `ScaleWithDistance`
- `meanBlueA`
- `meanBlueB`
- `switchInterval`
- `numberOfInstances`
- `spawnLengthX`
- `spawnLengthZ`
- `gridType`
- `kappa`
- `visibleOffDuration`
- `visibleOnDuration`
- `boundaryLengthZ`
- `boundaryLengthX`
- `lockBoundaryWithAnimalPosition`
- `lockAgentWithAnimalPosition`
- `prioritizeNumbers`
- `hexRadius`
- `sectionLengthZ`
- `sectionLengthX`
- `rotationAngle`

Position fields:
- Polar fields:
  - `radius`
  - `angle`
  - `height`
- Explicit Cartesian override fields:
  - `x`
  - `y`
  - `z`

Precedence:
- If `x`, `y`, and `z` are all provided, `ChoiceController` uses them directly
- Otherwise it falls back to polar conversion from `radius`, `angle`, and `height`

Notes:
- Use `null` for `x`, `y`, and `z` when you want polar positioning
- Band objects still use the same resolved position, but are instantiated once per VR rig

## Optomotor Config

Typical runtime path:
- any JSON referenced by `sequenceConfig.json`
- example: `Assets/StreamingAssets/optomotor_config_template.json`

Top-level fields:
- `loop`: `bool`
- `stimuli`: array of stimuli

Per-stimulus fields:
- `duration`: seconds
- `speed`: rotation speed
- `clockwise`: `bool`
- `rotationAxis`: usually `Yaw`, `Pitch`, or `Roll`
- `frequency`: grating frequency
- `contrast`: 0 to 1
- `dutyCycle`: 0 to 1
- `color1`: hex color string
- `color2`: hex color string
- `closedLoopOrientation`: `bool`
- `closedLoopPosition`: `bool`
