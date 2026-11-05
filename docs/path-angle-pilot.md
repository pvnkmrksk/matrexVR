# Ant path-angle pilot: source checkout and Ubuntu testing

This branch contains the pilot runtime, procedural ground shader, ten-condition protocol, adjustable Unity preview and MATREX sequence-picker integration. The historical six-condition reproduction is available in the same picker. macOS executables, generated desktop profiles, downloaded companion applications and recordings are not part of the source branch.

## Open on Ubuntu

1. Clone the `codex/ant-path-angle-pilot` branch (or fetch and switch to it in an existing checkout).
2. Install and activate **Unity 6000.3.16f1**, the version pinned in `ProjectSettings/ProjectVersion.txt`. Add this repository directory as a project in Unity Hub. Let the pinned packages restore.
3. Open `Assets/Scenes/ControlScene.unity` and enter Play mode. Alternatively use **Tools → Experiment setup → Open setup screen (Play)**, which also regenerates the configuration schemas.
4. Use **Configure** to select the actual walking-input backend, tracker host/port, sphere diameter, display mapping and gains for the apparatus. Save/complete the apparatus setup. Loading the pilot preserves these settings.
5. On **Start**, choose **Load sequence → Ant · path-angle pilot → Load sequence**. Review it, then return to **Start** and start the experiment when the rig is ready.

The repository also contains the editor keyboard launcher under **Ant experiments → Start path-angle pilot (keyboard)**. This is useful for a first visual check without a tracker; it explicitly selects the desktop keyboard system. Use the normal MATREX workflow above for the actual apparatus.

Existing tracker/calibration files from the Ubuntu apparatus should remain the reference for the hardware configuration. `Assets/StreamingAssets/Experiments/PathAnglePilot/system.apparatus-template.json` is only a starter: manual controls are disabled, but its localhost port 29951, front-camera display and sphere diameter are example values. It does not contain the Ubuntu rig's calibration. The keyboard preview uses `system.preview.json` and the same example port.

For external tracking, start the tracker already used on the apparatus and point MATREX at its publisher. The local Mac companion bundle was not committed; selecting a sequence does not install Linux tracker applications. See [tracker integration](tracker-integration.md), [source/tracker setup](getting-started.md#source-and-tracker-setup) and [input contract](data-formats.md#zmq-input-contract).

For a standalone Linux executable, add Linux desktop build support to that Unity installation and build the enabled scenes for Linux from Unity's Build Profiles. This branch has not yet been built or run on Ubuntu. A Mac `.app` is not needed for source testing and will not run there.

## Protocol and appearance

The pilot repeats randomized complete blocks of these ten conditions:

| Bars | Path opening angles |
| --- | --- |
| Two black scaling bars | 40°, 55°, 70°, 85°, 100°, 120°, 150°, 180°, 210° |
| No bars | 85° |

Goals remain at radius 60 cm and bearings −20°/+20°. Scaling bars maintain 10° apparent size. Two-bar trials end at historical goal contact or 120 seconds; the no-bars trial lasts 20 seconds regardless of position and has no goal triggers. Each trial is preceded by a five-second intertrial.

The hard three-hour deadline uses monotonic elapsed time from session start, including scene loading and intertrials. It overrides an ongoing trial or intertrial, independent of Unity time scale. An editor watchdog also enforces it during Play-mode pause. It is checked on the first available update at/after the deadline; measured overshoot is recorded.

The path is a visual marking without colliders. For opening angle A, the fork is at `z = 60 cos(20°) − 60 sin(20°) / tan(A/2)` cm. The 40° condition splits at the origin, 180° makes a T at goal depth, and 210° forks beyond the goals with branches leading back.

Use **Ant experiments → Adjust path-angle pilot appearance** in the Unity editor to inspect every condition from above and from the ant's start pose. Width, darkness, brightness, texture scale, ground height and extent are adjustable. **Save appearance** changes the shared appearance file for the next session. Choosing a preview angle does not change the protocol. Initial appearance is provisional: width 4 cm, darkness 0.65, brightness 0.55, grass scale 8 cm, extent 400 cm, ground height −1 cm.

All pilot configuration files are in `Assets/StreamingAssets/Experiments/PathAnglePilot/`:

- `design.json`: conditions, triggers, ground angles, deadline and seed. Seed `-1` chooses and records a new seed.
- `appearance.json`: shared appearance, loaded once per session.
- `sequence.json`: the prepared sequence listed in MATREX.
- `system.preview.json`: desktop keyboard preview.
- `system.apparatus-template.json`: apparatus starter settings, to configure for the rig.

For a supplied grass texture, set `appearance.json`'s `texturePath` relative to StreamingAssets. The texture repeats once per `textureScaleCm` and is multiplied by ground brightness. For a prepared ground/path image, set `preparedGroundImage: true`; it covers the whole square, centre at the start, top toward +z, right toward +x. Prepared imagery bypasses procedural painting and brightness. A condition's `pathGround.preparedTexturePath` overrides the shared image. Supplied images must match the intended geometry. Use relative paths when moving between computers; consumed image bytes are archived with the recording.

## Records and checks on the actual rig

The normal pose recordings and configuration archive are supplemented by `path_pilot_events.jsonl`: seed, block order, condition/angle/fork, trial/intertrial starts and ends, goal side and elapsed session time. Bar timeouts are `no_choice_timeout`; no-bars completions are `duration_complete`. Interrupted trials/intertrials and incomplete final blocks are explicit. Equal-black and no-bars conditions have no accuracy score.

Before animal acquisition, use a labelled setup test to inspect the ground and all angles on the real display, verify walking directions/gains and start resets, and test both goal contacts. Check that the no-bars trial continues across former goal locations. Keep these synthetic test recordings separate from animal data. Physical display appearance, latency and tracking calibration have not been validated by the Mac checks.

Editor recordings default to `Assets/RunData`. Standalone configuration lives in the application's persistent Config folder and existing files are preserved; recordings normally live beside the executable. `--config-dir` and `--data-dir` override these locations. See [run snapshots](run-config-snapshots.md).

## Reproduce the software validation

From a committed checkout, run:

```bash
python3 tools/run_path_pilot_validation.py --unity /path/to/Unity
```

The runner creates a temporary source copy from the selected Git revision, marks it as disposable, runs the runtime, appearance and UI checks there, and copies results into `validation-output/`. It does not change the current checkout's protocols or start the apparatus. It needs an activated Unity installation, package restoration access and a working graphics environment. The output records logs and synthetic recordings.

The [recorded macOS validation](path-angle-pilot-validation.json) contains 45 runtime checks, 39 configuration/rendering/texture checks and 16 UI checks. Real 5/20/120-second timings were tested; session-deadline tests used short durations, including paused Unity and zero time scale. A full three-hour soak and Ubuntu hardware validation remain to be done.

The local Mac standalone build initially hit a native Unity Burst compiler crash, then built successfully with `--burst-disable-compilation`. The ant trial code does not use Burst. No equivalent workaround is assumed necessary on Ubuntu.
