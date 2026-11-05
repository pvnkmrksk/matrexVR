# Getting started

**First time? Follow the [five-minute demo, recording and replay guide](five-minute-start.md).** This page covers platform requirements, setup, storage paths and source development. A desktop archive runs without Unity Editor or Python; building from source has separate prerequisites.

Choose an operating-system ZIP from [GitHub Releases](https://github.com/pvnkmrksk/matrexVR/releases), or use the [download website](https://pvnkmrksk.github.io/matrexVR/). Extract the **whole** ZIP to a writable location. Keep its application, data folder, Unity libraries and `START-HERE.txt` together. Players run without Unity Editor or Python.

## Launch and configure

- **Windows:** open the `.exe`. Its adjacent `_Data` directory and DLLs are required.
- **macOS:** open the `.app` (universal Apple silicon/Intel package). Releases are ad hoc signed but not notarized; use Finder's Open action and the corresponding Privacy & Security option if macOS requests it.
- **Linux:** open the `.x86_64` player in a graphical desktop with a compatible graphics driver. If extraction loses the executable permission, restore it in file properties or with `chmod +x`. The bundled HejjéTrac binary requires **glibc 2.39 or newer**; use an independently verified compatible external tracker on older distributions.

Read the version's included `START-HERE.txt`. **0.1** starts on the historical metadata form, with a manual-start Choice_Selwyn sequence. **0.2** retains the Config UI Test branch's automatic seven-condition TerrainCueLab sequence, 30 seconds per condition. Press Escape to return to Control, or launch with `-configure` to enter setup immediately. These version defaults are preserved for provenance.

For example:

```sh
# Linux (replace the filename with the downloaded player's name)
"./MATREX VR.x86_64" -configure

# macOS (replace the path)
open "/path/to/MATREX VR.app" --args -configure
```

**0.3** opens setup by default for a fresh profile (`autoStart: false`). **0.4 / 0.4.1** open the hardware-free demo/configuration home for a fresh profile. Existing user sequence flags remain authoritative. The [0.4 release section](releases.md#04--terrain-recording-and-replay) records publication status and known limits.

In the studio, **System config** describes hardware, **Sequence config** arranges experiment blocks, and **Experiment configs** edits each referenced stimulus. Session cards describe the real animals; `VR1`, `VR2`, etc. remain hardware routing identifiers. Set tracker endpoint/mode, sphere diameter and display layout for your actual apparatus. A supplied public system template opens the application; it does not calibrate an LED array.

Choose `closedLoopMode: "Rekkefly"` for modern flight input or `"Hejjetrac"` for walking. Historical `"Kinefly"` flight and `"FicTrac"` walking names remain recognized; walking uses a separate cumulative-displacement contract. [System examples](../Assets/StreamingAssets/Examples/System) and [input/data documentation](data-formats.md) describe the difference. A tracking publisher is needed for closed-loop motion, not for browsing setup or using configured manual controls.

In 0.4.1, use **Open tracker** (called **Initialize tracking** in earlier interfaces) to open the independent tracker and inspect input/calibration while setup remains open. This does not start an experiment or create its pose recording. Review **Save tracker video** and recorder-control mode before acquisition, then use **Save setup** and **Start experiment**. Recorder behavior depends on the build and companion capabilities: archived 0.4 trackers can record continuously from preflight; 0.4.1 supports acknowledged experiment-owned takes on compatible companions. See [native recording control](native-recording-control.md). Auto start controls the next launch. Escape returns from a run to Control; Escape from Control exits the standalone player. Each studio-started experiment gets a separate recording directory.

## Configuration and data locations

**0.4** keeps editable configuration in a writable user profile and recordings beside each installed player. The application seeds missing files into `Config` on first launch; updating the application never overwrites existing files. The recording library shows the actual recording root and lists its completed sessions.

| Environment | Configuration | Recordings |
| --- | --- | --- |
| Editor | `Assets/StreamingAssets` | `Assets/RunData` |
| 0.4 standalone, writable player directory | `Application.persistentDataPath/Config` | `MATREX VR RunData` beside the app/executable |
| 0.4 standalone, protected installation | `Application.persistentDataPath/Config` | `Application.persistentDataPath/Installations/<installation-id>/RunData` |
| Archived 0.3 standalone | `Application.persistentDataPath/Config` | `Application.persistentDataPath/RunData` |
| 0.1 / 0.2 Windows/Linux | `<application>_Data/StreamingAssets` | `<application>_Data/RunData` |
| 0.1 / 0.2 macOS | `<application>.app/Contents/Resources/Data/StreamingAssets` | `<application>.app/Contents/RunData` |

Unity's profile root normally lives under `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>` on Windows, `~/Library/Application Support/<Company>/<Product>` on macOS, or `$XDG_CONFIG_HOME/unity3d/<Company>/<Product>` on Linux (usually `~/.config/unity3d/...`). Company/product names come from the build's Player Settings. The application can also use explicit absolute roots:

```sh
"./MATREX VR.x86_64" -configure --config-dir "/path/to/my-config" --data-dir "/path/to/my-runs"
```

`system_config.json` is the hardware file; `sequenceConfig.json` contains ordered blocks. Each block's `parameters.configFile` identifies its experiment relative to the configuration root; `parameters.design` identifies a Dynamic Choice design. Preserve referenced subdirectories and data files. [Templates](../Assets/StreamingAssets/Templates/README.md) and [examples](../Assets/StreamingAssets/Examples/README.md) provide complete files.

Back up existing editable configs and run folders before switching versions. To retain 0.1/0.2 configuration, copy its complete tree to a chosen writable directory and use `--config-dir`. Use `--data-dir` to continue a previous recording root, or **Browse…** in the replay library to inspect old shared application-support recordings. The application does not move earlier runs or silently replace their files.

## Controls

Focus Game/the player. Metadata text entry suppresses experiment hotkeys.

| Keys / gesture | Action |
| --- | --- |
| R | Reset tracked rigs to the active experiment initial pose. |
| P / O | Toggle tracked translation / yaw. |
| Arrows / gamepad left stick | Manual translation, if enabled. |
| A / D; gamepad right stick | Positive / negative yaw. |
| Ctrl+Space | Toggle active experiment forward autopilot; steering and reverse remain available. |
| Shift+Up/Down | Adjust manual translation speed by 1 per press. |
| Shift+W/S | Adjust manual turn speed by 1 degree/s per press. |
| C / Z | Vertical motion if `allowVerticalTranslation` is enabled. |
| W / S; E / Q | Pitch; roll, if `allowPitchAndRoll` is enabled. |
| Escape, on release | Return to Control; from Control, quit the standalone player. |
| Tab, 0.2 onward | Show/hide runtime dashboard. In setup, normal field navigation. |
| Right / middle drag inside overview | Orbit / pan the operator camera. |
| Wheel inside overview | Zoom. |
| Replay: Space | Play/pause. |
| Replay: Left / Right | Seek 1 second; hold Shift for 10 seconds. |
| Replay: N / M | Previous / next recorded step. |

Kinefly/Bogong retains **+/−** yaw gain, **Ctrl+Y** yaw-rate mode, **1–9** selection for matching rig IDs, **0** or **Next animal** to cycle all configured IDs (including VR10), and held **[/]** DC offset. Dashboard trim controls affect the current sequence row. Optomotor owns its stimulus movement, disables autopilot, and pauses with Space/backslash. See the [full Input System map](unity6-controls.md) for gamepad and secondary controls.

For 0.4 replay, choose **Replay recordings** in setup to see this player’s runs. Choose **Browse…** for an external saved folder, or enter its path. Windows/macOS and the Editor use their system folder dialogs; Linux uses zenity/kdialog when available, otherwise the path input remains available. Standalone OS-level folder drag/drop is not implemented. See [replay](replay.md).

## Save the evidence

Retain the complete run directory, not only a rig CSV. New recordings include original system/sequence/experiment inputs, immutable snapshot versions and hashes, loaded external data, animal metadata, rig and population logs, `runtime_trace.log`, and generated `Skyboxes` with their manifests. Writers drain on clean exit; the original directory remains if ZIP creation fails.

Rig CSVs use wall-clock timestamps. TerrainChoice/rugosity and walking scenes use centimetres; Selwyn terrain/migration scenes use metres; raw Kinefly yaw input uses radians. Interpret the exact scene and saved scale settings. [Data formats](data-formats.md), [run snapshots](run-config-snapshots.md), and [replay](replay.md) explain interpretation and reconstruction limits.

## Terrain recipes and current limits

Load one of the four `Examples/Rugosity/sequence-treatment-*.json` sequences for matched terrain, visible terrain with flat navigation, flat appearance with terrain navigation, or both flat. Each sequence runs the four shared TerrainChoice layouts for 30 seconds each and uses `terrain-treatment.json` for common settings. The two placeholder trees are equally distant from the start. The recipe uses real tracked motion, 1 cm clearance and texture scale 4; open the tracker and inspect input before starting. Both-flat trials intentionally have identical surfaces. See [the four treatment files](rugosity.md#load-one-treatment-and-run-all-four-spatial-patterns).

Choice blocks expose **Edit config → Available objects & materials**. Under **Stimulus → Objects**, Type and Material controls offer names registered in that scene, preserving exact JSON values and existing custom names.

Archived 0.4 video recording follows the independently launched tracker and can span several VR experiments. 0.4.1 adds Automatic/Manual recorder control with acknowledgement where supported; an unavailable recorder is reported separately from VR readiness. See [native recording control](native-recording-control.md) for ownership, stop and finalization behavior. Pose recording and replay do not require camera footage. The linked external recorded-trajectory viewer is supported only by the reviewed Mac arm64 dashboard bundle; Unity’s internal pose/video replay remains independent. Older pinned strict flight-camera paths on other architectures remain blocked.

Evenly paced 60 FPS is [still under investigation](frame-pacing-20261027.md). Latest standalone mouse/drag acceptance was blocked by the native automation tool; keyboard, rendering and separate Unity GUI-event checks have their recorded scope. Windows/Linux builds are cross-built and have not received native execution walkthroughs. These limits and physical calibration requirements are recorded in [release verification](releases.md#04-verification-and-known-limits).

## Source and tracker setup

For a local macOS player, see the [README build instructions](../README.md#build-a-local-macos-preview). After building, launch **`Preview/Open MATREX VR.command`** to use `Preview/Config` and `Preview/RunData`; rebuilding preserves those folders. A build can take longer than five minutes and requires the pinned Unity installation, plus the native companion prerequisites linked there.

Clone [the repository](https://github.com/pvnkmrksk/matrexVR), add it to Unity Hub and open it using **Unity 6000.3.16f1**, as pinned in `ProjectSettings/ProjectVersion.txt`. Let Package Manager restore the pinned dependencies. Start at `Assets/Scenes/ControlScene.unity`. Restart an already open Editor after pulling Input System/backend changes.

FicTrac is an optional external acquisition component for walking experiments. The project's [FicTrac fork](https://github.com/pvnkmrksk/fictrac) includes `install_ubuntu.sh` for Linux setup. Follow that repository's installation instructions and confirm the publisher's units and endpoint against [the ZMQ contract](data-formats.md#zmq-input-contract). Kinefly and Tirbala use their own input paths.

Custom experiments require Unity scene/asset/controller authoring. Add them to the build scene list and rebuild; the packaged studio configures the experiments the build contains. [Experiment workflow](experiment-workflow.md) covers that extension path.

## If the first run does not work

| Symptom | Next step |
| --- | --- |
| An experiment starts before setup appears | Press **Escape** once, or relaunch with `-configure`. Existing sequences keep their saved Auto start setting. |
| Home has no **Start demo** button | You may have a configured apparatus or an archived build. Follow its `START-HERE.txt`; use separate `--config-dir` and `--data-dir` paths for a disposable demo profile. |
| A demo has no fresh tracking input | Check its native demo window and input status. Keep demonstration mode selected; use manual arrows/A/D to inspect the scene while diagnosing input. See [bundled companions](bundled-companions.md). |
| A saved run is missing from Replay | Check the root displayed above the library, wait for that run to finish saving, then **Refresh**. Use **Browse…** for a different installation's complete run folder. |
| Camera video is absent | Inspect saved recorder evidence. A working scene or pose CSV does not establish a completed video take; [recorder controls](native-recording-control.md) describe the states. |
| A source project has compile/import errors | Confirm Unity **6000.3.16f1**, let package restoration finish, and check the first Console error before trying to run a scene. |

When reporting a problem, include the release/build revision, operating system, exact action, visible error and relevant `runtime_trace.log` from the saved run. Preserve the original run and configuration files.
