# Unity experiment studio

The Config UI Test integration provides a working runtime UI Toolkit experiment studio to `ControlScene`. Experiment execution remains inside the Unity player; Python plotting and analysis remain optional. A desktop Unity release includes the UI and its JSON schemas without a Python installation, uv, browser server, or second application.

## Try it

In Unity 6000.3.16f1, choose **Tools → Experiment setup → Open setup screen (Play)**. This opens `ControlScene` and bypasses auto-start for this preview without editing any configuration file. Leave Play mode to end the preview.

Normal players honor the sequence's existing `autoStart` setting. Ship `autoStart: false` for a release that opens setup on double-click. `-configure` overrides auto-start when launching an existing player; `-no-configuration-ui` enables the original control form for legacy validation workflows.

The studio opens on **Session setup**. Experiment name, experimenter and overall comments sit above a card for each configured VR. Animal IDs are arbitrary text; the per-rig Species field writes system `speciesName` and metadata `SpeciesName`; sex starts at **Unspecified**, and an empty age remains unspecified. Each subject has its own comments, with starvation details behind the subject settings icon. These values are saved in `AnimalMetadata.json` with the run and reloaded when returning to setup; a transitional `FlyMetaData.json` copy supports old analysis. Hardware rig IDs remain the system's routing keys; the animal's Animal ID is its free-text label. Existing metadata extension properties are retained.

**Auto start**, **Loop** and **Randomize** are direct icon buttons with explicit ON/OFF indicators. They edit the original sequence flags and are also visible in the sequence editor. Auto start controls the next launch; the current setup session starts with **Start experiment**. A clickable system strip shows tracking modes, panel count/resolution/order, tracking input addresses and telemetry output. A compact virtualized **Coming up** list shows the current sequence, effective block timing and a stimulus summary. Longer plans scroll within that list.

The header opens **Sequence config**, **System config** or **Experiment configs**. The last lists only referenced files in the current sequence, with the number of blocks using each. **Session** returns to the simple screen while retaining unsaved labels, notes and config edits. Detailed editors expose full settings; **More → All parameters / Edit JSON** provides the complete document tools without putting them on the landing screen.

In **Sequence config**, blocks are virtualized cards showing their config path, environment, exact effective timing and stimulus/movement summary. Use the native drag handles to reorder, the copy icon to duplicate, or the bin icon to remove a block. Click a card for its environment, outer duration, gain, auto-trim and reload controls. A phased config owns its timeline, so the retained outer duration is visibly disabled in the builder; it remains editable under **All block settings** or JSON.

**Add experiment block** opens a searchable file library. Filter by paradigm, inspect timing/effects and phase states, then add the selected complete config in one action. Current configs, templates, examples and custom JSON are listed. The **Archive** category loads historical files on demand. A custom relative or absolute JSON path can be loaded from the same dialog. System and sequence files are excluded from individual block selection. The environment is suggested from the file and remains selectable from the compatible scene family. Replacing a block changes its scene/reference while retaining existing gain, duration, reload/trim flags and extension fields.

**Edit config** opens the referenced document directly. Essentials exposes common controls; Motion, Stimulus, Sky and Tuning group the rest on the same page. Pre/stimulus/post phase tabs show their on/off state and duration. Missing optional phases are displayed without adding JSON; the explicit Add action creates them. Enabling a phase with an omitted duration inserts its displayed 10-second authoring default so it can run. Disabled phases keep their values. Native vector icons, illuminated ON/OFF switches, XYZ controls, color swatches and sliders make values visible. **All parameters**, **All fields**, and **JSON** retain access to every stored and schema-defined setting, including extension keys.

Number entries retain their exact JSON text. Sliders are convenience ranges and never clamp a value simply by displaying it. Numbers outside a slider's range remain editable and visibly marked. Explicit slider changes use a documented step and synchronize the exact entry. Pending slider positions flush before navigation, undo, save and Start. Ports, IDs and values without meaningful slider ranges retain number entries.

The run overview shows exact total timing, heading-assessment time, loop/order state and effective config effects. Sky summaries follow uniform-color/panorama/procedural precedence and distinguish live from fixed sky. Disabled or absent phases are marked inactive. System overview shows input modes, frame synchronization, display, configured camera faces, telemetry and overview state. Configs shared by multiple blocks are explicitly labelled.

