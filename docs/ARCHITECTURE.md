# MATREX VR — system architecture (developer reference)

This document describes how the ledpanelVR runtime is structured: control flow, data paths, coordinate conventions, timing, and assumptions. For operator setup and JSON field tables, see [README.md](../README.md). Legacy assets: [LEGACY.md](LEGACY.md).

---

## 1. System context

```
                    ┌─────────────────────────────────────────┐
                    │  ControlScene (boot, UI, DDOL services)   │
                    │  MainController, MasterDataLogger, StatusUI │
                    └──────────────────┬──────────────────────┘
                                       │ StartSequence()
                                       ▼
              ┌────────────────────────────────────────────────────┐
              │  sequenceConfig.json → scene loads / in-scene steps   │
              └────────────────────────┬───────────────────────────────┘
                                       │
         ┌─────────────┬───────────────┼───────────────┬─────────────┐
         ▼             ▼               ▼               ▼             ▼
    Choice scene   Choice_3d      Optomotor      Dynamic (desync)  ReplayScene
    ChoiceCtrl     ChoiceCtrl     OptoCtrl       DynSeqCtrl        ReplayCtrl
         │             │               │               │             │
         └─────────────┴───────────────┴───────────────┴─────────────┘
                                       │
                    Per-VR prefab (VR1…VR4): ViewportSetter, ZmqListener,
                    ClosedLoop, DataLogger → CSV under Assets/RunData/
```

**External inputs:** ZMQ pose streams (FicTrac, Kinefly bridge, etc.) on ports from `system_config.json`.

**Outputs:** Timestamped CSV logs, copied configs, optional zip archive on quit.

---

## 2. Lifecycle and timing

### 2.1 Boot (`ControlScene`)

| Phase | Component | Behavior |
|-------|-----------|----------|
| `Awake` | `MainController` | `DontDestroyOnLoad`; loads `system_config.json`; creates black background camera + `StatusUI` |
| `Start` | `MainController` | Loads `sequenceConfig.json`; copies configs to run folder when logging exists |
| `Start` | `MasterDataLogger` | Singleton; creates `Assets/RunData/<yyyyMMdd_HHmmss>/` |
| User action | UI button | Calls `MainController.StartSequence()` |

### 2.2 Sequence clock (`MainController`)

- **`timer`** decrements with `Time.deltaTime` each frame while `sequenceStarted`.
- When **`timer <= 0`**, advance to next step in `executionOrder` (respects `randomise` / `loop`).
- On each new step: either **`SceneManager.LoadScene`** or **`IInSceneSequencer.AdvanceStep`** (in-place).

**Important:** MainController duration is always active. Scenes with **internal** timelines still need a long enough MC `duration` (or the MC step ends while internal coroutines run).

| Scene type | MC `duration` | Internal timing |
|------------|---------------|-----------------|
| Choice / homing | Wall-clock step length | None (static layout for whole step) |
| Optomotor | Caps entire opto scene | `OptomotorSceneController` coroutine: each `stimuli[].duration` |
| Dynamic (`Choice_desync`) | Should exceed sum of in-scene steps | Per-step `trigger.seconds` or area colliders |
| Replay | N/A (offline) | `replayTime` vs parsed CSV timestamps |

### 2.3 Scene discovery

`OnSceneLoaded`:

1. Re-applies frame rate (`targetFrameRate`, `vSyncCount = 0`).
2. **`FindObjectsOfType<MonoBehaviour>`** — first `ISceneController` wins (undefined order if multiple).
3. Calls `InitializeScene(parameters)` with `gain` injected if missing.
4. Resets `timer = currentStep.duration`.

**Assumption:** Exactly one `ISceneController` per experiment scene.

### 2.4 DontDestroyOnLoad objects

- `MainController` (and children: background cam, status UI root)
- `MasterDataLogger`

Experiment scenes are **single loaded scene** (not additive), but DDOL services persist.

---

## 3. Configuration layering

```
system_config.json          → per-VR hardware + ZMQ + closedLoopMode (global targetDisplay)
sequenceConfig.json         → ordered steps: sceneName, duration, gain, parameters
parameters.configFile       → Choice / Optomotor scene JSON (StreamingAssets)
parameters.design           → DynamicSequence design file (optional key name; see DynamicSequenceController)
```

