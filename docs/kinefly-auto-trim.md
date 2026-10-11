# Kinefly auto trim

Set `autoTrim` on a **sequence row**, beside `gain`. It defaults to false; omitted/false rows preserve the current per-rig DC offset. Only explicitly configured Kinefly yaw-rate input supports trimming. Walking/FicTrac and force/torque modes ignore it.

```json
{
  "sceneName": "Choice_Selwyn",
  "duration": 240,
  "gain": 2.5,
  "autoTrim": true,
  "reloadScene": false,
  "parameters": { "configFile": "Examples/Flight/auto-trim-calibration.example.json" }
}
```

Keep the sequence lean: only the opt-in `autoTrim` flag stays beside `gain`. Put tuning in the referenced **experiment JSON** (inside an enabled phase for a phased experiment):

```json
"autoTrimSettings": {
  "windowSeconds": 15,
  "maxWindowSeconds": 120,
  "centerStatistic": "mean",
  "noiseAware": true,
  "flightCheckEnabled": true,
  "flightWindowSeconds": 2,
  "flightVarianceThreshold": 0.01,
  "flightConfirmationSeconds": 1,
  "flightVarianceHysteresis": 0.5,
  "updateIntervalSeconds": 0.5,
  "aggressiveness": 1,
  "maxStepRadians": 1,
  "maxOffsetRadians": 2,
  "settleSeconds": 3,
  "toleranceDegPerSecond": 1
}
```

Omit this block to use these same defaults. The default now removes the full estimated residual, bounded to 1 rad (57.30°) per pass and ±2 rad (±114.59°) absolute DC. A stable noise-free bias anywhere in the allowed range takes at most four corrections even when starting at the opposite limit. The old 0.1-rad cap required at least ten passes for a 1-rad bias; increasing aggressiveness alone could not overcome that cap. Set a smaller step for an animal that needs gentler changes, accepting more passes. Experiment settings take precedence over legacy sequence-row `autoTrimSettings`, which remain readable for compatibility. Each new phase/row loads its experiment settings and clears a temporary dashboard flight bypass.

The active `sequenceConfig.json` and `Examples/Sequences/kinefly-auto-trim.example.json` now contain this single 240-second test. `Flight/auto-trim-calibration.example.json` uses the default Selwyn live Milky Way sky, terrain, 100 m AGL and 7 m/s autopilot, with no conspecific swarm or heading assessment. All four Kinefly rigs calibrate independently. Start from Control as usual; Tab displays flying/paused, collecting, trimming and centered states. The sequence does not loop and the player exits after 240 seconds, using the existing sequence completion behavior.

For a subsequent stimulus with the same animal, append a row using `autoTrim: false` **in the same sequence/application run** to retain learned offsets. The current Selwyn stars/swarm recipe remains at 256 dots per rig with its authored timing. For induced circling, leave `autoTrim: false` on the entire stimulus row; if calibration and stimulus share one phased file, split calibration into its own row. The flag applies to the whole row, including all phases.

## Settings from past recordings

The [recording analysis](kinefly-recording-analysis.json) samples the first 45,000 rows of six CSVs: four rigs from `20261010_023123` and VR1/VR4 from `20261007_225146`, under `/Volumes/MISC/Swarm_v1/RunData`. Raw yaw equals recorded left minus right wing angle in radians. Latest star-phase medians span −0.138 to +0.090 rad; median 2-second variances span 0.083–0.221 rad². The low-motion segment in latest VR4 reaches roughly 0.0005 rad².

With 0.3 rad², the latest rigs never maintain a 20-second flying window, so calibration would stall. The class, experiment template and test now all default to a provisional 0.01 rad² entry threshold, 0.005 rad² exit threshold and 1 second of confirmation. The previous general 0.3 default caused omitted settings to stall despite the tuned example. These recordings have no biological flight labels; this separates low-motion signal from variable signal without establishing flight sensitivity/specificity.

