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

## Workstation frame pacing

The reported LED setting is `target 60`, VSync off, with 58–60 FPS even in a simple scene. This uses Unity's software timer, which [Unity documents as susceptible to microstutter](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html). It does not by itself measure remaining CPU/GPU capacity. For a 60 Hz output, use top-level `"targetFrameRate":60, "vSyncCount":1`. The [60 Hz four-rig Kinefly example](../Assets/StreamingAssets/Examples/System/kinefly-six-cameras-60hz.example.json) includes these settings; preserve the workstation's actual tracker addresses, panel placement and target display. `/1` follows the actual refresh rate; on a 120 Hz output use `/2` for 60 FPS. Explicit existing pacing settings remain authoritative.

The FPS HUD now samples a fixed one-element `FrameTiming` workspace, refreshes text at 2 Hz, and reports frame mean/peak, CPU main/render work, GPU and presentation/target-FPS wait milliseconds. These summaries also appear in outbound telemetry. Unity's CPU work times already exclude its synchronization waits; do not subtract the wait again. A large wait can come from the cap/VSync **or the GPU**, so inspect the work timings together. Release builds enable Frame Timing Stats. Unsupported timings appear as a dash/null: [Linux OpenGL does not provide GPU frame time; Linux Vulkan does](https://docs.unity3d.com/6000.3/Documentation/Manual/frame-timing-manager.html). This instrumentation does not classify a machine as GPU-bound when GPU data is absent.

Tab now works throughout a running sequence even with lingering metadata focus. Hidden/inactive/non-interactable legacy and TMP text fields no longer suppress operator keys. Hiding deactivates dashboard graphics and stops its format/layout refresh; configuration refresh does not reopen it. The small timing readout remains available for comparing the same scene with the dashboard shown/hidden. New errors still open the panel.

The regression fixture failed on the previous implementation at hidden-canvas focus, then passed on the fix. The updated live validator also checks configuration visibility persistence, running versus idle metadata handling, duplicate removal/error capture, actual graphics deactivation, wire timing fields and timing-label layout. The [saved frame dashboard report](frame-dashboard-validation-results.json) records the source hashes and results. CPU/GPU numbers from batch Editor mode are not workstation measurements.

## Verification and reproduction

All 74 Gaussian cases match the preceding estimator's results exactly, including actual Unity heading integration and the five-pass convergence checks. The median selector matches independent full-sort truth in 80 cases, including ordered/reversed input, duplicate values, extreme values, odd/even counts and unused workspace capacity. Live flight/bypass/persistence/recording checks pass with 64 agents per rig. Telemetry checks pass with actual PUB/SUB packets, degree fields, dashboard controls, scene transitions and worker disable/re-enable.

Use a disposable Unity project containing the repository's Assets, Packages and ProjectSettings, plus a `KANNADI_VALIDATION_COPY` marker in its root. These validators refuse to run without that marker; the scene benchmark replaces only the disposable project's local system/sequence configs. Run Unity 6000.3.16f1 with `-batchmode -projectPath COPY -executeMethod METHOD -logFile LOG`:

- `DiagnosticsPerformanceValidation.Run`: warmed CPU callbacks, allocation-event recording, median truth checks and dashboard layout comparison.
- `DiagnosticsFrameValidation.Run`: real Selwyn sky scene, features isolated in sequence, explicit 24-camera submission, game callback timing and per-frame allocation bytes.
- `KineflyGaussianValidation.Run`: hidden-mean convergence and actual heading integration.
- `KineflyAutoTrimValidation.Run`: flight gating, controls, persistence, CSV phase capture and swarm worker drain.
- `HeadingTelemetryValidation.Run`: real telemetry transport and restart.

`GC.GetAllocatedBytesForCurrentThread()` reports no usable allocation data in this Unity Mono runtime. The callback benchmark therefore counts Unity `GC.Alloc` events rather than mislabeling their timing as bytes. The scene benchmark uses the `GC Allocated In Frame` byte counter. Callback timings exclude cold startup, but occasional garbage-collection or OS scheduling outliers remain visible in the report.
