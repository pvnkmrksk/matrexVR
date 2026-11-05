# Population performance investigation — 29 October 2026

The safe changes improve the supplied sixteen-rig Kannadi mirror workload from **20 to 30 engine FPS** on this Mac. **The 60 FPS target remains unmet.** Rendering and animation retain their original Unity components; no recording frames, pose evidence, animation phases, cameras, shadows, models or material features are discarded to obtain this result. Complete summaries, p95/p99 distributions, commands, build identities and raw-result hashes are in [the structured report](population-performance-20261029-results.json).

## Workloads and measurement conditions

Actual foreground macOS standalone players use Unity 6000.3.16f1, Metal/Apple M2 Pro, macOS 27.0.1, the existing 3024×1898 window, 60 engine cap/VSync 0, and six 128×128 LED cameras per rig. Normal source quality is Very Low, shadows None, shadowDistance 15, skinWeights 1, pixelLightCount 0, AA 0. Quality was not reduced for the optimization. Camera count, output size, display ownership and enabled native Animator counts are asserted by the probe. Existing user Preview and other desktop applications remained open; other agents' Unity/Cargo/native GUI tests were held during controlled final measurements. No physical tracker or video acquisition load is included.

The selected stimulus from `Templates/kannadi.template.json` has three mirror rings: **37 original animated animals per rig**, so sixteen rigs mean 592 native Animators and 96 cameras. The selected stimulus from `Examples/Swarm/generated-bogong-speed-distribution.example.json` is also tested with the real `SimulatedLocust` visual and 64 animals **per rig**, preserving its independent animation phases. One/four/sixteen rigs therefore mean 64/256/1,024 native Animators. These populations are different workloads; a 16-rig result must not be labelled “64 total animals.” Seed mode and resolved seed are archived; all listed runs used fixed seed 1.

| Workload, evidence on | Baseline FPS | Safe native renderer FPS | Final p95 / p99 frame(ms) |
| --- | ---: | ---: | ---: |
| Mirror37×16=592,96 cameras |20.05 /20.00 |29.88 /30.01 |36.49 /39.74;36.51 /37.24 |
|64×1=64,6 cameras |59.51 /59.34 |57.36 |20.08 /21.85 |
|64×4=256,24 cameras |59.3 /59.1 |56.99 |22.15 /25.00 |
|64×16=1,024,96 cameras |18.83 /10.4(variable repeat) |20.92 /20.75 |52.46 /54.14;52.42 /53.38 |

These are short workstation windows with pacing variance, not a sustained 60 FPS or physical LED timing guarantee. The one/four-rig cases have substantial GPU headroom but still show cap/presentation jitter. The native animation stress case remains below target. Repeated8-second mirror windows are the primary comparable safe result; the final native-only build below rechecks the remaining bottleneck.

## Identified work and safe changes

Original mirror recording spent **15.38 ms/frame on the Unity main thread** formatting rows and compressing gzip synchronously; world evidence took 3.77 ms. Final mirror capture is 5.56–5.64 ms and world evidence 0.50–0.51 ms. CSV formatting/compression now runs through the existing bounded, pooled asynchronous population writer, with original 20-column mirror schema, exact frame/timestamp/pose/scale/animation fields, writer drain and explicit failure on queue exhaustion. Worker cell escaping and invariant float formatting avoid intermediate row strings. Native Animator parameters/layers and standard JSON serializer buffers are reused, with actual values sampled every rendered frame and transition-only realized poses cleared correctly. The standard extensible serializer contract remains intact.

World evidence uses typed wind/particle queries instead of traversing every skeleton twice each frame. Recorded shader-clock vectors are evaluated/parsed once and installed across cameras/lights without per-camera JSON allocations. Camera/shadow clock command-buffer stages remain unchanged.

