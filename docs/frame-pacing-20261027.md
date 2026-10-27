# Mac 60 FPS pacing investigation — 27 October 2026

The Mac has ample rendering throughput, but **stable, evenly paced 60 FPS has not yet been established**. Two supported pacing alternatives were tested rather than enabled blindly. The main user build and real trackers remained open throughout; all experiments used disposable diagnostic apps and generated inputs.

The earlier short benchmark also had a reporting defect: its coroutine denominator extended one frame past the last LateUpdate sample and its first sample was partial. The diagnostic comparator now skips the first partial sample and uses complete interval count divided by the sum of those intervals. This affects reported averages, not the measured existence of20–29ms frame tails. Production `fps.cs` has correct consecutive-interval math after startup; separate frame-start/startup refinement preserves real peaks rather than snapping the label to 60.

The machine is an Apple M2 Pro on macOS 27.0.1, Unity 6000.3.16f1, built-in 120 Hz display. Each phase below records 10 seconds of complete wall-clock LateUpdate intervals; MATREX's second pass records 5 seconds. The cube comparator contains one camera, one cube/light, and no MATREX runtime scripts or tracker processes.

| Diagnostic condition | FPS | p95 frame ms | Maximum ms |
| --- | ---: | ---: | ---: |
| Minimal cube, legacy software 60 |59.901|16.943|17.916|
| Minimal cube, legacy VSync/2 |59.576|17.671|25.405|
| Minimal cube, MetalDisplayLink software 60 |57.238|17.799|17.938|
| Minimal cube, MetalDisplayLink VSync/2 |57.342|17.768|18.039|
| Terrain 4 rigs + native recording, legacy software 60 (first 10 s) |58.872|18.202|28.979|
| Terrain 4 rigs + native recording, legacy VSync/2 (first 10 s) |59.366|17.685|26.884|
| Terrain 4 rigs + native recording, MetalDisplayLink VSync/2 (5 s) |57.541|18.358|19.497|

MetalDisplayLink VSync/1 reaches 119.854 FPS in the minimal cube test, as expected for this 120 Hz screen; it is not a 60 FPS setting. The runtime reports the actual 120.0006 Hz refresh ratio. Neither VSync/2 nor the documented MetalDisplayLink option fixes the 60 Hz cadence in these tests. The~57.3 FPS MetalDisplayLink result reproduces without MATREX code, so that effect belongs to the engine/platform presentation path. The wider legacy software tails in MATREX plus four native encoders still need controlled capped profiling to assign their cause; they should not be attributed to terrain complexity or to recording without that comparison.

The first MetalDisplayLink launch stalled before managed startup. Its retained two-second stack sample shows AppKit event-loop/QuartzCore activity, not a MATREX C# lock. Only that diagnostic process was terminated; generated recorders finalized normally. A subsequent launch started with an initial windowed CLI option, then `DesktopDisplay` restored native fullscreen. Consequently the attempted 1920 × 1080 window run is explicitly **not** a successful FullHD window comparison.

Unity documents that [software targetFPS can microstutter](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html), and that [VSync uses native refresh/divisor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/QualitySettings-vSyncCount.html). Its [6000.3.11 release notes](https://unity.com/releases/editor/whats-new/6000.3.11f1) confirm that CAMetalDisplayLink and its environment opt-in are supported in this installed version. Those capabilities are not proof that enabling them solves this application's60Hz requirement.

The next useful test retains cached frame-start timing, wall intervals and available present counters, then compares identical capped 60 terrain runs while disabling native encoding, pose logging, input listeners, telemetry and overlays independently. GC/profiler events should accompany each outlier. A true 60 Hz output should then be measured directly; desktop engine FPS cannot certify physical LED/exposure timing. Existing explicit configuration must continue to win over any future automatic pacing policy.

[Machine-readable report and exact artifact hashes](frame-pacing-20261027-results.json). Raw intervals, app sources, logs and native counters remain under `.local/frame-pacing-20261027/`. No product timing-policy change or engine upgrade was made by this investigation.
