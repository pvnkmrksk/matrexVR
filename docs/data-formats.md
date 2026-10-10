# Recorded data and units

MasterDataLogger creates `Assets/RunData/yyyyMMdd_HHmmss/` in the Editor (`Application.dataPath/RunData` in a player). It records a runtime trace and copies the system config, sequence config, and referenced experiment config files. On shutdown it attempts a session ZIP. Keep the original JSON alongside exported data: it defines gains, stimulus conditions, camera layout, and physical scale.

Sampling is once per rendered Update, not a fixed Hz acquisition clock. Disk buffering flushes about every 0.5 seconds; clean scene exit flushes remaining rows. Wall-clock timestamps are local machine time and contain milliseconds, without a timezone offset. Record the workstation timezone and synchronize machines for cross-device analysis. Do not assume rows are evenly spaced or use row number as elapsed time.

Swarm position snapshots are captured in LateUpdate; CSV formatting, gzip and disk writes run on a worker. They retain sample-time metadata and drain on clean exit. The dashboard reports queue overflow/dropped frames. Embedded Choice/Selwyn swarms currently have no agent-position logger.

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
| `autoTrimEnabled`, `autoTrimState` | Kinefly automatic centering flag/state. Blank for unsupported tracking modes. |
| `flightDetected`, `flightVarianceRadiansSquared` | Flight decision and population variance of raw L−R before gain/offset; blank until the flight window is ready. |
| `autoTrimMedianDegPerSecond`, `autoTrimPasses` | Last evaluated effective median before correction, and correction count in the current row/configuration. Median is blank while no complete qualified window is available. |
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

## Kannadi clone logs (`*Kannadi*Clones.csv.gz`)

Gzip CSV with columns:

`Timestamp,VRIndex,CloneIndex,CloneName,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,NumberOfRings,hexRadius,skyboxId,skyboxSampleUtc,experimentPhase`

Each row is one visible-model root per frame. VRIndex is 1–4; CloneIndex is zero-based and can change when a grid is rebuilt. Positions are world cm, rotations are Euler degrees, hexRadius is world cm, NumberOfRings is an integer. A clone root can contain a whole mirrored band; it is not necessarily a single mesh/animal. Numeric clone values use invariant decimal points.

## Swarm and band logs

Swarm gzip CSV uses nine row columns: `Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase`. Its header then appends `NumberOfLocusts:...`, `SpawnAreaSize:...`, `Mu:...`, `Kappa:...`, `LocustSpeed:...` as **header metadata**, not per-row columns. Read nine data columns and parse the appended metadata separately (older files have six or eight data columns). Units: X/Y/Z and area size in cm, mu in degrees, kappa dimensionless, speed cm/s. These logs have positions but no heading column; use experiment settings or extend the logger if individual heading histories are required.

See [night-sky.md](night-sky.md) for the `Skyboxes/` image/manifest files, `usage.jsonl` activation audit, and `loads.csv` load history. Each load records the previous/new IDs, panorama filename, actual load time and represented sky time in UTC and observer-local time with an explicit offset. These are included in the normal run directory/ZIP. The sky ID identifies a scene stimulus, including when scene geometry occludes it; it does not identify a camera screenshot.

Band gzip CSV has twelve row columns: `Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,Speed,VisibilityPhase,experimentPhase`, followed by extra `Key:Value` header metadata for spawn/boundary/grid parameters. Speed is cm/s; VisibilityPhase is elapsed phase in seconds modulo the total on+off cycle, not a normalized fraction. Read twelve data columns, then parse header metadata separately (older files have eleven). Layer indicates the rig-specific stimulus population, not a physical display number.

Band logging follows the spawner's owned instances, including unlocked agents without a transform parent (these previously produced empty band logs).

Kannadi, Swarm and Band rows use the same `experimentPhase` values as rig rows. `VisibilityPhase` remains the band blinking timer; it is not the experiment phase.

## Optomotor and metadata

OptomotorDataLogger extends the rig/base schema with `StimulusIndex` (zero-based), `Frequency` (cycles/revolution), `Contrast` and `DutyCycle` (0–1), `Speed` (degrees/s), `RotationAxis`, `ClockwiseRotation`, `ClosedLoopOrientation`, `ClosedLoopPosition`. An instance on the drum records the drum transform; do not interpret it as animal pose. The world-pose logs on rig objects describe animal motion.

The compact ColorDriftLogger CSV now has a matching five-column header: `Current Time,CylinderName,CurrentBlue,TargetMean,experimentPhase`. Its first four data values retain their previous meaning; the old mismatched rig-style header is corrected.

Fly metadata JSON stores `ExperimenterName`, `Comments`, `UsedFlyIDs`, and `Flies`, whose entries have `VR`, `AgeDays`, `StarvedSinceHours`, `Sex`, and `FlyID`. Age and starvation are text fields supplied through the UI; validate them in analysis instead of assuming a numeric JSON type.

## Analysis and replay cautions

Unwrap yaw before angular-velocity calculations. Use wall-clock differences for elapsed time while checking duplicates/jumps; a future monotonic timestamp would be preferable for sub-millisecond synchronization. Periodic X/Z wrapping and R resets cause coordinate discontinuities: use arena size and experiment boundaries when reconstructing path lengths. The fading overview trail is an operator display, not an analysis record.

The older general rig/Band CSV writers use the machine's numeric culture and do not quote string delimiters. Use a decimal-point locale for collection, and keep commas/newlines out of object/step/config names. Kannadi/Swarm numeric row writers explicitly use invariant culture. Historical files can differ; inspect headers before concatenating runs. ReplayController expects the named base rig columns, parses numbers with invariant culture, and uses a simple comma split. It does not ingest clone/swarm gzip logs as rig trajectories.

Replay settings are inspector/UI fields: sessionFolder (newest when empty), absoluteSessionPath (optional override), playbackSpeed=1 seconds/second, loop=true, smallStepSeconds=1, largeStepSeconds=10, pauseOnSeek=true, autoResumeAfterSeek=true, createRigCameras=true, targetHorizontalFov=110 degrees. Replay split-screen cameras are analysis views, not LED panel viewport calibration. Keep the saved dynamic design/prefab catalog if reconstructing the stimulus environment.

## Live monitoring and heading-onset records

The dedicated `matrex.telemetry.v1` PUB stream provides all rigs on port 9880, separately from tracking inputs. Heading-relative presentations append per-rig onset context to `heading_reference.jsonl`. See [the versioned telemetry and onset schema](heading-reference-telemetry.md) for clocks, coordinate units, raw/consumed inputs, gains, sample freshness, circular mean and resultant length.
