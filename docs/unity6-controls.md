# Unity 6 input and movement

The project uses Unity **6000.3.16f1** with `com.unity.inputsystem` **1.20.0**. Unity recommends the [Input System package](https://docs.unity3d.com/6000.3/Documentation/Manual/input-introduction.html) for new input work; [1.20.0 supports Unity 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.inputsystem.html). This replaces the project-owned runtime calls to legacy `UnityEngine.Input`. It does not replace FicTrac with a physics character controller: the Swarm/Kannadi walking path uses measured displacement. Bogong/Kinefly uses the restored historical yaw-rate calculation; see [gain compatibility](bogong-gain-compatibility.md).

After pulling, let Package Manager resolve the pinned package and **restart the Unity Editor** if it was already open when Active Input Handling changed. Open ControlScene and focus its Game view. Entering text in a Unity UI or TMP input field suppresses experiment hotkeys, so typing names/comments cannot move animals or trigger reset.

`Assets/Resources/ExperimentControls.inputactions` is the editable action map. `ExperimentInput` loads one shared enabled copy, so all rig components observe the same input frame. Default keyboard and gamepad bindings work together, without PlayerInput auto-pairing stealing the keyboard when a gamepad connects. Operator movement affects each rig whose `manualControls.enabled` is true. This is an operator console, not four gamepad-assigned players.

| Action | Keyboard/mouse | Gamepad | Effect |
|---|---|---|---|
| Reset | R | Select / View | Reset all tracked rigs to current experiment initial position and rotation, even if tracking is absent/stale. Also resets an optomotor drum. |
| TogglePosition | P | West face button | Toggle tracked translation; not a position-only reset. |
| ToggleOrientation | O | North face button | Toggle tracked yaw; resuming rebases to current visible heading. |
| Move | Arrow keys | Left stick | Local X/Z translation. Diagonal keyboard motion is normalized. |
| Yaw | A positive, D negative | Right stick horizontal | Historical A/D polarity is preserved: A turns +Z toward +X. Remap in the action asset if desired. |
| Autopilot | Ctrl+Space | — | Toggle constant forward motion at the active individual config's `autopilotSpeed`; lateral, reverse and yaw input remain available for steering. |
| Vertical | C up, Z down | — | Requires allowVerticalTranslation. |
| Pitch | W positive, S negative | — | Requires allowPitchAndRoll. |
| Roll | E positive, Q negative | — | Requires allowPitchAndRoll. |
| SpeedModifier | Either Shift | — | With Up/Down: change translation speed by 1 cm/s per press. With W/S: change turn speed by 1 degree/s per press. No movement during adjustment. |
| Cancel | Escape, on release | Start / Menu, on release | MainController stops sequence and returns to Control. From Control, quits the standalone player. Unity Editor Application.Quit does not exit Play mode. |
| Orbit / Pan | Right / middle drag in overview | — | Move overview camera only. |
| Scroll | Wheel inside overview | — | Overview zoom. |
| Pause | Space | South face button | Replay play/pause or optomotor pause. Backslash also pauses optomotor. |
| Back / Forward | Left / Right | D-pad left / right | Replay seek by 1 second, or 10 with Shift. |
| PreviousStep / NextStep | N / M | Left / right shoulder | Replay jump to adjacent step marker. |
| Debug | D | — | Optomotor debug state in Console. Also retains D yaw when manual movement is enabled. |

Reset does not change P/O's selected tracking mode. Re-enabling tracking does not replay accumulated disabled motion. A manual heading change updates the displacement reference. Tracked yaw deltas add to the visible heading; they do not pull it toward an absolute target. Missing/stale sensor poses freeze the walking path but do not prevent operator controls. The historical Bogong/Kinefly path continues integrating the last yaw value. In that path, +/- adjusts yaw gain by 1 per press, Ctrl+Y toggles yaw-rate mode, number keys 1–4 select a rig, and held brackets adjust that rig's DC offset by 0.5 rad/s. Offsets persist across trials and scene reloads. Negative gains remain allowed as before.

Each individual experiment config file can use the Mario Kart-style autopilot. Set `autopilotEnabled` and `autopilotSpeed` in the file referenced by the sequence step’s `parameters.configFile`. The runtime shortcut Ctrl+Space toggles the mode. Optomotor scenes explicitly disable autopilot so the stimulus controller remains the sole source of scene motion.

Scroll uses Unity 6's uniform cross-platform wheel units: one tick maps to 0.1 legacy zoom units (20 world units with the default zoomSpeed=200). Fractional trackpad motion stays proportional.

UI EventSystems are upgraded at scene load from StandaloneInputModule to InputSystemUIInputModule; newly created overview UI uses the new module directly. Project Active Input Handling is **Both** for compatibility with unmodified third-party demo scripts (TMP examples, ant demo, free-flight texture demo). The experiment code and active UI use the new backend; Both is not double-reading operator actions. Switching to Input System Only would additionally require migrating or disabling any legacy vendor demos you choose to run.

## Validation and bench checks

`Assets/Editor/KannadiValidation.cs` runs on a disposable project copy with a `KANNADI_VALIDATION_COPY` sentinel. It tests queued keyboard, mouse, and gamepad events; real reset handling with no tracker; rig motion actions; UI module replacement; all current scene regressions; Optomotor startup; wind/AGL; and transitions back to normal Choice. The batch harness forces game input routing only while synthesizing events and restores the editor settings afterwards.

The final run passed **918 checks with zero runtime failures**; the report separately records an existing Unity Editor SearchDatabase cache exception. The report is [kannadi-validation-results.json](kannadi-validation-results.json). Tests do not certify LED scanout timing, live FicTrac latency, a particular physical gamepad, or monitor mapping. For a bench check, focus Game, press R/P/O, move with arrows and a connected controller, edit a metadata field to confirm hotkeys are suppressed, hide/show the overview, and press Escape to return to Control. Compare sensor-driven displacement against the measured 2.6 cm ball before collecting experimental data.
