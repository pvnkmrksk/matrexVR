# Desktop experience

This is the current source/local desktop build. Published 0.1–0.3 archives retain their historical behaviour. The local builder publishes no release.

## Everyday workflow

Open the app, leave **Flying** and **Demo** selected, pick **Night sky** or **Forest**, and press **Start demo**. The bundled native tracker starts independently and supplies clearly labelled demonstration input. Flying uses a generated source. Walking uses HejjéTrac’s attributed short recorded sample; after it finishes, use **More → Rig manager… → Start all** in the same tracker window to replay it. Manual movement remains available.

The start screen puts movement, world and sequence first. Edit sequence blocks and their parameters directly; apparatus setup is remembered separately. Animal metadata is optional while exploring. Hover help explains fields, and advanced/raw JSON editing remains available. The lossless editor document is unchanged by the temporary typed conversion used at the native launch boundary.

Each movement/world/input combination has separate editable files under `Config/QuickStart/`. Switching away and back retains edits. An existing system or sequence file is never overwritten to create a new profile. A normal desktop launch remembers the last prepared workspace in `Config/Desktop/workspace.json`; an explicit `--config-dir` respects that directory’s root selection.

During an experiment, the compact bar shows whether input is fresh, toggles forward motion, opens optional live plots, displays controls and returns to setup. **Tracker input** means a publisher is connected; transport freshness alone cannot prove a physical animal or camera. Verify the source, calibration and quality in the native tracker. Demo recordings are labelled simulated or prerecorded.

## Native display and text

Desktop players start in native-resolution fullscreen on the remembered application display. Select another application monitor in apparatus setup. The application monitor and calibrated animal/LED viewport routing are separate settings. Desktop preview fills its application view; existing calibrated panel rectangles retain their meanings.

Mac builds enable Retina support and size the UI using the Cocoa backing scale. UI Toolkit renders directly into the native framebuffer with bundled Inter SDF fonts and correctly sized Lucide artwork. The selected display’s native resolution supplies the rendering size; a fixed 1920×1080 canvas is not used as the final framebuffer.

The physical validation Mac has a 3024×1964 panel. Unity reports a 3024×1898 usable fullscreen surface, excluding the 66-pixel top/notch area, and a 2× UI backing scale. The actual player diagnostic confirms fullscreen mode, framebuffer size and direct panel rendering. This is one tested Mac, not a claim of measured behaviour on every monitor.

Windowed operation is an advanced source-build choice: compile with `MATREX_ALLOW_WINDOWED` and pass `--windowed`. Ordinary packaged players keep fullscreen enabled. Escape returns to setup; it does not switch to a low-resolution window.

## Independent native applications

Rekkefly and HejjéTrac retain their native windows and editable camera/calibration files. Starting the same rig reuses its exact existing process. A running app with a different backend, source or configuration requires an explicit Stop before switching. Closing or crashing Unity does not close tracker windows. An unrelated tracker process is never stopped just because it uses the same executable.

Selected companions are installed once into a stable versioned location under Unity’s persistent-data directory. Private receipts record PID, process start time and the requested identity; launch locks prevent concurrent duplicate starts. First-install copying and hashing run off the render/UI thread. Complete Mac apps launch through macOS Launch Services with their own application/privacy identity and a verified native PID. Other POSIX executables use detached diagnostic logs. Neither path depends on Unity’s standard-input/output lifetime. Explicit Open/Show tracker brings the tracker forward; experiment Start keeps Unity focused.

See [bundled companions](bundled-companions.md) for exact pinned releases, source-patch provenance, signing order and finite walking-demo behaviour. Native camera input requires a working camera mode and calibrated geometry. A camera failure must not silently turn an integrated camera recording into a sample recording.

## Optional plotting dashboard

Keep high-frequency stimulus rendering and tracking in their native applications. Unity supplies lightweight status; **Live plots** opens the optional Daari Deepa server and browser only when requested. It reuses an existing viewer on port 8060, verifies its identity, and connects it to the current telemetry endpoint/topic. It does not reset an already matching viewer connection. No Python installation is required for the packaged viewer.

The server survives Unity exit. There is no automatic restart loop or repeated extraction on every click. Its stable installation is keyed by the pinned archive digest. Private launch receipts are written atomically; stale or malformed older receipts do not permanently block a new launch. The bundled local server binds to loopback. For another computer, run the viewer/bridge there and configure the experimental telemetry address explicitly; see [live monitoring](manual/live-monitoring.md).

## Local build and evidence

```sh
python3 tools/build_local_desktop.py
```

The Mac helper copies the current workspace to a marked disposable project, builds `.local/MATREX VR.app`, stages native companions and the optional viewer, and seals the parent app after embedding. Calibration and recordings live outside the app. Local signing is ad hoc; it is not Developer ID signing or notarization.

Apple Silicon maintainer builds also use a current stable Rust toolchain to build a pinned, reviewed Rekkefly camera correction. Source is read from an available sibling clone or fetched at its exact locked revision into a disposable directory. The installed application needs no compiler or Python installation. Other platform entries retain their explicit capabilities; an older bundled flight app that cannot enforce camera-only acquisition refuses managed camera mode instead of substituting a sample. See [native camera validation](bundled-native-camera-validation-results.json).

The physical camera test subsequently passed **987 checks** across a fresh two-scene recording and actual ReplayScene, including forward/reverse precise seeking. Camera-source packets were verified before and after the run; the maximum mean absolute rendered-pixel difference was 0.0002461 (about 0.025%). No animal calibration or camera video recording was involved. [Actual player and camera evidence](desktop-player-validation-results.json) records the tested scope.

- [Native UI and Start-path validation](desktop-ui-validation-results.json): layout, Retina scaling, lossless typed launch, duplicate prevention, async failure recovery and retry.
- [Tracker installation and independent lifetime validation](bundled-companion-validation-results.json).
- [Dashboard package checks](bundled-dashboard-validation-results.json): all four desktop platform payloads, hashes and staging/signing order.
- [Recording/replay round trip](replay-roundtrip-validation.md): fresh two-scene recording, timeline seeking and paired rendered-frame comparisons.
- [Actual Mac player and physical-camera round trip](desktop-player-validation-results.json).
- `python3 tools/test_desktop_profile_paths.py`: production initialization preserves partial and complete existing profiles.

The deferred multi-monitor prism/cuboid wizard is a separate future task. This pass does not add a level builder.
