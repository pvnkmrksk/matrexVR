# Live browser monitoring

This chapter describes **source updates after the immutable 0.3.0 release**. The existing OS downloads remain unchanged. A browser dashboard needs a running bridge on the acquisition/server computer; GitHub Pages supplies static documentation, not a raw ZMQ connection.

## Two kinds of telemetry message

The lightweight frame stream retains **schemaVersion 2** on the configured topic (default `matrex.telemetry.v2`). Current source adds an experimentContext identity/reference describing the session/config/design/units context needed to interpret the snapshot without embedding a large configuration graph in every frame.

The separate context heartbeat uses:

| Item | Contract |
| --- | --- |
| Topic | `<telemetry.topic>.context`, normally `matrex.telemetry.v2.context` |
| Payload `schemaVersion` | `1` for the context-message schema; this is separate from frame schema 2 |
| Payload `kind` | `matrex.context` |
| Interval setting | `telemetry.contextIntervalSeconds` |
| Default interval | 2 seconds |
| Valid interval range | 0.25–60 seconds |

A subscriber joining mid-run can receive the periodically repeated context and associate it with lightweight frame snapshots. This does not turn the bounded monitoring channel into a durable record of every tracker frame. Keep the session archive for analysis and replay.

## Browser bridge direction

```text
Unity PUB: frames + context
       |
       v
local/server ZMQ subscriber
       |
       v
same-origin server-sent events (SSE)
       |
       v
read-only browser view at /live/
```

The server subscribes to ZMQ; the browser receives the server's SSE response. The browser never opens a raw ZMQ socket. Context and frame versions are parsed according to their own contracts rather than treating every JSON message as an interchangeable pose.

## Local and remote viewing

The trajectory-dashboard source adds `/live/` for this monitoring view. Connecting/changing the upstream subscription is permitted only from loopback controls. Remote viewers observe the read-only stream through the running server.

For a server intended only for live monitoring, `--live-only` disables the filesystem-analysis APIs rather than exposing the normal offline file workflow to LAN viewers. A LAN deployment still requires an explicitly running/bound server; opening this GitHub Pages manual does not start acquisition services.

See [Daari Deepa / trajectory-dashboard](https://github.com/pvnkmrksk/trajectory-dashboard) for the current launcher, packaging and server options. [Integrations](integrations.md) distinguishes tracker inputs, monitoring and saved-run analysis.

## Run the bridge

From a checkout of [Daari Deepa](https://github.com/pvnkmrksk/trajectory-dashboard) containing the live-monitor changes, install its native requirements and start the bridge:

```sh
python -m pip install -r requirements-native.txt
python native_app.py --telemetry tcp://127.0.0.1:9880 --open-browser
```

The server defaults to `127.0.0.1:8060`; open `http://127.0.0.1:8060/live/` for the monitor. `--telemetry-topic` changes the default `matrex.telemetry.v2` subscription. Use the endpoint actually enabled in Unity's telemetry settings. A bridge cannot discover a different rig's input port or enable telemetry inside an already running Unity player.

For a deliberately shared read-only monitor on a trusted lab network:

```sh
python native_app.py --telemetry tcp://127.0.0.1:9880 --host 0.0.0.0 --live-only --no-browser
```

Then viewers use `http://BRIDGE-HOST:8060/live/`. The bridge's ZMQ connection remains to the configured publisher; binding its web server to a LAN interface does not require changing the Unity PUB address when both processes run on the same machine. Connection-setting controls remain loopback-only.

## Standalone bridge packaging

The [dashboard download hub](https://pvnkmrksk.github.io/trajectory-dashboard/) serves complete ZIPs for Windows x64, Linux x64, macOS arm64 and macOS x64, with source revisions and SHA-256 checksums. Downloads are static and require neither sign-in nor Python. Passing main-branch builds update this hub without creating release tags. GitHub Actions also retains sign-in-only build artifacts for 14 days.

Each package is built on its target OS and tested with an actual PUB → packaged receiver → HTTP/SSE connection, late-joining context, static browser assets, and a recorded-data fixture whose duration must remain exactly two seconds. The [dashboard build workflow](https://github.com/pvnkmrksk/trajectory-dashboard/actions/workflows/native-build.yml) records the platform results. These checks exercise software and synthetic data; they do not measure a physical camera, tracking apparatus or LED display.

The source has 158 dashboard regression tests. pandas is constrained to the validated 2.x series because pandas 3 changes datetime integer units and writable-array semantics used by the numerical reference. Unsupported pandas 3 environments fail explicitly, rather than silently changing durations. The [source integration PR](https://github.com/pvnkmrksk/trajectory-dashboard/pull/1) records this independently of the unchanged 0.3 Unity downloads.

Snapshot and heartbeat ages are separate. Repeated idle/context events refresh visible tracker age without appending duplicate trajectory points. When a newer heartbeat advances the scene/trial before the next low-rate pose snapshot, the viewer clears the previous trajectory and waits for matching pose evidence; it does not combine an old pose with the new scene's units or config. An older heartbeat cannot relabel a newer frame.

## Saved-run metadata discovery

The related dashboard loader accepts canonical animal metadata filenames (`AnimalMetaData.json` and the runtime's `AnimalMetadata.json`) alongside historical `FlyMetaData.json`. Canonical `Animals` and `AnimalID` take precedence over `Flies` / `FlyID`; an explicit empty `Animals` list remains authoritative. `SpeciesName` is retained, and `FlyID` remains a compatibility alias for historical analysis. Segment identity continues to combine `SourceFile`, normalized trial and step, so equal block names in two files do not collapse into one segment.

These loader updates concern saved-run analysis, not the live transport. Retain the complete run directory and source revision for interpretation.

## Interpretation

Use the frame's scene, phase, rig identity, tracking interpretation and freshness along with the matching experiment context. A visible trace is a sampled monitor, not proof that every input packet was received or that LED output happened at a measured physical latency.

The default heartbeat makes late subscription practical; it is not a synchronization pulse. Frame timestamps describe the captured frame. A server/browser/network delay does not change the original acquisition time or repair an absent historical sample.

## Stable reference

The [0.3 telemetry reference](../heading-reference-telemetry.md) documents frame schema 2, its units and PUB queue behavior. This chapter describes the additional current-source context/browser path. Cite the actual source revision when using those changes; no new Unity release is implied.
