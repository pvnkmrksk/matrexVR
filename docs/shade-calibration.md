# Shade calibration build

This standalone build shows a spatially uniform measured RGB value on exactly one VR cube. Every panel of the other three cubes is held at RGB `(0, 0, 0)`. The operator label is positioned in the unused top strip, outside the configured LED-panel rectangles.

## Controls

- Arrow keys, Page Up/Down, or `+`/`-`: cycle through the nine shades
- `1`–`4`: select the active VR cube
- `Tab`: select the next VR cube
- `0`: jump directly to the pure-black control
- `B`: jump directly to the measured background
- `Esc`: quit

The current cube, shade name, index, hex value, and RGB value are always shown in the operator label.

## Measured values

The values in `Assets/StreamingAssets/shade_calibration.json` come from `greyCylinerSampler_withbg.csv`. The JSON contains the seven cylinder medians, the measured background, and an additional pure-black control. It also contains the four panel layouts copied from `system_config.json`, so the calibration executable is isolated from the normal experiment sequence.

## Build and run

Build from Unity with **Tools > Shade Calibration > Build Linux**, or in batch mode:

```bash
/home/flyvr01/Unity/Hub/Editor/2022.3.58f1/Editor/Unity \
  -batchmode -quit -projectPath /home/flyvr01/src/matrexVR \
  -executeMethod ShadeCalibrationBuild.BuildLinux
```

Run:

```bash
./Builds/ShadeCalibration/ShadeCalibration.x86_64
```
