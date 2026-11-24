# MATREX VR recording files

Each timestamped folder under `RunData/` is one recording. The name uses the acquisition computer’s local time (`yyyyMMdd_HHmmss`), with a numeric suffix if needed. This guide applies to the whole library and to individual run folders. Files depend on the player version, scene, tracking backend, and recording options. Setup-only folders can contain configuration and diagnostics before an experiment has produced any animal rows.

## Start here

1. Read `recording_manifest.json` for the outcome, recorded rigs, and file inventory. A sealed recording has `lifecycle.status` set to `finalized` and `lifecycle.writersDrained` set to `true`. Check `warnings`, `writerEvidence`, and `media` for coverage issues.
2. Open the per-rig animal CSVs for pose and tracking analysis. Read `AnimalMetadata.json` to link a rig to the recorded animal. Use the actual CSV header because fields vary with the installed build and scene.
3. For replay, select the whole folder in **Replay recordings**. Keep the saved manifests, journals, snapshots, and optional footage together. Extract a ZIP before opening it. Keep the acquisition player version and build identity for later use.

## Main files

| File or directory | What it contains |
| --- | --- |
| `recording_manifest.json` | Acquisition summary and seal. `lifecycle` holds the player identity, outcome, and timestamps. `poseRows` separates stored, playable, excluded, and rejected rows. `observed` describes conditions and rigs actually seen in the logs. `files` lists top-level CSV and `.matpop` files with their role, byte count, SHA-256, timestamps, and counts where known. `writerEvidence` records closed population writers, frames, rows, dropped frames, queue depth, and failures. `metadataFiles` inventories supporting files. `media` summarizes camera collection. `wallClockSpanSeconds` is a local-clock span from the files. |
| `<run>_<scene>_<object>_.csv` | Human-readable samples, generally one logged row per rendered frame. Identify animal rigs using `VR`, the object and scene, and `files[].role` in the manifest. Some CSVs record a stimulus object such as the optomotor drum or a color-drift cylinder. |
| `*.matpop` | Binary snapshots of simulated populations, Band agents, or Kannadi peer avatars. Each stream has a frame/time series, an identity table, and a footer with frame and sample counts. See **Population and fast replay files** below. |
| `AnimalMetadata.json` | Operator-supplied experiment and animal metadata, including `Animals`, `UsedAnimalIDs`, rig mapping, and notes. `FlyMetaData.json`, `Flies`, `UsedFlyIDs`, and `FlyID` are legacy equivalents or aliases. Prefer canonical animal fields when both are available. Library-wide last-used metadata may also appear under `RunData/Backup/`. |
| `system_config.json`, `sequenceConfig.json`, and `<run>_<scene>_*.json` | Saved system/rig/calibration options and authored sequence files. Timestamped or scene-prefixed files are saved copies. See the config manifest’s roles and load history when multiple versions exist. |
| `config_manifest.json` | Index of archived inputs. `complete`, `errors`, and `unavailableOptionalReferences` describe archive coverage. Each `files` entry gives source/reference names, roles, size, SHA-256, `snapshotPath`, `readablePath`, and load history. `consumed` distinguishes inputs actually loaded from captured dependencies. Use this file when selecting the exact input bytes for a particular load. |
| `Configs/Files/`, `Configs/Snapshots/` | Human-readable copies and content-addressed snapshots of the system, sequence, experiment JSON, and referenced images, models, trajectories, or calibration files captured by the run. `snapshotPath` in `config_manifest.json` points to the exact archived version. |
| `replay_contract.json` | Replay schema version and acquisition player identity. `player` includes application version, Unity version, build GUID, and source provenance where available. The compatible player uses this contract to select its reconstruction path. |
| `presentations.jsonl` | One entry per saved presentation, keyed by `id`. It records the actual execution step, authored row, trial, phase, effective config and parameters, RNG initialization, rig poses, and resolved population visuals. Join its `id` to `presentationId` in the pose CSV and world journal. |
| `world_state.jsonl` | Per-rendered-frame replay evidence. Entries include the presentation ID, Unity frame, clock and shader values, active target keys, and checkpoints for changed targets. It supports seeking and restoring scene objects and visual state. |
| `experiment_start.json` | Actual start marker with scene, first presentation ID, Unity frame, monotonic seconds, UTC, and preparation duration. |
| `reproducibility.json`, `random_streams.jsonl` | Configured and resolved seed, RNG mode/algorithm/scope, and individual random-stream keys, occurrence numbers, and seeds. Use the effective presentation and world evidence alongside them for reconstruction. |
| `experiment_phases.jsonl`, `heading_reference.jsonl`, `rugosity_states.jsonl`, `runtime_display_settings.jsonl` | Optional evidence for phase edges, heading references, terrain state, and runtime display/rendering changes. Join by the timestamps, scene, or frame fields each journal supplies. |
| `replay_evidence_errors.jsonl`, `runtime_trace.log` | Replay-evidence failures and chronological runtime/scene/recording diagnostics. Read these with `lifecycle` and `warnings` when investigating stops, gaps, or unsupported state. |
| `Skyboxes/` | Saved sky textures and provenance. `skyboxId` in the logs selects `<id>.png` and `<id>.json`. `usage.jsonl` and `loads.csv` document image activation, frame, wall time, and represented sky time when available. |
| `thumbnail.png` | Preview image used by the recording library. |

