# Linux 16-VR locust recording performance, 2026-11-06

The original locust workload now runs at 59.91–59.93 FPS with **64 locusts per VR, 16 VRs, and 64 enabled 128×128 cameras**, while preserving complete population and pose recordings. This is a local working tree build based on main `5fbfc05`, not a published release. The [structured report](population-performance-20261106-results.json) retains source hashes, build identity, measurements, recording completion and validation results.

## Cause and change

Aggregate CPU occupancy hid a serial frame thread bottleneck. The original workload used roughly 6.23 CPU cores across 32 logical threads, but Unity's main thread work took 22.26 ms, exceeding the 16.67 ms frame budget. Population capture alone consumed 10.63 ms because it encoded each animal's animation state as JSON on that thread; CSV/gzip writing was already asynchronous. Disabling only population logging restored approximately 60 FPS with unchanged geometry.

Population loggers now capture independent plain data snapshots into rented frame slots. Existing workers for each population serialize those snapshots directly into CSV; frames return to the pool only after serialization. The common native Animator shape uses reusable numeric buffers and writes the same JSON without allocating serializers or JSON strings for each animal. A schema guard and shape checks retain the standard serializer for transitions, bind poses, parameters, animators below the root, legacy animation, procedural Bogong state and nonfinite numbers. All current nested state arrays are copied; no Unity objects or mutable capture buffers reach workers. The original string API and CSV/replay schema remain available.

The first asynchronous candidate still averaged 57.3 FPS: worker allocations caused frequent global garbage collections. Removing the allocations from this common path reduced collections from 122–126 to 25–26 per 15 seconds and brought main thread work below the frame budget.

## Matched workstation results

Ubuntu 22.04.5 / Linux 6.8, Intel i9-13900K, RTX 4070, NVIDIA driver 580.178.04, Unity 6000.3.16f1, Vulkan. NOVA is currently the only connected 1920×1080 at 60 Hz display. Both builds use the same display 0 system configuration, 10 cm balls, original models, four RBLF faces per VR, and complete recording. The two columns occupy 1024×1024 pixels: VR1–8 left, VR9–16 right, eight rows. Measurement intervals exclude startup and include live external HejjeTrac on VR9–16; VR1–8 have no connected input.

| Workload | FPS | Main-thread mean | Population capture mean | Frame interval p99 |
|---|---:|---:|---:|---:|
| Original code, 16×64 locusts |44.55|22.26 ms|10.63 ms|32.31 ms|
| Optimized, 16×64 locusts |59.93|12.30 ms|0.61 ms|19.16 ms|
| Optimized repeat, 16×64 locusts |59.91|12.45 ms|0.62 ms|19.24 ms|
| Optimized Bogong moths, 16×64 |59.87|6.52 ms|0.54 ms|see report|
| Optimized Kannadi scene, no agents, 64 cameras |59.94|2.96 ms|0.09 ms|see report|

The four-VR case with four agents per VR, eight VRs with 16 agents each, and both eight-VR columns with 64 agents each all reached 59.94–60.02 FPS. Every case verified the configured camera count, display and pixel dimensions. The final 16×64 recording drained all 16 population writers, wrote 3,309,568 population rows, and finalized with zero dropped frames or writer failures. Production replay readers loaded 16 rig tracks and a representative population with 206,848 rows across 3,232 frames population without warnings. This establishes archive readability and fidelity checks; it does not claim an additional full interactive ReplayScene round trip for this workload.

All 8 connected tracks stayed fresh. In the two software capped measured intervals, consumed packet receive age at the recorded pose clock had median 7.11–7.24 ms and p99 14.08–14.54 ms, with no backward receive sequences. Five of 14,368 sampled poses reused the previous fresh input. This age excludes camera exposure and physical panel delay. Earlier isolated native PUB measurements were approximately 101 Hz with no missing/duplicate/backward source sequences; this fix does not alter tracking transport.

VSync 1 averaged 60.04 FPS but had 33.74 ms p99 frame intervals in this setup, versus 19.16–19.24 ms with the software cap. The local launchers therefore retain target 60 / VSync 0. Timing includes occasional OS/GC outliers; no claim of perfectly uniform 16.67 ms scanout is made.

