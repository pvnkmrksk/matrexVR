# Heading-relative stimulus presentation and live telemetry

## Experiment configuration

Use [pre-stimulus / stimulus / post-stimulus](experiment-phases.md) in one experimental file. Heading measurement belongs to `preStimulus.headingReference`, with `enabled` (default false), `startOffsetSeconds` (default 0) and `windowSeconds` (default 180). The configured pre-stimulus world remains visible and controllable while each rig measures its circular mean and resultant length. An overlong observation extends pre-stimulus. Each spawner separately opts into the frozen zero with `useHeadingReference: true`; computing the reference alone changes no stimulus direction.

Old flat experiments still work. Their sequence presentation timer begins after an enabled flat heading observation, and their configured objects remain visible during observation. The default pose reset switches are true; set both false for continuity between conditions. The phase reference documents supported spawners, angle conventions, independent reset switches, examples and audit fields.

The mean is `atan2(mean(sin(yaw)), mean(cos(yaw)))`; resultant length is `sqrt(mean(sin(yaw))² + mean(cos(yaw))²)`. One current virtual yaw sample is taken per rendered frame during the requested window. Samples are equally weighted, with no invented catch-up samples. There is no stability threshold; circling animals still get a finite angle with low resultant length. Kinefly input is control input, not the heading being averaged. Zero is +Z, +90° is right, and −90° is left of the frozen mean when opted in. Subsequent turns do not move that reference.

`heading_reference.jsonl` records per-rig measurement completion; `experiment_phases.jsonl` records actual phase onsets and the references used then. Telemetry repeats the frozen results for late subscribers. The active [Selwyn recipe](../Assets/StreamingAssets/Examples/Flight/selwyn-stars-left-swarm.example.json) runs four minutes of stars then four minutes of gray sky and a leftward dorsal swarm.

## Dedicated outbound ZMQ channel

Unity binds one **PUB** socket for all rigs. Tracking input remains on its existing per-rig SUB endpoints. Default output is `tcp://127.0.0.1:9880`, at 20 snapshots/second. Configure the hardware/network connection at the top level of **system_config.json**:

```json
"telemetry": {
  "enabled": true,
  "bindAddress": "127.0.0.1",
  "port": 9880,
  "rateHz": 20,
  "topic": "matrex.telemetry.v1"
},
"statusOverlay": {
  "visible": true,
  "targetDisplay": 0
}
```

Use `bindAddress: "*"` to accept connections from other machines; subscribers connect to the Unity computer's address. The port must differ from all configured tracking input ports. `enabled: false` disables output. `rateHz` must be in `(0,240]` and cannot exceed the actual render-frame rate. Output continues in Control and across scene changes. Bind/send errors are shown in the operator HUD; they do not prevent the experiment running. Reloading system config retries the publisher.