The [current replay](kinefly-recording-replay-results.json) passes flight/correction mechanics on all six recorded 240-second star segments, admitting 213–237 seconds using these flight thresholds. The real input is skewed and nonstationary; its tail median is a separate diagnostic from the default mean objective. Replay cannot establish convergence of a responding animal. The [Gaussian validation report](kinefly-gaussian-validation-results.json) passes 74 hidden-mean cases with independent random seeds, both gain signs, opposite initial offsets, and actual Unity heading integration: stationary inputs take 1–4 corrections; two changing means take 4–5. All final 60-second heading slopes lose at least 95% of the initial bias. The real-motion examples reduce true mean drift from approximately 107°/s to −0.08 and +0.22°/s. The noisier cases can remain `NOISE LIMITED` rather than falsely claiming 1°/s precision. Replay cannot model animal adaptation; use the live 240-second run to verify settling and the flight gate. Increase settling if adaptation needs more time.

## Behavior and units

The wire `yaw` is raw left minus right wing angle in radians. Flight uses its rolling **population variance before gain and DC offset**, with threshold 0.01 **rad²** by default. Tune this for the species and tracker units. Entry requires variance above the threshold continuously for `flightConfirmationSeconds`; once flying, variance at or below `threshold × flightVarianceHysteresis` exits immediately. This avoids rapid switching near the threshold. A constant, even large, biased signal is not classified as flight. Nonflight, open-loop yaw, zero gain, unsupported mode, missing/stale input, and stopped/failed experiments all pause corrections. This gate does not turn off historical yaw motion.

Each rig samples distinct fresh packets at up to 50 Hz. Repeated held packets cannot fill the windows. Flight assessment needs the configured window and at least five samples per second of window length (minimum five samples). A gap longer than one second clears evidence. The UI distinguishes unmeasured flight (`FLIGHT ?`) from measured `NOT FLYING` and `FLYING`, even when auto trim is off.

While flying in closed loop, the controller gathers qualified L−R samples. The default centers the **mean**, because heading integrates yaw rate: a zero mean removes expected heading drift. The original median goal remains available with `centerStatistic: "median"`; for a Gaussian distribution both target the same true center. A skewed signal can have zero median while retaining mean drift.

```text
meanYawRateDegPerSecond = gain × (meanRawWingDifference − currentDCOffset) × 180/π
correctionRadians = clamp(aggressiveness × (selectedRawCenter − currentDCOffset), ±maxStepRadians)
newDCOffset = currentDCOffset + boundedCorrection
```

The gain and its sign are known, so the linear offset response can be solved directly; binary search is unnecessary. Negative gains work too. Both the per-pass and absolute bounds apply. An unreachable center reports `OFFSET LIMIT`; manual offsets farther than one step outside the allowed range pause trimming. After each correction the animal gets `settleSeconds` to adapt, then a complete fresh measurement window. Nonflight, open loop and stale input clear the trim window; gain/manual-offset changes also restart settling.

With `noiseAware: true`, a correction from a complete precision window requires residual rate outside both the requested tolerance and an approximate two-standard-error noise band. This uses the larger of ordinary sample error and one-second block-mean error to account for short-term correlation. Median mode multiplies this by the Gaussian median efficiency factor; uncertainty is approximate for non-Gaussian or longer-correlated animal data. Once the estimate approaches zero, the window grows toward the precision needed to verify the tolerance, capped by `maxWindowSeconds`. During verification, a parallel initial-length window can interrupt the long wait when a new bias exceeds five combined standard errors of the current and learned estimates. This prevents a changing animal from waiting for a two-minute window while suppressing ordinary noise-driven corrections. After such a detected change, it takes another fresh short window before estimating long-window precision; the mixed old/new response cannot force a long delay. Completed precision windows use their own uncertainty to refine a proven remaining bias. The UI shows the longer collecting window and its uncertainty. `CENTERED` requires both residual and uncertainty within tolerance; `NOISE LIMITED` means the available evidence cannot verify that tolerance. It continues reassessing while enabled. `noiseAware: false` uses the configured window/tolerance alone.

