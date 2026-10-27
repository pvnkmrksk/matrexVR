# Evening capped-60 workload isolation — 27 October 2026

**Razor-sharp 60 FPS remains unresolved.** Disabling the individual MATREX subsystems did not consistently remove the pacing variation. A fresh minimal Unity cube control again reproduced software-cap jitter. No production timing defaults, user JSON settings, or FPS labels were changed to conceal it.

These are bounded software measurements, not display/exposure timing certification. The connected display inventory contains only the built-in 120 Hz Liquid Retina XDR panel; a separate genuine 60 Hz output was unavailable. Every player reported `Application.isFocused=false`. Native automation could inspect the diagnostic window but could not reliably activate it: its input bridge returned “Computer Use is not active” despite a current app state. Therefore this run does **not** establish foreground cadence. Unrelated OS/background work remained running, and no causal attribution to a specific OS task is made.

## Method and diagnostic corrections

An isolated universal Mac player was built from the working-tree source in `.local/evening-project`, with zero build errors. The final diagnostic app is `.local/evening-pacing/Evening Pacing.app`. The existing user build, independent trackers, configurations and prior recordings were not replaced.

The opt-in `RuntimePerformanceProbe` now retains cached frame-start intervals (`Time.unscaledTimeAsDouble`), wall-clock LateUpdate intervals, frame IDs, available profiler counters, delayed FrameTiming timestamps, GC collection deltas and explicitly unavailable allocation counters. Samples use preallocated lists of structs. The first partial interval is excluded, and FPS uses complete interval count divided by their actual summed duration. This corrects the old short-probe denominator; it does not improve physical pacing. The console's phase-boundary FPS remains separately reproducible in `phaseBoundaryFps`; the JSON `fps` is the complete-interval result.

`tools/run_terrain_recording_profile.py --cap 60` keeps workload toggles at the same actual cap, instead of comparing disabled subsystems uncapped. The isolated rig configurations use six on-screen **128 × 128** LED cameras per rig. One- and four-rig runs each use independent real Rekkefly processes with generated 640 × 480, 60 FPS inputs, first without video and then with native MJPEG saving. Each phase settles for two seconds and measures eight seconds. No physical acquisition is involved.

Seven phases cover all workload enabled, listeners off, pose logging off, telemetry off, overlays off, tracking/listeners off, and baseline repeated. A separate twelve-phase follow-up compares software 60 against VSync/2 on the same workloads. Forty terrain phases retain **18,954 complete frame intervals** in total. All runtime failure lists are empty. Native encoders finalized every accepted frame.

The initial isolation report's aggregate GC totals could include allocation during summary construction. Its **raw per-frame collection deltas** remain authoritative. The final source freezes aggregate collection totals before reporting; the subsequent VSync comparison validates that correction. Main-thread allocation counters are unavailable in this player and remain explicit rather than reported as zero. Profiler `LastValue` and FrameTiming records can refer to earlier completed frames; their timestamps/availability must be respected when interpreting outliers.

## Results

The first fully enabled software-cap phase of each isolation case is below. p95 and maximum use cached frame-start intervals; FPS uses complete wall-clock callback intervals.

| Rigs | Native video | FPS | p95 frame ms | Maximum frame ms |
| --- | --- | ---: | ---: | ---: |
| 1 | Off | 59.54 | 17.48 | 20.25 |
| 1 | On | 59.41 | 17.58 | 19.52 |
| 4 | Off | 58.55 | 17.58 | 49.97 |
| 4 | On | 59.29 | 17.57 | 25.07 |

Disabled-workload and repeated-baseline phases remain around 59 FPS. Listener/tracking removal reduces some four-rig CPU work, but no individual toggle establishes a stable 16.67 ms cadence. Video-on does not consistently worsen the cadence, so these results do not justify blaming video encoding. Only three of 74 intervals over 20 ms in the isolation series fall within one frame of an observed GC collection; this does not rule out incremental GC work or other application hitches, but it does not identify a reliable collection-triggered fix either.

In the VSync follow-up, VSync/2 yields 59.26, 59.40, 59.13 and 59.39 FPS for one/video-off, one/video-on, four/video-off and four/video-on respectively; p95 is approximately 17.58 ms in each case. It is not a terrain pacing fix and was not enabled as a product default.

The minimal cube, run separately without native tracker processes, measured:

| Condition | FPS | p95 wall interval ms | Maximum ms |
| --- | ---: | ---: | ---: |
| Software 60 | 59.51 | 17.57 | 18.24 |
| VSync/1 on 120 Hz | 119.70 | 8.55 | 9.32 |
| VSync/2 on 120 Hz | 59.90 | 16.84 | 17.84 |

VSync/2 improves the cube's distribution in this repeat but does not reproduce that improvement in the terrain player. The cube and terrain have different rendering and instrumentation workloads; neither counter is proof of actual screen presentation intervals. Unity also documents that [software target frame-rate limiting can microstutter](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html).

## Validation and next discriminating test

The [machine-readable report](frame-pacing-20261027-evening-results.json) records source identities, raw-file hashes, phase policies, timing distributions, GC correlation, encoder results and limitations. Its consistency checker passed **655 assertions**. The existing production FPS update test passed **1,393 assertions** and compiled against the installed Unity assemblies. Nine finalized native videos passed **207 production import/decode/reverse-seek assertions**, preserving original source hashes. The separate [recording-control audit](recording-control-20261027.md) records the new native protocol tests and remaining integration blockers.

The next useful timing test is a verified foreground run on a fixed **60 Hz** display using explicit VSync/1, alongside the software-cap control and real presentation tracing where available. The raw counters still do not isolate an application callback responsible for the broadest tails; a development-player timeline with usable script markers would be needed for finer attribution. Do not promise 60 Hz stimulus or camera timing from an engine FPS readout. No engine upgrade, blanket VSync policy change, upstream tracker modification, push or release was made.
