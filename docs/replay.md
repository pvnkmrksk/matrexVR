# Replay a recorded experiment

The 0.3 replay path loads evidence from a **saved run folder**: animal poses, saved sequence/system/experiment files, archived sky images and recorded population poses. It opens with the **God's-eye view** so the whole experiment is visible. It does not substitute today's installed config for an absent historical file.

## Open a run

Keep the complete session directory, including `config_manifest.json`, `Configs`, `Skyboxes`, rig CSVs and population CSV/gzip logs. Extract a session ZIP before opening it.

From the setup sidebar, choose **Replay a recorded run**. The setup/session can be saved before entering ReplayScene. You can also launch a player with:

```sh
"./MATREX VR.x86_64" --replay-session "/path/to/RunData/session-folder"
```

On macOS:

```sh
open "/path/to/MATREX VR.app" --args --replay-session "/path/to/session-folder"
```

This loads `ReplayScene`. Use **Browse…** to choose a folder, or paste its path into **run folder** and choose **Open run folder**. The Editor and Windows/macOS use native system folder dialogs; Linux uses zenity or kdialog when installed, with manual path entry as the fallback. In the Unity Editor, dropping a folder into the replay UI is supported. Native operating-system folder drag/drop into a standalone player is not implemented; use Browse, the folder field or launch argument. Source integrations can call `ReplayController.OpenSession(path)`.

## Timeline and views

- **Space** plays/pauses. Scrub the timeline forward or backward.
- **Left/Right** seek one second; hold **Shift** for ten seconds.
- **N/M** jump to the previous/next recorded step marker.
- Choose a playback speed from **0.1× to 4×**.
- The **God's-eye view** is the default. Use fit/zoom and the animal views to inspect movement at different scales.

Animal-view cameras are analysis views; they do not reproduce calibrated LED-panel viewports.

All animal timestamps use a common session origin so independent rigs do not each start at their first row. A backward seek updates animals, recorded populations and applicable saved stimulus context together. Replay controls operate on recorded time; they never restart live tracking or the acquisition sequence.

## What is restored

The saved manifest resolves immutable configuration snapshots and verifies their hashes. Old recordings without a manifest can use the config copies actually present in that session; a fidelity warning identifies the missing provenance. Recorded scene/config/phase fields identify the stimulus context. Archived `Skyboxes/<skyboxId>.png` supplies the actual texture displayed at that point.

Where the named scene is included in the build, replay loads its geometry additively and freezes live acquisition scripts. Saved Choice/Dynamic Choice content and disk-indexed population poses restore supported experiment elements. Large population CSV/gzip recordings are indexed for seeking instead of loading every agent pose into memory at once.

The fidelity panel identifies missing configs, built geometry, sky images, population data and other unavailable evidence. It also distinguishes recordings that lack agent orientation/animation information. A recording cannot establish pixels, random states, wall-clock corrections or stimulus details that were never saved. Replay is an evidence-based reconstruction, not a guarantee of historical pixel equivalence.

Do not fill an old recording with today's similarly named JSON. If a reference has multiple archived versions and the correct version cannot be selected from the saved acquisition context, replay reports the ambiguity rather than choosing silently.

## Historical formats

Versions **0.1/0.2** contain the older rig-trajectory viewer, with inspector fields and the same basic keyboard seek controls. Their replay does not automatically restore every generated sky or population. Keep their exact source/configs when comparing archival behavior.

0.3 Swarm position logs append `RotationX`, `RotationY`, `RotationZ` after the existing `experimentPhase` column. The old nine-column prefix remains, and old recordings remain readable, but headings that were not recorded cannot be recovered as measurements. [Data formats](data-formats.md) documents the schemas and clocks.

The [lifecycle/replay report](lifecycle-replay-validation-results.json) records actual ReplayScene loading, archived sky/geometry/population, scrubbing and overview tests. See [release verification](releases.md#software-verification) for the tested scope.
