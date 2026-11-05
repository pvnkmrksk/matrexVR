# Tracker backends and optional app launch

Current source connects [Rekkefly](https://github.com/pvnkmrksk/rekkefly) flight tracking and [HejjéTrac](https://github.com/pvnkmrksk/hejjetrak) walking tracking to the existing per-rig ZMQ input. The live telemetry output goes to [Daari Deepa](https://github.com/pvnkmrksk/trajectory-dashboard). Tracker measurement ports and the MATREX telemetry output port are separate. A dashboard subscribes to telemetry; it does not feed movement back into Unity.

The setup screen shows modern backend names. `Rekkefly` (enum 4) and `Hejjetrac` (enum 3) are explicit new config choices. `Kinefly` (1), `FicTrac` (0), and `Tirbala` (2) remain accepted; opening a file does not rewrite them. `HejjéTrac` and historical `hejjetrak` spelling are accepted input aliases. Real subject metadata uses AnimalID and SpeciesName; generated or replayed individuals remain agents.

## Measurement wire contract

- Rekkefly: one UTF-8 JSON frame, compatible with Kinefly. `x` is left wing angle, `y` is right wing angle, `yaw` is left minus right, all radians. Unity retains the raw radian signal for flight gains/auto trim. Additional tracker timestamp/frame fields are allowed.
- HejjéTrac: one UTF-8 frame beginning `FT, ` followed by exactly 25 numeric CSV columns. Numeric columns 15/16 contain world forward/east position in sphere radians; 17 contains heading radians. Unity maps east to X and forward to Z, scales displacement by sphere radius in centimetres, and converts heading to quaternion degrees. Full upstream data remains in tracker recordings.
- Existing single JSON, two-frame topic+JSON, and separate legacy topic/JSON messages remain accepted. A topic-only message is ignored. Nonfinite/malformed measurements are rejected without publishing a partial pose.

Explicit Hejjetrac mode uses walking interpretation. A native FT row also selects walking under the legacy FicTrac name. Historical FicTrac-named Bogong JSON retains its previous gain behavior. A walking FT row is rejected on a flight backend rather than being mistaken for wing angles.

MATREX bundles NetMQ 4.0.1.13, AsyncIO 0.1.69 and NaCl.Net 0.1.13. This fixes the old 4.0.0.1 client's handshake with the native HejjéTrac publisher. Exact DLL hashes/frameworks and official package sources are in [the transport manifest](../Assets/Plugins/tracker-transport-manifest.json); licenses are in [TrackerTransport](../Assets/ThirdParty/TrackerTransport/README.md). No tracker output algorithm or calibration is changed.

## Independent native windows and recording control

See the [current recording contract and executed validation](native-recording-control.md) for Start/Stop, cancellation, override and build details.

Initialize tracking opens the rig's ordinary Rekkefly or HejjéTrac window for source selection and calibration. Tracking initialization does not record an automatic experiment take. Native windows persist across scene changes, experiment Stop and MATREX exit; explicitly closing a tracker is a separate action. The launcher checks its persisted process start time, executable identity and per-instance endpoint token. It never controls an unrelated process merely because it has the same executable name.

Each rig has a `tracker.saveVideo` checkbox and a recording-control choice:

- **Automatic experiment (`Experiment`)** attempts recording through verified, control-capable native windows owned by MATREX. Missing, failed or incompatible recorders produce warnings and never prevent VR startup. Each rig starts independently with a two-second preparation budget in parallel (up to one additional second to settle an already-sent command). A fresh take directory and the first accepted/submitted raw-video frame are required to claim acknowledgement. Stop seals VR writers immediately, sends ownership-fenced stops to all actually started recorders, waits for their exact completion receipts, then archives and creates the ZIP.
- **Manual external (`Manual`)** leaves recording to the operator or external system. MATREX does not automatically start, stop or claim acknowledgement of that recorder. Turn off tracker video if only the input stream is needed.

A native GUI pause, stop, replaced take, lost endpoint or recorder error latches an explicit override/failure; MATREX never silently restarts it. The native completion receipt retains the owned stop request ID and the number of capture pauses. This catches even short manual gaps that live polling might miss. Physical closure and a fully recorded requested interval remain distinct: safely closed partial files can be retained while the experiment is marked failed and no allegedly complete ZIP is created. A canceled preparation with no presented VR frames can retain closed, hashed native artifacts, explicitly labeled unaligned and without a playable VR timeline.

Example optional fields (merge into a complete system config):

```json
{
  "vrId": "VR1",
  "closedLoopMode": "Rekkefly",
  "zmqAddress": "127.0.0.1",
  "zmqPort": 9871,
  "tracker": {
    "executablePath": "/absolute/path/to/Rekkefly.app/Contents/MacOS/rekkefly",
    "configFile": "Trackers/VR1.toml",
    "saveVideo": true,
    "recordingControl": "Experiment"
  }
}
```

Recorder control additionally requires the current rig’s local address, backend, port, explicit executable/calibration/device and profile to match the owned receipt. A conflict warns and leaves the previous window untouched, including sending no status command.

The ordinary launcher passes direct argv without a shell and does not attach `--supervised` or `--control-stdio`. Opted-in capable adapters expose a private reconnectable filesystem mailbox with an instance token, process ID, request/reply IDs and expiry. Queued command acceptance, inactive recording state, file-size stability and elapsed delay are never treated as a file-finalization acknowledgement. Recorder completion requires the exact take's successful encoder/container drain and matching frame counts. Recording config, metadata, numeric data, video and completion receipts are copied only after closure; sources are not modified. Each take is bound to the captured MATREX run GUID and logger directory, so a late callback cannot enter a later run.

Relative native config paths resolve under the MATREX configuration directory. HejjéTrac's calibration file sets its source, `zmq_port` and loopback `zmq_host`; the launcher has no walking-port override. Optional custom executables need the same verified endpoint to acknowledge automatic video; otherwise that rig warns and VR continues. Other installed trackers remain usable in explicit Manual mode or with video disabled.

The reviewed Mac arm64 adapters are source overlays with pinned commits, complete patch hashes and executable provenance in [native build instructions](../tools/native_patches/README.md). They retain native calibration, detector algorithms, attributed resources and independent window lifetimes. Other platform packages do not claim these new recording capabilities and report automatic video unavailable with Manual/update guidance while allowing VR startup.

## Native verification

[The machine-readable native report](tracker-native-validation-results.json) records the exact measured scope. On macOS arm64/Unity 6000.3.16f1, real HejjéTrac processed 300 sample-video frames and Unity received all 300, alongside 100 legacy JSON frames. Current Rekkefly 0.6.2 processed 400 synthetic frames with simultaneous Unity and libzmq subscribers: both received 391 identical stream samples, with 9 frames missed during subscription startup and 0 publisher queue drops. This demonstrates live fanout, with normal PUB startup loss rather than a recording/replay guarantee. 150 listener shutdown cycles returned the worker count to 0 and kept OS thread count bounded. The telemetry probe also tests an external libzmq subscriber, Unicode metadata, schema 2 frames, and schema 1 context heartbeats at a faster independent cadence. This is not physical camera validation or a Windows/Linux execution claim.

`python3 tools/test_tracker_contracts.py` passes 140 offline backend/decoder and native owned-process checks, including removal/rename, cleared launch fields, argument quoting and leaving unrelated processes running. `tools/test_native_tracker_interop.py --help` describes the optional real Unity/HejjéTrac probe; optional --rekkefly-bin/--rekkefly-config arguments add current Rekkefly fanout; it creates a separate tiny project and leaves the source checkout and tracker calibration files untouched. Python/pyzmq are development test dependencies, not requirements for running MATREX or the native trackers.
