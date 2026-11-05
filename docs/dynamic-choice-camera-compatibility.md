# Dynamic-choice cameras beyond four rigs

The historical and path-angle pilot designs repeat the same camera settings for
VR1, VR2, VR3 and VR4. The original controller matched camera settings only by
rig ID. Consequently VR5 and later rigs kept their previous clear flags during
intertrials instead of switching to the skybox; trial background colors were
also omitted for those rigs.

The controller now treats an exactly four-entry VR1–VR4 list as a shared legacy
appearance only when every entry has identical clear flags and background color.
A configured rig without its own entry inherits that appearance. Explicit matches
retain priority. Mixed appearances, partial lists and duplicate IDs do not imply
a shared default. For those designs, provide the desired per-rig camera entries.
This compatibility rule applies to live trials and original-controller replay.

Adaptive fallback camera settings are generated for the active rigs instead of
hardcoding four names. Saved designs, camera geometry, calibration, protocol
timing and recordings are not rewritten.

## Verification on 2026-11-05

`python3 tools/test_dynamic_choice_cameras.py` failed before the change with
`PathAnglePilot intertrial must show skybox on every camera of VR5`. It now passes
208 assertions using the production camera methods, saved protocol JSON and
camera spies. These cover VR1/5/8/16, all six faces, trial/intertrial transitions,
explicit overrides, mixed/partial lists, duplicate IDs and adaptive defaults.

`DynamicChoiceCameraValidation.Run` passes 1,070 assertions in Unity 6000.3.16f1.
It opens the real `Choice_desync` scene, uses production rig expansion for 5/8/16
rigs, applies both saved protocols, checks all six camera faces, and compares
rendered sky pixels against a solid-color control. It also exercises the original
controller's replay camera path. This is scene/render validation, not a physical
tracker or LED-panel test.

Run it with `-batchmode -quit -executeMethod DynamicChoiceCameraValidation.Run`
in a disposable project marked `KANNADI_VALIDATION_COPY`. Results and images are
written to `validation-output/dynamic-cameras` in that project. Local evidence
for this fix is under `../validation/vr5-skybox-20261105/`.

The apparatus-profile suite passes 58 assertions. The existing
`test_system_display_routing.py` harness fails to compile because its isolated
fixture omits `TrackerRecordingControlMode`; neither that harness nor its
production input files changed in this fix.
