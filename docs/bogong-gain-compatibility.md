# Bogong/Kinefly gain compatibility

The reference is `BogongAustralia`, commit `ef8a68b2ad2ff690e803007511666296a31eda5a`. Its Kinefly input represents a wing-angle difference in radians, interpreted as a steering rate. It is not an absolute heading.

The walking implementation inherited by this branch interpreted the wire yaw as degrees, then applied only changes in that value. It also stopped reading the historical top-level sequence `gain`. A constant wing-angle difference therefore stopped turning the animal. Multiplying that implementation by a compensating gain would not restore the old dynamics.

## Restored calculation

```text
yawRateDegreesPerSecond = sequenceGain * (wireYawRadians - dcOffsetRadians) * Rad2Deg
rotationThisFrame = yawRateDegreesPerSecond * deltaTime
```

The code retains signed, unwrapped raw radians and the historical arithmetic/order of operations. For example, yaw 0.1 rad, gain 10 and offset 0 produce approximately 57.2958 degrees/s continuously. A fixed input does not become zero after its first packet. No shortest-angle conversion, smoothing, response cap or extra gain is inserted.

- Sequence step `gain` defaults to **1** when omitted; signed values and zero are accepted. It is applied on scene loads and in-place transitions. The active Selwyn sequence omits it and therefore uses 1. The archived Bogong migration example explicitly used 10; those are different conditions.
- `closedLoopOrientation` remains the on/off switch. `+`/`-` change yaw gain by 1 per press. Number keys 1–4 select a rig; held `]`/`[` adjust its DC offset by ±0.5 rad/s, exactly as the historical MainController did. Offsets survive trial/scene changes within a run.
- The historical path keeps integrating the last input when no new packet arrives, including after a disconnect. This is intentionally retained for behavioral equivalence.
- Position conversion keeps the reference implementation's axis swap, initial heading offset and **prefab** sphere radius. The historical Bogong script did not apply `system_config.sphereDiameter` to that calculation. The Swarm/Kannadi walking path continues to use the system-config sphere diameter and its numeric displacement/angular multipliers.
- The reference branch sets `useYawMode = true` for both its `FicTrac` and `Kinefly` enum values, despite its comment saying otherwise. That behavior is preserved for the historical path. The reference's raw-delta alternative and Tirbala placeholder arithmetic are also retained; no claim of physical Tirbala calibration is made.

## Select Kinefly explicitly

Set `"closedLoopMode": "Kinefly"` in each rig entry of `system_config.json`. The local setup has this value on all four rigs. `Templates/system_config.template.json` reproduces the six-camera Kinefly layout; edit hardware addresses before use. Existing Bogong configs omitting the field retain the reference's `FicTrac` enum default and historical yaw-mode behavior in Choice.

Swarm/Kannadi's separate walking path, selected through their existing walking gain configuration with the default `FicTrac` system mode, retains its existing degree-based Pose input and delta-gain behavior. Explicit Kinefly mode prevents those controllers from replacing the historical wing-angle path. There is no automatic inference of units from sample size.

Terrain, wind/AGL, autopilot and manual navigation remain their current implementations. This comparison establishes equivalence of the tracking/gain calculation, not every scene/environment behavior or physical tracker latency.

## Kinefly wire compatibility and port diagnostics

The existing Kinefly ROS bridge publishes **one UTF-8 JSON frame**, without a topic, using `send_string(json.dumps(kinefly_data))`. Its flat fields are `x` (left wing angle), `y` (right wing angle), `z`, `yaw` (left minus right), `pitch`, `roll`, plus optional `frame_number` and `timestamp_unix` metadata. For example:

```json
{"x":1.25,"y":1.6,"z":0.0,"yaw":-0.35,"pitch":0.0,"roll":0.0,"frame_number":42,"timestamp_unix":1791170000.125}
```

The telemetry implementation in commit `d4dadd88` inadvertently required two tracking frames and rejected this existing publisher with `Expected topic + JSON pose frames`. The receiver now accepts the original single JSON frame, a two-frame topic/JSON message, and a topic sent separately before JSON. It consumes every JSON sample. The historical listener read frames in pairs, which happened to accept every second single-frame sample. Missing pose fields still default to zero, additional metadata is accepted, and signed yaw remains raw radians. No new input schema, mode field or publisher change is required. Malformed/nonfinite poses are reported without terminating reception of subsequent valid samples.

Press **Tab** to show the status panel. Every active rig displays `Kinefly / flight (yaw-rate-radians)`, its actual `Input SUB: tcp://...:port` and `waiting for packets`, `receiving`, `stale` or `error`. Waiting means no valid sample has arrived; it does not claim a successful network connection. The separate `Telemetry PUB (output)` line is outbound monitoring, normally on 9880. It does not replace input ports 9871–9874. Invalid optional telemetry settings cannot clear the rig input configurations. If a rig has no matching config, the listener reports that error and retains its component endpoint instead of silently switching to the default localhost:9872.

`KineflyWireValidation.Run` replays these wire formats through four actual PUB/SUB socket pairs and checks the resulting flight steering, malformed-packet recovery, per-rig ports, monitoring isolation and HUD labels. Use the same disposable Unity validation copy as the suites below. The [wire regression report](kinefly-wire-validation-results.json) records the original failure and the corrected runs.

## Verification and audit

`tools/prepare_bogong_gain_reference.py <disposable-project>` reads the exact historical ClosedLoop source from Git and checks SHA-256 `84312430379fd7ffa0cdbe56b19297a4999fd7d70198c75ae68f44cfc8b70286`. It wraps that source in a test namespace with input adapters and a controlled clock; the calculation is not rewritten into a new expected-value formula.

Run `BogongGainValidation.Run` with graphics-enabled Unity on that disposable copy (marker `KANNADI_VALIDATION_COPY` required). It compares exact Vector3/Quaternion component equality for every frame across gains −10…40, three DC offsets, 30/60/120 Hz and irregular timing, constant/changing/signed/unwrapped input, packet holds, tracking toggles and resets. It also tests actual ControlScene sequence gain loading and transitions. `KannadiValidation.Run` checks the separate walking path.

The [2026-10-05 validation record](bogong-gain-validation-results.json) records **73,746 passing historical comparison/integration checks** and **1,004 passing walking checks**, with hashes of the tested source files. The historical suite covers six-camera Choice; the broad walking suite uses its supported four-panel fixture. The record includes the editor indexing message and the broad suite's six-camera JuliusTree fixture limitation.

New rig-log columns identify the tracking implementation, wire units, raw yaw, effective gain, DC offset and yaw-rate output. For Kinefly, the historical sensor Euler-angle calculation and `SensRotXRad/YRad/ZRad` columns are restored too. Walking sensor values are unchanged. Previous data must keep its original code/config provenance; this fix does not retroactively make runs recorded with the intervening walking implementation equivalent to historical Kinefly experiments.
