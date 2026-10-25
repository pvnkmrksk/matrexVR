<p align="center">
  <img src="docs/site/assets/favicon.svg" width="72" height="72" alt="MATREX VR app icon">
</p>

<h1 align="center">MATREX VR</h1>

<p align="center"><strong>Animal movement. Controlled worlds.</strong><br>Configurable Unity environments for flight, walking and collective-behaviour experiments.</p>

<p align="center">
  <a href="https://github.com/pvnkmrksk/matrexVR/releases"><img src="https://img.shields.io/github/v/release/pvnkmrksk/matrexVR?label=archived%20downloads&amp;color=425a66" alt="Published archival downloads"></a>
  <a href="https://pvnkmrksk.github.io/matrexVR/manual/"><img src="docs/site/assets/docs-badge.svg" alt="Complete online manual"></a>
  <a href="tools/build_local_desktop.py"><img src="https://img.shields.io/badge/build-local%20desktop-1f5c66" alt="Build the current desktop preview"></a>
  <a href="CITATION.cff"><img src="docs/site/assets/cite-badge.svg" alt="Cite MATREX VR"></a>
</p>

<p align="center"><b><a href="#try-the-current-desktop-preview">Try the desktop preview</a> · <a href="https://pvnkmrksk.github.io/matrexVR/manual/">Documentation</a> · <a href="https://pvnkmrksk.github.io/matrexVR/manual/gallery.html">Scene gallery</a> · <a href="https://github.com/pvnkmrksk/matrexVR/releases">Archived downloads</a></b></p>

MATREX VR turns measured animal movement into a controlled visual world: Choice tasks, forest and terrain, astronomical night sky, Optomotor, generated or recorded swarms, and **Kannadi** — ಕನ್ನಡಿ, “mirror” in Kannada — a matrix of mirrored animal poses. Configure an experiment visually, inspect the same JSON, and keep the evidence for analysis and replay.

> **Current desktop preview: source/local build.** Native fullscreen and Retina-aware UI, the demo/Configure workflow, bundled native trackers and the latest replay fixes are current source. Public **0.1–0.3 downloads remain archival** and do not contain this new desktop experience. No new signed release is implied.

[![MATREX VR native Mac start screen with a hardware-free Night sky demo and separate Configure workspace](docs/images/desktop-ui/start-native-3024.png)](docs/images/desktop-ui/start-native-3024.png)

*Actual Mac player at native 3024 × 1898 with 2× UI scale and a fresh demo profile. [Capture provenance](docs/images/desktop-ui/current-player-provenance.json). Earlier editor views: [Sequence](docs/images/desktop-ui/sequence-1920.png) · [Parameters](docs/images/desktop-ui/motion-1920.png) · [Tooltips](docs/images/desktop-ui/hover-help-1920.png).*

## Try the current desktop preview

For a local Mac build, install and activate **Unity 6000.3.16f1** with its desktop build support. Apple Silicon maintainer builds also need a current stable **Rust** toolchain for the pinned native camera correction; the finished app needs neither Unity nor Rust installed. Then run:

```sh
python3 tools/build_local_desktop.py
```

The [local builder](tools/build_local_desktop.py) produces **`.local/MATREX VR.app`**, including checksum-verified native companion packages. Apple Silicon builds require the reviewed dashboard archive described in [local companion builds](tools/native_patches/README.md). It builds your current workspace and publishes no release. [Bundled apps, installation and signing](docs/bundled-companions.md) explains the pinned versions and independent process lifetimes.

1. **Open the app and press Start demo.** Without a configured apparatus, the default is a flying **Night sky** demo; **Forest** and walking are available. The native tracker opens independently with labelled simulated or prerecorded input. No animal or hardware is required.
2. **Configure your apparatus.** Set species, walking/flying input, VR count, displays, LED dimensions, face order and tracker ports. The graphical map uses the same pixel geometry as the stimulus cameras. Calibrate real acquisition in the tracker window, then save the configuration.
3. **Run daily experiments.** The configured home puts the sequence, block parameters and animal/session labels first. Hardware and species remain under **Configure**. Gain sliders span **0–50**; exact text entry also accepts negative or larger values.

The app starts fullscreen at the selected display's native resolution. Apparatus setup stays out of the daily workflow; sequence editing, experimental parameters and contextual help stay within reach. Existing custom JSON and calibrated apparatus files are preserved. Read the [desktop workflow and validation notes](docs/desktop-experience.md).

