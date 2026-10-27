# Experiment-scoped native recording: evening integration audit

Run-scoped recording is **not integrated into MATREX Start/Stop yet**. The native protocols can create separate takes, but a completed command must not be confused with a finalized camera file. No published release, bundled tracker pin, upstream working tree, or user recording was changed by this audit.

## Published versions checked

- Rekkefly latest release remains `v0.6.1`; the published main tip is `f9a9ba71619b86becf0fb1ce1fc2cf5aa3d851bb`. Tests use the existing, provenance-verified local camera correction built from that source, not unpublished upstream changes.
- HejjéTrac now publishes `v0.3.2`. A new, clean source checkout at `6c0bea66d77d55eab5b3669d90ca3c0dae9a2dd7` was inspected without modification. Its official Apple Silicon archive was downloaded separately and verified against GitHub's SHA-256: `26072da06436829cc5cccce0bbad921b9f47f22eb08eaeb370e59cd36616c832`. The extracted CLI reports `0.3.2`; MATREX's bundled `v0.1.4` pin was not silently replaced.

## Actual native tests

**HejjéTrac native desktop:** launched with an explicit prerecorded sample configuration and `--control-stdio`. The discovered transport is `native-gui-jsonl`, protocol version 1. The desktop's real worker acknowledged a paused, nonrecording preflight, a 45-frame take, a five-frame nonrecorded gap, a 40-frame take, and another five-frame gap. Both `record_start` responses were only `queued:true`; status was polled until recording actually started. Both stops were polled until `recording:false` **and the matching directory's `recording_finalizations` entry was `complete`**, without numeric/media/finalization errors. Encoders confirmed 45 and 40 frames, finalized containers, and source ranges 0–44 and 50–89. All 60 protocol assertions passed and the test desktop closed normally. This exercises the native GUI's control protocol, not pointer interaction with its Record button.

**Rekkefly MCP:** an independent MCP-owned generated-input rig recorded two 55-frame takes, with tracking advancing in nonrecorded gaps and distinct returned recording directories. Thirty assertions passed. Files were checked only after `stop_rig` joined the recorder workers and the MCP process exited normally. This establishes repeated-take capability in a headless engine, not control of a separately launched desktop window.

Nine real finalized clips (24,140 encoded frames in total) also passed 207 production MATREX archive-import, indexed JPEG decoding and forward/reverse seek assertions. Source-file hashes remained unchanged. Their timeline uses an explicitly approximate operator offset; no consumed-input or exposure synchronization is invented.

Raw request/response traces, native status receipts, test scripts, media and hashes remain under `.local/evening-recording/`. No physical camera, animal, exposure synchronization, or automatic MATREX run association was tested.

## Concrete integration blockers

1. MATREX currently launches detached trackers through Launch Services on macOS (or detached native processes elsewhere). Their stdin is not a retained command channel, and another MCP process cannot attach to an existing Rekkefly GUI engine. Rekkefly's published `mcp` command owns separate headless rigs; it has no native-desktop control transport corresponding to HejjéTrac's `--control-stdio`. Starting a second tracker would duplicate acquisition and would not respect the operator's existing GUI recording control.
2. Rekkefly's published recorder status lacks a per-take finalization receipt. `Recorder::request_stop` clears `active` **before** the writer has necessarily drained/finalized; the MCP `record` tool's fixed 150 ms wait is not a file-closure acknowledgement. `active:false`, stable file size, or a guessed delay cannot safely set MATREX's `SourceClosed` flag. An immutable take ID/directory, terminal finalization state, accepted/written counts and encoder errors need to be observable without terminating tracking.
3. HejjéTrac's new desktop bridge provides the relevant finalization information, but MATREX still needs a persistent, reconnectable local broker/control owner to retain its JSONL pipe while the native GUI remains independent across Unity restarts. Implementing only a Unity-owned pipe would lose control on restart and undermine the independent-tracker lifecycle.

The next required upstream input is a **published Rekkefly native-GUI control transport with per-take completion acknowledgement**, or an explicitly reviewed upstream implementation of that transport. If the user's pending tracker changes already contain it, publish those changes first; do not overwrite or reconstruct unpublished work. This audit can feed the scheduled weekend JSON/MCP orchestration task.

## Required integration behavior once the transport exists

Use a shared, version/capability-discovered recorder adapter behind GUI, JSON and future MCP commands. Keep requested mode, acknowledged state, error, tracker identity, run ID and native take identity distinct. Tracking initialization must not implicitly start experiment-scoped recording. Start the run only after required recorders acknowledge the requested state; finalize asynchronously after Stop and archive media only after the matching take's completion acknowledgement. Associate receipts with the immutable MATREX run, not with whichever run happens to be current when finalization finishes.

Keep continuous/manual mode explicit. An unexpected native GUI stop while VR recording is requested must latch a visible manual override; do not restart the recorder automatically while that override is active. Clear it only through an explicit operator action. Timeouts, disconnects, unsupported capabilities and failed encoders must remain visible, without claiming successful recording or changing calibration. Repeated-run, override, reconnect, concurrent-finalization and saving-performance tests must cover both native GUIs before enabling this feature in an experiment build.