The residual mirror workload issues about **10,168 draw batches and 3.12 million triangles per frame**, despite the small camera outputs. Final GPU time is 10.95–11.23 ms, essentially unchanged from baseline 11.17 ms; total Unity process CPU is 97.6–98.3 ms/frame across threads. That total is concurrent CPU work, not a wall-time or main-thread budget. In the first safe isolation, disabling population logging still gave 30 FPS, while disabling cameras gave 58.9 FPS. This identifies the many-camera native skinned render submission path as the main remaining scaling cost. Low pixel resolution does not remove per-camera culling/draw preparation/skin submission. The final native-only standalone repeats 30.02 FPS. Turning Animator updates off gives 30.01 FPS (process CPU 99.36→93.78 ms/frame); disabling all evidence also gives 30.01 FPS; disabling cameras gives 56.50 FPS (process CPU 15.92 ms, GPU 0.199 ms). Removing animation updates saves some CPU but does not remove the limit. The remaining camera cost includes culling, native skin/draw preparation and submission; this release build does not expose enough detailed profiler markers to divide those native substeps further.

## Fidelity gate and excluded experiment

A guarded shared-pose mesh/instancing prototype passed an Editor fixture, including deformed vertices, multiple transforms, transitions, fallback and mostly identical pixels. The actual packaged 96-camera scene then **failed** the native-versus-proxy pixel gate: mean RGB error 0.34747. The runtime hook, implementation, retained shader variant and prototype-only validator were removed completely. No performance gain from that path is accepted or shipped. A separate disposable test fixture had persisted a nonoriginal shadow setting; that performance case is excluded rather than used for a comparison. Neither failure altered the source project quality settings.

## Recording/replay verification

The full native mirror run finalized 16 population streams with 24,457 rows each, **zero dropped frames and no writer failures**. Its manifest reports writers drained, 10,560 accepted animal-pose rows and no rejected pose rows. Read-only production replay parsing restored 16 rigs, the whole presentation configuration, and a representative 661-frame mirror stream without warnings. This is hardware-free software evidence, not a native tracker acquisition test.

`ReplayAnimationValidation` passed 371 checks, including original controller deformation at forward/backward seeks, procedural clock/phase, imported animated GLB, immutable CSV/cache hashes, and 12 new comparisons of reused animation capture against the full standard serializer before/during/after transitions. The archive/writer suite passed 690 checks plus 150 async lifecycle cycles; recording seal tests passed 16. Root combined GPU shader-clock and same-build replay tests cover final integration separately.

## Reproduce

Build only in a marked disposable project using the normal `MatrexReleaseBuilder.Build` entry point. Keep original `ProjectSettings/QualitySettings.asset`; run graphics normally, without `-nographics`, and close other owned tests during timed intervals. Use fresh writable config/data/output directories:

```sh
"/path/to/MATREX VR.app/Contents/MacOS/MATREX VR" \
  --config-dir /tmp/population-check/Config \
  --data-dir /tmp/population-check/RunData \
  --performance-probe /tmp/population-check \
  --performance-scene Kannadi --performance-layout led --performance-rigs 16 \
  --performance-experiment Templates/kannadi.template.json \
  --performance-experiment-phase stimulus --performance-agents 64 \
  --performance-visual ScenePrefab --performance-cap 60 --performance-seconds 4 \
  --performance-phases all-60,animation-off,all-evidence-off,no-cameras,all-60-repeat
```

Isolation phases deliberately turn evidence/animation/cameras off to identify costs, so their datasets are diagnostic fixtures, not complete replay recordings. For a complete evidence-on recording use only `all-60,all-60-repeat`. The earlier `native-renderers` phase name in archived commands disabled the rejected prototype; it is unnecessary and removed from the final probe.

```sh
python3 tools/test_replay_archive.py
python3 tools/test_recording_seal.py
# In a marked disposable Unity project:
Unity -batchmode -projectPath /path/to/copy -executeMethod ReplayAnimationValidation.Run -logFile /tmp/replay-animation.log
```
