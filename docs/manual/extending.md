# Extend the source

Use the existing JSON controls when the experiment differs only in parameters. Add or modify Unity scenes/assets/controllers when the environment or behavior itself changes. MATREX VR intentionally does not provide a general level editor.

## Add a recipe first

Copy a complete compatible experiment and sequence example, retain its subdirectories, and point a block's configFile or design to the new file. Set explicit tracking/reset/units choices. The studio can create an independent variation of a shared experiment without relinking every block.

Use the [configuration reference](../configuration-reference.md) to distinguish omitted defaults from template example values. Do not infer support merely because the lossless editor retains a new extension key: a production parser must consume it to affect rendering.

## Add an ordinary parameter

1. Add the field/property to the runtime configuration type used by the owning parser, with a clear default, units and finite/range validation.
2. Apply it in the owning controller at the appropriate initialization/phase boundary.
3. Regenerate configuration schemas using ConfigurationSchemaBuilder. Add help/conditional UI annotations when they clarify behavior.
4. Add a complete recipe demonstrating the field and check its saved/telemetry/replay implications.
5. Verify that old omitted values preserve the intended behavior and that an actual edit is reversible in the shared document model.

Unknown fields survive editor round trips, but known fields must fit their runtime type before Start. Preserve an explicit compatibility alias only with a documented precedence rule; do not silently rewrite old acquisition files.

## Add a scene

Start from a compatible authored environment and preserve the rig/component pattern. Register the scene's controller in the orchestration path, associate its schema/parser in ConfigSchema where necessary, and enable the scene in the build settings. A config naming an unbuilt scene cannot make its geometry appear in the standalone player.

For efficient repeated conditions, implement `IInSceneSequencer.AdvanceStep` where supported. That method must fully reapply/reset the condition and release replaced populations/materials; skipping a Unity scene load is not permission to accumulate trial resources.

## Add prefabs, materials and agents

Use the owning scene/catalog's registered assets. Kannadi prefabs belong in the KannadiPrefabCatalog; Choice/dynamic content uses its own registered resources. Keep authored physical scale, pivots, culling layers and per-rig visibility explicit.

A new real species is metadata. It does not automatically select a virtual model, locomotion interpretation or tracker calibration. A new virtual agent visual is an asset/config choice. Preserve that distinction in UI labels and recordings.

## Add a tracker or adapter

Implement the [input contract](input-contracts.md), including wire shape, units, cumulative versus incremental/rate meaning and stale behavior. An adapter can normalize an external stream without making ZmqListener guess units from species names. Validate complete samples and keep socket ownership on the receive worker.

Do not reuse the outbound telemetry topic as a tracking input contract. If extending versioned telemetry, preserve semantic versioning of payload meaning and document angular conversions.

## Make recording and replay part of the change

Record original consumed files and the minimal dynamic state needed to interpret the experiment. Append compatible columns where possible; retain source identity/version and units. If the new stimulus cannot be reconstructed from current recordings, surface a fidelity warning in replay rather than substituting today's defaults.

For large populations, use bounded worker queues and seekable indexed playback. Preserve discontinuities and use a shared acquisition timeline across animals.

## Validate on a disposable copy

Many Unity validators intentionally replace test configs and create synthetic sessions. They require `KANNADI_VALIDATION_COPY` at the disposable project's root. Do not run them against a live authoring/acquisition configuration.

Run the relevant static template/asset checks, production JSON/replay contract checks and native scene/UI/resource tests for the changed path. A test that synthesizes input or shortens trials does not replace a physical apparatus bench check. See [release evidence](../releases.md#software-verification) and [lifecycle](../scene-lifecycle.md).

## Build and distribute

Use the pinned Unity version and required desktop modules. The source build helper prepares a revision, public hardware defaults, manifests, hashes and complete archives; it does not publish them. Custom-source builds should retain a distinct version/source identity so recordings can identify their renderer.

This documentation can be rebuilt with the manual builder described in [the documentation source](https://github.com/pvnkmrksk/matrexVR/tree/main/docs/manual). Static HTML is committed for GitHub Pages; readers do not need Unity, Pandoc or a server-side renderer.
