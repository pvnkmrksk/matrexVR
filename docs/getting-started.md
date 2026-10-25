# Getting started

Choose an operating-system ZIP from [GitHub Releases](https://github.com/pvnkmrksk/matrexVR/releases), or use the [download website](https://pvnkmrksk.github.io/matrexVR/). Extract the **whole** ZIP to a writable location. Keep its application, data folder, Unity libraries and `START-HERE.txt` together. Players run without Unity Editor or Python.

## Launch and configure

- **Windows:** open the `.exe`. Its adjacent `_Data` directory and DLLs are required.
- **macOS:** open the `.app` (universal Apple silicon/Intel package). Releases are ad hoc signed but not notarized; use Finder's Open action and the corresponding Privacy & Security option if macOS requests it.
- **Linux:** open the `.x86_64` player in a graphical desktop with a compatible graphics driver. If extraction loses the executable permission, restore it in file properties or with `chmod +x`.

Read the version's included `START-HERE.txt`. **0.1** starts on the historical metadata form, with a manual-start Choice_Selwyn sequence. **0.2** retains the Config UI Test branch's automatic seven-condition TerrainCueLab sequence, 30 seconds per condition. Press Escape to return to Control, or launch with `-configure` to enter setup immediately. These version defaults are preserved for provenance.

For example:

```sh
# Linux (replace the filename with the downloaded player's name)
"./MATREX VR.x86_64" -configure

# macOS (replace the path)
open "/path/to/MATREX VR.app" --args -configure
```

**0.3** opens setup by default for a fresh profile (`autoStart: false`). Existing user sequence flags remain authoritative.

In the studio, **System config** describes hardware, **Sequence config** arranges experiment blocks, and **Experiment configs** edits each referenced stimulus. Session cards describe the real animals; `VR1`, `VR2`, etc. remain hardware routing identifiers. Set tracker endpoint/mode, sphere diameter and display layout for your actual apparatus. A supplied public system template opens the application; it does not calibrate an LED array.

Choose `closedLoopMode: "Kinefly"` for the historical flight yaw-rate input. FicTrac walking uses its separate cumulative-displacement contract. [System examples](../Assets/StreamingAssets/Examples/System) and [input/data documentation](data-formats.md) describe the difference. A tracking publisher is needed for closed-loop motion, not for browsing setup or using configured manual controls.

Use **Save setup**, then **Start experiment**. Auto start controls the next launch. Escape returns from a run to Control; Escape from Control exits the standalone player. Each studio-started experiment gets a separate recording directory.

## Configuration and data locations

**0.3** uses a writable user profile. The application seeds missing files into `Config` on first launch; updating the application never overwrites existing files. `RunData` contains recorded sessions.

| Environment | Configuration | Recordings |
| --- | --- | --- |
| Editor | `Assets/StreamingAssets` | `Assets/RunData` |
| 0.3 standalone | `Application.persistentDataPath/Config` | `Application.persistentDataPath/RunData` |
| 0.1 / 0.2 Windows/Linux | `<application>_Data/StreamingAssets` | `<application>_Data/RunData` |
| 0.1 / 0.2 macOS | `<application>.app/Contents/Resources/Data/StreamingAssets` | `<application>.app/Contents/RunData` |

Unity's profile root normally lives under `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>` on Windows, `~/Library/Application Support/<Company>/<Product>` on macOS, or `$XDG_CONFIG_HOME/unity3d/<Company>/<Product>` on Linux (usually `~/.config/unity3d/...`). Company/product names come from the build's Player Settings. The application can also use explicit absolute roots:

```sh
"./MATREX VR.x86_64" -configure --config-dir "/path/to/my-config" --data-dir "/path/to/my-runs"
```

`system_config.json` is the hardware file; `sequenceConfig.json` contains ordered blocks. Each block's `parameters.configFile` identifies its experiment relative to the configuration root; `parameters.design` identifies a Dynamic Choice design. Preserve referenced subdirectories and data files. [Templates](../Assets/StreamingAssets/Templates/README.md) and [examples](../Assets/StreamingAssets/Examples/README.md) provide complete files.

Back up your 0.1/0.2 editable configs and run folders before switching versions. To retain them in 0.3, copy the complete configuration tree to a chosen writable directory and use `--config-dir`; use `--data-dir` for the recordings directory. The application does not infer a prior installation or silently replace its files.

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

For replay, choose **Replay a recorded run** in setup, then **Browse…** to choose the saved folder, or paste its path. Windows/macOS and the Editor use their system folder dialogs; Linux uses zenity/kdialog when available, otherwise the path input remains available. Standalone OS-level folder drag/drop is not implemented. See [replay](replay.md).

## Save the evidence

Retain the complete run directory, not only a rig CSV. New recordings include original system/sequence/experiment inputs, immutable snapshot versions and hashes, loaded external data, animal metadata, rig and population logs, `runtime_trace.log`, and generated `Skyboxes` with their manifests. Writers drain on clean exit; the original directory remains if ZIP creation fails.

Rig CSVs use wall-clock timestamps; walking scenes use centimetres, terrain/migration scenes metres, and raw Kinefly yaw input radians. [Data formats](data-formats.md), [run snapshots](run-config-snapshots.md), and [replay](replay.md) explain interpretation and reconstruction limits.

## Source and tracker setup

Clone [the repository](https://github.com/pvnkmrksk/matrexVR), add it to Unity Hub and open it using **Unity 6000.3.16f1**, as pinned in `ProjectSettings/ProjectVersion.txt`. Let Package Manager restore the pinned dependencies. Start at `Assets/Scenes/ControlScene.unity`. Restart an already open Editor after pulling Input System/backend changes.

FicTrac is an optional external acquisition component for walking experiments. The project's [FicTrac fork](https://github.com/pvnkmrksk/fictrac) includes `install_ubuntu.sh` for Linux setup. Follow that repository's installation instructions and confirm the publisher's units and endpoint against [the ZMQ contract](data-formats.md#zmq-input-contract). Kinefly and Tirbala use their own input paths.

Custom experiments require Unity scene/asset/controller authoring. Add them to the build scene list and rebuild; the packaged studio configures the experiments the build contains. [Experiment workflow](experiment-workflow.md) covers that extension path.
