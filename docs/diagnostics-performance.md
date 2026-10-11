# Flight diagnostics performance

Profiling found two avoidable sources of frame pressure: repeated full-window sorts in auto trim, and repeated whole-row string copying in the four rig CSV loggers. Telemetry also serialized and sent its JSON on the Unity frame thread. The fixes preserve sampling, units, logged columns and calibration behavior.

- Auto trim retains its sample/value workspace and selects the exact median by partitioning, with a bounded fallback for difficult inputs. Both the long precision window and short recovery window retain their existing statistics.
- CSV formatting reuses a `StringBuilder`; every column no longer copies the growing row.
- Telemetry captures Unity values on the frame thread and serializes/publishes on one worker. Its pending slot retains the latest snapshot and counts overwritten snapshots. Socket creation, use and disposal remain on that worker, including configuration reload and disable/re-enable.
- The dashboard still updates at 2 Hz. Numeric changes no longer trigger an additional preferred-height text layout when its rows and error history are unchanged.

## Measurements

The [saved report](diagnostics-performance-results.json) includes source hashes, independent correctness checks and the benchmark conditions. These are Apple M2 Pro Unity Editor measurements, **not LED workstation FPS**.

| Work | Before | After |
|---|---:|---:|
| Four trim ticks, p99 CPU time, full 120-second windows | 2.36 ms | 0.35 ms |
| Four trim ticks, observed maximum CPU time | 8.42 ms | 0.50 ms |
| CSV allocation overhead per frame, four rigs, above the no-diagnostics baseline | 121,882 bytes | approximately 30,476 bytes |
| Dashboard full layout versus cached panel layout, paired final-run median | 0.96 ms | 0.67 ms |
| Telemetry JSON serialization/socket sending on frame thread | every publish | worker |

The live Selwyn MW sky fixture uses four rigs and six 128px cameras per rig. Camera rendering takes approximately **2.54–2.60 ms of CPU submission time**, while game callbacks take approximately **0.16 ms without diagnostics** and **0.33 ms with all diagnostics**. CSV formatting is the largest steady diagnostic cost; trim produces occasional evaluation work. Camera submission is the larger steady workload in this fixture. This measures CPU submission, not completed GPU time, display synchronization or the operator HUD's GPU fill cost.

Batch Editor mode does not automatically render a Game view. The final scene benchmark explicitly renders all 24 cameras and records their CPU time separately from `PlayerLoop`. The earlier callback-only baseline is retained only for callback and allocation comparisons; its very short frame intervals must not be interpreted as thousands of displayed frames per second. The old publisher did not restart after component re-enable, so its affected baseline cases are excluded. The optimized publisher is explicitly configured and its restart is checked with real received snapshots.

Historical VR1 data from `20261011_035104` has median frame intervals of 17 ms in star setup and 22 ms during swarm stimulus, with p99 intervals of 20 and 33 ms. That recording predates the current auto trim, so it shows scene-related slowdown without identifying today's controller cost. It does not prove a GPU bottleneck on the LED computer.

## Verification and reproduction

All 74 Gaussian cases match the preceding estimator's results exactly, including actual Unity heading integration and the five-pass convergence checks. The median selector matches independent full-sort truth in 80 cases, including ordered/reversed input, duplicate values, extreme values, odd/even counts and unused workspace capacity. Live flight/bypass/persistence/recording checks pass with 64 agents per rig. Telemetry checks pass with actual PUB/SUB packets, degree fields, dashboard controls, scene transitions and worker disable/re-enable.

Use a disposable Unity project containing the repository's Assets, Packages and ProjectSettings, plus a `KANNADI_VALIDATION_COPY` marker in its root. These validators refuse to run without that marker; the scene benchmark replaces only the disposable project's local system/sequence configs. Run Unity 6000.3.16f1 with `-batchmode -projectPath COPY -executeMethod METHOD -logFile LOG`:

- `DiagnosticsPerformanceValidation.Run`: warmed CPU callbacks, allocation-event recording, median truth checks and dashboard layout comparison.
- `DiagnosticsFrameValidation.Run`: real Selwyn sky scene, features isolated in sequence, explicit 24-camera submission, game callback timing and per-frame allocation bytes.
- `KineflyGaussianValidation.Run`: hidden-mean convergence and actual heading integration.
- `KineflyAutoTrimValidation.Run`: flight gating, controls, persistence, CSV phase capture and swarm worker drain.
- `HeadingTelemetryValidation.Run`: real telemetry transport and restart.

`GC.GetAllocatedBytesForCurrentThread()` reports no usable allocation data in this Unity Mono runtime. The callback benchmark therefore counts Unity `GC.Alloc` events rather than mislabeling their timing as bytes. The scene benchmark uses the `GC Allocated In Frame` byte counter. Callback timings exclude cold startup, but occasional garbage-collection or OS scheduling outliers remain visible in the report.
