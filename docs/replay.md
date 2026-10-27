# Replay a recorded experiment

The saved-run replay path loads evidence from a **saved run folder**: animal poses, saved sequence/system/experiment files, archived sky images and recorded population poses. It opens with the **God's-eye view** so the whole experiment is visible. It does not substitute today's installed config for an absent historical file.

## Open a run

Keep the complete session directory, including `config_manifest.json`, `Configs`, `Skyboxes`, rig CSVs, population CSV/gzip logs and any `media` directory. Extract a session ZIP before opening it. The folder is the portable unit; copying only a CSV loses the saved environment and optional video evidence.

The **0.4 release target** adds the recording library, tracker video controls and linked recorded trajectories described below; [release publication and limits](releases.md#04--terrain-recording-and-replay) identify the actual available packages. These additions are not part of the frozen 0.3.1 download, whose existing replay still opens a saved folder with Browse, a path or the launch argument.

In 0.4, choose **Replay recordings** from the setup sidebar. The library automatically lists recordings under this installation's RunData directory, newest first. By default this is `MATREX VR RunData` beside the app or executable. Protected installation directories use a stable per-installation folder in application support; the exact path is always shown above the list. `--data-dir` overrides it. Earlier builds' shared application-support recordings remain available through Browse. It shows the recorded span, observed rigs, animal metadata, tracker backend, sequence cycles and per-rig condition presentations. These are derived from saved rows; planned steps are not counted as completed trials. Setup-only folders and duplicate ZIP snapshots are not presented as experiment recordings.

Select a recording to inspect **Recorded files** and **Saved evidence & limitations**, then choose **Replay selected**. This saves the current setup draft before opening the saved run. A finishing recording remains unavailable until its data writers have closed; other recordings can still be opened. Use **Refresh** to rescan or search by name, animal, backend or recorded scene. **Browse…** and **Open an external folder path → Inspect folder** open a recording on another drive or computer without moving it into the local library.

You can also launch a player with:

```sh
"./MATREX VR.x86_64" --replay-session "/path/to/RunData/session-folder"
```

On macOS:

```sh
open "/path/to/MATREX VR.app" --args --replay-session "/path/to/session-folder"
```

This loads `ReplayScene`. Choose **Open run…**, then **Browse…** or paste the path into the run-folder field and choose **Open run folder**. The Editor and Windows/macOS use native system folder dialogs; Linux uses zenity or kdialog when installed, with manual path entry as the fallback. In the Unity Editor, dropping a folder into the replay UI is supported. Native operating-system folder drag/drop into a standalone player is not implemented; use Browse, the folder field or launch argument. Source integrations can call `ReplayController.OpenSession(path)`.

## Timeline and views

- **Space** plays/pauses. Scrub the timeline forward or backward.
- **Left/Right** seek one second; hold **Shift** for ten seconds.
- **N/M** jump to the previous/next recorded step marker.
- Choose a playback speed from **0.1× to 4×**.
- The **God's-eye view** is the default. **Fit** frames the recorded trajectory and returns to the overview.
- In 0.4, right-drag or **I/J/K/L** turns the overview; middle-drag pans; the wheel zooms. **W/A/S/D** moves horizontally and **Q/E** moves down/up; hold **Shift** to move faster. **Perspective / Top-down view** changes the overview projection. These controls are inactive while editing a path or media form.
- Click **God's-eye view / Animal VR…** to cycle the main view. The **Cameras** panel can show the saved front-camera reconstruction beside the tracker video while the main image stays overhead. **Hide** collapses that panel; **− / +** resize both previews together while keeping their original aspect ratios and the transport clear. **Media tools…** reveals import, collection, refresh and offset controls.

In the God’s-eye view, each rig has a stable color, visible marker and heading arrow. The bright tail shows its last five recorded seconds; the faint path belongs to the current recorded condition. A wire cone indicates the front-camera field of view. VR labels and edge arrows help locate rigs outside the current view. These are analysis annotations, excluded from the saved animal-camera reconstruction. Adaptive clipping improves close inspection; **Fit** restores the full track view.

Where saved camera data is available, the animal reconstruction preserves its projection and panel aspect with letterboxing. This is an analysis view of saved evidence; it does not establish physical LED scanout or pixel-for-pixel historical output.

All animal timestamps use a common session origin so independent rigs do not each start at their first row. A backward seek updates animals, recorded populations and applicable saved stimulus context together. Replay controls operate on recorded time; they never restart live tracking or the acquisition sequence.

## Record and replay tracker video

These controls belong to the 0.4 release target. Video is optional: pose recording and replay work without it. **Acknowledged per-experiment recording Start/Stop, manual override and recorder-state reconciliation are not implemented.** One independent tracker recording can span preflight and multiple experiment runs; stopping a VR experiment does not stop that recorder.

1. Before starting, review **Save tracker video** for the relevant rig. 0.4 enables it by default for supported bundled Rekkefly/HejjéTrac launches; explicit `tracker.saveVideo:false` remains an opt-out. This requests native MJPEG AVI plus frame/configuration logs. A tracker already running with another recording option must be closed explicitly before changing it; calibration stays in its shared native configuration. The legacy Kinefly/FicTrac backend names can also select those bundled successors. An independently running external legacy tracker supplies camera footage only if that application actually records it; its tracking transport alone contains no camera pixels.
2. Use **Initialize tracking** for input/calibration preflight, then start the experiment or labeled demo. Preflight creates no experiment pose recording, but a tracker launched with video enabled may already record. The tracker remains an independent application. For real experiments, inspect its camera, calibration and tracking quality before presenting stimuli. Demonstration input is simulated or prerecorded, not a new animal measurement.
3. End the experiment with **Back to setup / Esc**, or let its configured sequence finish. Wait for **Finishing recording…** to clear before opening that run. Data writers are drained and the saved context and recording summary are finalized in the background.
4. Close the native tracker normally when its video recording is finished. Unity never stops it as part of archival. If it closes cleanly while Unity is still running, the app can acknowledge the completed recorder. If the tracker is still open when the experiment finalizes, camera media stays **pending** while the poses remain replayable.
5. Open the run and choose **Media tools… → Collect media** to gather completed app-launched footage. **Refresh** reloads media already present in the run. The original tracker recording stays at its source; copied media and sidecars are preserved under the run's `media` directory with integrity hashes.

A linked clip follows the same replay clock as the trajectory, including pause, playback speed and forward/backward scrubbing. **Software consumed-input frame association** means a saved pose used the tracker input frame matched to that video frame; it is not an exposure or display synchronization measurement. Gaps or unavailable coverage remain visible rather than being filled with guessed frames.

The video panel selects footage belonging to the chosen animal and the segment covering the replay time. A rig with no saved footage does not inherit a different animal's camera. The panel explains whether footage was switched off, is still pending, or must be imported from an external tracker. Enabling recording now cannot recover video that an earlier run never saved. The [27 October local-run audit](replay-video-diagnosis-20261027.json) records the concrete missing-video finding in the preview runs.

For another finished tracker recording, choose **Media tools… → Import footage…**, select its folder and confirm **The tracker has finished saving this recording**. Leave the optional offset blank to preserve unknown timing. To align it approximately, enter an explicit offset using `replay time = video time + offset`; a positive offset means the video began later. **Set offset…** changes that alignment metadata without rewriting raw footage. Importing old video does not recover frame associations that were never recorded. Unsupported media may be archived without a playable or synchronized native preview.

Collecting media or changing an offset updates the run folder. A ZIP created earlier remains its original snapshot and is not silently replaced. Transfer the complete updated folder when sharing a run with newly collected footage. See the [recorded-media contract](recorded-media-contract.md) for clocks, manifests, import checks and synchronization limits.

## Linked recorded trajectories

In 0.4, **Trajectory plots** opens the independent Daari Deepa recorded viewer. Unity remains the replay clock: the plots receive its time, speed and paused state, while seek/play/pause requests from the viewer return to Unity. The recorded viewer uses loopback port **8061**; acquisition **Live plots** remains separate on **8060**. Closing a replay session disconnects its controls without terminating the independent viewer.

The linked native viewer is included only in the reviewed **macOS Apple Silicon bundle**, pinned to Daari Deepa `1486b81e`. Older pinned Intel macOS, Windows and Linux dashboard archives lack the required replay API/assets; an explicit capability message is shown instead of spawning repeated copies. Unity’s internal pose/video replay does not depend on that external viewer and remains part of every platform build. An ordinary standalone analysis view can still inspect the saved data independently.

## What is restored

The saved manifest resolves immutable configuration snapshots and verifies their hashes. Old recordings without a manifest can use the config copies actually present in that session; a fidelity warning identifies the missing provenance. Recorded scene/config/phase fields identify the stimulus context. Archived `Skyboxes/<skyboxId>.png` supplies the actual texture displayed at that point.

Where the named scene is included in the build, replay loads its geometry additively and freezes live acquisition scripts. Saved Choice/Dynamic Choice content and disk-indexed population poses restore supported experiment elements. Large population CSV/gzip recordings are indexed for seeking instead of loading every agent pose into memory at once.

The fidelity panel identifies missing configs, built geometry, sky images, population data and other unavailable evidence. It also distinguishes recordings that lack agent orientation/animation information. A recording cannot establish pixels, random states, wall-clock corrections or stimulus details that were never saved. Replay is an evidence-based reconstruction, not a guarantee of historical pixel equivalence.

Do not fill an old recording with today's similarly named JSON. If a reference has multiple archived versions and the correct version cannot be selected from the saved acquisition context, replay reports the ambiguity rather than choosing silently.

## Historical formats

Versions **0.1/0.2** contain the older rig-trajectory viewer, with inspector fields and the same basic keyboard seek controls. Their replay does not automatically restore every generated sky or population. Keep their exact source/configs when comparing archival behavior.

0.3 Swarm position logs append `RotationX`, `RotationY`, `RotationZ` after the existing `experimentPhase` column. The old nine-column prefix remains, and old recordings remain readable, but headings that were not recorded cannot be recovered as measurements. [Data formats](data-formats.md) documents the schemas and clocks.

The [lifecycle/replay report](lifecycle-replay-validation-results.json) records actual ReplayScene loading, archived sky/geometry/population, scrubbing and overview tests. See [release verification](releases.md#software-verification) for the tested scope.

## 0.4 recording/replay verification

The [round-trip validation](replay-roundtrip-validation.md) records two actual scenes and reopens the resulting folder in ReplayScene. It compares the saved frame poses and rendered animal-camera images before/after a dynamic color swap, then seeks backward across the swap and scene boundary. The measured mean RGB error is at most 0.0158% in the six tested 256 px views. Inputs are explicitly synthetic, sent through a real native-format PUB; this does not claim physical camera or LED validation.

Current source starts replay at the first experiment row instead of leaving the playhead in a long initial ControlScene wait. Saved camera projection/panel aspect is preserved where available, with letterboxing rather than stretching; God's-eye markers and paths are excluded from the animal camera. Replay preview lights are suspended while the imported scene supplies its original lighting.

Logged dynamic swap and adaptive black/gray onset decisions are restored without rerunning random choices. Legacy timestamp/scene-prefixed configuration copies are resolved by suffix only when their saved content is unambiguous. Missing or conflicting evidence remains an explicit fidelity warning. New Band records preserve actual visible/hidden state; older phase-only records cannot prove exact blinking.

The [27 October local review](replay-recording-mac-review-20261027.md) records native keyboard/rendering acceptance and current UI checks. The [48 focused resize/header assertions](replay-preview-resize-20261027-results.json) use actual production IMGUI events and Full HD/Retina framebuffers; the [82 camera assertions](replay-camera-review-20261027-results.json) cover markers, clipping, resets and preserved animal-view pixels. Latest standalone pointer/drag acceptance remains incomplete because native automation returned `noWindowsAvailable`. Windows/Linux builds have no native execution walkthrough, and [evenly paced 60 FPS remains unresolved](frame-pacing-20261027.md). These software checks do not establish physical camera/LED timing.
