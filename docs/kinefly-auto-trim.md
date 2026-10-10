# Kinefly auto trim

Set `autoTrim` on a **sequence row**, beside `gain`. It defaults to false; omitted/false rows preserve the current per-rig DC offset. Only explicitly configured Kinefly yaw-rate input supports trimming. Walking/FicTrac and force/torque modes ignore it.

```json
{
  "sceneName": "Choice_Selwyn",
  "duration": 240,
  "gain": 2.5,
  "autoTrim": true,
  "autoTrimSettings": {
    "windowSeconds": 20,
    "flightWindowSeconds": 2,
    "flightVarianceThreshold": 0.01,
    "flightConfirmationSeconds": 1,
    "flightVarianceHysteresis": 0.5,
    "updateIntervalSeconds": 0.5,
    "aggressiveness": 0.5,
    "maxStepRadians": 0.04,
    "settleSeconds": 3,
    "toleranceDegPerSecond": 1
  },
  "reloadScene": false,
  "parameters": { "configFile": "Examples/Flight/auto-trim-calibration.example.json" }
}
```

The active `sequenceConfig.json` and `Examples/Sequences/kinefly-auto-trim.example.json` now contain this single 240-second test. `Flight/auto-trim-calibration.example.json` uses the default Selwyn live Milky Way sky, terrain, 100 m AGL and 7 m/s autopilot, with no conspecific swarm or heading assessment. All four Kinefly rigs calibrate independently. Start from Control as usual; Tab displays flying/paused, collecting, trimming and centered states. The sequence does not loop and the player exits after 240 seconds, using the existing sequence completion behavior.

For a subsequent stimulus with the same animal, append a row using `autoTrim: false` **in the same sequence/application run** to retain learned offsets. The current Selwyn stars/swarm recipe remains at 256 dots per rig with its authored timing. For induced circling, leave `autoTrim: false` on the entire stimulus row; if calibration and stimulus share one phased file, split calibration into its own row. The flag applies to the whole row, including all phases.

## Settings from past recordings

The [recording analysis](kinefly-recording-analysis.json) samples the first 45,000 rows of six CSVs: four rigs from `20261010_023123` and VR1/VR4 from `20261007_225146`, under `/Volumes/MISC/Swarm_v1/RunData`. Raw yaw equals recorded left minus right wing angle in radians. Latest star-phase medians span −0.138 to +0.090 rad; median 2-second variances span 0.083–0.221 rad². The low-motion segment in latest VR4 reaches roughly 0.0005 rad².

With 0.3 rad², the latest rigs never maintain a 20-second flying window, so calibration would stall. The **test example** uses a provisional 0.01 rad² entry threshold, 0.005 rad² exit threshold and 1 second of confirmation. The general class/template default remains 0.3 for explicit species-specific tuning. These recordings have no biological flight labels; this separates low-motion signal from variable signal without establishing flight sensitivity/specificity.

Production-estimator replay of each recording's 240-second star phase tested correction fractions 0.25/0.35/0.5/0.65 and settling times 3/5 seconds. The selected 0.5/3 s pair had the lowest mean absolute final-window residual among those candidates (1.72 deg/s). The six replays made 6–10 bounded passes and admitted 213–237 seconds of flight-qualified input. Final 20-second medians after correction ranged −3.22 to +4.08 deg/s because the recorded distribution drifts; replay cannot model how an animal responds to the changing offset. A live 240-second run should verify the threshold and adaptation interval before experiments. Increase settling if adaptation needs more time.

## Behavior and units

The wire `yaw` is raw left minus right wing angle in radians. Flight uses its rolling **population variance before gain and DC offset**, with threshold 0.3 **rad²** by default. Tune this for the species and tracker units. Entry requires variance above the threshold continuously for `flightConfirmationSeconds`; once flying, variance at or below `threshold × flightVarianceHysteresis` exits immediately. This avoids rapid switching near the threshold. A constant, even large, biased signal is not classified as flight. Nonflight, open-loop yaw, zero gain, unsupported mode, missing/stale input, and stopped/failed experiments all pause corrections. This gate does not turn off historical yaw motion.

Each rig samples distinct fresh packets at up to 50 Hz. Repeated held packets cannot fill the windows. Flight assessment needs the configured window and at least five samples per second of window length (minimum five samples). A gap longer than one second clears evidence. The UI distinguishes unmeasured flight (`FLIGHT ?`) from measured `NOT FLYING` and `FLYING`, even when auto trim is off.

While flying in closed loop, the controller gathers a complete window of qualified L−R samples. It computes the effective median using the current motion calculation:

```text
medianYawRateDegPerSecond = gain × (medianRawWingDifference − currentDCOffset) × 180/π
correctionRadians = clamp(aggressiveness × (medianRawWingDifference − currentDCOffset), ±maxStepRadians)
newDCOffset = currentDCOffset + correctionRadians
```

Negative gains work too. A correction happens only outside the effective-output tolerance. After every pass, the animal gets `settleSeconds` to adapt, followed by a complete new measurement window; old pre-correction samples are discarded. Passing through nonflight/open loop/stale input clears the trim window. Gain changes and manual bracket corrections restart measurement after a settling period. It continuously reassesses a centered window at the update interval while enabled.

