# Replay camera validation — 27 October 2026

The missing feed in the recent human preview runs was an acquisition setting: saved provenance explicitly reports `savedVideoRequested:false`, and those runs contain no media manifest or camera footage. The [read-only run audit](replay-video-diagnosis-20261027.json) records that evidence. Video that was never recorded cannot be reconstructed from tracking coordinates.

Current development enables recording for supported bundled trackers by default, including omitted `tracker` objects, while preserving explicit false. The UI, runtime/schema default, recorder preparation and retained media-source ownership now agree. Switching an already open tracker requires an explicit normal close/restart and keeps its canonical calibration. Replay explains disabled, pending and external footage states and follows the selected animal and clip segment rather than displaying another rig's camera.

Native headless HejjéTrac also exposed an import gap: its `raw.avi` + `all-frames.dat` layout differs from the desktop `epoch-N-media/raw.avi` + `epoch-N-all-frames.dat` layout. Both now use verified frame counts, source counters and timestamps; no ordinal alignment is guessed.

| Actual Mac software exercise | Acquisition → replay checks | Real media-panel GPU checks | Forward / reverse camera frames |
|---|---:|---:|---|
| Rekkefly generated demo; 1,800 saved native frames | 999 | 41 | 83 → 334 → 560 → 334 → 83 |
| HejjéTrac bundled prerecorded sample; 89 saved native frames | 1,002 | 41 | 2 → 40 → 75 → 40 → 2 |

Both exercises acquired actual native input through the production Unity listener and recorded two scenes. Completed native video was imported into a diagnostic copy, linked to the consumed input-frame IDs in the saved pose rows, and opened in the actual `ReplayScene`. The asynchronous `ReplayMediaPanel` followed the paused shared replay clock. Every GPU-rendered visible panel texture matched the requested native frame exactly by PNG SHA256, including repeated reverse seeks. The God's-eye camera and animal front-camera preview stayed active simultaneously. All source run and native recording files remained byte-identical.

`ReplayPanelVideoValidation.Run` is the guarded reproducible editor harness. Its visible texture gate is the same one used by the real panel's draw function. These checks cover the media panel and its rendered pixels; the batch editor did not publish a full GameView screenshot, so final native-player overlay layout needs its own inspection. [Structured results and local evidence paths](replay-camera-validation-20261027.json).

Inputs were explicitly generated or previously recorded samples. These exercises do not establish physical camera calibration, a new animal measurement, camera exposure synchronization, LED timing or runtime behavior on another operating system.
