# Yaw-Based Closed Loop Orientation Mode

## Overview

A new closed loop orientation mode has been added to the ClosedLoop component that allows for gain-scaled yaw input with a DC offset. This mode is particularly useful for experiments where you want to scale or bias the rotational response based on yaw movements.

## How It Works

The system has two modes controlled by the **Yaw Mode** setting:

### **Standard Mode** (`useYawMode = false`)
- **Closed Loop Orientation ON**: Uses delta-based rotation (original behavior)
  - Formula: `rotation = yaw_delta` 
- **Closed Loop Orientation OFF**: No rotation applied

### **Yaw Mode** (`useYawMode = true`)
- **Closed Loop Orientation ON**: Uses absolute yaw with gain and DC offset
  - Formula: `rotation = (gain * absolute_yaw) - dc_offset`
- **Closed Loop Orientation OFF**: No rotation applied (yaw mode disabled)

Where:
- `yaw_delta` is the change in yaw between frames (standard closed loop)
- `absolute_yaw` is the direct yaw value from FicTrac/ZMQ (in degrees)
- `gain` is a scaling factor (default: 1.0)
- `dc_offset` is a constant bias subtracted from the output (default: 0.0 degrees)

## Keyboard Controls

The following keyboard shortcuts are available during runtime:

| Key | Function |
|-----|----------|
| **Ctrl+Y** | Toggle yaw mode ON/OFF |
| **+** (or keypad +) | Increase gain by 0.1 |
| **-** (or keypad -) | Decrease gain by 0.1 |
| **]** | Increase DC offset by 0.1° |
| **[** | Decrease DC offset by 0.1° |
| **O** | Toggle closed loop orientation ON/OFF |
| **P** | Toggle closed loop position ON/OFF |
| **R** | Reset position and rotation |
| **Esc** | Quit application |

## Configuration

You can adjust the following parameters in the Unity Inspector:

- **Use Yaw Mode**: Enable/disable yaw mode
- **Yaw Gain**: Scaling factor for yaw input (default: 1.0)
- **Yaw DC Offset**: Constant offset in degrees (default: 0.0)
- **Gain Step**: Step size for gain adjustments (default: 0.1)
- **DC Offset Step**: Step size for offset adjustments (default: 0.1°)

## Logging

When using the `OptomotorDataLogger` (existing optomotor logging system), the following additional columns are now logged:

- `ClosedLoopPosition`: Whether position closed loop is active
- `ClosedLoopOrientation`: Whether orientation closed loop is active  
- `UseYawMode`: Whether yaw mode is currently enabled
- `YawGain`: Current gain value
- `YawDCOffset`: Current DC offset value
- `YawInput`: Raw yaw input value (degrees)
- `YawOutput`: Processed output value after gain and offset (degrees)
- `SphereDiameter`: Sphere diameter setting

The yaw mode data is automatically collected from the first ClosedLoop component found in the scene and logged alongside the existing optomotor stimulus parameters.

## Usage Example

1. Start your scene with a ClosedLoop component
2. Press **Ctrl+Y** to enable yaw mode
3. Use **+**/**-** to adjust the gain scaling
4. Use **]**/**[** to adjust the DC offset
5. Monitor the debug output to see input/output values
6. Data will be automatically logged if OptomotorDataLogger is attached to the drum object

## Debug Output

When yaw mode is active, debug messages show:
```
Yaw Mode: Input=5.23°, Gain=1.50, DCOffset=10.00°, Output=17.85°
```

This helps you understand how the input is being transformed by the gain and offset. 