**Make an independent copy** on a selected sequence block creates a complete variation file and relinks only that block. Undo restores the original reference; the newly created file remains on disk. **Save a variation** in the sidebar copies the current document. Existing files are never overwritten by these creation actions. Absolute paths are supported; relative experiment/data references resolve against `RuntimePaths.ConfigRoot` (Editor: StreamingAssets; standalone: writable profile), matching the runtime loaders. **Open a file** loads a different primary system/sequence or standalone config.

**Edit JSON** exposes the complete document. Apply a draft to synchronize the form, or discard it. An unapplied draft blocks navigation, save and start. **Save setup** writes changed loaded documents and a metadata draft under `RunData/Backup`, leaving completed-run metadata intact. **Start experiment** validates the active file graph, saves changes, archives a fresh session using the selected system and sequence paths, copies metadata into that new run folder, and starts the existing runner. Session metadata includes every configured VR even when subject details were not edited. Non-looping runs started here return to session setup; Escape also returns from a running experiment. The next run gets a separate data folder.

## JSON parity

The editable source of truth is a Newtonsoft `JToken` tree, rather than a deserialized C# config object. Unknown properties and arrays remain editable. Exact number lexemes are retained, including long integers, scientific notation and decimal values beyond floating-point precision. Numeric controls use delayed text commits to avoid float-widget coercion. Missing, null, false and zero are distinct values. Removing a property restores omission; the ∅ button sets an explicit null.

A save with no value changes preserves the original bytes, including BOM, indentation, whitespace and line endings. Edited files use pretty-printed UTF-8 JSON with the original property order and preserved untouched numeric lexemes. Whitespace is not preserved after an actual edit. Duplicate property names and nesting deeper than 128 levels are rejected explicitly rather than silently losing data. Known parameters must fit their runtime types before starting; arbitrary extension numbers are preserved without being interpreted as experiment settings.

External changes are checked once per second while setup is active. Clean documents reload into the form. A changed file with an unsaved local edit produces a conflict and cannot be overwritten. Copy the draft from the JSON view before using **Reload disk**. Each file is replaced atomically with a `.bak` of its previous version. Saves across multiple files are not a filesystem-wide transaction. Undo/redo keeps the latest 32 editing transactions. A block reorder is one transaction and moves the complete JSON object, including unknown fields.

## Schema and extension points

`Assets/Resources/ConfigurationUI/schemas.json` contains JSON Schema Draft 2020-12 documents for system, sequence, Choice, Swarm, Kannadi, Optomotor and Dynamic Choice. `ConfigurationSchemaBuilder` generates definitions from the Newtonsoft serialization contracts used by the runtime loaders, including inherited fields, converter unions, private Dynamic Choice types, and shared phase/auto-trim fields. It regenerates before a player build; use **Tools → Experiment setup → Regenerate JSON schemas** after changing config types.

The runtime validator implements the keywords emitted by the generator: `type`, `enum`, `required`, `properties`, `items`, `minItems`, `minimum`, `maximum`, `exclusiveMinimum`, and permissive additional properties. It also runs the existing production validators for cross-field rules. It is not a general-purpose validator for arbitrary third-party JSON Schemas.

The generator's `x-ui` annotations define conditional visibility, enablement, help text, units and soft slider ranges. Compound enable rules cover pose reset plus random-heading conditions. Per-rig poses do not inherit nonexistent sibling flags. Current examples include:

- Kinefly/FicTrac input and flight terrain fields; walking ball diameter hides in Kinefly mode.
- Disabled phases, manual controls, telemetry, overview and sky blocks grey their dependent controls while retaining their enable switch and saved values.
- Autopilot and animation dependent settings; software FPS cap under vSync.
- 2D/3D swarm elevation, scene/Bogong visuals, dot/moth dimensions, world/angular sizing, recorded playback, and continuation controls.
- Auto-trim and flight-check tuning; time/area Dynamic Choice triggers.

Inline Swarm/Kannadi sequence overrides use their scene's field definitions and conditional rules. Autopilot and heading settings stay owned by the experiment file, as required by the current loaders. Changing modes hides controls without deleting values. Disabled contents remain accessible in JSON.

To add an ordinary parameter, add it to the relevant runtime config type and regenerate schemas. Existing scalar/container renderers handle it automatically. Add optional annotations in `ConfigurationSchemaBuilder.Annotate` for mode-specific rules or units. For a new scene/parser, register its schema type and extend `ConfigSchema.ForScene`; keep loading and validation aligned with that parser.

## Performance and scope

