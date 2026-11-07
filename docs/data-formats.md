# Recorded data and units

Recorded swarm **input** uses `ID,X,Y,Z,time` with Unity axes (+X east, +Y up, +Z north), metres, and relative seconds. See [swarm-playback.md](swarm-playback.md). Presented/logged positions use the scene's world units: metres on terrain/migration, centimetres in walking scenes. The input CSV scale and physical moth scale are configured independently.

MasterDataLogger creates `Assets/RunData/yyyyMMdd_HHmmss/` in the Editor (current standalone source uses `RuntimePaths.RunDataRoot`, default `<player directory>/MATREX VR RunData` beside the app or executable; a protected installation uses `Application.persistentDataPath/Installations/<installation-id>/RunData`. `--data-dir` overrides either location. Earlier standalone builds used the shared `Application.persistentDataPath/RunData`; 0.1/0.2 retain `Application.dataPath/RunData`). A numeric suffix is added if that timestamp already has a folder or ZIP. It snapshots the system, sequence and referenced experiment files before the sequence starts, and retains exact versions consumed on later loads. Stop seals the run directory. [Package the run](manual/recording-pipeline.md#stop-and-package-a-run) separately when acquisition is idle. Keep the original bundle alongside exported data to retain gains, stimulus conditions, camera layout and physical scale. See [run configuration snapshots](run-config-snapshots.md) for the layout, manifest and historical audit.

Sampling follows rendered frames. The animal CSV buffer writes about every 0.5 seconds and clean scene exit drains remaining rows. Wall-clock timestamps use local machine time with seven fractional digits and no timezone offset. Record the workstation timezone and synchronize machines for cross-device analysis. Do not assume rows are evenly spaced or use row number as elapsed time.

Swarm, Kannadi clones and Band capture the actual agent snapshot in LateUpdate and stream it to `.matpop` on a bounded worker. The format preserves float32 positions, rotations, scales, full animation evidence and the Unity frame and local timestamp. Agent identities appear once in the file and common per-frame values are shared. The worker computes the digest as it writes and closes the file with a footer containing frame and row counts. Embedded Choice/Selwyn populations also use one file per rig/population. **Skip swarm data** disables virtual population capture for the session while leaving animal CSV recording active.

Use Python 3.8 or newer to export a finalized population file as CSV. The exporter uses the standard library. Extract a session ZIP before exporting a file from it.

```sh
python3 tools/unpack_population.py path/to/session/agents.matpop exports/agents.csv
```

Use an output directory outside the saved session so replay's acquisition inventory stays intact. The default output is standard output. Use `--legacy-header` to append historical `Key:Value` fields to the header. The default CSV is rectangular and retains the original column order, including frame identity, scale and JSON animation state. Empty population frames have no CSV rows and remain available in the `.matpop` file. Recording directories store the packed streams without compression to keep compression off the acquisition path. These files can occupy more disk space than the old gzip logs. Optional offline ZIP packaging can compress them after the run.

## Offline population Parquet

The recording process appends the native population stream. After the run is finalized, the optional Parquet utility makes a separate standard dataset for analysis. It uses Polars with one worker by default and verifies the acquisition checksums when exporting a complete run. Install Polars in the Python environment used for this task. Launch the export after acquisition, when disk and CPU use can run in the background.

```sh
nice -n 15 python3 tools/population_parquet.py pack path/to/finalized/run exports/run
python3 tools/population_parquet.py csv exports/run/stream-001 exports/rig.csv
python3 tools/population_parquet.py csv exports/run/stream-001 exports/selected.csv --first-frame 1200 --last-frame 1800
```

The export can also take one finalized `.matpop` file. A run export includes a `run.json` mapping each stream to the source filename. Within a stream, standard Zstandard-compressed `samples-*.parquet` files contain the typed population columns, identity and source indexes. The corresponding `frames-*.parquet` files retain frame context and zero-agent frames. `population.json` records the original header, exact time units and frame ranges for selecting only the required chunks. Timestamp values in Parquet are the original 100 ns local clock ticks. The CSV conversion restores the readable wall time and historical data-column order. Keep exported datasets and CSV files outside the acquisition directory.

## Rig CSV (`*_VRn_.csv`)

DataLogger emits a header and comma-separated rows. The rig's transform is the world pose, not raw tracker coordinates.

| Column | Meaning / units |
|---|---|
| `Current Time` | Local `yyyy-MM-dd HH:mm:ss.fff`. |
| `VR` | Rig object name. |
| `Scene` | Actual loaded Unity scene. |
| `CurrentSequenceScene` | Configured outer sequence scene. |
| `ConfigFile` | Experiment file referenced by configFile; blank for a design-only dynamic step. |
| `CurrentTrial` | Outer sequence trial counter. |
| `CurrentStep` | Zero-based execution-order position, not necessarily index in the original randomized list. |
| `GameObjectPosX/Y/Z` | World units (cm for the current walking scenes). X right, Y up, Z forward. |
| `GameObjectRotX/Y/Z` | Euler degrees, wrapped 0–360. Y is yaw; positive yaw turns +Z toward +X. |
| `SensPosX/Y/Z` | Raw position values received over ZMQ, before sphere scaling or X/Y remapping. Not already world centimeters. |
| `SensRotX/Y/Z` | Euler degrees obtained from the received pose quaternion: pitch, yaw, roll respectively. |
| `stepIndex`, `stepName` | Inner dynamic step index/name; index starts at -1 when unused. |
| `loopIndex`, `cumulativeStep` | Inner loop and cumulative-step counters. |
| `swapElapsedSec` | Seconds since dynamic step start when a color swap was recorded; event field, otherwise blank. |
| `swapWallClock` | Local wall time of that swap. |
| `grayAtTrialStart` | Normalized starting gray intensity for adaptive trials. |
| `blackSideAtTrialStart` | Adaptive trial side label. |
| `skyboxId` | SHA-256 ID of the active managed Swarm/Kannadi sky. Join to `Skyboxes/<id>.json` and `.png`; otherwise blank. |
| `skyboxSampleUtc` | Astronomical sample time as ISO-8601 UTC; blank for an explicit image or no managed sky. |
| `yawDCOffsetDegrees`, `yawDCCompensationDegPerSecond` | DC angle in degrees and its gain-scaled term subtracted from untrimmed yaw in deg/s; raw DC columns remain for compatibility. |
| `autoTrimMeanDegPerSecond`, `autoTrimUncertaintyDegPerSecond`, `autoTrimMeasurementSeconds` | Latest evaluated mean under the applied offset, approximate two-standard-error uncertainty, and active qualified window length. Blank before a full initial window. |
| `autoTrimEnabled`, `autoTrimState` | Kinefly automatic centering flag/state. Blank for unsupported tracking modes. |
| `flightDetected`, `flightVarianceRadiansSquared` | Flight decision and population variance of raw L−R before gain/offset; blank until the flight window is ready. |
| `autoTrimMedianDegPerSecond`, `autoTrimPasses` | Last evaluated effective median under the applied offset, and correction count in the current row/configuration. Median is blank while no complete qualified window is available. |
| `flightCheckEnabled`, `flightThresholdRadiansSquared`, `flightSamples` | Actual flight gate flag, entry threshold in rad² and distinct samples in the rolling flight window; measured flight remains independent of a bypass. |
| `experimentPhase` | Experiment state at row sampling: `preStimulus`, `stimulus`, or `postStimulus`. Flat experiments use `stimulus`; legacy flat heading observation uses `assessment`. Outside a running experiment: `idle`; failed experiment: `error`. |

`experimentPhase` is appended after the existing rig columns, including on Optomotor/other loggers using the base schema. It is recorded on **every row**, before disk buffering, so no event-log join is needed. Existing `CurrentStep`, `stepIndex`, `stepName` and band `VisibilityPhase` retain their original meanings. For example:

```python
import pandas as pd

rows = pd.read_csv("your_VR1_.csv")
pre = rows[rows["experimentPhase"] == "preStimulus"]
stim = rows[rows["experimentPhase"] == "stimulus"]
post = rows[rows["experimentPhase"] == "postStimulus"]
```

The value uses the same state source as live telemetry's `system.phase`. It describes the state at the logger's sampling callback; phase transitions occur in LateUpdate. Historical CSVs collected before this column was added remain unchanged.

Sensor columns are present only when includeZmqData is enabled on the logger. A stale packet holds the last pose, and the CSV does not currently include a freshness/validity column. A stationary logged pose alone cannot distinguish stationary behavior from packet loss; use runtime_trace.log and the original tracker stream.

### ZMQ input contract

ZmqListener subscribes to a TCP publisher using two frames: a topic frame followed by JSON with `x`, `y`, `z`, `roll`, `pitch`, `yaw`. The receiver preserves the raw wire rotation values alongside the existing walking Pose. The walking path interprets those values as **degrees**; the restored Bogong/Kinefly path reads signed raw **radians**, without quaternion conversion or wrapping. ClosedLoop takes sensor `y` as Unity X displacement and sensor `x` as Unity Z displacement, rotates this by the reset heading offset, then scales by `sphereDiameter/2` and the optional locust position gain.

For FicTrac ball calibration to yield centimeters, the displacement stream must contain **cumulative ball angular displacement in radians**, not an already converted cm position or a velocity. Confirm the publisher's convention before connecting another tracker. Sensor Z is logged but not used for walking translation. Orientation uses Unity Y/yaw; the Swarm/Kannadi walking path applies wrapped yaw deltas at unit gain unless a Swarm/Kannadi numeric gain explicitly overrides it. Rotation is not rate-limited or multiplied by frame duration. The migration wind/AGL adjustment is visible in the world transform columns but does not alter sensor columns.

Kinefly instead integrates `gain * (rawYawRadians - yawDCOffsetRadians) * Rad2Deg * deltaTime`, matching `BogongAustralia`. A held value keeps turning between packets, including after a disconnect, as in that implementation. For Kinefly, `SensRotX/Y/Z` again contain Euler display angles calculated from the raw radians, and `SensRotXRad/YRad/ZRad` preserve the raw radians, matching the historical logger. The walking sensor columns keep their previous values. Use the added `trackingImplementation`, `trackingMode`, `trackingInputUnits`, `wireYaw`, `yawGain`, `yawDCOffsetRadians`, and `yawOutputDegPerSecond` columns to identify the calculation used. `wireYaw` is the untouched received value; its unit is given by `trackingInputUnits`. Kinefly rate output is blank outside yaw mode.

## Kannadi clone logs (`*Kannadi*Clones.matpop`)

The CSV export begins with these columns:

`Timestamp,VRIndex,CloneIndex,CloneName,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,NumberOfRings,hexRadius,skyboxId,skyboxSampleUtc,experimentPhase`

Each row is one visible-model root per frame. VRIndex is the configured numeric rig identifier (historically 1–4); CloneIndex is zero-based and can change when a grid is rebuilt. Positions are world cm, rotations are Euler degrees, hexRadius is world cm, NumberOfRings is an integer. A clone root can contain a whole mirrored band; it is not necessarily a single mesh/animal. Numeric clone values use invariant decimal points.

Current source appends `AnimationState` after this existing prefix. It is a quoted JSON field, schema `version:1`, containing `animators` (hierarchy paths, controller layers/state hashes/normalized times/weights and non-trigger parameters), `legacy` (clip names/times/speeds/weights and enabled state), and optional `procedural` shader clock/phase. Empty fields mean that root has no supported animation. Paths use child indices relative to the recorded root. Replay requires the same original controller/model hierarchy; state hashes alone do not embed a controller. Transition blending and consumed triggers are not fully captured. Existing recordings remain readable.

## Swarm and band logs

Historical Swarm gzip CSV uses nine row columns: `Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase`. Its header then appends `NumberOfLocusts:...`, `SpawnAreaSize:...`, `Mu:...`, `Kappa:...`, `LocustSpeed:...` as **header metadata**, not per-row columns. New 0.3 files append `RotationX,RotationY,RotationZ` after `experimentPhase`, yielding twelve data columns while retaining the old nine-column prefix. Parse column names and the appended metadata separately; older files have six, eight or nine data columns. Units: X/Y/Z and area size in scene world units (cm in walking scenes, m on terrain), mu in degrees, kappa dimensionless, speed in world units/s. Pre-0.3 logs contain positions without heading columns; their missing headings cannot be recovered as measurements. Current logs append recorded Euler rotations in degrees.

Current source additionally appends `AnimationState` after `RotationZ` and before header-only metadata, using the same JSON schema as Kannadi clone rows. Bogong moth/dot records preserve the actual shader clock and per-agent phase, so replay flap/flicker does not depend on the wall clock at playback. Use a real CSV reader: the quoted JSON contains commas and quotes.

New headers additionally include `LocustSpeedStdDev` in world units/s, `DirectionDistribution` and `KappaElevation`; the existing data-column prefix is unchanged. `LocustSpeed` is the generated population mean, not each individual's sampled speed. The archived experiment specifies the distribution; successive recorded positions give the realized motion. Periodic crossings jump across the cube and must be unwrapped before estimating speed. Embedded Choice populations create a new per-rig file when a phase rebuilds them.

See [night-sky.md](night-sky.md) for the `Skyboxes/` image/manifest files, `usage.jsonl` activation audit, and `loads.csv` load history. Each load records the previous/new IDs, panorama filename, actual load time and represented sky time in UTC and observer-local time with an explicit offset. These are included in the normal run directory/ZIP. The sky ID identifies a scene stimulus, including when scene geometry occludes it; it does not identify a camera screenshot.

Historical Band gzip CSV has twelve row columns: `Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,Speed,VisibilityPhase,experimentPhase`, followed by extra `Key:Value` header metadata for spawn/boundary/grid parameters. Speed is cm/s; VisibilityPhase is elapsed phase in seconds modulo the total on+off cycle, not a normalized fraction. Read twelve data columns, then parse header metadata separately (older files have eleven). Layer indicates the rig-specific stimulus population, not a physical display number.

Band logging follows the spawner's owned instances, including unlocked agents without a transform parent (these previously produced empty band logs).

Kannadi, Swarm and Band rows use the same `experimentPhase` values as rig rows. `VisibilityPhase` remains the band blinking timer; it is not the experiment phase.

## Optomotor and metadata

OptomotorDataLogger extends the rig/base schema with `StimulusIndex` (zero-based), `Frequency` (cycles/revolution), `Contrast` and `DutyCycle` (0–1), `Speed` (degrees/s), `RotationAxis`, `ClockwiseRotation`, `ClosedLoopOrientation`, `ClosedLoopPosition`. An instance on the drum records the drum transform; do not interpret it as animal pose. The world-pose logs on rig objects describe animal motion.

The compact ColorDriftLogger CSV now has a matching five-column header: `Current Time,CylinderName,CurrentBlue,TargetMean,experimentPhase`. Its first four data values retain their previous meaning; the old mismatched rig-style header is corrected.

Current animal metadata JSON stores `ExperimentName`, `ExperimenterName`, `Comments`, `UsedAnimalIDs` and `Animals`, whose entries include `VR`, `SpeciesName`, `AgeDays`, `StarvedSinceHours`, `Sex`, `Comments` and `AnimalID`. Transitional `UsedFlyIDs`, `Flies` and `FlyID` aliases remain for old analysis; canonical values win when both occur. Historical files use those legacy names. See [compatibility](animal-agent-compatibility.md). Age and starvation are text fields supplied through the UI; validate them in analysis instead of assuming a numeric JSON type.

## Native tracker provenance

Current source writes `companion_provenance.json` at run startup and embeds the same tracker descriptors in `config_manifest.json` as `companionProvenance`. Each configured rig records whether an explicitly launched tracker process was found and its identity verified, the requested demo/strict-camera policy, release tag, executable SHA-256, PID/start time and configuration path when available. `actualInputSource: "unverified"` deliberately distinguishes process ownership from observed camera/animal provenance.

Only a verified owned process matching the selected rig's backend, local endpoint and requested demo mode authorizes a native calibration snapshot. Its exact on-disk TOML/TXT/JSON bytes enter the normal SHA-256 archive with role `tracker-calibration`; `configSnapshotPath` and `configSha256` locate and verify them. The primary system JSON remains unchanged. External/unmanaged inputs, stale/mismatched launches and missing configurations are explicitly marked unavailable or unverified. Missing optional native evidence does not invalidate otherwise complete experiment configuration snapshots.

This captures the native configuration file on disk when the Unity run starts. It cannot establish unsaved native in-memory edits, physical tracking accuracy or actual source type from a legacy wire packet. Referenced background images, videos, executables and arbitrary sidecars are not recursively copied. Keep those native application files separately when needed for acquisition reproducibility. Saved animal poses and stimulus evidence remain authoritative for visual replay.

## Analysis and replay cautions

Unwrap yaw before angular-velocity calculations. Use wall-clock differences for elapsed time while checking duplicates/jumps; a future monotonic timestamp would be preferable for sub-millisecond synchronization. Periodic X/Z wrapping and R resets cause coordinate discontinuities: use arena size and experiment boundaries when reconstructing path lengths. The fading overview trail is an operator display, not an analysis record.

Current source writes rig numbers with invariant culture and round-trip float precision, quotes strings with CSV delimiters, and supports quoted multiline fields on replay. This prevents decimal-comma Mac locales or comma-containing step/config names from shifting pose columns. Rig timestamps retain seven fractional digits; `unityFrame` identifies the rendered frame. Regular rig capture runs in LateUpdate at order 1200, after movement/terrain adjustment and before telemetry. Immediate onset/swap event rows remain additional evidence.

Archived versions used machine numeric culture and unquoted strings. A legacy row with a different field count is now rejected with an explicit warning rather than silently interpreting shifted numbers as a pose. Such corrupted rows cannot be recovered unambiguously. Kannadi/Swarm numeric writers already used invariant culture. Population gzip logs are loaded separately from animal trajectories.

New Band rows additionally record `Visible` and `UnityFrame`; the visibility flag describes the actual renderer state. Older `VisibilityPhase` values used global time while rendering used an agent-local timer, so they cannot by themselves establish exact historical blinking. Replay marks that limitation instead of inventing visibility.

Replay settings are inspector/UI fields: sessionFolder (newest when empty), absoluteSessionPath (optional override), playbackSpeed=1 seconds/second, loop=true, smallStepSeconds=1, largeStepSeconds=10, pauseOnSeek=true, autoResumeAfterSeek=true, createRigCameras=true, targetHorizontalFov=110 degrees. Replay split-screen cameras are analysis views, not LED panel viewport calibration. Keep the saved dynamic design/prefab catalog if reconstructing the stimulus environment.

## Live monitoring and heading-onset records

The dedicated `matrex.telemetry.v2` PUB stream (schema version 2) provides all rigs on port 9880, separately from tracking inputs. Heading-relative presentations append per-rig onset context to `heading_reference.jsonl`. See [the versioned telemetry and onset schema](heading-reference-telemetry.md) for clocks, coordinate units, raw/consumed inputs, gains, sample freshness, circular mean and resultant length.

For named cross-block heading references, `heading_reference.jsonl` additionally records `savedAs` and `measureDuringPresentation`. In the current Selwyn cycle, measurements are independent per rig, saved as `milky-way-baseline` each 240-second Milky Way block, and the following two swarm headers record the resolved world `Mu`. Sequence `CurrentTrial` increments when the repeated block group restarts; the setup prefix occurs only in the first pass.


## Greenfield same-build replay

New recordings add `presentationId`, `unityFrame` and `monotonicTime` to pose evidence. `Current Time` remains the wall-clock timestamp; playback uses monotonic elapsed time and the shared rendered-frame identity. Complete controller inputs are in `presentations.jsonl`, serial/world checkpoints in `world_state.jsonl`, and the required creating build/schema in `replay_contract.json`. The finalized pose and population inventory and hashes are in `recording_manifest.json`. See the [same-build replay contract](same-build-replay-contract.md) for strict evidence, extension hooks and read-only seek behavior.
