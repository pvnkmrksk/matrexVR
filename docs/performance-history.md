# MATREX VR recording and rendering performance history

The Linux workstation progressed from 44.55 FPS at 16 rigs with 64 native animated locusts per rig to 66.76 FPS at 16 rigs with 1,280 cached locusts per rig and complete recording. The matched comparisons below isolate each change. The [structured evidence](performance-history-results.json) retains measured phases, build identities where available, recording outcomes, source-result hashes, and selected frame-interval series.

Counts in the form 16 × 768 mean 16 rigs and 768 locusts **per rig**. The later stress tests used an i9-13900K with 32 logical CPUs, RTX 4070, Unity 6000.3.16f1, and Vulkan. They rendered 64 stimulus cameras with 128 × 128 faces, generally for 65–75 seconds after warmup. The earlier Mac mirror investigation used 96 cameras and a different scene. Later controlled Linux runs used isolated profiles without live trackers. The early Linux results identify the scope of live tracking separately. A 60 FPS mean can include intervals above the 16.67 ms frame budget. Zero population drops describes the recorder queue, separately from render pacing.

## Native renderers and the four-rig starting point

On the Mac, four rigs with 64 locusts each ran near 59 FPS. Sixteen rigs with the same count per rig rendered roughly 19 FPS in the initial workload, with a variable repeat. A separate 16-rig mirror scene with 37 animals per rig rose from 20 to 30 FPS after moving row formatting and compression to bounded workers and reducing world capture. Turning off all evidence left that mirror scene near 30 FPS, while turning off cameras raised it to 56.50 FPS. The remaining cost was preparing and submitting native skinned objects across many views. [Mac population investigation](population-performance-20261029.md)

The early Mac shared-pose proxy passed an Editor geometry check and failed the native 96-camera pixel gate with a mean RGB error of 0.34747. Its runtime hook and shader were removed. A separate run used a fixture with altered shadows and was excluded. [Mac fidelity results](population-performance-20261029-results.json)

On Linux, 16 × 64 initially rendered at 44.55 FPS. Animation JSON formatting took 10.63 ms of the 22.26 ms main path while the existing writer handled file output. The first asynchronous snapshot candidate reached roughly 57 FPS. Reusing frame slots and numeric formatting buffers reduced global garbage collections from 122–126 to 25–26 per 15 seconds. The final repeat reached 59.91–59.93 FPS with 0.61–0.62 ms population capture and 12.30–12.45 ms main-thread time. The original native geometry, movement, poses, and animation evidence remained part of the measured workload. Shortening the far clip to 500 or 300 cm left the original recording bottleneck in place. [Linux recording optimization](population-performance-20261106.md)

The native-renderer capacity sweep put four rigs × 512 near 59.94 FPS and sixteen rigs × 64 near 59.99 FPS with recording. Sixteen × 80 averaged 59.63 FPS and sixteen × 256 fell to 23.28 FPS. Fewer rigs reduced camera and rig overhead as well as total animal count. The single-rig 1,792-agent case grew its writer queue. A single rig with 2,048 agents lost 165 recording frames despite rendering near 60 FPS, establishing a separate per-writer limit in the older format. [Native capacity sweep](population-capacity-20261106.md)

## Stream the recording and move packaging off Stop

An earlier ten-minute run held 575,968 rig poses, 35,999 world rows, and 36.86 million population rows. The compressed population streams occupied about 803 MiB. Historical finalization took roughly 240–247 seconds from the last sample to the manifest and another 19 seconds for the ZIP. On that run, a replay-reader refactor cut rig-pose loading from 216.5 s to 7.4 s by eliminating repeated presentation copies and simplifying validation reads. Conversion of one 2.3-million-row legacy stream went from 21.5 s to 17.0 s. These were reader-stage timings, separate from total replay time.

Acquisition now streams .matpop per population on bounded workers. Each stream retains shared frame context, interned agent identities, float32 poses and scale, Unity frame identity, timestamp, and actual animation evidence. Typed steady Animator and procedural states use a compact representation. Rich animation states retain the JSON fallback. Workers hash bytes as they write, append a footer with counts, and report the final size and digest. Stop drains the writers, verifies their sealed evidence, and publishes the manifest. ZIP packaging runs separately while acquisition is idle. Rig pose CSV remains a distinct track. The same-build reader validates inventory, digests, and exact frame coverage and seeks directly in native population files. Legacy CSV uses a disposable cache. [Data formats](data-formats.md) · [Same-build replay contract](same-build-replay-contract.md)

