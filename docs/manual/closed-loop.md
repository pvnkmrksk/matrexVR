# ClosedLoop and motion

ClosedLoop converts a complete tracker sample into movement of the animal's virtual rig. Its interpretation depends on the active walking/flight controller path, not only on the spelling of one field in the system file. Check the dashboard's effective input mode and the recorded tracking implementation.

## Walking displacement and orientation

The walking path consumes cumulative ball displacement. It swaps the incoming planar axes into Unity's horizontal frame, differences successive samples and applies the reset/manual orientation reference. Multiplication by `sphereDiameter / 2` converts angular ball displacement to a linear distance when the publisher uses the documented radians contract.

At walking position gain 1, no extra multiplier is inserted. At orientation gain 1, one measured yaw degree produces one world degree. Wrapped angular differences use the shortest delta around 0/360. Rotation is not multiplied by deltaTime and is not smoothed toward an absolute heading by a turn-speed cap.

Walking waits for a pose and freezes tracking contribution when the sample is stale. A held/stale pose is still present in historic rows; its presence alone is not proof of fresh acquisition.

## Kinefly flight yaw-rate compatibility

The historical flight path preserves raw signed radians rather than round-tripping through a Unity quaternion. In yaw-rate mode, the applied virtual yaw increment is:

```text
yaw increment (degrees) =
  yawGain * (rawYawRadians - yawDCOffsetRadians) * Rad2Deg * deltaTime
```

The latest value continues to integrate between packets, including the historical held-input behavior during a disconnect. That differs from the walking freshness freeze. The dashboard and telemetry expose state; they do not silently change the compatibility arithmetic.

Negative yaw gains remain supported in this historical path. DC offsets persist across configured trials in the current sequence process. Use [flight gain compatibility](../bogong-gain-compatibility.md) when comparing an older acquisition build.

## Tirbala force/torque path

Tirbala selects the retained force/torque branch. Its channels are control quantities, not ordinary measured Euler angles. Do not label those raw rotation channels as degrees merely because the wire object calls them pitch/yaw/roll. Read the effective mode and its units in telemetry and the experiment's saved config.

## Reset, toggles and references

**R** applies the current experiment's initial pose and restarts tracking initialization. It works without a fresh tracker. It does not change the operator's selected **P/O** tracking switches.

**P** toggles measured translation; **O** toggles measured orientation. Re-enabling tracking rebases the appropriate reference rather than replaying accumulated disabled movement. Manual yaw also updates the displacement reference so later tracker motion remains consistent with the visible heading.

Experiment pose reset flags and operator tracking toggles are separate controls. A phase/row can deliberately retain motion continuity with `resetPositionOnStart` and `resetRotationOnStart` disabled.

## Manual motion and autopilot

Keyboard input respects each rig's `manualControls` and pauses while an operator edits a text field. Use **1–9** to select VR1–VR9, **Shift+1–7** for VR10–VR16, or **0** to apply keyboard input to all rigs. Selection applies to manual translation, rotation, pose reset and tracking shortcuts. The runtime dashboard also cycles through individual rigs and all rigs.

Forward autopilot uses the active experiment's `autopilotSpeed`. The arrow keys and **A/D** steer the selected rig while it moves forward. **Ctrl+Space** toggles autopilot for the selection. An autopilot already running on another rig continues to run.

**Tab** toggles the operator overlays. **Escape** returns to setup and shows **Saving data…** while the recording finishes. Escape becomes available again when setup returns.

Walking scenes use cm/s while metre-scale flight scenes use m/s. Manual operator speed, generated **agentSpeed** and the real animal's measured speed are different quantities. Optomotor disables forward autopilot to retain ownership of stimulus motion.

## Terrain pose ownership

When RugosityRig controls the pose, horizontal motion is applied in a level yaw frame; the terrain follower supplies height/normal alignment. This prevents pitch/roll from accidentally shortening horizontal speed or adding steering. Migration wind can remain active while yielding the overlapping altitude role.

## Flight gating and auto trim

KineflyAutoTrim observes distinct, fresh input samples, qualifies flight and applies the configured iterative centering only in its supported flight/yaw mode. The measured flight state, a bypass switch and trim enablement are independent. See [auto trim](../kinefly-auto-trim.md) for variance thresholds, median/uncertainty windows, current-row scope and recorded diagnostics.

## Source and related contracts

[ClosedLoop](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/ClosedLoop.cs), [Keyboard](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/Keyboard.cs), [input contract](input-contracts.md), [data columns](../data-formats.md), [operator controls](../unity6-controls.md).


## Stock locust rendering

Generated stock locust swarms use instanced skinning when the scene uses Vulkan, Built-in Forward, one skin weight, and the supported stock material and lighting profile. The renderer uses each original Animator and its live bone transforms. Incompatible models, camera passes, lighting, or materials use Unity's stock renderers. Large independent generated swarms also distribute transform movement across Unity workers before the frame's animation and recording.

Use `--stock-locust-renderers` or `--serial-swarm-motion` to select the respective reference paths for a comparison. The recording manifest lists the permitted renderer and motion options. The run trace records which rig layers activated instancing and any fallback. Same-build replay restores recorded transforms and animation through the stock rendering path. The supported instanced profile matched the stock mesh geometry to floating-point tolerance, with minor rasterization differences at isolated pixels.