| `autoTrimSettings` field | Default | Range / effect |
|---|---|---|
| `windowSeconds` | 20 | 1–300 s of qualified samples per pass; 30 is also supported. |
| `flightWindowSeconds` | 2 | 0.2–30 s raw-signal variance window. |
| `flightVarianceThreshold` | 0.3 | 0–1,000,000 rad²; entry requires variance strictly above it. Test example uses 0.01. |
| `flightConfirmationSeconds` | 1 | 0–30 s continuously above entry threshold before declaring flight. |
| `flightVarianceHysteresis` | 0.5 | 0–1 multiplier of entry threshold for exiting flight. |
| `updateIntervalSeconds` | 0.5 | 0.1–60 s between median evaluations. |
| `aggressiveness` | 0.25 | 0.001–1 fraction of median residual removed per pass. |
| `maxStepRadians` | 0.02 | 0.000001–10 rad maximum DC change per pass. |
| `settleSeconds` | 5 | 0–300 s adaptation time after corrections or gain/manual-offset changes. |
| `toleranceDegPerSecond` | 1 | 0–1,000,000 deg/s effective median tolerance after gain. |

All numbers must be finite. These defaults favor gradual correction; increasing aggressiveness/step size or shortening windows makes it faster. No algorithm can distinguish a persistent stimulus-driven turn from a calibration bias using this signal alone: use the row flag to determine when centering is scientifically appropriate.

## Controls and persistence

**Tab** shows the dashboard; **1–4** select the rig for manual **[ / ]** offset adjustment. The dashboard's **AUTO TRIM ON/OFF** button changes the running row's in-memory flag for all supported rigs; each subsequent row loads its own flag. It is disabled outside an experiment or without Kinefly yaw-rate rigs. No config file is rewritten. Button changes appear in rig CSV and telemetry.

Dashboard text updates every 0.5 seconds. Each Kinefly rig always shows its current signed **DC offset in radians**, including while trim is off or paused. Each rig also shows yaw/position loop switches, active/dimmed gain, flight state/variance, collecting progress, settling, centered or paused status and pass count. `last median` is the most recent evaluated window's effective median, measured before its correction.

`LAST AUTO: DC UP +0.0400 rad  +0.0020 -> +0.0420 rad` shows the last adjustment actually applied to that rig. `DC DOWN` means the signed offset decreased; these labels describe offset direction, independent of gain sign. Recent adjustments highlight amber for five seconds while trimming is active. The history remains visible in gray when paused/off; `DC HELD / no automatic adjustment yet` means that rig has not applied a trim in the current scene. Green means flying/closed/centered, amber means measurement/correction, gray means inactive/off/paused.

Offsets persist independently for the four rigs across in-place rows and scene reloads during the current application run, including rows with auto trim false. They also survive Stop/Start within that process, just as manual offsets do. A new application run starts from the component's configured offset; this feature does not transfer an animal's calibration to a later session. Rig CSV records each changing `yawDCOffsetRadians`, trim state, flight variance, median and pass count for analysis.

## Swarm recording and performance

The standalone Swarm logger previously searched all tagged agents, formatted CSV, compressed with optimal gzip, and wrote the recording from every Update on the render thread. It now caches its spawner's agents (refreshing after rebuilds), captures their positions in LateUpdate, and queues pooled snapshots. One worker per logger formats invariant CSV, uses fast gzip, and writes to disk. Timestamp, skybox and phase are captured with the positions; worker timing cannot relabel a frame. Column layout and gzip filenames remain compatible.

The queue holds at most 240 frames per logger. Scene unload requests an asynchronous drain; normal application exit waits for pending recordings to finish. If storage cannot keep up, capture remains nonblocking and dropped frames are counted and reported as errors. The dashboard shows capture milliseconds, queued frames and dropped frames. The Unity Profiler marker `SwarmLogger.Capture` measures the main-thread capture work. Abrupt process termination cannot guarantee a finalized gzip file.

The embedded swarm created by Choice/Selwyn has **no SwarmLogger**. It now uses `BogongSwarmRenderer`: one dynamic mesh/material/camera callback per rig rather than one renderer/material/callback per agent. Centers update once after movement; the GPU calculates each circle's angular radius and facing for the camera rendering it. Movement roots, speed, heading-relative spawning, boundaries, opaque RGB, per-rig layers and world/angular size remain unchanged. The existing Selwyn stimulus stays at 256 dots per rig. Scene-prefab visuals continue using their existing rendering path.

The sampled latest session records median frame intervals of 17 ms under stars and 22 ms during swarm stimulus (95th percentile 35 ms in stimulus). The [Selwyn validation report](selwyn-swarm-validation-results.json) compares exact reference pixels and isolated render CPU timings for four rigs, six 128×128 cameras each: 64 dots/rig improved from 4.16 to 1.80 ms, and 256 dots/rig from 10.65 to 1.83 ms on an Apple M2 Pro. All 786,432 compared pixels matched. These timings exclude terrain and normal whole-frame work and do not establish FPS on the LED workstation.

Run `KineflyAutoTrimValidation.Run` on a disposable Unity project with the `KANNADI_VALIDATION_COPY` marker. It checks signed-gain convergence, flight/input/control gates, multi-pass behavior, row and scene persistence, dashboard toggle/status, 64-agent recording on four rigs, gzip/FIFO metadata integrity, and legacy synchronous versus new capture timing. The desktop benchmark is diagnostic; it does not establish LED workstation GPU throughput or validate animal adaptation.

The [saved validation report](kinefly-auto-trim-validation-results.json) records passing live integration/capture tests and 73,746 historical gain comparisons with auto trim off. `SelwynSwarmValidation.Run` adds recorded-input replay and reference-render comparisons; `BogongDotValidation.Run` covers both rendering paths at near/far distances and cube-face seams in world/angular size modes.