At 16 × 64, the quiet stream build averaged 59.95 FPS and took 8.07 s to finalize. The offline-packaging build averaged 59.97 FPS and sealed 4.91 million rows with zero drops in 1.37 s. Packaging separately took 4.63 s. A 306,816-row sample occupied 16.49 MB in .matpop, 5.91 MB in chunked Zstandard Parquet, and 129.22 MB as CSV. Packing took 1.96 s, reconstruction took 2.02 s, and a single-frame CSV export took 0.085 s. The reconstructed canonical CSV matched. The 16-stream, 4.91-million-row export took 35.86 s offline. [Structured evidence](performance-history-results.json)

## Stock locust instancing and parallel movement

At 16 × 256 with native skinned draws, the quiet full-recording run averaged 26.61 FPS. Omitting population logging raised it to 28.83 FPS. A separate renderer isolation measured 29.10 FPS with native draws, 37.92 FPS without animation, 73.65 FPS without skinned renderers, and 82.68 FPS without cameras. Graphics Jobs alone gave 29.30 FPS. Native graphics plus the motion job reached 30.67 FPS in its diagnostic. [Structured evidence](performance-history-results.json)

The stock path batches eligible groups with the same mesh, skeleton layout, and compatible shared material. Unity still evaluates live bone poses at this stage. The batched GPU path retains individual poses and the four stimulus faces per rig. Parallel generated motion preserves ordering and periodic behavior. With population recording disabled for diagnosis, instancing reached 56.49 FPS and the instancing-plus-motion variant reached 62.05 FPS. With full recording, CPU culling reached 52.16 FPS and GPU clipping reached 53.03 FPS. The default build averaged 53.09 FPS with 17.60 million rows, zero drops, and a 1.37 s seal. It submitted approximately 261 batches per frame. The frozen comparison found 17 differing stimulus pixels out of 1,048,576, a maximum single-channel delta of 173, and a mean channel delta of 0.000159. That difference was accepted for the stock workload. Incompatible groups and passes retain the native path. [Structured evidence](performance-history-results.json)

## Stagger movement checks and retain animation phases

Eligible stock swarms divide the movement decision into ten cohorts and estimate horizontal speed across boundary crossings. They start walking above the configured threshold and stop at or below 80% of it. At 60 FPS each locust is checked about every 167 ms. The Animator still advances the leg poses and the recorder captures the realized phase.

The matched 16 × 256 run rose from 52.48 to 57.42 FPS and its p99 interval fell from 23.76 to 21.40 ms. The quiet final repeat gave 57.21 FPS and 21.55 ms p99. Another repeat averaged 55.33 FPS and had a 114 ms maximum interval around a window of concurrent Git activity. The writer queue was empty during the stall, and the quiet repeat did not reproduce it. Disabling the Animators in a diagnostic reached 88.98 FPS and demonstrated the cost of evaluating poses after the staggered gate. [Structured evidence](performance-history-results.json)

## Cache one stock walk cycle and instance multiple materials

The stock cycle bakes 4,096 phase samples into one 2.8 MiB GPU table. Each cached locust carries its root transform, selected phase, and motion-gate state. The stock clip had zero root displacement in validation, so the generated movement controller still owns movement and wrapping. Stock and flat-black material groups can share the same phase table. The native animation path covers incompatible models.

At 16 × 256, the same player measured 56.71 FPS with live Animators and 146.72 FPS with the shared cache. The p99 interval fell from 21.86 to 10.74 ms. At a 60 FPS software cap, that workload averaged 59.92 FPS, had zero intervals above 25 ms, and sealed 17.17 million rows with zero drops in 1.18 s. VSync on the same workstation averaged 60.00 FPS with a 30.54 ms p99. The local calibrated profile therefore used the software cap. At 16 × 512 it averaged 59.60 FPS with 22.40 ms p99. At 16 × 768 the cache alone averaged 52.96 FPS. An optional parallel culling trial reached 55.79 FPS and was left out of the cache-stage default while recording and submission costs were investigated. [Structured evidence](performance-history-results.json)

