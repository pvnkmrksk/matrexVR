# Input contracts

Each configured animal rig has a **ZMQ SUB input**. The tracker or adapter publishes; Unity connects to the configured `zmqAddress` and `zmqPort`. The separate experiment telemetry endpoint is a **Unity PUB output**. Matching port numbers without matching the direction and wire contract does not make two tools interoperable.

## Stable 0.3 JSON frame shapes

ZmqListener accepts a single UTF-8 JSON frame, or two UTF-8 frames containing topic then JSON. The listener subscribes to any topic. The pose object uses finite numeric channels:

```json
{"x": 0.0, "y": 0.0, "z": 0.0, "roll": 0.0, "pitch": 0.0, "yaw": 0.0}
```

The six names identify channel positions, not universal units. The selected tracker/motion path determines the meaning. The historical Kinefly bridge sends one JSON frame; do not add a mandatory topic frame to an existing publisher simply because outbound telemetry has one.

## Walking contract

Legacy walking JSON uses cumulative ball angular displacement for planar motion, in radians before sphere-radius conversion. Its yaw is interpreted as degrees through the historical Unity quaternion path. Sensor y maps to Unity X; sensor x maps to Unity Z. The configured sphere diameter is in centimetres for centimetre-scale walking scenes.

Do not publish an already converted linear position or a velocity into that cumulative angular-displacement contract. The same JSON shape with the wrong meaning will produce plausible but incorrectly scaled motion. Verify a known ball movement before collecting data.

## Flight contract

Kinefly-compatible flight uses signed raw yaw radians for the historical yaw-rate calculation. Unity retains raw rotations as well as a display quaternion; telemetry version 2 converts angular reporting to degrees while raw CSV compatibility fields remain distinguishable.

Rekkefly is the modern flight-tracking project in this toolchain. Use its documented Unity-compatible fanout output rather than guessing that every monitoring socket has the same frame shape. [Integration guide](integrations.md) links the verified repository and launch contract.

## Current-source native FT rows

Source updates after 0.3 add `Hejjetrac` and `Rekkefly` backend names while preserving serialized `FicTrac=0`, `Kinefly=1`, `Tirbala=2`; the new enum values are `Hejjetrac=3` and `Rekkefly=4`. Legacy JSON is not silently rewritten. Modern flight naming normalizes to the flight calculation; modern walking selects the walking contract.

HejjéTrac's native payload is one UTF-8 frame: `FT, ` followed by **exactly 25 numeric CSV fields**. Counting the numeric columns from 1 after FT, forward sphere displacement is column 15, east displacement column 16, and heading column 17; these quantities are radians. The current decoder maps east to Unity X, forward to Unity Z, converts heading for the walking orientation representation, and applies sphere radius for linear displacement. This differs from the old JSON yaw-degrees representation even though both reach the walking controller.

The format decoder and the native ZeroMQ transport connection are separate checks. The [native source integration report](../tracker-native-validation-results.json) received **791 input frames**: 300 actual HejjéTrac FT rows, 100 mixed legacy JSON messages and 391 actual Rekkefly messages. The Rekkefly fixture processed 400 synthetic source frames; Unity and an independent libzmq subscriber each received 391 after normal PUB startup subscription delay. This is not a claim that all 400 were delivered. The report also covers 150 listener shutdown cycles with worker count returning to zero and 25 telemetry publisher teardown cycles with thread count returning to baseline. The source bundle uses NetMQ 4.0.1.13, AsyncIO 0.1.69 and NaCl.Net 0.1.13; the [transport manifest](../../Assets/Plugins/tracker-transport-manifest.json) records exact artifacts. This macOS no-graphics probe is not a physical camera or other-OS execution claim. The immutable 0.3 application should use its established JSON adapter rather than assuming it includes this post-release FT decoder.

## Latest-sample handoff

The receive worker validates a complete message and updates position, raw rotation, converted rotation, receive UTC and a sequence counter under one lock. ClosedLoop sees one atomic sample; it cannot mix channels from two partially received packets.

Samples are consumed at rendered-frame cadence. Multiple frames can consume the same input sample; multiple packets can arrive between renders. `input.latest` and `input.consumed` in telemetry distinguish reception from the sample most recently read by motion processing. They are not a lossless log of every camera frame or tracker pulse.

## Freshness and disconnects

The listener records receipt age. Walking freezes its tracking contribution when stale; historical Kinefly yaw-rate integration holds the latest value. The dashboard reports source, endpoint, wire format and freshness. Operator movement/reset can still work with a stale input.

When a listener component is disabled/destroyed, it requests worker shutdown; its worker owns socket disposal. A timed-out stopping worker must not be overwritten by a second live socket thread. This ownership is part of [scene lifecycle validation](../scene-lifecycle.md).

## A useful bench sequence

1. Connect a test publisher to one configured VR input and confirm endpoint and packet sequence in the dashboard.
2. Send a stationary reference and verify no unintended world movement under the chosen gain/DC offset.
3. Apply one known translation/yaw increment and compare actual world displacement/heading to the contract.
4. Stop publishing and observe the mode-specific stale behavior.
5. Re-enable P/O and reset; verify that accumulated disabled motion is not replayed.

This checks the software connection. Physical camera calibration, ball slip, north alignment and LED timing require the actual apparatus.

## Source and reference

[ZmqListener](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/ZmqListener.cs), [ClosedLoop](closed-loop.md), [recorded units](../data-formats.md), [external trackers](integrations.md).
