# MATREX VR (ledpanelVR)

Panoramic LED-panel VR for naturalistic behavior experiments: multi-display rendering, closed-loop tracking (FicTrac / Kinefly / force-torque), scripted scene sequences, and CSV logging.

## Quick start (built executable or Editor)

1. Install **FicTrac** (or your tracker bridge) and ensure it publishes JSON pose messages on ZMQ (see [ZMQ input](#zmq-input)).
2. Copy config templates into `Assets/StreamingAssets/`:
   - `system_config.json` ← [`docs/templates/system_config.template.json`](docs/templates/system_config.template.json)
   - `sequenceConfig.json` ← [`docs/templates/sequenceConfig.template.json`](docs/templates/sequenceConfig.template.json)
3. Edit scene steps and per-scene JSON paths (see [Configuration](#configuration)).
4. Launch the build (or Unity Editor). The app starts in **ControlScene**.
5. Press the UI control that calls **Start Sequence** (wired to `MainController.StartSequence()`).
6. After the run, data appear under `Assets/RunData/<timestamp>/` and `Assets/RunData/<timestamp>.zip` on exit.

All runtime JSON lives in **`Assets/StreamingAssets/`** (readable in builds). Paths in JSON are relative to that folder unless noted.

---

## Configuration overview

Three layers work together:

| Layer | File | Loaded by | Purpose |
|--------|------|-----------|---------|
| **System** | `system_config.json` | `MainController` at startup | Per-VR LED layout, ZMQ endpoints, closed-loop mode |
| **Sequence** | `sequenceConfig.json` | `MainController` at startup | Ordered list of Unity scenes, durations, scene JSON refs |
| **Scene / internal** | `*.json` in StreamingAssets | Scene controllers | Stimuli for one step (choice world, optomotor drum, in-scene dynamic design) |

```
sequenceConfig.json  →  sceneName + parameters.configFile
                              ↓
         Choice / Optomotor / DynamicSequence scene loads internal JSON
                              ↓
              DataLogger CSVs + copied configs in RunData/<timestamp>/
```

Full field templates: [`docs/templates/`](docs/templates/). Working examples: `system_config_example.json`, `sequenceConfig.json`, `Homing_*.json`, `optomotor_config_arc.json`, `dynamicSequenceDesign.json`.

---

## System config (`system_config.json`)

One file per machine setup. Top-level fields:

| Field | Type | Description |
|--------|------|-------------|
| `targetDisplay` | int | Unity display index for all VR viewports (0 = primary, 1 = secondary, …) |
| `configs` | array | One entry per VR rig (`VR1` … `VR4`) |

Per-VR entry (`configs[]`):

| Field | Type | Units / values | Description |
|--------|------|----------------|-------------|
| `sphereDiameter` | float | **cm** | Tracker ball diameter; used for closed-loop scaling |
| `ledPanelWidth`, `ledPanelHeight` | int | pixels | LED panel resolution |
| `startRow`, `startCol` | int | panel coords | Viewport origin on the panel grid |
| `horizontal` | bool | — | Panel orientation flag for `ViewportSetter` |
| `zmqAddress` | string | host | ZMQ publisher host |
| `zmqPort` | int | — | Unique port per VR (e.g. 9871–9874) |
| `vrId` | string | `VR1`–`VR4` | Must match GameObject / layer naming |
| `displayOrder` | string | e.g. `RBLFU` | Order of face cameras: **D**own, **R**ight, **B**ack, **L**eft, **F**ront, **U**p — maps to `Main Camera {letter}` |
| `closedLoopMode` | string | `FicTrac`, `Kinefly`, `Tirbala` | Default closed-loop behavior (see below) |

**Closed-loop modes** (set in system config, overridable per scene JSON):

- **FicTrac** — delta orientation from tracker (classic path); yaw mode off, force mode off.
- **Kinefly** — yaw-based closed loop (gain × absolute yaw − DC offset); yaw mode on.
- **Tirbala** — force/torque accumulation mode; force mode on.

GameObject names should include the `vrId` (e.g. `VR1`) so `MainController` applies the right row of `configs`.

---

## Sequence config (`sequenceConfig.json`)

Defines the experiment timeline.

| Field | Type | Description |
|--------|------|-------------|
| `randomise` | bool | If true, step order is shuffled at start |
| `loop` | bool | Repeat entire sequence when finished |
| `sequences` | array | Steps to run |

Each step:

| Field | Type | Description |
|--------|------|-------------|
| `sceneName` | string | Unity scene name (must be in *File → Build Settings*) |
| `duration` | float | **seconds** on this step (unless scene uses internal timing only) |
| `gain` | float | Passed into closed loop as yaw gain for this step (default 1.0) |
| `reloadScene` | bool | If false, same scene can be reused without reload (default true) |
| `parameters` | object | Scene-specific; almost always includes `configFile` |

**`parameters.configFile`** — filename under `StreamingAssets/`:

- **Choice** / **Choice_3d** scenes → scene layout JSON (`Homing_*.json`, etc.)
- **Optomotor** scene → optomotor stimulus list JSON
- Scenes with **DynamicSequenceController** → design file (e.g. `dynamicSequenceDesign.json`) referenced from the scene or sequence as your build expects

Common `sceneName` values in this repo include: `Choice`, `Choice_3d`, `Optomotor`, `ReplayScene`, and terrain variants (`texrug_*`, `two_tree_*`, …). Enable the scene in Build Settings before referencing it.

At run start, `MainController` copies `sequenceConfig.json` and referenced choice/optomotor configs into `RunData/<timestamp>/` with a timestamp prefix.

---

## Scene config — Choice / homing (`Homing_*.json`, etc.)

Loaded by `ChoiceController` when the sequence step sets `parameters.configFile`.

| Field | Type | Description |
|--------|------|-------------|
| `objects` | array | Prefabs to spawn (see below) |
| `closedLoopOrientation` | bool | Scene default for orientation closed loop |
| `closedLoopPosition` | bool | Scene default for position closed loop |
| `initialPosition` | `{x,y,z}` | Unity world units (m) |
| `initialRotation` | `{x,y,z}` | Euler degrees |
| `randomInitialRotation` | bool | Randomize heading at start |
| `backgroundColor` | `{r,g,b,a}` | 0–1; used if no skybox |
| `skyboxPath` | string | Path under StreamingAssets (e.g. `Photosphere/…jpg`) |
| `windSpeed`, `windDirection` | float | Wind slip model; direction = **degrees**, wind **from** that bearing |
| `aglHeight` | float | Fixed height above terrain (m) when used |

**`objects[]`** (prefab `type` must match a prefab name in the scene):

| Field | Type | Units | Description |
|--------|------|-------|-------------|
| `type` | string | — | Prefab name (`tree01`, band types, cylinders, …) |
| `position.radius` | float | Unity units | Distance from origin in XZ |
| `position.angle` | float | **degrees** | Azimuth; 0° = +Z |
| `position.height` | float | Unity units | Y offset |
| `scale` | `{x,y,z}` | — | Local scale |
| `flip` | bool | — | Mirror / alternate spawn path for bands |
| `speed` | float | — | Locust mover speed when component present |
| `mu`, `visualAngleDegrees` | float | — | Experiment-specific; see prefab docs |
| Band / swarm fields | various | — | `numberOfInstances`, `kappa`, `spawnLengthX/Z`, `hexRadius`, … for band prefabs |

Each object is instantiated per VR layer (`ChoiceVR1` … `ChoiceVR4`).

---

## Scene config — Optomotor (`optomotor_*.json`)

Loaded by `OptomotorSceneController`.

| Field | Type | Description |
|--------|------|-------------|
| `loop` | bool | Repeat `stimuli` list |
| `stimuli` | array | Timed drum segments |

Each stimulus:

| Field | Type | Units | Description |
|--------|------|-------|-------------|
| `duration` | float | **seconds** | Segment length |
| `speed` | float | **deg/s** | Drum rotation speed |
| `clockwise` | bool | — | Rotation direction |
| `rotationAxis` | string | `Yaw`, `Pitch`, `Roll` | Drum axis |
| `frequency` | float | cycles/revolution | Grating spatial frequency |
| `contrast` | float | 0–1 | Grating contrast |
| `dutyCycle` | float | 0–1 | Dark/light duty |
| `color1`, `color2` | string | `#RRGGBB` | Grating colors |
| `closedLoopOrientation` | bool | — | Per-segment override |
| `closedLoopPosition` | bool | — | Per-segment override |

---

## Internal config — Dynamic in-scene sequence (`dynamicSequenceDesign.json`)

Used by `DynamicSequenceController` inside scenes that implement `IInSceneSequencer` (no `MainController` step switch; steps advance on triggers).

| Field | Type | Description |
|--------|------|-------------|
| `seed` | int | Random seed; `-1` = random |
| `repetitions` | int | Repeat full step list |
| `sync` | bool | Synchronize step transitions across VRs |
| `steps` | array | In-scene steps |

Each step: `name`, `trigger`, `objects`, `camera`, optional `skybox`, `closedLoopOrientation`, `closedLoopPosition`, `initialPosition`, `initialRotation`, `randomInitialRotation`.

**Triggers** (`trigger.type`):

- `time` — advance after `seconds`
- `area` — enter/exit volume (`areaTag`, `vrId`, `shape` `box`|`cylinder`, `size` / `boxSize` / `radius` / `height`, optional `timeoutSeconds`, `triggerOnExit`, `advanceOnTrigger`)

**Objects** use `polar` `{radius, angle, height}` (same geometry as Choice), `material`, `scale`, `flip`, `visualAngleDegrees`, optional `color` `[r,g,b,a]`.

**Camera** per VR: `vrId`, `clearFlags` (`SolidColor`, `Skybox`, …), optional `bgColor` `[r,g,b,a]`.

---

## ZMQ input

Each VR’s `ZmqListener` subscribes to `tcp://{zmqAddress}:{zmqPort}` and expects JSON messages:

```json
{ "x": 0.0, "y": 0.0, "z": 0.0, "roll": 0.0, "pitch": 0.0, "yaw": 0.0 }
```

| Field | Meaning |
|--------|---------|
| `x`, `y`, `z` | Position — **instrument-specific** (FicTrac: spatial units; Kinefly: often radians in x/y for left/right eye) |
| `roll`, `pitch`, `yaw` | Euler angles in **degrees** for Unity rotation |

Logged columns preserve raw ZMQ position and radians in `SensRot*Rad`; Unity euler copies are in `SensRot*`.

---

## Keyboard shortcuts

### Global (`MainController` — active during sequences)

| Key | Action |
|-----|--------|
| `Tab` | Toggle status overlay (`StatusUI`) |
| `1`–`4` | Select VR1–VR4 for DC offset adjustment |
| `[` / `]` (hold) | Decrease / increase **yaw DC offset** for selected VR |
| `Esc` | Quit application |

### Closed loop (`ClosedLoop` on each VR rig)

| Key | Action |
|-----|--------|
| `O` | Toggle closed-loop **orientation** |
| `P` | Toggle closed-loop **position** |
| `Ctrl+Y` | Toggle **yaw mode** (Kinefly-style) |
| `Ctrl+F` | Toggle **force/torque mode** (Tirbala) |
| `+` / `-` | Increase / decrease yaw gain |
| `Ctrl+` / `Ctrl-` | Increase / decrease force gain |
| `Ctrl+]` / `Ctrl+[` | Increase / decrease torque gain |
| `R` | Reset position and rotation |
| `Esc` | Quit (when focus on closed-loop object) |

### Manual navigation (`Keyboard` on fly / debug rigs)

| Key | Action |
|-----|--------|
| `↑` `↓` `←` `→` | Move forward / back / left / right |
| `C` / `Z` | Up / down |
| `W` / `S` | Pitch down / up |
| `A` / `D` | Yaw left / right |
| `Q` / `E` | Roll CCW / CW |
| `Ctrl+Space` | Toggle autopilot (constant forward) |
| `Shift+↑`/`↓` | Increase / decrease translate speed |
| `Shift+W`/`S` | Increase / decrease rotate speed |
| `Esc` | Stop sequence and load **ControlScene** |

### Optomotor drum debug (`DrumRotator`, when manual control enabled)

| Key | Action |
|-----|--------|
| `R` | Reset drum rotation |
| `Space` or `\` | Pause / resume rotation |
| `D` | Debug log drum state |

### Replay (`ReplayController` in ReplayScene)

| Key | Action |
|-----|--------|
| `Space` | Play / pause |
| `←` / `→` | Step back / forward |
| `Shift+←` / `Shift+→` | Faster seek |
| `N` / `M` | Previous / next sequence step |

More detail on yaw closed loop: [`YAW_MODE_README.md`](YAW_MODE_README.md).

---

## Data logging

### Run folder

On play, `MasterDataLogger` creates:

```
Assets/RunData/<yyyyMMdd_HHmmss>/
```

On application exit, the folder is zipped to `Assets/RunData/<yyyyMMdd_HHmmss>.zip`.

Copied into the run folder: `system_config.json`, `sequenceConfig.json`, and each step’s `configFile` (with timestamp prefix).

### CSV files

Per logger component: `<timestamp>_<SceneName>_<GameObjectName>_.csv`

**Base columns** (all `DataLogger` derivatives):

| Column | Description |
|--------|-------------|
| `Current Time` | Local time `yyyy-MM-dd HH:mm:ss.fff` |
| `VR` | Logger GameObject name |
| `Scene` | Active Unity scene |
| `CurrentSequenceScene` | Sequence step scene name |
| `ConfigFile` | Internal JSON filename for this step |
| `CurrentTrial`, `CurrentStep` | Sequence indices |
| `GameObjectPosX/Y/Z` | Unity world position (m) |
| `GameObjectRotX/Y/Z` | Unity euler rotation (degrees) |

**With ZMQ** (`includeZmqData = true`):

| Column | Description |
|--------|-------------|
| `SensPosX/Y/Z` | Raw ZMQ position |
| `SensRotX/Y/Z` | Unity euler from quaternion (degrees) |
| `SensRotXRad/YRad/ZRad` | Raw rotation (radians) |

**Optomotor** (`OptomotorDataLogger`): adds `StimulusIndex`, `Frequency`, `Contrast`, `DutyCycle`, `Speed`, `RotationAxis`, `ClockwiseRotation`, closed-loop flags, `UseYawMode`, `YawGain`, `YawDCOffset`, `YawInput`, `YawOutput`, `SphereDiameter`.

Derived loggers may add columns via `AddColumns()` in code.

### Analysis tips

- One row per frame per logger object.
- Join on `Current Time` or row index within a file; align VRs by timestamp.
- `stepIndex`, `stepName`, `loopIndex`, `cumulativeStep` appear when the logger’s `SetStep()` is used.
- Python helper: `Assets/Scripts/plot_by_stepname.py` (expects logged CSV layout).

---

## Installation (development)

### Prerequisites

- Ubuntu (tested workflow) or macOS/Windows with Unity Hub
- Unity **2024.x LTS** (open project with Hub-recommended LTS; avoid non-LTS 6000 unless you know the project compiles)
- FicTrac or compatible ZMQ tracker bridge
- NetMQ / ZeroMQ for tracking

### FicTrac

```bash
mkdir -p ~/src && cd ~/src
git clone https://github.com/pvnkmrksk/fictrac.git
cd fictrac
chmod +x install_ubuntu.sh
./install_ubuntu.sh
```

### Clone and open in Unity

```bash
cd ~/src
git clone https://github.com/pvnkmrksk/ledpanelVR.git
# or matrexVR remote name — use your fork URL
unityhub  # Add project folder, install Editor LTS, open Assets/Scenes/ControlScene.unity
```

### Unity Hub (Linux)

```bash
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
sudo apt update && sudo apt-get install unityhub
```

---

## Core scripts (runtime)

| Script | Role |
|--------|------|
| `MainController.cs` | Loads configs, runs sequence, VR DC offset UI |
| `ClosedLoop.cs` | Tracker → transform; modes FicTrac / Kinefly / Tirbala |
| `ZmqListener.cs` / `ZmqSender.cs` | ZMQ pose in / metadata out |
| `ChoiceController.cs` | Choice scene JSON → spawned world |
| `OptomotorSceneController.cs` | Optomotor JSON → drum stimuli |
| `DynamicSequenceController.cs` | In-scene stepped designs |
| `DataLogger.cs` / `MasterDataLogger.cs` | CSV logging and run archive |
| `Keyboard.cs` | Manual fly controls |
| `ReplayController.cs` | Playback from `RunData` |
| `ViewportSetter.cs` | LED panel viewport layout |

---

## Contributing

Issues and pull requests welcome. Keep experiment JSON under `StreamingAssets/`; add templates under `docs/templates/` when introducing new fields.

## License

[MIT License](LICENSE)

## Acknowledgements

- [Unity](https://unity.com/)
- [NetMQ](https://github.com/zeromq/netmq)
- [FicTrac](https://github.com/rjdmoore/fictrac)
