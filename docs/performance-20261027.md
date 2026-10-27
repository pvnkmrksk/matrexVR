# Mac performance investigation — 27 October 2026

The recent four-rig operator profiles explicitly set `targetFrameRate: 60`; this is a software cap, not an observed 60 FPS throughput ceiling. The fixed terrain scene now respects the apparatus instead of substituting one fullscreen camera. The diagnostic player can exceed the requested 400–500 engine FPS for the simple one-rig workloads tested here.

These are standalone native-resolution (3024×1898) software measurements on this Mac, using independent generated native tracker input and actual per-rig pose CSV logging. All other Unity editor validations and native acquisition tests were closed during the measurements. The player ran in the background with `runInBackground=true` and active camera draws; **engine FPS is not physical displayed/LED FPS**. Native camera/video encoding was tested separately. Full raw distributions, camera rects, input freshness, process CPU and metadata are retained in the [structured report](performance-20261027-results.json).

| Workload | Cameras | Uncapped baseline / repeat | Repeated 60 FPS cap | Process CPU per frame at 60 cap, all Unity threads |
| --- | ---: | ---: | ---: | ---: |
| Empty Choice, one desktop rig | 1 native-resolution view | 1,326 / 1,414 FPS | 59.8 FPS | 2.51 ms |
| Terrain choice, one LED rig | 6 × 128² views | 1,898 / 1,591 FPS | 59.8 FPS | 3.23 ms |
| Terrain choice, four LED rigs | 24 × 128² views | 492 / 443 FPS | 59.6 FPS | 8.31 ms |

The LED cases assert the actual standalone camera count, 128×128 output dimensions, correct target display, and ownership by `ViewportSetter`. The full hardware-free 64-trial terrain test separately checks horizontal/vertical/desktop routing, all sixteen mesh-role combinations, saved poses and archived replay roles.

## Isolate the work

Each case includes separate uncapped phases with tracking disabled, CSV logging disabled, telemetry disabled, overlays disabled, all those services disabled, all cameras disabled, and 128² offscreen targets. Components are restored between phases. Two-second settling intervals and five-second measurements are used, with repeated capped and uncapped baselines. Input freshness is recorded.

For the four-rig case, tracking-off measured 638 FPS; logging-off 499; telemetry-off 454; overlays-off 463; all services-off 489; and cameras-off 5,402. With the same small geometry, 24 camera render passes are the main scaling cost in these samples. Disabling logging or telemetry did not reveal a large hidden bottleneck. The phase-to-phase range and two uncapped baselines show ordinary variability, so small differences are not treated as causal speedups. Tracking-off is an isolation of Unity listeners/motion; independent generated publishers remain outside the measured Unity process.

No 16-rig physical-camera/LED throughput claim is made. These tests do not include live calibrated acquisition, native video encoding load, or a displayed-frame timing measurement. Other desktop applications remained open; this is an operational workstation measurement, not a laboratory hardware benchmark.

## Correct the readout

The former overlay labelled a platform-reported main-thread frame span as `CPU` and one presentation field as `wait`. In the capped empty-scene test, that main span was about 16 ms while the Unity process consumed only 15–17% of one CPU core. The raw span is not a useful CPU-work estimate for this case. A low presentation-wait value is also not a measurement of every idle interval. Unity documents separate [frame-timing counters](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/counter-reference) and [rendering/wait markers](https://docs.unity.cn/Documentation/Manual/profiler-markers.html).

The header now states the cap/VSync/uncapped policy. The operator overlay shows **CPU proc**, measured from OS process CPU time and summed across Unity threads, separately from frame wall time, GPU time and **present wait**. Setup hover help explains those meanings. Raw main/render fields remain in telemetry for compatibility. CPU process time can exceed frame wall time when threads run concurrently; subtracting it from a frame is not a precise headroom calculation.

## Limits and retained findings

The initial one-rig terrain 60-FPS window contained one 1,000 ms stall: mean 48.1 FPS, median 16.69 ms, p95 17.42 ms. The repeated window averaged 59.8 FPS with a 19.77 ms maximum. The stall is retained in the report; it has not been attributed to scene workload or claimed eliminated. A first diagnostic terrain snapshot had no active cameras because assets were being generated while it was copied; that invalid result is explicitly excluded.

Several release profiler counters were unavailable, and this Mono build returned a zero allocation count despite CSV string formatting. Allocation counts are therefore marked unavailable, not claimed as zero. The probe now checks counter support and excludes counter-initialization calls from its timed interval.

The opt-in `--performance-probe <output>` runtime diagnostic is installed only when explicitly requested. Options include `--performance-scene`, `--performance-layout desktop|led`, `--performance-rigs`, `--performance-seconds`, and an optional comma-separated `--performance-phases`. Use a disposable config/data directory. An uncapped probe is a software investigation; normal experiment pacing remains controlled by system JSON.
