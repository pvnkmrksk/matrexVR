# Same-build replay contract

This is a greenfield version. The release requirement is that **a given build can replay every supported experiment recorded by that build**. Replaying older or different builds is optional, and must not constrain the design of the new recording format. Backward-readable configuration files are useful where they remain simple; they are not the governing replay requirement.

## One experiment implementation

Acquisition and replay apply the same complete configuration through the original scene/paradigm controllers. A newly added config variable must not require a second interpretation of that variable in a replay-only field whitelist. The saved configuration bytes remain intact, and each presentation also records the effective payload, sequence parameters, authored row identity, phase, seed/RNG initialization state and build identity that actually produced it.

The tracker is replaced by recorded input/poses. Live acquisition, recording writers, tracker workers and experiment-control decisions must remain disconnected during replay. Recorded outcomes, material/animation state and events override decisions that depended on measured input or serial evolution. Replay changes the operator camera/view, resolution and playback time; it does not edit the saved experiment.

## Identity and evidence

A versioned recording contract declares its required evidence. New-contract recordings are checked against the creating player build; a mismatched build, missing journal, corrupt model/config snapshot or unsupported required state is a compatibility/completeness error, rather than an unnoticed partial replay. Embedded build identity comes from the actual player, not a mutable installed config directory.

Raw system, sequence and experiment JSON, loaded models, custom skies and referenced dependencies are archived with hashes. Effective per-presentation data resolves inherited defaults, phase selection, sequence-level overrides and shuffled row identity. Fixed seeds and resolved random seeds are recorded, along with the initialization RNG state needed when original configuration code creates stochastic objects.

## Time and seeking

Most visible state can be sampled directly: recorded rig/population poses, sky image, configured geometry, sun, original animation clips and realized renderer state. Stateful effects such as color drift, motion integration or an input-driven trigger cannot always be recovered by setting a seed and jumping to a later time. Preserve their realized outcomes or checkpoint their original state so backward and forward seeks do not depend on the replay's previous playhead position.

Preprocessing and indexes/caches are allowed for serial state and video. They are disposable and stored outside the recording. Static state should be shared/deduplicated, and seeks should use indexed checkpoints rather than repeatedly integrating from frame zero. Runtime render clocks must use recorded experiment time, including procedural animation/shader clocks where supported.

Future components with additional state need an explicit capture/apply/seek extension hook. Adding a component or shader that cannot be reconstructed must produce a clear unsupported-state result until its hook and record/replay test are supplied. A seed alone does not promise identical measured input, physics, arbitrary scripts or shader globals.

## Acceptance gate

Record and replay each supported paradigm with the actual build and archived assets. Compare the same rendered frames, including phase/config transitions, randomized repeated rows, serial effects, original animations, sky/lighting and world placement. Seek in both directions, pause and change playback speed. Verify source recording hashes before/after. Reject a deliberately changed build identity and incomplete new-contract evidence.

Historical reconstruction tests can remain as optional utilities, but do not replace this gate. The exact implemented journal/state schema and tested scope are recorded in the current validation report.

## Current implementation

`replay_contract.json` identifies the exact acquisition player and schema. `presentations.jsonl` hashes the complete effective controller inputs, authored row, phase, rig initialization poses and RNG checkpoint. `world_state.jsonl` hashes every rendered-frame identity and target membership; complete target checkpoints are emitted only when their captured state changes. The read-only reader indexes those checkpoints and uses a bounded 16-row / 32 MiB serialized-payload cache. Rig, population, animation and world evidence use one Unity frame identity. Playback duration/order use the pose CSV's monotonic unscaled clock, while wall timestamps remain metadata; operating-system clock changes cannot reorder frames. Event callbacks queue metadata into the final pose snapshot rather than creating competing rows in the same frame. A missing exact frame stops authoritative playback.

The original Choice, asynchronous-choice and optomotor controllers configure replay from archived inputs. Actual adaptive steps and blank transition frames are checkpointed. Their public fields are serialized without recursive computed Unity properties and preserve null branch semantics. Each presentation also records resolved population model/visual defaults, including selections preserved across phase changes. Animation transitions also carry realized local bone transforms and blendshape weights. Native procedural drum texture updates run through their original controller. New variables on those controller paths flow through their full saved configurations. Serial extensions implement `IReplayStateParticipant` on a recorded target; same-type components are distinguished by their ordinal.

Acquisition and replay use the same explicit shader-clock vectors at camera and directional-shadow passes, restoring the engine clock after drawing. Live native WindZone integration and particle internals require an explicit recorded seek-state owner; acquisition rejects them when absent. The unowned `tree01_windy` asset is omitted from the object picker. Non-windy trees, original animation controllers and baked GLB animation remain available.

Replay requires finalized, drained evidence with no dropped population frames. The finalized run directory records each closed population writer's byte count and SHA-256. Stop checks those receipts and footer counts before publishing the manifest. Replay independently verifies the required pose CSV and population stream inventory, byte lengths and SHA-256 before playback. The `.matpop` population reader seeks by frame in the original stream. Older CSV population caches and indexes are disposable. When population recording was explicitly skipped, replay shows the recorded animal and environment without virtual population tracks. Missing models/scenes, changed journal hashes, unsupported state and foreign builds are errors. Editor recordings are scoped to their runtime session. Acquire portable recordings with the saved player build.
