# Experiment-scoped native recording

Automatic recording now controls the ordinary Rekkefly and HejjéTrac GUI workers. Tracking, calibration and the native window remain independent of the experiment; no second tracker engine is started to control recording.

Each rig exposes a video checkbox and **Automatic experiment / Manual external** choice. Automatic video is required once requested: a missing, unverified or incompatible endpoint blocks acquisition with a visible error. Manual mode reports operator control and never automatically starts or stops external recording. Turning off video permits input-only use.

## Lifecycle contract

1. Initialize tracking opens the native window without starting an automatic take.
2. Experiment Start captures a fresh run GUID and logger directory, verifies the owned process/endpoint identity, and requests a fresh native take. A queue reply is insufficient: the exact take directory and at least one accepted/submitted raw-video frame must be observable before the stimulus starts.
3. Experiment Stop immediately seals VR writers. It sends all matching owned recorder stops, waits for their physical completion receipts, then archives closed files and creates the final ZIP. The native windows and tracking sources continue independently.
4. Each receipt must prove successful numeric/media/container closure with matching accepted/written or submitted/encoded counts and no pending frames or errors. The capture worker stamps the exact owned Stop request ID. A pause count catches short manual gaps even when live polling misses them.

A native manual pause/stop/replaced take, encoder failure, endpoint loss or timeout remains explicit. Automatic restart is disabled for that run. Closed partial artifacts can still be copied safely, while the overall run is marked failed and no complete ZIP is produced. Canceling preparation before any VR pose retains closed native artifacts with hashes and explicit `presented:false`, `playableVrTimeline:false`, unavailable alignment. No VR offset or playable timeline is invented.

Native sources are never modified during archival or replay. Registration binds the captured GUID and logger directory, so a late callback or previous take cannot enter a later run. Settled run/task/cancellation structures are removed between repetitions.

## Endpoint and platform scope

The native apps opt in through `--control-dir ABSOLUTE_PRIVATE_DIRECTORY --control-instance 32_HEX_TOKEN`. `endpoint.json` identifies protocol version 1, instance token, backend, process ID and these required capabilities:

- `native-gui-control`
- `per-take-finalization`
- `owned-take-stop-v1`

Local request/reply files carry matching random IDs, instance identity and expiry. Commands are `status`, `record_start`, ownership-fenced `record_stop`, `start` and `pause`. `record_stop` requires the expected take directory and immutable stop request ID. `pause` affects tracking and is distinct from recorder Stop. Idle status, stable file size or elapsed delay never prove closure.

The reviewed source adapters currently ship for **Mac arm64**. The flight detector and walking numerical core are unchanged. Intel Mac, Windows and Linux packages retain their previous capabilities and reject automatic video with update/Manual guidance. Physical camera start/stop, exposure timing, attached hardware and other-platform runtime acceptance remain pending.

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

[Sanitized machine-readable results](native-recording-control-validation-results.json) record the exact source/patch/executable identities and outcomes. The actual Unity MainController → two owned native GUI test passed **58 checks across four runs**: two successful recorded runs, cancellation after the first rig acknowledged while the second prepared, and a native manual pause. Normal runs archived both clips with consumed-frame-ID alignment and no media warnings. Cancellation produced an explicitly unpresented artifact archive; manual pause produced an error manifest and no complete ZIP. Both native processes were verified closed after validation cleanup.

Separately, both actual native endpoints passed 30 two-take checks each; pure production transport/finalization checks passed 20; archive/source-ID checks passed 73 plus three logging guards; process/config/lifecycle checks passed 109, including 120 settled repetitions. The native source formatting, strict Clippy and tests passed the required normal/no-default Rekkefly and HejjéTrac workspace matrices. These results use generated/sample inputs and do not establish physical-camera acceptance.
