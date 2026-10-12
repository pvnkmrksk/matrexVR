# Unity experiment setup prototype

The `codex/config-ui-test` branch adds a working runtime UI Toolkit setup screen to `ControlScene`. Experiment execution remains inside the Unity player; Python plotting and analysis remain optional. A desktop Unity release includes the UI and its JSON schemas without a Python installation, uv, browser server, or second application.

## Try it

In Unity 6000.3.16f1, choose **Tools → Experiment setup → Open setup screen (Play)**. This opens `ControlScene` and bypasses auto-start for this preview without editing any configuration file. Leave Play mode to end the preview.

Normal players honor the sequence's existing `autoStart` setting. Ship `autoStart: false` for a release that opens setup on double-click. `-configure` overrides auto-start when launching an existing player; `-no-configuration-ui` enables the original control form for legacy validation workflows.

Choose **System & VR rigs**, **Experiment sequence**, or a referenced experiment file on the left. Containers open on demand; **Parent** returns to their containing object. Arrays support add, copy, delete, and reorder. Missing fields appear under **Omitted fields** and are inserted only when explicitly selected. **All modes** reveals fields hidden by the selected mode.

The file picker includes every current template and example. **Create editable copy** copies the selected document to a new filename; it refuses to overwrite existing files. For an experiment copy, update the relevant sequence row's `parameters.configFile` or `parameters.design`. System and sequence copies become the selected primary configuration. Absolute paths are supported; relative experiment/data references still resolve against `StreamingAssets`, matching the existing loaders.

**JSON** exposes the complete document. Apply a draft to synchronize the form, or discard it. An unapplied draft blocks navigation, save and start. **Save all** writes the loaded documents you changed. **Start experiment** validates the active file graph, saves changes, archives a fresh session using the selected system and sequence paths, and starts the existing runner. Session metadata is saved with the run even when its tab was never opened. Non-looping runs started here return to setup; Escape also returns from a running experiment. The next run gets a separate data folder.

## JSON parity

The editable source of truth is a Newtonsoft `JToken` tree, rather than a deserialized C# config object. Unknown properties and arrays remain editable. Exact number lexemes are retained, including long integers, scientific notation and decimal values beyond floating-point precision. Numeric controls use delayed text commits to avoid float-widget coercion. Missing, null, false and zero are distinct values. Removing a property restores omission; the ∅ button sets an explicit null.

A save with no value changes preserves the original bytes, including BOM, indentation, whitespace and line endings. Edited files use pretty-printed UTF-8 JSON with the original property order and preserved untouched numeric lexemes. Whitespace is not preserved after an actual edit. Duplicate property names and nesting deeper than 128 levels are rejected explicitly rather than silently losing data. Known parameters must fit their runtime types before starting; arbitrary extension numbers are preserved without being interpreted as experiment settings.

External changes are checked once per second while setup is active. Clean documents reload into the form. A changed file with an unsaved local edit produces a conflict and cannot be overwritten. Copy the draft from the JSON view before using **Reload disk**. Each file is replaced atomically with a `.bak` of its previous version. Saves across multiple files are not a filesystem-wide transaction. Undo/redo keeps the latest 32 editing transactions.

## Schema and extension points

`Assets/Resources/ConfigurationUI/schemas.json` contains JSON Schema Draft 2020-12 documents for system, sequence, Choice, Swarm, Kannadi, Optomotor and Dynamic Choice. `ConfigurationSchemaBuilder` generates definitions from the Newtonsoft serialization contracts used by the runtime loaders, including inherited fields, converter unions, private Dynamic Choice types, and shared phase/auto-trim fields. It regenerates before a player build; use **Tools → Experiment setup → Regenerate JSON schemas** after changing config types.

The runtime validator implements the keywords emitted by the generator: `type`, `enum`, `required`, `properties`, `items`, `minItems`, `minimum`, `maximum`, `exclusiveMinimum`, and permissive additional properties. It also runs the existing production validators for cross-field rules. It is not a general-purpose validator for arbitrary third-party JSON Schemas.

The generator's `x-ui` annotations define conditional visibility, enablement, help text and units. Current examples include:

- Kinefly/FicTrac input and flight terrain fields; walking ball diameter hides in Kinefly mode.
- Disabled phases, manual controls, telemetry, overview and sky blocks grey their dependent controls while retaining their enable switch and saved values.
- Autopilot and animation dependent settings; software FPS cap under vSync.
- 2D/3D swarm elevation, scene/Bogong visuals, dot/moth dimensions, world/angular sizing, recorded playback, and continuation controls.
- Auto-trim and flight-check tuning; time/area Dynamic Choice triggers.

Inline Swarm/Kannadi sequence overrides use their scene's field definitions and conditional rules. Autopilot and heading settings stay owned by the experiment file, as required by the current loaders. Changing modes hides controls without deleting values. Disabled contents remain accessible in JSON.

To add an ordinary parameter, add it to the relevant runtime config type and regenerate schemas. Existing scalar/container renderers handle it automatically. Add optional annotations in `ConfigurationSchemaBuilder.Annotate` for mode-specific rules or units. For a new scene/parser, register its schema type and extend `ConfigSchema.ForScene`; keep loading and validation aligned with that parser.

## Performance and scope

The setup screen uses a fixed-height virtualized `ListView` and mounts only visible rows. Nested objects/arrays render when opened. There is no workbench `Update` method. At start the `UIDocument` is disabled immediately and its scene-owned host and `PanelSettings` are destroyed; there is no configuration form, polling, or binding during the run. Setup is recreated only on return to `ControlScene`. Existing runtime diagnostics and optional telemetry keep their own lifecycle.

The UI and JSON arrays support arbitrary numbers of rigs and sequence entries. Current experiment scenes and several production controllers still author VR1–VR4, and Kannadi validates indices 1–4. Adding VR5 in the form does not create its scene cameras, prefab, tracking or experiment-controller support. This prototype makes the authoring model extensible; extending the experiment engine remains a separate change.

This is a desktop prototype using the project's existing file-based `StreamingAssets` and `Application.dataPath/RunData` conventions. A release must be extracted to a writable location. Signed/read-only app bundles, writable user-profile configuration migration, WebGL/mobile storage, hardware display calibration, and physical tracker timing have not been validated here. File selection uses an in-app path field/catalog rather than a native OS file dialog.

## Validation

`ConfigurationUIValidation.Run` requires a disposable project with `KANNADI_VALIDATION_COPY` at its root. Run graphics-enabled Unity with `-batchmode -projectPath <copy> -executeMethod ConfigurationUIValidation.Run -logFile <log>` without `-quit`; the runner exits after its asynchronous checks.

The suite checks every leaf of all current templates/examples, byte-identical no-op saves, raw/form round trips, numeric precision, external conflicts, conditional controls, 10,000 sequence rows, actual UI callbacks and rendering, selected non-default paths, metadata capture, and two complete experiment transitions. The committed [validation report](configuration-ui-validation-results.json) records counts and timings from the final run. [Preview](images/config-ui-preview.png) was rendered by Unity's runtime panel; the short sequence shown is a synthetic validation fixture.

Unity's existing editor search-indexing exception is excluded by its specific stack trace; runtime UI/experiment errors fail the suite. The config-template checker and run-archive regression suite are also run. Runtime UI Toolkit input integration follows [Unity's runtime event-system guidance](https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-runtime-event-system).
