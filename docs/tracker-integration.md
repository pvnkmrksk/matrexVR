# Tracker backends and optional app launch

Current source connects [Rekkefly](https://github.com/pvnkmrksk/rekkefly) flight tracking and [HejjéTrac](https://github.com/pvnkmrksk/hejjetrak) walking tracking to the existing per-rig ZMQ input. The live telemetry output goes to [Daari Deepa](https://github.com/pvnkmrksk/trajectory-dashboard). Tracker measurement ports and the MATREX telemetry output port are separate. A dashboard subscribes to telemetry; it does not feed movement back into Unity.

The setup screen shows modern backend names. `Rekkefly` (enum 4) and `Hejjetrac` (enum 3) are explicit new config choices. `Kinefly` (1), `FicTrac` (0), and `Tirbala` (2) remain accepted; opening a file does not rewrite them. `HejjéTrac` and historical `hejjetrak` spelling are accepted input aliases. Real subject metadata uses AnimalID and SpeciesName; generated or replayed individuals remain agents.

## Measurement wire contract

- Rekkefly: one UTF-8 JSON frame, compatible with Kinefly. `x` is left wing angle, `y` is right wing angle, `yaw` is left minus right, all radians. Unity retains the raw radian signal for flight gains/auto trim. Additional tracker timestamp/frame fields are allowed.
- HejjéTrac: one UTF-8 frame beginning `FT, ` followed by exactly 25 numeric CSV columns. Numeric columns 15/16 contain world forward/east position in sphere radians; 17 contains heading radians. Unity maps east to X and forward to Z, scales displacement by sphere radius in centimetres, and converts heading to quaternion degrees. Full upstream data remains in tracker recordings.
- Existing single JSON, two-frame topic+JSON, and separate legacy topic/JSON messages remain accepted. A topic-only message is ignored. Nonfinite/malformed measurements are rejected without publishing a partial pose.

Explicit Hejjetrac mode uses walking interpretation. A native FT row also selects walking under the legacy FicTrac name. Historical FicTrac-named Bogong JSON retains its previous gain behavior. A walking FT row is rejected on a flight backend rather than being mistaken for wing angles.

MATREX bundles NetMQ 4.0.1.13, AsyncIO 0.1.69 and NaCl.Net 0.1.13. This fixes the old 4.0.0.1 client's handshake with the native HejjéTrac publisher. Exact DLL hashes/frameworks and official package sources are in [the transport manifest](../Assets/Plugins/tracker-transport-manifest.json); licenses are in [TrackerTransport](../Assets/ThirdParty/TrackerTransport/README.md). No tracker output algorithm or calibration is changed.

## Optional native app buttons

In System details, expand a rig's Tracker field and choose Set value. Provide the absolute installed executable and an existing calibration/config file. MATREX never discovers/downloads executables and never launches them automatically. On macOS, use the binary inside the app's Contents/MacOS folder. Launch/Stop buttons appear for configured rigs in setup. An external tracker started by the operator continues to work normally.

Example optional per-rig fields (merge into a complete existing system config):

```json
{
  "vrId": "VR1",
  "speciesName": "Drosophila melanogaster",
  "closedLoopMode": "Rekkefly",
  "zmqAddress": "127.0.0.1",
  "zmqPort": 9871,
  "tracker": {
    "executablePath": "/absolute/path/to/rekkefly",
    "configFile": "Trackers/VR1.toml",
    "device": "",
    "headless": false
  }
}
```

The launcher uses the audited Rekkefly 0.6.2 (`2df99c3`) supervision interface and HejjéTrac `94404c64` stdio interface. Older installed trackers can be started manually if they lack those launch flags. The launcher passes argv directly, without a shell. Relative tracker config paths resolve under the MATREX config root. Rekkefly receives `--config FILE --label VRn --zmq-port PORT --supervised`, with optional `--device`/`--headless`. Supervision disables fallback and supports stdin shutdown. HejjéTrac receives `--config FILE --windows 1 --control-stdio`, then a typed session 1 start request. Installed HejjéTrac packages name the desktop executable `hejjetrac` and the CLI `hejjetrac-cli`; a Cargo source build produces `fictrac-desktop` and `fictrac`. Its calibration config must set `zmq_port` to the same rig port and `zmq_host` to loopback; there is no port CLI override and MATREX never rewrites the calibration file. Device/source selection for HejjéTrac stays in that file.

Only locally bound trackers can be launched by these optional buttons. Save calibration edits in the native tracker before Stop: MATREX asks its owned process to close, then terminates it if it does not exit promptly. Unrelated processes, including another instance of the same executable, are never stopped. Owned trackers persist across experiment scene transitions and stop when MATREX quits. Stop remains available if you clear an owned tracker’s optional launch fields. Applying a system configuration that removes or renames a rig stops only the process MATREX owned for that rig; changing the selected backend does not relabel a running process.

## Native verification

[The machine-readable native report](tracker-native-validation-results.json) records the exact measured scope. On macOS arm64/Unity 6000.3.16f1, real HejjéTrac processed 300 sample-video frames and Unity received all 300, alongside 100 legacy JSON frames. Current Rekkefly 0.6.2 processed 400 synthetic frames with simultaneous Unity and libzmq subscribers: both received 391 identical stream samples, with 9 frames missed during subscription startup and 0 publisher queue drops. This demonstrates live fanout, with normal PUB startup loss rather than a recording/replay guarantee. 150 listener shutdown cycles returned the worker count to 0 and kept OS thread count bounded. The telemetry probe also tests an external libzmq subscriber, Unicode metadata, schema 2 frames, and schema 1 context heartbeats at a faster independent cadence. This is not physical camera validation or a Windows/Linux execution claim.

`python3 tools/test_tracker_contracts.py` passes 66 offline backend/decoder and native owned-process checks, including removal/rename, cleared launch fields, argument quoting and leaving unrelated processes running. `tools/test_native_tracker_interop.py --help` describes the optional real Unity/HejjéTrac probe; optional --rekkefly-bin/--rekkefly-config arguments add current Rekkefly fanout; it creates a separate tiny project and leaves the source checkout and tracker calibration files untouched. Python/pyzmq are development test dependencies, not requirements for running MATREX or the native trackers.
