# Recorded camera media and linked trajectories

`media_manifest.json` describes optional camera footage inside a saved run. It does not replace pose CSVs or `config_manifest.json`. A recording can have complete pose data and pending camera media independently.

The opt-in `tracker.saveVideo` flag defaults to false. Bundled Rekkefly and HejjéTrac use intra-frame MJPEG AVI when requested. Recording options change atomically in the same native calibration file; calibration values and operator edits remain shared when recording is switched on or off. A running tracker using another recording option must be closed explicitly before the option changes. Tracking processes remain independent of Unity and archival never stops them.

Rekkefly saves segmented video and `frames.jsonl`, with zero-based clip-local frame, upstream source sequence and host-receive Unix nanoseconds. HejjéTrac saves epoch clips plus all-frames, quality and configuration logs. Its native source clock is platform-specific; it must not be reinterpreted as UTC. Original files and sidecars are preserved and hashed when imported.

## Manifest and timing

Schema version 1 has `timeline.clock = recorded-wallclock-ticks`, explicit decimal-string `originTicks`, and `ticksPerSecond = 10000000`. This is the same timezone-naive recorded wall-clock domain used by the pose replay. The reader refuses a different origin.

Each clip records `id`, `rigId`, run-relative `path`, `container`, `codec`, `originalCodec`, `frameCount`, `nominalFps`, `frameMetadataBasis`, `byteLength`, `sha256`, `complete`, `state`, tracker provenance and `sync`. An optional frame index has its own SHA-256. Every normalized index row has `replaySeconds`, zero-based **clip-local** `mediaFrame`, encoded `mediaSeconds`, `poseWallTicks`, `unityFrame`, source frame/timestamp/clock and receiver/session/epoch identifiers. Long source IDs and timestamps stay decimal strings.

The synchronization modes are:

- `consumed-frame-id`: a recorded pose consumed an upstream tracker input frame that was matched unambiguously to the saved video index. This is a software input-frame association, not camera exposure or physical display synchronization. The reader returns the preceding associated frame and its age, refuses extrapolation outside coverage, and rejects conflicting equal-time anchors or backward frame/time progression.
- `operator-offset`: an explicit approximate alignment, `replaySeconds = encodedMediaSeconds + manualOffsetSeconds`. Frame count/rate must come from the clip or a prepared native decoder; no default FPS is invented. Changing the offset edits metadata without copying or changing raw footage.
- `unavailable`: footage is preserved for independent browsing; no automatic time alignment is claimed.

New pose CSVs append `poseWallTicks`, `trackerAppliedThisFrame`, upstream input identifiers/clock, receiver identity/epoch and receive sequence/time. The metadata snapshot comes from `ClosedLoop.LastConsumedSample`, rather than the latest unused network packet. `poseWallTicks` and `Current Time` use the same captured `DateTime`. Legacy JSON and FT messages without source metadata remain supported, with unknown synchronization.

## Import and finalization

`MediaArchive.ImportAsync` requires an explicitly closed source folder. `.partial` outputs, symbolic links, changing files and overlapping source/destination folders are rejected. Copying and hashing happen off the Unity thread; originals remain. AVI association uses the validated native frame index. An unsupported video codec remains archivable for platform playback, without an invented exact join.

`CompanionMediaArchiveSession` accepts playable rows from the shared `RecordingSummary` scanner. No-video sessions finalize immediately. An independently running recorder stays pending until a matching app-owned process closes cleanly with exit code zero. Forced shutdown or loss of the owner produces no false close acknowledgment. `CollectPendingAsync` can later reconcile retained source identities or trusted close markers without reusing the cached initial finalization result, restarting a tracker or recopying already-collected media. An old source without verifiable acknowledgment can instead be imported explicitly as a closed folder.

Collecting footage or changing alignment updates the run folder. An existing ZIP remains its older snapshot and is not silently overwritten; receipts warn about this. Camera metadata changes also invalidate a finalized recording summary's freshness inventory. Historical build identity and lifecycle must remain attributed to the original run when exporting an updated archive.

## Independent recorded dashboard

`RecordedDashboardLink` uses a dedicated loopback viewer on port 8061 and a nonce/version handshake. It sends the Unity replay clock and receives bounded seek/play/pause requests. It does not use the acquisition telemetry topic or change the live viewer on 8060. Late responses from a closed/replaced session cannot queue commands or change current readiness.

Daari Deepa reuses its compiled native table and trajectory renderer, preserving analysis-local time and segment grouping. The linked view adds global recorded-time offsets, faint full-path context, a bright past path and current heading arrows. It uses retained analysis points with a drawing budget, rather than claiming every raw log row or camera frame is displayed. Its native packaged HTTP and browser integration are verified separately from Unity rendering.

Offline regression entry points: `tools/test_replay_media.py`, `tools/test_tracker_contracts.py` and `tools/test_recorded_dashboard_link.py`. These test production parsers, file integrity, clock/identity policy and controlled process boundaries; they do not establish hardware synchronization accuracy.
