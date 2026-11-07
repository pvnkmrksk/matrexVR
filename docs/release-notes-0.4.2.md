# MATREX VR 0.4.2 for Linux x86-64

This release adds streamed swarm recording, faster stock locust rendering, and a more compact configuration workbench. The Linux x86-64 archive and its SHA-256 file are the desktop release assets. The archive manifest identifies the pinned source revision, Unity version, compiled scenes, and bundled companion payloads. [Release downloads](https://github.com/pvnkmrksk/matrexVR/releases/tag/v0.4.2)

## Operator workflow

- **Recording** now streams virtual population evidence to bounded per-rig .matpop files. Stop drains the writers and seals the run with counts, digests, and recording status. Choose **Skip swarm data** to omit the virtual population while retaining the animal pose CSV. ZIP packaging and Parquet export run separately when acquisition is idle. [Recording pipeline](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/manual/recording-pipeline.md) · [Data formats and export commands](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/data-formats.md)
- **Replay** reads and seeks directly in sealed .matpop tracks. A run retains the creating build identity, original configuration and assets, resolved presentations, and exact rendered-frame evidence. Keep the complete sealed run and its creating player together. [Same-build replay contract](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/same-build-replay-contract.md)
- **Swarm rendering** batches eligible stock locusts on Vulkan. Each locust retains its own pose and animation phase while sharing the baked walk cycle. Larger eligible groups use worker jobs for pose reads, camera culling, and ordered draw packets. Incompatible meshes and rendering passes use their native paths. [Performance history](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/performance-history.md)
- **Setup and examples** use compact numeric controls, consistent vector alignment, clearer group labels, and wider paths where appropriate. Choose **Load → Kannadi · tight stock and black swarms** to open two eye-level 128-locust blocks. The second block mixes textured locusts and black silhouettes. [Example sequence](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/Assets/StreamingAssets/Examples/Sequences/kannadi-tight-swarms.sequence.json)

## Measured performance

The controlled Linux tests used an i9-13900K, RTX 4070, Unity 6000.3.16f1, Vulkan, 16 rigs, and 64 stimulus cameras with 128 × 128 faces. The later high-count samples lasted 65 seconds with full recording, software-generated swarms, and no live tracker input. The saved reports identify each measured build, camera recipe, phase, and frame-interval distribution.

| Measured workload and change | Before | After |
| --- | ---: | ---: |
| 16 × 64, move animation serialization off the main thread | 44.55 FPS | 59.91–59.93 FPS |
| 16 × 64, seal without synchronous ZIP packaging | 8.07 s | 1.37 s |
| 16 × 256, native renderer to stock instancing and motion jobs | 26.61 FPS | 53.09 FPS |
| 16 × 256, live Animators to shared walk cache, uncapped | 56.71 FPS | 146.72 FPS |
| 16 × 1,024, add worker draw preparation | 61.02 FPS | 83.07 FPS |
| 16 × 1,280, add worker draw preparation | 48.22 FPS | 66.76 FPS |

At 1,280 locusts per rig, the final development player recorded 94.95 million rows with zero population drops, sealed in 1.56 seconds, and measured a 29.78 ms frame interval p99. At 1,536 locusts per rig it averaged 53.31 FPS. Population capture remained the largest measured main-thread stage at 8.45 ms, including live Unity state reads and recording-frame assembly. GPU time averaged 6.67 ms against an 18.76 ms main-thread span. [Full chronology, rejected trials, and reproduction](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/performance-history.md) · [Structured evidence and raw interval series](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.2/docs/performance-history-results.json)

The frozen serial-versus-parallel render and draw-packing checks each matched all 1,048,576 stimulus pixels. The responsive workbench checks exercised editing and undo at 1920 × 1080 and 1024 × 720. The pinned Linux tracker and dashboard payloads are listed in the archive manifest. Bundled HejjéTrac requires glibc 2.39 or newer. The workstation measurements characterize software render pacing and recording. Physical LED scanout, camera exposure synchronization, and acquisition on a live apparatus require separate validation. The release asset manifest identifies the pinned player independently of the development benchmark builds.
