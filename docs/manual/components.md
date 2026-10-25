# Component index

This index maps runtime responsibilities to the production classes. Start with the owning component, then use the linked contract guide before changing its fields. Unity asset names and serialized class identities are retained even where operator terminology now says animal or agent.

## Session and orchestration

| Component | Role | Read with |
| --- | --- | --- |
| `MainController` | System/sequence loading, trial order, scene application, registered ClosedLoops, auto trim and persistent per-rig DC/reference state | [Architecture](architecture.md), [phases](../experiment-phases.md) |
| `RuntimePaths` | Editor/profile/override roots and non-overwriting initial seeding | [Storage](../getting-started.md#configuration-and-data-locations) |
| `RuntimeRigRegistry` | Positive VR IDs, template provisioning, omitted-rig disabling and isolated stimulus layers | [Rigs and viewports](viewports.md) |
| `ExperimentConfigFiles` | Central consumed-byte reads and legacy archive fallback | [Recording pipeline](recording-pipeline.md) |
| `ExperimentPhases` / `StimulusHeadingReference` | Per-phase configuration, bounded circular heading windows and frozen references | [Phase reference](../experiment-phases.md) |

## Input, motion and cameras

| Component | Role | Read with |
| --- | --- | --- |
| `TrackerProcessManager` (current source after 0.3) | Explicit optional local installed tracker launch/stop with direct argv and owned-process scope | [Native integration](../tracker-integration.md) |
| `TrackerBackend`, `TrackerWireDecoder` (current source after 0.3) | Modern/legacy backend naming and explicit JSON/native FT decoding; transport compatibility is validated separately | [Input contracts](input-contracts.md) |
| `ZmqListener` | Per-rig SUB endpoint; single or topic/JSON frames; complete latest-sample handoff | [Input contracts](input-contracts.md) |
| `ClosedLoop` | Pose/gain interpretation and tracking switches; historic flight arithmetic | [ClosedLoop](closed-loop.md) |
| `KineflyAutoTrim` | Flight-qualified offset estimation and its recorded diagnostics | [Auto trim](../kinefly-auto-trim.md) |
| `ExperimentInput` | Shared Input System actions and editing-focus guards | [Controls](../unity6-controls.md) |
| `Keyboard` | Permitted operator movement and forward autopilot | [ClosedLoop](closed-loop.md#manual-motion-and-autopilot) |
| `ViewportSetter` | Per-rig camera allow-list, physical-display selection and normalized panel rectangles | [Viewports](viewports.md) |
| `SimpleOverheadCamera`, `OverheadCameraUI`, `OverheadTrackOverlay` | Operator view, gestures, numbered animal markers and bounded trails | [Viewport reference](../six-camera-rig.md) |
| `MigrationMotion` | Wind/altitude behavior in supported flight scenes | [Configuration reference](../configuration-reference.md) |
| `RugosityWorld`, `RugositySurface`, `RugosityRig` | Explicit visible/navigation surfaces, sampled ground geometry and rig height/normal ownership | [Terrain cue lab](../rugosity.md) |

## Experiment families

| Component | Role |
| --- | --- |
| `ChoiceController` | Configured objects, materials, sky, movement and compatible per-condition rebuilding in Choice-family scenes |
| `DynamicSequenceController` | Design-driven Choice; inner steps, triggers, phase/loop state and per-animal asynchronous progression |
| `SwarmController`, `SwarmConfiguration`, `LocustSpawner` | Swarm config application, generated/recorded populations, layering and rendering |
| `Kannadi`, `KannadiConfig` | Mirrored animal poses arranged in a hexagonal matrix, with self/peer viewing controls |
| `BandSpawner`, `PeriodicBoundary`, `AnimateOnMove` | Supported grouped stimulus instances, wrapping and translation-based animation gating |
| `OptomotorSceneController`, `DrumRotator`, `SinusoidalGrating` | Rotating drum/grating stimulus and explicit generated native resources |
| `NightSkyController`, `NightSkyConfig`, `NightSkyOrientation` | Observer/time resolution, astronomical panorama generation/activation and provenance |

## Saving and monitoring

| Component | Role |
| --- | --- |
| `MasterDataLogger` | Session ownership, threaded trace, writer-close/drain and session archive boundary |
| `DataLogger`, `SceneDataLogger`, optomotor loggers | Animal/world/sensor rows and stimulus-specific columns |
| `AnimalData`, `AnimalMetadata`, `UIDataLogger` | Canonical real-animal metadata and transitional legacy aliases |
| `RunConfigArchive` | Recursive original-input capture, immutable content snapshots, SHA-256 manifest and consumed-load attribution |
| `SwarmLogger`, `AsyncSwarmWriter` | Population snapshots, bounded queue/pool, worker encoding/gzip and drain diagnostics |
| `KannadiLogger`, `BandLogger` | Replicated-pose and band stimulus rows |
| `ExperimentTelemetry` | Immutable frame snapshots and a separate bounded PUB publisher |
| `RuntimeStatusOverlay`, frame timing/FPS components | Operator dashboard, errors, frame-work/wait visibility and timing summaries |
| `RuntimeMaterialOwner`, `DisplayFrameClear` | Owned instance materials and bounded display-clearing resources |

## Configuration studio and replay

| Component | Role |
| --- | --- |
| `LosslessJson`, `ConfigDocument` | Original numeric lexemes/bytes, JToken values, per-document history, conflicts and atomic saves |
| `ConfigWorkspace`, `ConfigSchema`, `ConfigParameterHelp` | Selected file graph, emitted schema validation and shared field/default descriptions |
| `ConfigWorkbench` partial classes, `ExperimentBlocks`, `JsonNumberStepper` | Session, sequence, detailed controls, raw JSON, precise spin arithmetic and setup launch actions |
| `ConfigurationSchemaBuilder` (Editor) | Runtime-type schema generation before builds and on request |
| `ReplayController`, `ReplayFolderPicker` | Folder entry, common timeline, UI/views and native folder dialog |
| `ReplaySessionArchive` | Saved file/hash resolution, rig samples and explicit missing/ambiguous evidence |
| `ReplayAgentStream` | Disk-indexed recorded population frames for seeks |
| `ReplayEnvironmentLoader` | Supported saved Choice/design objects and camera/environment specifications |

Browse the [complete runtime source](https://github.com/pvnkmrksk/matrexVR/tree/main/Assets/Scripts) and [Editor builders/validation](https://github.com/pvnkmrksk/matrexVR/tree/main/Assets/Editor). The tables describe the current experimental path; third-party demo scripts are not alternative authoritative config parsers.