Copied at run start into `RunData/<timestamp>/` with prefixes for provenance.

**`closedLoopMode`** (system config, enum): `FicTrac` | `Kinefly` | `Tirbala` — sets defaults on `ClosedLoop` at Start. Scene JSON can still toggle `closedLoopOrientation` / `closedLoopPosition`.

---

## 4. Data flow: tracking → world → logs

### 4.1 ZMQ ingress (`ZmqListener`)

- **Thread:** blocking NetMQ subscriber loop (not main thread).
- **Endpoint:** `tcp://{address}:{port}` from `SystemConfig` matched by GameObject name containing `vrId`.
- **Payload (JSON):** `{ x, y, z, roll, pitch, yaw }` — angles in **degrees** for `Quaternion.Euler(pitch, yaw, roll)`.

**Assumption / caveat:** Pose fields are read from the socket thread in `ClosedLoop.Update` without locking — acceptable if updates are atomic enough for floats; not guaranteed thread-safe.

### 4.2 Closed-loop extraction (`ClosedLoop.GetCurrentFicTracData`)

Internal 3-vector (historical naming — used for all modes):

| Component | Source | Meaning |
|-----------|--------|---------|
| `.x` | `position.y` | Horizontal channel 1 (FicTrac: tangent plane; Kinefly: often left eye rad) |
| `.y` | `position.x` | Horizontal channel 2 |
| `.z` | `quaternion.eulerAngles.y` → rad | Yaw |

Position closed loop (FicTrac-style):

```text
delta = current - last
positionDelta = ficTracRotationOffset * Vector3(delta.x, 0, delta.y) * sphereRadius
```

- **`sphereRadius`** = `sphereDiameter / 2` with diameter in **cm** from system config.
- **`ficTracRotationOffset`** accounts for initial heading + optional `randomInitialRotation` from scene JSON.

### 4.3 Orientation modes

| Mode | `useYawMode` | `useForceMode` | Orientation when ON |
|------|--------------|----------------|---------------------|
| **FicTrac** | false | false | Delta yaw (rad→deg per frame): `Rotate(0, Δyaw°, 0)` local |
| **Kinefly** | true | false | `yawGain * (yaw_rad - yawDCOffset_rad)` → deg/s × `dt` on local Y |
| **Tirbala** | false | true | **Placeholder:** force on X, torque on Y from `.x`/`.z` channels |

Scene-level flags **`closedLoopOrientation`** / **`closedLoopPosition`** gate whether each path runs.

**Per-VR DC offset:** `MainController` keys `1–4` select VR; `[` / `]` adjust `yawDCOffset` (persisted across scene loads).

### 4.4 Viewport / LED layout (`ViewportSetter`)

- Reads same `SystemConfig` as sibling `ZmqListener`.
- **`displayOrder`:** string of letters `D,R,B,L,F,U` → child cameras named `Main Camera {letter}`.
- Maps cameras into a horizontal or vertical strip on the LED panel (`ledPanelWidth/Height`, `startRow/Col`).

**World vs display:** Unity world +X/+Y/+Z are standard; animal heading uses yaw about **world Y** after offsets. LED faces are a **remapping** of six cube faces onto a flat panel — not a rotation of the physics frame.

### 4.5 Logging (`MasterDataLogger` → `DataLogger`)

| Stage | Detail |
|-------|--------|
| Init | `MasterDataLogger.Start` finds `DataLogger` in **same scene as MDL at Start** (ControlScene has MDL; experiment loggers init when their scene loads — see note below) |
| Per frame | Base columns + optional ZMQ + subclass `CollectAdditionalData` |
| Flush | Buffered lines, async write coroutine |
| Shutdown | Zip `RunData/<timestamp>/` → `<timestamp>.zip` |

**Note:** `DataLogger.Start` calls `InitLog` which uses `MasterDataLogger.Instance.directoryPath` — DDOL master must exist (ControlScene loads first). Loggers on VR prefabs in experiment scenes append to the same folder.

**Time column:** `DateTime.Now` string, not `Time.time`. Replay converts to seconds relative to first row.

**ZMQ columns:** `SensPos*` raw; `SensRot*` Unity euler (deg); `SensRot*Rad` euler×Deg2Rad (not raw socket roll/pitch/yaw).

