# Experiment-scoped native recording

Automatic recording now controls the ordinary Rekkefly and HejjéTrac GUI workers. Tracking, calibration and the native window remain independent of the experiment; no second tracker engine is started to control recording.

**Start → Save tracker video** is the experiment-wide permission for automatic camera recording. Set `saveTrackerVideo: false` in system JSON or choose **Skip tracker video** to suppress all automatic Start and Stop requests for that run. Omission preserves the existing enabled behavior. Each rig still exposes its own `tracker.saveVideo` checkbox and **Automatic experiment / Manual external** choice. Automatic video is best effort: missing, failed, unverified or incompatible trackers produce per-rig warnings while VR startup continues. Available rigs attempt recording independently, so one failed recorder cannot skip another. Manual mode reports operator control and never automatically starts or stops external recording. Turning off video permits input-only use.

## Lifecycle contract

1. Initialize tracking opens the native window without starting an automatic take.
2. Experiment Start captures a fresh run GUID and logger directory, verifies the owned process/endpoint identity, and requests a fresh native take. A queue reply is insufficient to claim recording: the exact take directory and at least one accepted/submitted raw-video frame must be observable for acknowledgement. Each rig has a two-second preparation budget, in parallel; an already-sent Start may use at most one additional second to settle command delivery safely. VR then starts with warnings for any unavailable recorder.
3. Experiment Stop immediately seals VR writers. It sends matching owned stops for takes that were actually accepted or whose outcome is still ambiguous, waits for their physical completion receipts, then archives closed files and creates the final ZIP. A recorder that never started does not fail VR finalization. The native windows and tracking sources continue independently.
4. Each receipt must prove successful numeric/media/container closure with matching accepted/written or submitted/encoded counts and no pending frames or errors. The capture worker stamps the exact owned Stop request ID. A pause count catches short manual gaps even when live polling misses them.

Startup absence/timeouts stay warnings, with `acknowledged:false`, in `native_recording_start.json` and the per-rig status ledger. They do not prevent recording the VR experiment. Once a native take is sent/started, a manual pause/stop/replaced take, encoder failure, lost endpoint or unresolved closure remains explicit. Automatic restart is disabled for that run. Closed partial artifacts can still be copied safely, while the overall run is marked failed and no complete ZIP is produced. Canceling preparation before any VR pose retains closed native artifacts with hashes and explicit `presented:false`, `playableVrTimeline:false`, unavailable alignment. No VR offset or playable timeline is invented.

A recorder that closes with zero video frames stays `closed-unacknowledged`, with `acknowledged:false`. Its closed config/completion files are retained in `native_unrecorded_takes.json`; it never becomes a playable video clip or a successful video-recording claim. VR pose recording remains separate.

Before any native command, the owned window must also match the current rig: local tracking address, normalized backend, port, explicitly selected executable/calibration/device, requested launch mode, and configuration profile. A mismatch produces an unavailable warning and sends no status/start/stop command to the old window. New receipts retain launch profile and device/mode identity. A legacy receipt with unknown profile identity is reused only when its calibration belongs to the current profile; otherwise reinitialize it explicitly. VR still starts.

Native sources are never modified during archival or replay. Registration binds the captured GUID and logger directory, so a late callback or previous take cannot enter a later run. Settled run/task/cancellation structures are removed between repetitions.

## Endpoint and platform scope

The native apps opt in through `--control-dir ABSOLUTE_PRIVATE_DIRECTORY --control-instance 32_HEX_TOKEN`. `endpoint.json` identifies protocol version 1, instance token, backend, process ID and these required capabilities:

- `native-gui-control`
- `per-take-finalization`
- `owned-take-stop-v1`

Local request/reply files carry matching random IDs, instance identity and expiry. Commands are `status`, `record_start`, ownership-fenced `record_stop`, `start` and `pause`. `record_stop` requires the expected take directory and immutable stop request ID. `pause` affects tracking and is distinct from recorder Stop. Idle status, stable file size or elapsed delay never prove closure.