Each **outbound telemetry** message is two UTF-8 frames: topic, then one JSON object. This requirement does not apply to tracking input: the existing Kinefly single-frame JSON protocol is accepted unchanged, as are topic/JSON messages. This follows [NetMQ PUB/SUB framing](https://github.com/zeromq/netmq/blob/master/docs/pub-sub.md). It is live monitoring, not a lossless recording: the PUB queue is bounded to two messages per subscriber, slow subscribers may miss snapshots, and no subscriber can block the render loop. The socket is created, used and disposed on one thread. Tracking SUB sockets now also own their entire lifecycle on their receive thread.

```sh
python3 -m pip install pyzmq
python3 tools/listen_telemetry.py --pretty
python3 tools/listen_telemetry.py --endpoint tcp://UNITY_HOST:9880 > live.jsonl
```

### Version 1 payload

| Field | Meaning |
|---|---|
| `schemaVersion`, `sequence` | Version 1; increasing publisher snapshot number, useful for detecting gaps. |
| `timestampUtc`, `realtimeSeconds`, `experimentSeconds`, `frame` | UTC timestamp; Unity unscaled uptime, scaled experiment time and frame. |
| `system` | Running flag; one-based trial and scene-sequence numbers; actual scene name/build index; remaining cycle and phase time; phase (`preStimulus`, `stimulus`, `postStimulus`); publisher state and local dropped-send count. Scene number is 0 while idle. Trial counts passes through the sequence, matching MainController. |
| `rigs` | All configured/registered VR IDs. A rig absent from the current scene has `active: false`, without fabricated pose/input. |
| `rigs[].pose` | `x,y,z,pitch,yaw,roll`: Unity world coordinates and Euler degrees. Heading is yaw; +Z is zero, +X is +90°. Units are the scene's authored world units (current walking scenes use cm). |
| `input.source`, `interpretation`, `endpoint` | Configured tracker type, active calculation path and tracking endpoint. |
| `input.motionMode`, `input.state`, `input.wireFormat` | Effective motion (`flight`, `walking`, `force/torque`), listener reception state and detected framing. `input.endpoint` is the endpoint used by the running listener. |
| `input.latest` | Atomic latest sensor packet: six **unconverted wire values**, receive sequence/UTC timestamp, age and freshness. Fields omitted by a legacy tracker retain the existing zero defaults. Raw units depend on the tracker: historical Kinefly yaw is signed radians; the walking path preserves its existing degree-encoded quaternion conversion. |
| `input.consumed` | The exact atomic packet most recently read by closed-loop processing. May be older than `latest`. Repeated render frames can consume the same packet. |
| `closedLoop` | Actual position/orientation gain, yaw gain, DC offset in radians, force/torque gain, P/O enabled flags and whether tracking was applied in this frame. Applied position/yaw deltas describe that frame's tracking contribution, excluding later manual/autopilot movement. Yaw delta is wrapped to ±180°; it is not an integrated turn count. |
| `headingReference` | `disabled`, `observing`, `presented` or `error`; remaining assessment time; frozen circular result after onset. |
| `errors` | Up to eight recent errors/assertions/exceptions with UTC time, message and stack trace; each text field bounded to 8192 characters. Full details remain in the session trace. |

`fresh` means received within one second. A missing sample uses `available: false`, `receivedUtc: null`, `ageSeconds: null`; its zero numeric placeholders are not measurements. Telemetry snapshots are sampled monitoring records, not every sensor pulse. Receive sequence counters reveal packets skipped between snapshots. Tracker-generated timestamps or pulses not present in the existing six-number input protocol cannot be recovered. Legacy Kinefly retains held-input integration, while walking still freezes on stale input. Telemetry does not change those calculations.

## On-screen diagnostics

The bottom-right panel appears by default on display 0, including in standalone builds. **Tab toggles it** while Game has focus and no text-entry field is active. This restores the historical shortcut. `statusOverlay.targetDisplay` can move it to another connected display. The panel shows trial, sequence scene number/name, assessment/presentation state, per-rig gains, DC offsets and effective input mode (for example, `Kinefly / flight`). Each `Input SUB` line shows the actual tracking endpoint and reception state. `Telemetry PUB (output)` labels the separate outbound monitoring port. It persists across scene changes independently of the overview camera.

Any new Unity error, assertion or exception, including errors logged on a background tracking thread, automatically reopens the panel and shows the latest message. Full recent error details are in telemetry and `runtime_trace.log`. `Debugger` errors are no longer suppressed at log level zero. Capturing an error reports it; it does not imply that the failing operation recovered. A presentation callback failure holds the sequence timer and reports the error.

## Validation

Run the static validators, then `HeadingTelemetryValidation.Run` using Unity 6000.3.16f1 on a disposable copy with `KANNADI_VALIDATION_COPY` in its root. It exercises circular wraparound/low-r/window eviction, opt-in and sequence precedence, generic/Choice adapters, actual Swarm scenes with distinct rigs, onset and repeated-trial continuity, real ZMQ input/output, signed raw data and error-driven HUD recovery. `BogongGainValidation.Run` and `KannadiValidation.Run` retain the historical Kinefly and walking arithmetic regression coverage. Physical tracking and LED timing require a bench run.

The [saved validation report](heading-telemetry-validation-results.json) records 76 feature checks, 73,746 historical Kinefly checks, 1,008 walking/template checks, and a separate Python subscriber receiving real Unity snapshots.