## Parallelize frame capture and draw preparation

At 16 × 768, the controlled baseline measured 52.33 FPS, a 19.11 ms main path, 4.24 ms population capture, and 3.61 ms world capture. Diagnostic phases reached 67.84 FPS with world capture omitted, 70.91 FPS with population capture omitted, and 100.82 FPS with all evidence omitted. The final pipeline retains complete recording. It reads population positions and rotations in worker jobs, reuses shared world capture state, and prepares camera visibility and ordered draw packets in Burst jobs. Unity's draw calls and assembly of the recording frame still use the main thread. At 768 per rig, world capture fell to 0.10 ms. At 1,024 per rig, packed draws cut main-thread submission from 5.09 ms to 0.84 ms. [Structured evidence](performance-history-results.json)

| Locusts per rig at 16 rigs | Compared stage | Average FPS | Frame interval p99 |
| ---: | --- | ---: | ---: |
| 768 | Cache baseline → parallel capture and culling | 52.33 → 81.21 | 27.66 → 21.32 ms |
| 768 | Parallel capture and culling with four configured workers | 83.34 | 20.90 ms |
| 768 | Parallel capture and culling at 60 software cap | 59.12 | 26.80 ms |
| 1,024 | Before → after worker draw preparation | 61.02 → 83.07 | 28.31 → 23.98 ms |
| 1,280 | Before → after worker draw preparation | 48.22 → 66.76 | 35.43 → 29.78 ms |
| 1,536 | Final default parallel build | 53.31 | 38.60 ms |

The final 1,280-per-rig run recorded 94.95 million rows and sealed in 1.56 s. The 1,536-per-rig run recorded 91.40 million rows and sealed in 1.32 s. Both had zero recorded population drops. Frozen serial-versus-parallel capture and draw-packing comparisons matched all 1,048,576 stimulus pixels. A pose verification run matched 12,288 per-frame reads. The eye-level preview moved the example camera from 6 cm to 1.6 cm above the spawn root. The throughput comparisons keep the original fixed camera recipe. [Structured evidence](performance-history-results.json)

At 1,536 per rig, the main thread averaged 18.76 ms and the GPU averaged 6.67 ms. Population capture remained the largest measured main-thread stage at 8.45 ms. It checks active object state, reads live Unity scale for exact evidence, and assembles each frame on the main thread. All 31 Unity workers made progress in the median sample, with the pool using about 3.36 logical-core equivalents and the whole process about 16% of CPU capacity. The four-worker comparison suggests the earlier pipeline already had spare worker capacity. The remaining measured opportunity is the serial part of population capture. [Structured evidence](performance-history-results.json)

## Reproduce and compare

Use an isolated profile, the same scene recipe and seed, and fresh writable output paths. Record the build identity and preserve the population count, camera order, resolution, and pacing. The standalone RuntimePerformanceProbe supports full-recording and diagnostic phases.

    "/path/to/MATREX VR.x86_64" -force-vulkan \
      --config-dir /path/to/isolated/Config \
      --data-dir /path/to/isolated/RunData \
      --performance-probe /path/to/results \
      --performance-system /path/to/isolated/Config/system_config.json \
      --performance-scene Kannadi \
      --performance-experiment Examples/Kannadi/tight-stock.example.json \
      --performance-seed 4318 --performance-agents 1280 \
      --performance-phases all-uncapped --performance-seconds 65

The current tight example uses the eye-level camera. To reproduce the earlier stress geometry, use the referenceTightSwarm experiment in the structured report with the original four-camera, 16-rig apparatus. Use the per-case flags when isolating renderers, workers, timing, or recording. Physical LED scanout and live tracker latency require separate measurements. [Earlier matched Linux workload](population-performance-20261106.md) · [Phase summaries and selected raw intervals](performance-history-results.json)

The post-0.4.2 follow-up moves stock population packet assembly off the Unity thread and removes repeated scene scans from the dense status overlay. At 16 × 1,536 with full recording, the 65-second comparison reached 95.09 FPS and a 16.39 ms p99, with 160.4 million rows and zero recording drops. [Worker capture and CPU timing](population-worker-performance.md)
