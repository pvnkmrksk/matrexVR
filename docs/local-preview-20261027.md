# Local Mac preview — 27 October 2026

The active branch is `codex/seamless-desktop-experience`. This is a local development build with local commits; nothing has been pushed, released, or built for Windows/Linux in this pass. See the [follow-up review](local-preview-review-20261027.md) for the latest design, camera-routing, video and performance results.

## Open and test

Open `.local/Open MATREX Preview.command` to use the isolated `.local/Preview Config` profile, or open `.local/MATREX VR.app` with your existing profile. The launcher does not change existing calibration or configuration files. The app opens fullscreen at the display’s native resolution.

On Start, **Initialize tracking** opens the independent tracking application before the experiment begins. Inspect the source and calibration there; **Start demo / Start experiment** starts the actual sequence and run recording separately. **F1** shows keyboard shortcuts. **Tab / Shift+Tab** navigates controls; Escape closes a dialog or returns to Start before quitting.

**Load sequence**, beside **Edit sequence**, offers six prepared sequences. Loading retains the apparatus and other drafts and opens the blocks for review.

| Preset | What it runs |
| --- | --- |
| Terrain · matched relief | RoughSmooth, SmoothRough, SmoothSmooth, RoughRough, with matching visual/navigation surfaces |
| Terrain · visible relief, flat navigation | The same four patterns, navigating a flat surface |
| Terrain · flat appearance, relief navigation | The same four patterns, navigating their hidden relief |
| Terrain · matched flat | Four flat negative-control presentations |
| Swarm · entrainment then social coupling | 120 seconds ordered swarm, then 300 seconds Kannadi |
| Social coupling · control | The same 300-second Kannadi without entrainment |

The terrain presets share `Examples/Rugosity/terrain-treatment.json`: tracking is on, autopilot is off. The four distinct `TerrainChoice_*` scenes share `Assets/TerrainCueLab/TerrainChoiceWorld.prefab`; each treatment sequence chooses the visible and navigation surface independently. Two identical target trees sit at equal distances on the left and right. The older `TerrainCueLab` remains compatible. The forest-floor albedo was recovered from the original packed Blender image; the flat appearance has no normal-map relief. The both-flat counterbalanced blocks deliberately have identical geometry.

Both social sequences share `Examples/Kannadi/social-coupling.example.json`. Change **numberOfRings** in that one file: 0 means a center representation with no extra copies; 1 adds six copies per tracked animal. Mutual interaction requires at least two independently tracked rigs. Both stages independently sample cardinal headings and reset their presentation at the transition. See [social experiment details](social-swarm-experiment.md).

The existing generated/recorded `Swarm` runtime is reused. It is not a second legacy presentation to maintain. Existing scene assets and historical configurations remain available for compatibility.

## JSON and recordings

Forms show declared effective defaults immediately, and external JSON edits reload when the draft is clean. Dirty edits retain conflict protection. **More → Effective JSON & defaults** previews/copies resolved settings or explicitly materializes them into the draft. Context-dependent display inheritance remains omitted until explicitly chosen. Malformed files remain intact for repair; Undo/Redo retains their source and diagnostic state.

New runs default to `.local/MATREX VR RunData` for this app. A protected installation uses a stable per-installation application-support directory. **Replay recordings** shows the exact active root, newest recordings, saved files, duration, rigs, and evidence. Browse opens older/shared/external folders; existing data is not moved. `--data-dir` remains an explicit override.

Replay opens overhead. Right-drag or I/J/K/L orbits, middle-drag pans, the wheel zooms, WASD/QE translates, and Shift moves faster. Fit restores framing. The Recording views panel shows the saved animal view alongside optional tracker footage; −/+ resizes the previews without stretching them. Trajectory plots follows the replay clock in the independent dashboard. Space pauses; arrows seek; N/M changes condition. Scrubbing moves supported saved evidence together.

Native trackers save independently. Close a tracker normally to finish its video, then use **Collect media** in replay. Pending sources are shown as pending. Software frame association is not a measurement of camera exposure or LED timing. See [replay details](replay.md).

## First preview verification

Actual Unity checks on a guarded disposable project passed: 171 UI/render/preset checks at Full HD, 4K and 2560×1440; 425 configuration/keyboard/tracking-preflight checks; 252 terrain checks across sixteen combinations; 1,075 four-rig swarm/social checks; 42 fresh native-video import/decode/backward-seek checks; and a 999-check two-scene record/replay comparison. The maximum mean absolute RGB error of the paired 256px replay images was 0.0001791 on a 0–1 scale (about 0.018%).

Native generated demos wrote all 180 and 4,200 accepted video frames without recording errors. Media import preserved original source hashes. Additional offline checks cover defaults, exact JSON/history/conflicts, installation paths, AVI boundaries, recording finalization, replay pointers, and companion staging. These are software/simulated-input tests, not physical animal, calibrated camera, LED timing, Windows or Linux validation.

Local test logs, JSON reports and rendered screenshots are under `.local/acceptance-20261027`. The build report is `.local/build-report.json`. The guided tutorial and other-OS builds remain deferred until this local preview is reviewed.

The first preview’s signed Mac player was opened and captured at native 3024×1898, 2× backing scale, using the isolated preview profile and per-installation recording root. The build succeeded with zero errors and 56 warnings. The final 425-check interaction validation passed again after the JSON history fixes. Native coordinate input through the computer-control tool failed with `noWindowsAvailable`; native menu access and framebuffer capture worked. Final physical mouse/replay-gesture acceptance is therefore left to the operator rather than claimed as completed.

The requested next-day tour follow-up was not scheduled: the automation service rejected the future schedules with “no future runs.” No automation was created. The tutorial remains deferred pending preview feedback.