Random yaw noise integrates into a wandering heading even with the correct DC. Expect the strong linear trend to disappear, not a perfectly horizontal trace. [Gaussian heading plots](figures/kinefly-gaussian-heading.svg) compare the same input with and without trimming. Statistical refinement can require additional passes or a longer run; a dynamically changing animal does not carry a five-pass guarantee.

| `autoTrimSettings` field | Default | Range / effect |
|---|---|---|
| `windowSeconds` | 15 | 1–300 s initial qualified measurement window. |
| `maxWindowSeconds` | 120 | 1–300 s maximum noise-verification window, at least the initial window in effect. |
| `centerStatistic` | mean | `mean` removes expected heading drift; `median` retains median centering. |
| `noiseAware` | true | Extend measurements and suppress corrections within the approximate noise band. |
| `flightWindowSeconds` | 2 | 0.2–30 s raw-signal variance window. |
| `flightCheckEnabled` | true | Set false to bypass nonflight while retaining measured flight status. |
| `flightVarianceThreshold` | 0.01 | 0–1,000,000 rad²; entry requires variance strictly above it. |
| `flightConfirmationSeconds` | 1 | 0–30 s continuously above entry threshold before declaring flight. |
| `flightVarianceHysteresis` | 0.5 | 0–1 multiplier of entry threshold for exiting flight. |
| `updateIntervalSeconds` | 0.5 | 0.1–60 s between center evaluations. |
| `aggressiveness` | 1 | 0.001–1 fraction of selected residual removed per pass. |
| `maxStepRadians` | 1 | 0.000001–10 rad maximum DC change per pass. |
| `maxOffsetRadians` | 2 | 0.000001–10 rad maximum absolute automatic offset. |
| `settleSeconds` | 3 | 0–300 s adaptation time after corrections or gain/manual-offset changes. |
| `toleranceDegPerSecond` | 1 | 0–1,000,000 deg/s effective center tolerance after gain. |

All numbers must be finite. Increasing aggressiveness/step size or shortening windows makes it faster. No algorithm can distinguish a persistent stimulus-driven turn from a calibration bias using this signal alone: use the row flag to determine when centering is scientifically appropriate.

## Controls and persistence

**Tab** shows the dashboard; **1–4** select the rig for manual **[ / ]** offset adjustment. The dashboard's **AUTO TRIM ON/OFF** button changes the running row's in-memory flag for all supported rigs; each subsequent row loads its own flag. It is disabled outside an experiment or without Kinefly yaw-rate rigs. No config file is rewritten. Button changes appear in rig CSV and telemetry.

**FLIGHT CHECK ON / bypass** temporarily permits trimming without a flying classification; click **FLIGHT BYPASSED / restore check** to restore the gate. The override lasts until a new experiment phase/row or restart. For a deliberately ungated calibration, set `autoTrimSettings.flightCheckEnabled: false` in the experiment file. The measured `FLYING`/`NOT FLYING` indicator continues independently. Bypassing flight does not bypass missing/stale/duplicate input, unsupported tracking, open-loop yaw, zero gain, stopped/failed sessions or the offset limits.

Dashboard text updates every 0.5 seconds. Every active rig starts with signed **DC in degrees**, including while trim is off or paused. It also shows instantaneous yaw and the gain-scaled DC compensation in **°/s** (the term subtracted from untrimmed yaw), loop switches, gain, flight state, wing standard deviation and its flight threshold in degrees, distinct samples, bypass, measurement progress, settling and pass count. The latest evaluated window shows both mean and median under the **applied** offset, plus estimated uncertainty in °/s. The previous UI showed the pre-correction residual, which could look as if trimming had done nothing.

