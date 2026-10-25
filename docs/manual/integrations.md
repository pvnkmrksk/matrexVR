# Trackers and analysis

The toolchain separates visual experiment control from tracking and analysis. MATREX VR owns Unity stimuli, rig motion, recordings and replay; an external tracker supplies the measured input. Analysis tools operate on saved evidence or a deliberately bridged monitoring feed.

## Verified related projects

| Project | Role | Repository |
| --- | --- | --- |
| **Rekkefly** | Modern Rust flight/Kinefly tracking path; use its documented Unity-compatible ZMQ fanout | [pvnkmrksk/rekkefly](https://github.com/pvnkmrksk/rekkefly) |
| **HejjéTrac** | Native Rust walking tracker; packaged `hejjetrac` desktop / `hejjetrac-cli`, with source binary names distinguished below | [pvnkmrksk/hejjetrak](https://github.com/pvnkmrksk/hejjetrak) |
| **Daari Deepa** | Browser-native trajectory views using WebGL/Canvas with Python preprocessing/serving | [pvnkmrksk/trajectory-dashboard](https://github.com/pvnkmrksk/trajectory-dashboard) |

These are separate repositories and release/installation flows. Their names do not establish that every available output socket already matches MATREX VR. Use the adapter/launch contract appropriate to the selected tracker and software revision.

## Verified tracker launch surfaces

Rekkefly accepts a configuration, rig label and per-process output port:

```sh
rekkefly --config "/path/to/tracker-config" --label VR1 --zmq-port 9871 --headless
```

An optional `--device DEVICE` selects the camera. Its supervised shutdown flag is intended for an owning launcher: stdin newline/EOF requests a graceful stop and disables fallback behavior. Keep that ownership separate from attaching to an already running tracker.

HejjéTrac uses its configuration's `zmq_port`; there is no command-line port override in the audited launcher:

```sh
hejjetrac --config "/path/to/tracker-config" --windows 1
```

This example uses the packaged desktop executable. The audited source checkout names its Cargo binaries `fictrac-desktop` and `fictrac`; the packaging script supplies the `hejjetrac` / `hejjetrac-cli` names and `hejjetrak` aliases. Match the executable to the installed version rather than renaming a CLI binary by assumption. The config port must match the Unity rig. Its native `FT` row is not the old JSON pose object. See the [exact input contract](input-contracts.md#current-source-native-ft-rows).

## Release and transport boundary

The stable 0.3 player retains the established JSON tracker contract. Current source adds modern tracker names and native FT decoding. The [native integration guide](../tracker-integration.md) and [791-frame native report](../tracker-native-validation-results.json) document 300 actual HejjéTrac FT rows, 100 mixed legacy JSON frames and 391 actual Rekkefly messages (391 of 400 synthetic source frames, also observed by an independent subscriber after PUB startup) with the updated NetMQ bundle, and separate transport evidence from decoding-only tests. These source changes do not move or replace the 0.3 release tag.

## Optional owned tracker launch

Current source can show explicit Launch/Stop buttons for configured local installed trackers. It does not download, discover or automatically launch executables, and never stops unrelated processes. Installed executable/calibration paths, direct argv handling, local-only limits and supervision behavior are documented in [native tracker integration](../tracker-integration.md). Stable 0.3 does not gain these source additions retroactively.

## Direction matters

```text
tracker / adapter PUB  -->  MATREX VR per-rig SUB input
MATREX VR PUB telemetry  -->  external subscriber / browser bridge
saved run folder  -->  replay or offline trajectory analysis
```

Tracker input has the [six-channel pose contract](input-contracts.md). Outbound monitoring has the [versioned telemetry schema](../heading-reference-telemetry.md). Saved population/animal CSV and config manifests have their own [data formats](../data-formats.md). They are not interchangeable just because all are JSON, CSV or ZMQ.

## Flight and walking

Rekkefly is the modern flight source. Select its Unity-compatible fanout and the source `Rekkefly` backend (or the established Kinefly-compatible path for stable 0.3), then verify signed yaw/DC/gain behavior. HejjéTrac supplies the walking acquisition path: current source decodes native FT rows with the `Hejjetrac` backend; stable 0.3 uses the established JSON adapter. Verify cumulative angular displacement and yaw conventions before applying sphere-radius conversion.

A physical tracker connection is not required to browse setup or inspect an existing run. Manual/autopilot movement is a simulation control, not a replacement measurement of the animal.

## Browser monitoring

An ordinary static web page cannot open a raw ZMQ socket. A live browser dashboard requires a running bridge/subscriber on an authorized workstation/server, with an explicit browser transport and access configuration. GitHub Pages serves the documentation and downloads; it is not an acquisition server.

Current source provides the [live browser monitoring contract](live-monitoring.md), including the context heartbeat and same-origin SSE bridge. The trajectory-dashboard project documents its browser/processing environment. Keep live monitoring separate from the durable run archive: bounded telemetry may omit snapshots and is not the full tracker stream.

## Icons and project identity

The documentation uses locally bundled Lucide icons with the retained ISC license, following the icon library used in the HejjéTrac project. Rekkefly has its own custom application icon/native controls; this manual does not imply that its existing UI uses Lucide.

## Source of truth

Use the linked repositories for their current builds and platform-specific dependencies. MATREX VR's accepted fields/units remain defined by ZmqListener and ClosedLoop. When a new integration changes a wire contract, record the exact revisions with the experiment and validate a known motion before collecting data.