## Model, distance and mirror comparison

The imported locust is already 310 triangles, 447 vertices, 15 bones and one material. Its original looping animation and mesh were retained. Cutting far clip from 1,000 cm to 500 or 300 cm produced 44.61 and 45.45 FPS before the fix, essentially unchanged from the original 45 FPS workload: the tested population was already inside that range. Reducing polygon count or view distance did not address the measured recording cost.

Bogong uses one combined population renderer per rig, explaining its lower rendering/submission cost. Native locusts and mirror clones both instantiate individual original Animator/SkinnedMeshRenderer objects; mirror mode does not automatically share one animated instance. The earlier `mirror3` fixture had 37 animals per VR (592 total), versus 64 per VR (1,024 total), and copied tracked motion instead of generating independent movement. Its result was not an equal-count comparison. Mirror cameras can also see peer populations while Swarm cameras isolate their own population.

Linux OpenGL's blank GPU timing is an unsupported counter, not evidence of CPU rendering. The final player used Vulkan on the RTX 4070, confirmed in the player log and NVIDIA process list. GPU mean time with VSync off was approximately 8.48 ms.

## Verification and local use

- `PopulationSnapshotValidation.Run`: 443 checks, including standard JSON and CSV parity, numeric edge cases, real native Animator/transitions, procedural state, blocked-worker ownership and pool reuse.
- `ReplayAnimationValidation.Run`: 371 checks covering original deformed geometry, forward/backward/paused replay, imported animated GLB and immutable source evidence.
- Existing production archive/writer regressions: 694 checks and 150 asynchronous writer lifecycle cycles.
- Linux player build succeeded with 0 errors; physical desktop capture verified the two-column/eight-row layout. Camera calibration, photodiode scanout timing, hour-long experiments and other computers were not measured here.

Artifacts and isolated 4/8/16 VR launchers are in `/home/flyvr01/Documents/ChatGPT/MatrexPerformance-20261105`. `Start-16-VRs.sh` opens setup with the optimized player and 64 original locusts per VR; left/right 8 and live right 4 launchers are alongside it. Each profile has its own editable `Config` and `RunData`. External trackers, calibration and earlier recordings remain independent.

Monitor numbering changed during the session: Samsung was disconnected and NOVA moved from Unity display 1 to display 0. The isolated profiles now select display 0. Verify NOVA routing if another monitor is reconnected. The rejected display 1 run rendered no active stimulus cameras and is excluded from performance claims.

To reproduce the measurement with a built player and explicit isolated directories:

```sh
"/path/to/MATREX VR.x86_64" -force-vulkan \
  --config-dir /path/to/isolated/Config --data-dir /path/to/isolated/RunData \
  --performance-probe /path/to/results \
  --performance-system /path/to/16-VR-system.json \
  --performance-scene Kannadi --performance-experiment /path/to/64-locusts.json \
  --performance-phases all-60,all-60-repeat --performance-seconds 15 --performance-cap 60
```

The probe is opt-in and never installed during ordinary runs. The system JSON owns real tracker endpoints, display order, rows/columns, panel dimensions and target display; the probe rejects invalid camera counts rather than reporting a blank render as a successful benchmark.


## GUI build and BLFR display follow-up (2026-11-06)

The local GUI build is `0.4.2-gui-led-final`, build GUID `976c027f031c4427a37c12b5321cb3b8`, using Unity6000.3.16f1. Origin/main was fetched again and remains5fbfc05; the new optimization/display changes remain local working-tree changes. Detailed evidence is in [population-gui-20261106-results.json](population-gui-20261106-results.json).