`LAST AUTO: DC UP +5.73°  +0.11 -> +5.84°` shows the actual last per-rig adjustment. UP/DOWN describes DC direction independently of gain sign. Changes highlight amber for five seconds while active; history stays gray when paused/off. Green means flying/closed/centered; amber means measurement/correction; gray means inactive/off/paused. Unsupported modes show a dimmed DC and their input mode.

Offsets persist independently for the four rigs across in-place rows and scene reloads during the current application run, including rows with auto trim false. They also survive Stop/Start within that process, just as manual offsets do. A new application run starts from the component's configured offset; this feature does not transfer an animal's calibration to a later session. Rig CSV retains its existing raw-radian columns and adds `yawDCOffsetDegrees`, `yawDCCompensationDegPerSecond`, `autoTrimMeanDegPerSecond`, `autoTrimUncertaintyDegPerSecond` and `autoTrimMeasurementSeconds`. Outbound telemetry schema 2 uses degrees for all angular values and °/s for rates; its default topic is `matrex.telemetry.v2`.

## Swarm recording and performance

The standalone Swarm logger previously searched all tagged agents, formatted CSV, compressed with optimal gzip, and wrote the recording from every Update on the render thread. It now caches its spawner's agents (refreshing after rebuilds), captures their positions in LateUpdate, and queues pooled snapshots. One worker per logger formats invariant CSV, uses fast gzip, and writes to disk. Timestamp, skybox and phase are captured with the positions; worker timing cannot relabel a frame. Column layout and gzip filenames remain compatible.

The queue holds at most 240 frames per logger. Scene unload requests an asynchronous drain; normal application exit waits for pending recordings to finish. If storage cannot keep up, capture remains nonblocking and dropped frames are counted and reported as errors. The dashboard shows capture milliseconds, queued frames and dropped frames. The Unity Profiler marker `SwarmLogger.Capture` measures the main-thread capture work. Abrupt process termination cannot guarantee a finalized gzip file.

The embedded swarm created by Choice/Selwyn has **no SwarmLogger**. It now uses `BogongSwarmRenderer`: one dynamic mesh/material/camera callback per rig rather than one renderer/material/callback per agent. Centers update once after movement; the GPU calculates each circle's angular radius and facing for the camera rendering it. Movement roots, speed, heading-relative spawning, boundaries, opaque RGB, per-rig layers and world/angular size remain unchanged. The existing Selwyn stimulus stays at 256 dots per rig. Scene-prefab visuals continue using their existing rendering path.

The sampled latest session records median frame intervals of 17 ms under stars and 22 ms during swarm stimulus (95th percentile 35 ms in stimulus). The [Selwyn validation report](selwyn-swarm-validation-results.json) compares exact reference pixels and isolated render CPU timings for four rigs, six 128×128 cameras each: 64 dots/rig improved from 4.16 to 1.80 ms, and 256 dots/rig from 10.65 to 1.83 ms on an Apple M2 Pro. All 786,432 compared pixels matched. These timings exclude terrain and normal whole-frame work and do not establish FPS on the LED workstation.

Run `KineflyGaussianValidation.Run` in a marked disposable project to regenerate the seeded cases and actual heading CSV traces. Plot them with `python3 tools/plot_kinefly_gaussian.py /path/to/disposable-project` (requires Matplotlib).

Run `KineflyAutoTrimValidation.Run` on a disposable Unity project with the `KANNADI_VALIDATION_COPY` marker. It checks signed-gain convergence, flight/input/control gates, multi-pass behavior, row and scene persistence, dashboard toggle/status, 64-agent recording on four rigs, gzip/FIFO metadata integrity, and legacy synchronous versus new capture timing. The desktop benchmark is diagnostic; it does not establish LED workstation GPU throughput or validate animal adaptation.

The [saved validation report](kinefly-auto-trim-validation-results.json) records passing live integration/capture tests and 73,746 historical gain comparisons with auto trim off. `SelwynSwarmValidation.Run` adds recorded-input replay and reference-render comparisons; `BogongDotValidation.Run` covers both rendering paths at near/far distances and cube-face seams in world/angular size modes.
