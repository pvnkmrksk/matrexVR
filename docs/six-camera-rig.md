# Six camera directions

The shared `Assets/Prefabs/Camera Prefabs/VR.prefab` now contains Front, Back, Left, Right, Up and Down. Standard VR1–VR4, Swarm and Kannadi variants inherit these faces. The older Cube variants inherit Down instead of adding a duplicate. ControlScene's historical removal overrides for Up have been removed.

`ViewportSetter` activates exactly the directions listed in the system config. All enabled faces use a 90° vertical field of view. Up/Down use Front's camera origin, near/far clip planes and culling mask, including Kannadi's eye-height offset and each rig's population layers. They keep their ±90° pitch. Four-face `RBLF` configurations still work and leave Up/Down disabled.

## Enable all six

Copy `Assets/StreamingAssets/Examples/System/fictrac-six-cameras.example.json` to `Assets/StreamingAssets/system_config.json`, then set tracker addresses and ports for the rig. The template uses display 0 and four rows of six 128×128 panels: **768×512 output pixels**. The order is **Down, Right, Back, Left, Front, Up** (`DRBLFU`). The local hardware config is ignored by Git; the template is versioned.

For an existing config, set each required rig's `displayOrder` to `DRBLFU` and allocate six panel slots without overlap. The template uses `startCol: 0`, `startRow: 0/1/2/3`, and `horizontal: true`. Face letters select rendering; they do not change experiment movement or sensor logging.

| Face | Rig-local viewing direction | Rotation relative to Front |
|---|---|---|
| F | +Z | Identity |
| B | −Z | Yaw 180° |
| R | +X | Yaw +90° |
| L | −X | Yaw −90° |
| U | +Y | Pitch −90° |
| D | −Y | Pitch +90° |

North is +Z for the astronomical sky when `northYawDegrees` is zero. The night-sky horizon mask makes the lower celestial hemisphere black; the scene's ground can still appear in Down.

## Validation

Use Unity 6000.3.16f1 with graphics enabled on a disposable project copy containing `KANNADI_VALIDATION_COPY`:

```text
Unity -batchmode -projectPath <copy> -executeMethod SixCameraValidation.Run -logFile <log>
```

The check loads the base rig and fourteen variants, verifies unique faces, renders a direction marker through each standard camera, checks origins and layer masks, and tests six-to-four-face reconfiguration. It also covers the actual ControlScene, Choice, Swarm and Kannadi scene instances. `KannadiValidation.Run` covers the broader sequence and runtime behavior; `NightSkyValidation.RunControlPreview` verifies the live sky and Bogong preview with the local system layout.