**Tab** hides or restores operator graphics together. Status, FPS and overview panels use free display pixels and never cover configured LED rectangles; a display fully occupied by LED output omits these panels.

## Build an experiment

- **Choose and arrange blocks.** Reuse Choice, sky, terrain, swarm, Kannadi and Optomotor recipes; change their motion and stimulus parameters.
- **Reveal the details when needed.** Hover help explains fields; advanced settings and raw JSON remain available. Unknown fields, untouched numbers and omitted values survive editing. Undo/Redo and external-file conflict checks protect the workflow.
- **Record and revisit.** Keep the complete run folder, configuration snapshots and sky assets. Replay animal poses and saved stimulus evidence with a timeline and an overview camera.

[Configuration studio](docs/configuration-ui.md) · [Templates and examples](Assets/StreamingAssets/Examples/README.md) · [Recording formats](docs/data-formats.md) · [Replay guide](docs/replay.md)

## See the environments

| Forest source example | Terrain + Milky Way | Kannadi · ಕನ್ನಡಿ |
| --- | --- | --- |
| [![Choice_Forrest with the bundled Landscape prefab](docs/site/assets/gallery/forest-choice.jpg)](https://pvnkmrksk.github.io/matrexVR/manual/gallery.html#forest-choice) | [![Actual Selwyn terrain beneath the generated Milky Way](docs/site/assets/gallery/milky-way-terrain.jpg)](https://pvnkmrksk.github.io/matrexVR/manual/gallery.html#milky-way-terrain) | [![Actual Kannadi hexagonal mirror matrix](docs/site/assets/gallery/kannadi.jpg)](https://pvnkmrksk.github.io/matrexVR/manual/gallery.html#kannadi) |

Actual Unity renders of synthetic demonstrations. The forest image is **Choice_Forrest + Landscape**, an optional source customization; the desktop Forest starter is a separate terrain/tree preset. The [nine-capability gallery](https://pvnkmrksk.github.io/matrexVR/manual/gallery.html) also shows bars, stripe fixation, objects, Optomotor, locust swarm and custom panorama terrain, with [exact configs and provenance](docs/site/assets/gallery/provenance.json).

## One connected software path

| Component | Its job |
| --- | --- |
| [**Rekkefly**](https://github.com/pvnkmrksk/rekkefly) / [**HejjéTrac**](https://github.com/pvnkmrksk/hejjetrak) | Native flight/wing tracking or spherical-treadmill walking input. |
| **MATREX VR** | Closed-loop visual environments, experimental sequencing and recording. |
| [**Daari Deepa**](https://pvnkmrksk.github.io/trajectory-dashboard/) | Optional live browser plots and saved-trajectory analysis; standalone downloads for Windows, Linux and both Mac architectures. |

Tracker windows run independently; closing Unity does not automatically close them. Unity keeps lightweight run status. The external plotting dashboard can run locally or on another computer, so it can stay out of the rendering workload. Read the [tracker connection guide](docs/tracker-integration.md), [bundled companion guide](docs/bundled-companions.md) and [live monitoring setup](https://pvnkmrksk.github.io/matrexVR/manual/live-monitoring.html).

## Documentation and evidence

The [searchable manual](https://pvnkmrksk.github.io/matrexVR/manual/) covers the architecture, components, scenes, viewports, closed loop, every configuration family, saving, telemetry and replay.

| Start here | Go deeper |
| --- | --- |
| [Getting started and controls](docs/getting-started.md) | [Exact configuration reference](docs/configuration-reference.md) |
| [Scene gallery](https://pvnkmrksk.github.io/matrexVR/manual/gallery.html) | [Viewports and rig routing](https://pvnkmrksk.github.io/matrexVR/manual/viewports.html) |
| [Recording and data](docs/data-formats.md) | [Run snapshots and provenance](docs/run-config-snapshots.md) |
| [Replay](docs/replay.md) | [Full system manual](https://pvnkmrksk.github.io/matrexVR/manual/) |

Current software evidence includes [3,023 native UI checks](docs/desktop-ui-validation-results.json), [native companion installation/lifecycle checks](docs/bundled-companion-validation-results.json), and [992 record→replay checks with paired renders](docs/replay-roundtrip-validation.md). Tests use simulated or prerecorded data; physical tracking calibration and LED timing remain apparatus-specific. The [scene-reset investigation](docs/diagnostics.md) isolates listener resource growth and material ownership defects without claiming that the original native crash was captured.

The [actual Mac player test](docs/desktop-player-validation-results.json) additionally verified camera-only acquisition launched from Unity, live dashboard input, native fullscreen and calibration snapshots. A separate physical-camera record→replay run passed 987 checks; its uncalibrated camera view tested the software path, rather than animal-tracking accuracy.

<details>
<summary><strong>Archived versions and their startup behaviour</strong></summary>

## Choose a version

| Version | Purpose | Source |
| --- | --- | --- |
| **0.1** | Archive of `main`, before the configuration studio and telemetry integration. | [`779926a8`](https://github.com/pvnkmrksk/matrexVR/tree/779926a80b02190d6dd0410f260e52bba105a04e) |
| **0.2** | Preserved Config UI Test baseline: studio, live dashboard, telemetry, modern Kannadi/Bogong and terrain cue lab. | [`c13d33dd`](https://github.com/pvnkmrksk/matrexVR/tree/c13d33dd) |
| **0.3** | Holistic improvements: reversible configuration, writable profiles, configurable animals and saved-run replay/lifecycle cleanup. | [`v0.3.0`](https://github.com/pvnkmrksk/matrexVR/tree/v0.3.0) · [verification](docs/releases.md#software-verification) |

Download the ZIP for your operating system, extract the **whole archive**, and launch the application. Unity Editor is needed only to modify or build the source. Each release includes `START-HERE.txt` with its exact startup behavior, configuration/data paths and shortcuts. Published download availability is listed on [GitHub Releases](https://github.com/pvnkmrksk/matrexVR/releases); the website resolves actual uploaded OS archives.

### Archived 0.3 workflow

1. Open **ControlScene** in Unity **6000.3.16f1**, or launch a standalone player. Use `-configure` to open setup when a sequence has auto-start enabled. Escape returns a running experiment to Control.
2. In **System config**, configure each rig's tracker endpoint/mode, physical display, panel geometry, camera order and sphere diameter. The complete [system template](Assets/StreamingAssets/Templates/system_config.template.json) is a starting point for your apparatus. Keep `closedLoopMode: "Kinefly"` for flight; [walking examples](Assets/StreamingAssets/Examples/System) select their own input contract.
3. In **Session**, enter experimenter, animal IDs, species and comments. The system configuration's `speciesName` records species per rig. Hardware IDs such as `VR1` route inputs/displays; `AnimalID` labels the real animal. Simulated and replayed individuals are **agents**.
4. Arrange blocks in **Sequence config**. Choose a recipe from **Experiment configs**, edit its motion/stimulus/sky settings, or inspect **Edit JSON**. [Templates](Assets/StreamingAssets/Templates/README.md) and [examples](Assets/StreamingAssets/Examples/README.md) cover the supported paradigms.
5. **Save setup**, then **Start experiment**. Keep the whole saved run, including configuration snapshots, data and generated skies, for analysis and replay.

The configuration studio and JSON share one lossless document model. No-op saves preserve the original file bytes. Actual edits preserve unknown fields, property order and untouched numeric text; absent settings, explicit null, false and zero remain distinct. Showing an **Inherited** default never inserts it. Undo/Redo and external-file conflict checks protect the editing workflow. See [GUI and JSON parity](docs/configuration-ui.md).

A fresh 0.3 profile opens setup by default. Its supplied demonstration is **TerrainCueLab**: four visual/navigation mesh combinations followed by height-only, tilt-only and fixed-pose controls, 30 seconds each, with `reloadScene: false`. The previous Selwyn cycle is preserved in `Examples/Sequences/selwyn-before-rugosity.json`. Configuration JSON is authoritative; old branch guides may describe their historical default sequence.



</details>

<details>
<summary><strong>Controls, paradigms, data compatibility and source reference</strong></summary>

## Controls and live monitoring

Focus the Game/player view. Experiment hotkeys are suppressed while typing into metadata fields.

| Control | Action |
| --- | --- |
| **R** | Reset rigs to the current experiment's initial pose. |
| **P / O** | Toggle tracked position / tracked orientation. |
| **Arrow keys; A / D** | Manual translation; positive / negative yaw, when manual controls are enabled. |
| **Ctrl+Space** | Toggle forward autopilot at the active config's `autopilotSpeed`. |
| **Shift+Up/Down; Shift+W/S** | Adjust manual translation speed; adjust turn speed. |
| **Tab** | Hide/restore operator graphics together during a run; navigate fields during setup. |
| **Escape, on release** | Stop and return to Control; from Control, exit the standalone player. |
| **Right / middle drag; wheel** | Orbit / pan; zoom inside the operator overview. |
| **Space; Left/Right; N/M** | Replay play/pause; seek (Shift for 10 seconds); previous/next step. |

Kinefly controls retain **+/−** for yaw gain, **Ctrl+Y** for yaw-rate mode, **1–9** for matching rig IDs, **0** or the HUD’s **Next animal** button to cycle all configured IDs (including `VR10`), and held **[/]** for DC offset. Optional vertical/pitch/roll and gamepad bindings are covered in the [full controls guide](docs/unity6-controls.md). Autopilot retains steering/reverse input; Optomotor disables it.

The in-app dashboard reports tracking freshness, flight and trim state, gains/DC offsets, heading measurements, phase/sequence state, frame timing and errors. Error capture and telemetry continue while graphics are hidden. Outbound **ZMQ PUB telemetry** is separate from tracking inputs: default `127.0.0.1:9880`, 20 Hz, topic `matrex.telemetry.v2`, schema version 2. A static website cannot consume raw ZMQ directly. See [telemetry](docs/heading-reference-telemetry.md), [auto trim](docs/kinefly-auto-trim.md) and [data formats](docs/data-formats.md).

![Flight dashboard](docs/images/ux/flight-hud.png)

*Native 0.2 HUD capture with synthetic validation inputs; current selection also supports 1–9 and 0/Next animal.*

## Automatic night sky

Choose observer latitude/longitude, date/local time and UTC offset, or set `localTime: "now"`. The bundled NASA star panorama and Astronomy Engine generate the stars/Milky Way orientation offline; no Stellarium process or runtime internet is required. Configurable updates save each displayed PNG, hash, represented UTC and provenance alongside the run. Explicit `skyboxPath` images remain supported.

![Actual generated Selwyn night-sky panorama](docs/site/assets/selwyn-night-sky.jpg)

*Actual full-sphere stimulus panorama, downsampled without changing brightness: Selwyn, −35.894409°, 148.453948°, sampled 7 October 2026 at 07:14 UTC. [Manifest](docs/site/assets/selwyn-night-sky.provenance.json) · [credits](docs/site/assets/README.md). NASA/Goddard SVS; Ernie Wright (USRA); ESA/Gaia/DPAC. Stars and Milky Way only; atmosphere, Sun, Moon and planets are not modeled.*

See [night-sky configuration and audit files](docs/night-sky.md), [Selwyn cycle](docs/selwyn-milky-way-swarm-cycle.md), [generated swarms](docs/generated-bogong-swarm.md) and [recorded swarm playback](docs/swarm-playback.md).

## Terrain and experimental scenes

**TerrainCueLab** varies visual and navigation geometry independently; height and orientation controls isolate vertical movement from pitch/roll. The earlier Rugosity_RS and historical RS scenes retain their identities. Use **Tools → Rugosity → Open terrain cue lab** for source authoring. [The rugosity guide](docs/rugosity.md) explains measured geometry, JSON precedence and native validation.

![Four measured terrain conditions](docs/terrain-cue-four-conditions.svg)

The compact build contains ten scenes: **ControlScene, Choice, Choice_desync, Choice_Selwyn, Swarm, Kannadi, Optomotor, TerrainCueLab, Rugosity_RS and ReplayScene**. The current desktop Forest starter uses Choice_Selwyn terrain with grounded tree objects. Separate legacy forest/two-tree examples are marked `sourceBuildOnly`; enable their scenes and rebuild to run those examples. Add custom scenes, assets and controllers in Unity, enable them in the build scene list, and rebuild. See [experiment workflow](docs/experiment-workflow.md); this project does not provide a general level editor.

## Compatibility and saved runs

Use `agentCount`, `agentSpeed` and `agentSpeedStdDev` for simulated populations; `locustSpeed` and `locoSpeed` remain accepted. Precedence is `agentSpeed` → `locustSpeed` → `locoSpeed`. The UI preserves existing key spellings; using a canonical key is an explicit configuration choice. New metadata uses `Animals`, `AnimalID`, `SpeciesName` and `UsedAnimalIDs`, retaining transitional `Flies`/`FlyID`/`UsedFlyIDs` aliases for existing analysis. [Animal/agent compatibility](docs/animal-agent-compatibility.md) and [version differences](docs/releases.md) give the scope.

In 0.3 standalone players, editable files are seeded under `Application.persistentDataPath/Config` and recordings under `Application.persistentDataPath/RunData`; existing user files are never overwritten during seeding. `--config-dir <folder>` and `--data-dir <folder>` override these roots. Editor defaults remain `Assets/StreamingAssets` and `Assets/RunData`. Versions 0.1/0.2 retain their older application-relative paths. See [getting started](docs/getting-started.md) and [run snapshots](docs/run-config-snapshots.md).

Repeated transitions now have explicit owners for trial materials, generated Optomotor resources, writer handles and tracking workers. The in-scene `reloadScene: false` path remains available. See [scene lifecycle fixes and their limits](docs/scene-lifecycle.md); no original crash dump establishes a single cause.

Replay reads saved animal poses and recorded stimulus evidence. Preserve the complete run directory, its `Configs` archive and `Skyboxes`; choose **Replay a recorded run** in setup, use its **Browse…** folder picker, or pass `--replay-session <folder>`, then follow the [replay guide](docs/replay.md). Older incomplete recordings cannot establish stimulus details that were never saved.

## Reference and source development

| Guide | Contents |
| --- | --- |
| [Getting started](docs/getting-started.md) | Downloads, launch/configuration, data locations and controls. |
| [Configuration reference](docs/configuration-reference.md) | Parameters, units, defaults, precedence and historical no-op fields. |
| [Configuration studio](docs/configuration-ui.md) / [GUI and HUD](docs/configuration-ux.md) | Forms, raw JSON, inherited values, Undo/Redo and dashboard. |
| [Data formats](docs/data-formats.md) / [run snapshots](docs/run-config-snapshots.md) | CSVs, clocks, units, original inputs and immutable versions. |
| [Replay](docs/replay.md) | Saved-run loading, timeline, overview and reconstruction limits. |
| [Kannadi](docs/kannadi-modernization.md) | Mirror populations, camera routing and software validation history. |
| [Experiment phases](docs/experiment-phases.md) | Pre/stimulus/post conditions and heading references. |
| [Release history](docs/releases.md) | Archival source pins, platform builds and compatibility changes. |

Packages are pinned in `Packages/manifest.json` and `packages-lock.json`. After pulling an input-backend change into an open Editor, restart it. For optional Linux/FicTrac acquisition setup, see the [source installation guide](docs/getting-started.md#source-and-tracker-setup).

Archived 0.3 verification includes [1,293 Unity lifecycle/replay checks](docs/lifecycle-replay-validation-results.json), with **150 actual Choice↔Swarm scene loads** at accelerated 0.1-second durations and 150 native resource teardown cycles; [43 JSON compatibility contracts](docs/config-compatibility-validation-results.json); [71 rig isolation checks](docs/release-readiness-results.json); and [2,946 fresh 0.3 GUI checks/captures](docs/configuration-dashboard-v0.3-validation-results.json). [Release verification](docs/releases.md#software-verification) gives the scope, including 630 offline replay checks. This does not establish a cure for the historical crash during 30-second hardware acquisition.

Run `python3 tools/validate_kannadi_assets.py` and `python3 tools/validate_config_templates.py` for static checks. Unity regression runners under `Assets/Editor` require a disposable copy containing the `KANNADI_VALIDATION_COPY` sentinel. Historical reports document synthetic/rendered software checks; physical LED timing, tracker calibration and display mapping require a bench check on the actual apparatus.

The prior README identifies MIT licensing, but this checkout has no root LICENSE file. Third-party assets retain their own license terms. Engine: [Unity](https://unity.com/). Tracking transport: [NetMQ](https://github.com/zeromq/netmq). Astronomical orientation: bundled Astronomy Engine with its retained MIT notice.

</details>

## Citation

Use [CITATION.cff](CITATION.cff) and record the exact release/source revision used for acquisition. [Citation and stimulus credits](https://pvnkmrksk.github.io/matrexVR/manual/citation.html) distinguish the software from its bundled dependencies and sky sources.