JSONL is UTF-8 with one JSON object per line. `presentations.jsonl` and `world_state.jsonl` append a `sha256` member covering the compact JSON payload bytes before that member is appended, including the original closing brace. Preserve the lines verbatim for verification. A reserialized or reformatted object can have a different hash despite equivalent values.

## Time, rigs, and coordinates in the animal CSV

| Column family | How to read it |
| --- | --- |
| `Current Time`, `poseWallTicks` | Local wall-clock time, with up to seven fractional digits in the text field. Ticks are .NET 100 ns units. Preserve the workstation’s timezone for cross-device analysis. |
| `monotonicTime`, `unityFrame`, `presentationId` | Monotonic Unity seconds, rendered-frame number, and presentation key. Use these for within-run ordering and joining replay evidence. Older CSVs may only provide wall time. Time differences and gaps should come from timestamps. |
| `VR`, `Scene`, `CurrentSequenceScene`, `sourceSequenceScene` | Rig name, loaded scene, configured outer scene, and authored source scene where available. The loaded scene and the source scene can differ across versions. |
| `CurrentTrial`, `CurrentStep`, `stepIndex`, `stepName`, `loopIndex`, `cumulativeStep`, `experimentPhase` | Trial and execution order, inner-step and loop context, and recorded phase. `CurrentStep` is the run’s execution order, which can differ from authored order when randomized. An inner step may be `-1` when unused. Use the actual phase and presentation for analysis. |
| `GameObjectPosX/Y/Z`, `GameObjectRotX/Y/Z` | Virtual rig’s presented world position and Euler rotation in degrees. Unity axes are X right, Y up, Z forward. Position units come from the scene configuration, commonly centimetres in walking scenes and metres in terrain or migration scenes. Handle wraps and resets as discontinuities. |
| `SensPos*`, `SensRot*`, `SensRot*Rad`, `wireYaw`, `trackingMode`, `trackingImplementation`, `trackingInputUnits` | Tracker input values, Euler display values, explicit raw-radian fields where supported, and the conversion/units labels. Use the backend and units fields before comparing input data across rigs. |
| `trackerAppliedThisFrame`, `trackerInput*`, `trackerReceiverSessionId`, `trackerReceiveSequence`, `trackerReceivedUtcTicks` | Whether an input was applied on that rendered frame, its source identity/epoch/sequence/time, and receiver identity and UTC receipt time. Use the labeled source clock. These fields help identify repeats and gaps and align archived video where supported. |
| `yawGain`, `yawDC*`, `yawOutputDegPerSecond`, `autoTrim*`, `flight*` | Applied tracking gain, offsets, yaw rate, and optional Kinefly trim and flight detection evidence. The suffixes identify degree, radian, rate, window, or sample-count units where present. |
| `swapElapsedSec`, `swapWallClock`, `grayAtTrialStart`, `blackSideAtTrialStart`, `skyboxId`, `skyboxSampleUtc` | Event or stimulus context. Blank fields are normal when a mechanism is unused or the event did not occur on that row. |