- All16 rigs use BLFR,128×128 pixels per face,10cm balls. VR1–8 occupy columns0–3 and rows0–7. VR9–16 occupy columns6–9 and rows0–7, beginning at pixel768 as confirmed by the operator.
- A persistent empty black camera clears every active output before the partial stimulus views. The older startup/HUD clear painted only one frame, and the overview's background disappeared with that optional widget. The new clear does not render scene geometry and stays active with the HUD hidden or overview disabled.
- The prior graphical fixture supplied its own background and masked this defect. The revised visible-Editor test uses production clearing, passes280 assertions, protects panel pixels, and checks all unused pixels are black across five presented frames for the actual16-rig layout. The final native player separately passed five consecutive black gap/lower-strip captures.
- The GUI defaults to Vulkan on Linux, opens the selected16-rig workspace, and keeps both status and performance overlays visible in free pixels. The compact table fits all16 inputs; mirror clone counts and disabled-logger capture timings are handled. A no-argument executable launch was inspected on the same GUI-default setup. The final automated player uses only control-directory/log-file arguments, with no graphics/config/data override.
- On the operator's final HDMI1920×1080 plus4K extended desktop, the35-second passive render/record sample achieved59.942FPS,23.19% total Unity CPU across32 logical CPUs,10.91ms mean main-thread span and2.35ms GPU time. All16 population writers finalized with zero dropped frames.

**Live-camera qualification:** Hejjetrac was not publishing during that35-second sample: every input port had zero packets. This proves the specified1024-animal render/record workload, not the complete live-camera apparatus. Earlier tracked/control-polled samples had substantial stalls, so sustained60FPS with tracking still needs a matched, uninterrupted retest. Display hot-plug changes invalidated an earlier sample and caused a native Unity resize crash (`Desktop0×0`, signal5); another tracked sample still stalled on fixed display geometry. Do not attribute all stalls to display changes or control polling without a matched comparison. CPU affinity includes0–31 and cgroup counters reported no throttling.

The executable is `/home/flyvr01/Documents/ChatGPT/Matrex VR/Build/MATREX VR.x86_64`. Its configuration is in `Build/Config`, and recordings are in `Build/MATREX VR RunData`. The previous complete player and recordings are retained in `Previous Builds/gui-led-fixed`. Original user calibration JSON remains preserved; only the saved GUI workspace selection points at the new profile.


## Final configured GUI build, without live tracking

The delivered local build is `0.4.2-gui-final` (GUID `6b0b39fed82a4957b3050c9254fbd5d1`), at `/home/flyvr01/Documents/ChatGPT/Matrex VR/Build/MATREX VR.x86_64`. The operator explicitly requested proceeding without Hejjetrac after moving it to another computer. The earlier live-camera retest is therefore outside the final requested scope. [Final evidence](population-gui-final-20261106-results.json) records the creating build, configuration, launch arguments, frame checks and recording outcome.

The final25-second render/record sample measured59.999FPS and23.27% Unity CPU normalized across32 logical processors, with mean main-thread span10.85ms and GPU time5.35ms. The largest reported frame peak in the sampled windows was39.40ms; do not describe the software as hard-real-time. All16 population writers finalized with zero dropped frames. Tracking input was absent intentionally.

BLFR is applied to all16 rigs. VR1–8 occupy columns0–3;VR9–16 occupy columns6–9(pixel768), with8rows per group and128×128 pixels per face. Ball diameter is10cm. A persistent empty-camera pass clears every active output before the partial stimulus views.

Software-capped, unsynchronized X11 captures exposed intermittent black bands across both scene and readout. The configured60Hz HDMI profile now uses VSync1. All five final captured frames had zero nonblack pixels in the gap/lower blank strip, a visible FPS heading, and all32768 tested panel-top pixels rendered. The performance widget uses an explicit padded text rectangle; all16 tracker rows and recording counters remain visible beside the panels.

Configured builds now keep their last GUI selection in `Build/Config/Desktop/workspace.json`, gated by the explicit configured-build marker. Ordinary unmarked installations retain their existing shared user-profile behavior. This prevents another installed build's saved experiment from redirecting this build. The global selection deliberately still pointed at the stock entrainment experiment during validation, while the configured player opened its own64-locust profile. Seven focused Unity path/selection assertions passed. The launcher now invokes the same executable without graphics/config/data overrides.

Previous complete builds and recordings are retained under `Previous Builds/`. Stopping long test recordings can take minutes to inspect and archive them: the earlier583-second generated run finalized35,567,616 population rows with no dropped frames; its data and ZIP were retained before replacement. No operator calibration files were discarded.