The sequence board, landing-screen plan and file library use fixed-height virtualized `ListView` cards. The board supports native reordering. The full-field editor also mounts only visible rows. Nested objects/arrays render when opened. There is no workbench `Update` method. At start the `UIDocument` is disabled immediately and its scene-owned host and `PanelSettings` are destroyed; there is no configuration form, polling, or binding during the run. Setup is recreated only on return to `ControlScene`.

The flight dashboard automatically suspends its panel and refresh work while setup is open, retaining its runtime visibility preference. Tab remains available for navigating the setup form and toggles the flight dashboard during a run. Dashboard diagnostics and optional telemetry keep their separate runtime lifecycle.

The UI and JSON arrays support configurable rig/sequence counts. Current source provisions configured rigs from authored scene templates before their tracking components start, and disables absent rigs. Each isolated population consumes a Unity rendering layer; exceeding the available layer capacity produces a configuration error. Hardware IDs remain `VR` followed by a positive integer. See the 0.3 release verification for tested scene/count combinations. Versions 0.1/0.2 retain the authored VR1–VR4 engine limit.

Current desktop source uses `RuntimePaths.ConfigRoot` and `RunDataRoot`: writable user-profile Config/RunData directories in players, existing StreamingAssets/RunData directories in the Editor. Seeding copies missing files and never overwrites a user config. `--config-dir` and `--data-dir` choose explicit roots. Versions 0.1/0.2 retain application-relative storage. WebGL/mobile storage, hardware display calibration and physical tracker timing remain outside this workflow. File selection uses an in-app path field/catalog rather than a native OS file dialog.

## Validation

`ConfigurationUIValidation.Run` requires a disposable project with `KANNADI_VALIDATION_COPY` at its root. Run graphics-enabled Unity with `-batchmode -projectPath <copy> -executeMethod ConfigurationUIValidation.Run -logFile <log>` without `-quit`; the runner exits after its asynchronous checks.

The suite checks every leaf of all current templates/examples, byte-identical no-op saves, raw/form round trips, numeric precision, external conflicts, conditional controls, 10,000 sequence rows, actual UI callbacks and rendering, selected non-default paths, metadata capture, and two complete experiment transitions. The [base validation report](configuration-ui-validation-results.json) covers the full-field and archive checks. The [studio validation report](configuration-studio-validation-results.json) also records session defaults, icon button state, navigation persistence, metadata extensions, dashboard suppression and actual Input System Tab presses, alongside library insertion/custom paths, native drag events, duplicate/remove/replace/variation/undo, numeric controls, absent phases, 10,000 block cards and repeated run teardown. [Session preview](images/config-ui-session.png), [sequence preview](images/config-ui-studio-sequence.png), [settings preview](images/config-ui-studio-settings.png), [system preview](images/config-ui-studio-system.png), and [library preview](images/config-ui-studio-library.png) are rendered by the actual runtime panel using synthetic validation sequences. `ConfigurationStudioValidation.Run` uses the same disposable-copy marker and invocation pattern.

Unity's existing editor search-indexing exception is excluded by its specific stack trace; runtime UI/experiment errors fail the suite. The config-template checker and run-archive regression suite are also run. Runtime UI Toolkit input integration follows [Unity's runtime event-system guidance](https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-runtime-event-system).

## 0.3 native GUI verification

**2,946 native checks passed** with no errors. Fresh Full HD captures show the current animal/species Session screen, sequence/system/settings panels, raw JSON, all fields and replay launcher. See the [0.3 GUI report](configuration-dashboard-v0.3-validation-results.json), [capture manifest](images/dashboard-v0.3/provenance.json), [Session](images/dashboard-v0.3/session-1920.png), [raw JSON](images/dashboard-v0.3/raw-json-1920.png) and [replay launcher](images/dashboard-v0.3/replay-launcher-1920.png). These are actual graphics-enabled runtime UI Toolkit captures with synthetic validation animals, not live collection data.

## Current-source ecosystem UI after 0.3

The [ecosystem GUI report](configuration-dashboard-ecosystem-validation-results.json) passed **2,970 checks** with no runtime errors, including modern backend labels, explicit optional tracker launch controls, metadata/JSON/Undo/navigation and readable full-width selectors. [Fresh current-source Session](images/dashboard-ecosystem/session-1920.jpg) and [capture provenance](images/dashboard-ecosystem/provenance.json) use synthetic animals, not camera acquisition. These additions are separate from the immutable 0.3 player. See [native tracker integration](tracker-integration.md).