---

## 5. Scene paradigms

### 5.1 Choice / homing (`ChoiceController`)

- **Input:** `parameters.configFile` → `SceneConfig` JSON.
- **Spawn:** For each `objects[]` entry, instantiate prefab (name = `type`) × 4 VR layers (`ChoiceVR1`…`ChoiceVR4`).
- **Position:** Polar — `angle`° azimuth, `radius` & `height` in Unity units; 0° → +Z.

```text
x = radius * sin(angle°)
z = radius * cos(angle°)
y = height
```

- **Skybox:** `skyboxPath` under StreamingAssets.
- **Wind / AGL:** Passed to `ClosedLoop` when set on config.

### 5.2 Optomotor (`OptomotorSceneController`)

- **Input:** optomotor JSON (`loop`, `stimuli[]`).
- Creates drum + `OptomotorDataLogger` (in `Loggers/`).
- **`DrumRotator`:** `rotationAxis` Yaw | Pitch | Roll; non-yaw axes tilt 90° then spin local yaw; `speed` in **deg/s**.
- **Grating:** `frequency` cycles/revolution, `contrast` 0–1, `dutyCycle` 0–1.

### 5.3 Dynamic in-scene (`DynamicSequenceController`)

- Implements **`IInSceneSequencer`** (per VR rig coroutines).
- Design file: default `dynamicSequenceDesign.json`, overridable via `parameters.design`.
- Triggers: `time` (`seconds`) or `area` (box/cylinder, optional timeout / exit).
- **`AdvanceStep` on interface is empty** — MC in-place advance does not drive this controller; use long `duration` or `reloadScene: true` between MC steps.

### 5.4 Swarm (`SwarmController`, `Vishwaroopa`)

- Present in codebase; **Swarm scene disabled** in build settings; no prefab wiring in active pipeline. Treat as legacy unless re-enabled.

### 5.5 Replay (`ReplayController`, `ReplayEnvironmentLoader`)