Use a CSV parser that handles quotes and embedded delimiters. Count playable samples separately from setup/idle/handoff rows and malformed rows when comparing the CSV with the recording library. The recorded world pose can incorporate closed-loop gain, resets, or virtual motion in addition to the measured tracker input.

## Population and fast replay files

- `MATPOP01` files hold uncompressed binary frames. `MATPOP02` files use independently gzip-compressed blocks and a frame directory for seeking. Both preserve the sampled float32 positions, Euler rotations, scale, and available animation and visibility state. The file’s first eight bytes identify its version. Unity frame and local wall-clock ticks identify a frame, and the footer identifies stream totals and the identity table. A zero-agent frame is still a recorded frame. Streams without a valid footer can be unreadable after an interruption.
- **Skip swarm data** leaves the animal CSV and scene evidence available and omits the virtual population snapshots. Historical population formats can also appear as `*.csv` or `*.csv.gz`. Read the header and schema for the run’s version.
- `.matpose` files are disposable local binary caches of the animal CSV fields used in replay. The packaged launcher stores them under `Cache/MatrexReplay/AnimalPose-v1/`, outside `RunData/`. Replay builds or refreshes these from the CSV as needed, and future recordings can prime them during finalization. Preserve the original CSVs when archiving or sharing a run.

The repository’s standard-library Python exporter supports both population file versions. Use an output folder outside the acquisition directory. Extract a ZIP before exporting its streams.

```sh
python3 tools/unpack_population.py "/path/to/RunData/<run>/<stream>.matpop" "/path/to/exports/agents.csv"
```

The exported CSV retains sample identity, frame time, positions, rotations, scale, and animation fields where captured. Zero-agent frames are represented in the source binary. An ordinary spreadsheet or Python CSV reader can read the original animal CSVs directly.

## Optional tracker media

| File or directory | What it contains |
| --- | --- |
| `companion_provenance.json` | Tracker backend and rig mapping, process ownership and verification, version/executable identity, requested recording settings, and captured calibration paths/hashes where available. The same descriptors can be embedded in the config manifest. |
| `native_recording_run.json`, `native_recording_start.json`, `native_recording_control.jsonl` | Run/take identity, recorder-control policy and per-rig startup results, and chronological requests/acknowledgments or errors. Use the control state and media manifest to confirm that footage was actually captured and archived. |
| `media_manifest.json`, `media/` | Optional collected clips, source and rig IDs, original paths or relative archive paths, file lengths and hashes, container/codec metadata, sync mode, warnings, and indexed frame mappings such as `*.index.jsonl`. Native ancillary files can include camera frame logs, tracking data, and settings. |
| `native_unpresented_takes.json`, `native_unrecorded_takes.json`, `native-takes/` | Optional native artifacts when a take ended before any presented poses or when the tracker reports that there are no usable video frames. |

`consumed-frame-id` aligns supported camera frames to the software input that Unity consumed. `operator-offset` is an approximate manually set alignment. `unavailable` is footage without a verified mapping. Camera recording settings and tracker ownership determine whether clips are available.

## Open, copy, and verify

The workstation launcher uses a `RunData/` folder beside the launcher’s profile files. `Config/` contains the live settings, and `Cache/` contains disposable indexes. The **Replay recordings** screen displays the configured run-data location. `--data-dir` selects a different destination, and **Browse…** opens past folders in place, including the older `MATREX VR RunData` layout.

Keep the original folder intact. Use its saved per-file size and SHA-256 values to check acquisition files against the recording manifest. The config manifest covers configuration snapshots, and the media manifest covers archived camera artifacts. A recording copied with only some files can remain usable for the evidence that survives, with missing tracks or incomplete frames indicated by the player. Preserve the original manifests and journals for that diagnosis.

The repository’s packaging command checks a finalized archive and writes a separate ZIP. The normal Stop action seals the run folder.

```sh
python3 tools/package_recording.py "/path/to/RunData/<run>" "/path/to/exports/<run>.zip"
```

Source and companion documentation are in the [MATREX VR repository](https://github.com/pvnkmrksk/matrexVR).