The reviewed source adapters ship for **Mac arm64**. MATREX VR **0.4.3 Linux x86-64** also has a separately checksummed **HejjéTrac recording adapter** on its release page. The add-on retains the upstream 0.5.0 tracking engine and adds the local mailbox and worker-side ownership check. Extract it and run its included installer against the MATREX `Companions` directory. Existing tracker calibration stays in the same profile. Start the bundled tracker again and set the session and rig to save video with **Automatic · experiment**. [Adapter download and instructions](https://github.com/pvnkmrksk/matrexVR/releases/tag/v0.4.3) · [Linux lifecycle results](hejjetrac-linux-recording-validation.json)

The base Linux archive ships the official upstream tracker. The flight detector and walking numerical core are unchanged. Stock Linux, Intel Mac, and Windows packages report automatic video as unavailable when the verified endpoint is absent. VR can still start. Physical camera start/stop, exposure timing, attached hardware and other-platform runtime acceptance remain pending.

After upgrading, an already-open tracker may still be the older executable.
Close that tracker with the explicit tracker-window control, then initialize it
again from the new MATREX build. Replacing the app bundle does not replace a
running process or its calibration. Automatic recording deliberately refuses
an older endpoint rather than assuming that a visible tracker window is capable.

## Source build and reuse

The [pinned source patches and locks](../tools/native_patches/README.md) record exact upstream commits, patch hashes and capabilities. They preserve native attributed resources and distinguish local builds from upstream releases. Existing user checkouts and unpublished work were not overwritten.

Build the current desktop and both adapters with:

```sh
python3 tools/build_local_desktop.py
```

A previous unsigned source build can be reused explicitly, after source/patch and executable-provenance verification:

```sh
python3 tools/build_local_desktop.py \
  --verified-native-binary /absolute/path/to/rekkefly \
  --native-binary-provenance /absolute/path/to/rekkefly-provenance.json \
  --verified-hejjetrac-binary /absolute/path/to/fictrac-desktop \
  --hejjetrac-binary-provenance /absolute/path/to/hejjetrac-provenance.json
```

The per-backend helpers are `tools/build_local_rekkefly.py` and `tools/build_local_hejjetrac.py`; each accepts `--companions`, `--verified-binary` and `--binary-provenance`. HejjéTrac optionally accepts `--template-archive` for the same checksum-pinned resource archive. Runtime never downloads these packages.

## Executed validation

[Native source/build evidence](native-recording-control-validation-results.json) records exact source/patch/executable identities. The final [best-effort MainController acceptance](native-recording-best-effort-validation-results.json) passed **76 checks across five runs**: two successful recorded runs, a mixed run with two healthy native GUIs and a missing third tracker, cancellation after the first rig acknowledged while the second prepared, and a native manual pause. Normal and mixed runs archived both healthy clips with consumed-frame-ID alignment and no media warnings. The missing third tracker produced one unavailable/unacknowledged warning while VR and its ZIP finalized normally. Cancellation produced an explicitly unpresented artifact archive; manual pause produced an error manifest and no complete ZIP. Both native processes were verified closed after validation cleanup.

Separately, both actual native endpoints passed 30 two-take checks each; pure production transport/finalization checks pass 21; archive/source-ID checks pass 76 plus three logging guards; process/config/lifecycle checks pass 140, including 120 settled repetitions. The native source formatting, strict Clippy and tests passed the required normal/no-default Rekkefly and HejjéTrac workspace matrices. These results use generated/sample inputs and do not establish physical-camera acceptance.


[Best-effort regression results](native-recording-best-effort-validation-results.json) use real isolated owned fixture processes and filesystem requests. Two silent endpoints share a bounded parallel wait; missing and rejected recorders produce independent warnings while a healthy later recorder acknowledges its take. A definitive pre-queue rejection is distinct from an ambiguous timeout. An armed recorder without a first frame times out honestly, and successful physical closure does not turn that empty take into an acknowledgement. Unsupported native companions prepare input with a warning and create no fake pending-media source. The fixtures simulate frame counts; they do not substitute for the separate actual native-video and MainController acceptance tests.


The owner-identity regressions additionally verify zero native commands for mismatched port, remote address, calibration, executable, device, backend, camera-only mode, profile and unknown legacy profile, followed by a successful unchanged owner using a normalized backend alias.