Offline path (see [§7](#7-replay-subsystem)).

---

## 6. Coordinate and unit reference

| Quantity | Unit / frame | Where |
|----------|----------------|-------|
| Unity positions | meters (scene units) | transforms, CSV `GameObjectPos*` |
| Unity rotations (logged) | degrees (euler) | CSV `GameObjectRot*` |
| ZMQ roll/pitch/yaw | degrees → quaternion | `ZmqListener` |
| FicTrac internal yaw | radians in `.z` of 3-vector | `ClosedLoop` |
| Yaw DC offset | radians (inspector/log) | `ClosedLoop`, README UI in deg via ×Rad2Deg |
| Sphere diameter | **cm** | `system_config.json` |
| Optomotor speed | deg/s | stimulus JSON |
| Choice polar angle | degrees | scene JSON |
| Wind direction | degrees, meteorological “from” | scene JSON |
| Playback time (replay) | seconds since first CSV row | `ReplayController` |

### Axis summary

| Axis | Typical use |
|------|-------------|
| **World Y** | Vertical; yaw rotation for heading |
| **World XZ** | Ground plane; FicTrac position deltas after offset |
| **Local Y** | Closed-loop incremental rotation application |
| **Drum local** | Grating rotation after axis tilt for pitch/roll stimuli |

---

## 7. Replay subsystem

**Purpose:** Relive a recorded session from a RunData folder — configs + CSV — without live ZMQ or closed loop.

```
RunData/<session>/
  *.csv                    → ReplaySessionData (per rig = VR column)
  *_sequenceConfig.json    → planned steps + design/config file names
  frames/<VRn>/            → live experiment PNG strips (ExperimentFrameRecorder)
  frames_replay/<VRn>/     → replay export PNGs (ReplayFrameRecorder)
```

| Component | Role |
|-----------|------|
| `ReplaySessionUI` | Folder picker / dropdown → `LoadSessionFromPath` |
| `ReplaySessionArchive` | Index session JSON + CSV; resolve archived config filenames |
| `ReplaySessionData` | CSV interpolation (poses as logged) |
| `ReplaySceneOrchestrator` | Load real scenes; `InitializeScene` with archived configs |
| `ReplayPoseApplier` | Apply CSV pose; disables `ClosedLoop` / `ZmqListener` |
| `ReplayExperimentHost` | System config stand-in for `MainController` during replay |
| `ReplayConfigPaths` | JSON/skybox paths from session folder when replay active |
| `ReplayController` | Transport controls + frame export |
| `VrPanelFrameCapture` | AsyncGPUReadback LED-strip PNG capture |
| `ExperimentFrameRecorder` | Live capture on VR prefab (with `ViewportSetter`) |

**Replay controls (ReplayScene):**

| Input | Action |
|-------|--------|
| Space | Play / pause |
| ← / → | ±1 s seek (Shift: ±10 s) |
| , / . (hold) | Rewind / fast-forward |
| [ / ] | Halve / double playback speed |
| 1–5 | Speed presets 0.25× … 4× |
| N / M | Previous / next sequence step |
| J / K | Previous / next logged frame |
| Home / End | Start / end |
| R | Start frame export to `frames_replay/` |
| Slider drag | Scrub time (`ReplayScrubSlider`) |

**Build status:** `ReplayScene` is **enabled** in `EditorBuildSettings`.

**Layer naming:** rig `VR1` → layer `ChoiceVR1` for culling.

---

## 8. Optional / legacy components

| Path | Status |
|------|--------|
| `Assets/StreamingAssets/Archive/` | Old sequence/scene JSON (~200+ files). **Not referenced** by active configs in repo root StreamingAssets. Safe to ignore unless you reference paths explicitly. |
| `Assets/Scripts/ZmqSender.cs` | Publishes trial metadata; **not attached** in scenes — optional integration. |
| `Assets/Scripts/Swarm/*` | Legacy swarm experiments |
| `Assets/Scripts/Build*.cs`, `Editor/Build*.cs` | Offline JSON **generators**, not runtime |
| Disabled scenes in `EditorBuildSettings` | Historical variants (`texrug_*`, `two_tree_*`, …) |

Removed in hygiene pass (feature/replay-architecture): commented duplicate `Optomotor/OptomotorDataLogger.cs`, `OptomotorDebugHelper.cs`, unused `SceneDataLogger`, `ExportTransformsToCSV`, `LuminanceChanger`.

---

## 9. Threading and performance assumptions

- Target **60 FPS** (`targetFrameRate`, `vSyncCount = 0` on scene load).
- One ZMQ thread per `ZmqListener`; one CSV row per frame per `DataLogger`.
- `FindObjectOfType` used for `MainController` / `MasterDataLogger` discovery — assumes single instances.
- Four VR rigs maximum in naming convention (`VR1`–`VR4`).

---

## 10. Key source files

| Area | Path |
|------|------|
| Coordinator | `Assets/Scripts/MainController.cs` |
| In-scene API | `Assets/Scripts/InSceneSequence.cs` |
| Tracking | `Assets/Scripts/ZmqListener.cs`, `ClosedLoop.cs` |
| Choice | `Assets/Scripts/ChoiceController.cs` |
| Optomotor | `Assets/Scripts/Optomotor/OptomotorSceneController.cs`, `DrumRotator.cs` |
| Dynamic | `Assets/Scripts/DynamicSequenceController.cs` |
| Replay | `Assets/Scripts/Replay/ReplayController.cs`, `ReplayEnvironmentLoader.cs` |
| Logging | `Assets/Scripts/Loggers/MasterDataLogger.cs`, `DataLogger.cs` |
| VR prefab | `Assets/Prefabs/Camera Prefabs/VR.prefab` |
| Boot scene | `Assets/Scenes/ControlScene.unity` |
| Build list | `ProjectSettings/EditorBuildSettings.asset` |

---

## 11. Extension guidelines

1. New experiment scene: implement `ISceneController`, add to Build Settings, reference in `sequenceConfig.json`.
2. New logged fields: subclass `DataLogger`, `AddColumns` in `Start`, override `CollectAdditionalData`.
3. New closed-loop mode: extend `ClosedLoopMode` + `ApplyModeConfiguration` + document ZMQ semantics.
4. Replay: ensure CSV includes `stepName` / `stepIndex` (set via `DataLogger.SetStep` from controllers that call it).

For yaw-mode math and keyboard detail: [YAW_MODE_README.md](../YAW_MODE_README.md).
