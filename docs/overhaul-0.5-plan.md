# Experiment and replay overhaul — requirements and delivery plan

Requested 29 October 2026. Baseline: `72c772b0` (0.4 plus local pacing evidence).
This is the implementation checklist and handoff record. Checked items are implemented and software-validated at the scope in the linked reports; physical apparatus acceptance remains separate. Unchecked items are not shipped claims.

## Purpose and priorities

An experimenter should configure and run a packaged application without rebuilding Unity: select a world, objects, skies, tracked individuals and a named experimental sequence. Saved runs must retain the evidence needed to reconstruct the stimulus, and replay must never silently change acquisition data. Replay animation is the first priority. Supported apparatus size is **1–16 VRs**, with explicit acceptance at **1, 2, 4, 8 and 16**. Other counts use the next display-layout capacity with unused slots hidden; above 16 is out of scope.

## Design decisions

**Greenfield clarification:** the governing replay requirement is now the [same-build replay contract](same-build-replay-contract.md). New builds must replay all supported experiments they record; older/different-build recordings are optional. Historical fallbacks do not constrain the new journal, controller sharing or state-cache design. The original historical-compatibility notes below describe the earlier pass, rather than a requirement to keep every old replay working.

* Reuse the actual scene geometry, models, materials and animation engine in replay; feed saved poses and animation evidence into them. Do not run live tracker input, loggers or acquisition decisions during replay. A seed cannot recover historical unrecorded input, frame timing or arbitrary stateful scripts; identify such limits explicitly.
* Keep CSV as the version of record. Additional columns/sidecars are additive. Derived replay indexes/caches belong outside source recordings, are disposable, and must not overwrite CSVs. Explicit media-import actions remain distinct from ordinary replay.
* Use one reusable six-face VR prefab and runtime capacity provisioning to 16, with editor inspection/baking support where useful. Avoid sixteen independently maintained variants. Active VR cameras hide their own bodies and see other active bodies; God's-eye replay renders each individual once.
* **Kannadi** is the single population/mirror example entry point. Preserve old Swarm JSON/scene references through an explicit compatibility route rather than silently changing old recordings. Swarm generation remains a reusable stimulus feature in other worlds. Kannadi and Choice are paradigms; worlds provide terrain/scenery.
* External object interchange starts with portable glTF/GLB (embedded animation/textures where supported), not arbitrary Unity asset-store projects, scripts or FBX import at runtime. Unity packages that need editor import still require conversion. Archive imported dependencies and hashes with the run, and resolve archived copies during replay.
* Random/fixed experiment seed selection must save the resolved seed before stochastic configuration. Separate repeatable stream keys where necessary. Record realized poses/animation and event outcomes as stronger evidence than a seed alone.

## P0 — faithful replay and reproducibility

- [x] Restore original prefab animation during replay and scrubbing, including reverse seeks, pause and speed changes. Capture animation state/time for new recordings; clearly label approximation in legacy recordings.
- [x] Preserve procedural moth flap/flicker appearance using recorded experiment time/state, not replay wall time.
- [x] Restore world, phase, object, sky, material, dynamic event and camera configuration from archived evidence. Never substitute a current working config for an ambiguous old snapshot.
- [x] Fixed/random seed option, explicit integer seed, resolved seed in run output; scope stochastic initialization and document limits of determinism.
- [x] Keep acquisition CSV/config/model files unchanged when merely loading, playing or seeking a recording; validate hashes before/after.
- [x] Validate record → replay → forward/backward seek using actual Unity rendering and animation state.

## P1 — experimenter-facing configuration and 16 VRs

### Names, sequencing and continuity

- [x] Optional human-readable paradigm/sequence-config title, sequence-row title, experiment-config title and comments. Preserve legacy filename-only JSON.
- [x] Show names in config editor, session overview and replay; show the immutable relative config path in faint read-only text, including nested folders beneath the config/StreamingAssets root.
- [x] GUI creation/editing of pre-stimulus, stimulus and post-stimulus using identical category layouts, explicit enable controls, durations and plain tooltips.
- [x] Shuffle/repeat a full pair/triplet as one experimental unit. Support grouped sequence rows where phases span files. Disabled phases do not execute.
- [x] Continue the same scene, tracked pose and population across phases when reset is false. Independent position/orientation reset true restores authored initial pose. Entrainment can serve as pre-stimulus before the Kannadi condition.
- [x] Preserve direct JSON operation, unknown fields, inherited values, disabled values and existing undo/redo behavior.

### VR and visibility

- [x] Provision 1–16 rigs in every supported example world from a shared six-camera rig. Faces retain labels and 90-degree projection; system face selection remains honored.
- [x] Validate unique rig identity/input/display routing, active/inactive state, culling layers and self-hidden/peers-visible Kannadi bodies at 1/2/4/8/16.
- [x] System GUI supports all 16 rigs, keyboard navigation and one action to advance individual identifiers by the configured population size, with undo through existing document history if practical.
- [x] Deprecate Swarm in new scene selectors/starters; migrate supplied population recipes to Kannadi with legacy input compatibility and documented behavioral units.

### External objects and world appearance

- [x] Choose built-in or external objects; set enabled, scale, position and orientation. Preserve embedded animation. Reuse existing Choice/band/mirror capabilities.
- [x] Archive externally loaded object bytes/dependencies and provenance in saved runs; replay uses those archived assets.
- [x] Organize world examples: Optomotor; flat/desert base with ground contrast; forest floor; dense forest; real-world terrain. Reuse suitable existing assets/worlds before introducing duplicates.
- [x] Configurable directional sun for all experiments: enabled, intensity, color/hex and orientation. Directional light position is irrelevant.
- [x] Four sky modes: Unity default, uniform color, custom panorama, astronomical stars. Explicit enable controls and inactive controls gray out.
- [x] Night sky latitude/longitude, fixed local time plus UTC offset or current time; current time disables the fixed-time field. Preserve generated sky images/provenance.
- [ ] Optional panorama azimuth rotation, default zero; normalize bounded angles.

### Replay controls and readability

- [x] God's-eye view, original animal view, original pixel-resolution view and detail preview; aspect-preserving resize.
- [x] Middle-drag pan, robust near clipping, smooth forward/backward seeking and bounded media frame cache.
- [x] Two trail controls: duration and prominence (not a gallery of technical parameters).
- [x] Third-person follow selected animal; click empty background to leave follow. Placeholder body where the original world has no focal model.
- [x] Run/sequence/experiment titles and a screenshot thumbnail in the recording library.

### GUI and operation

- [x] Default dark palette independently of OS preference; optional explicit light selection only if implemented consistently.
- [x] Related controls together, category tabs/foldouts, clear enable switches, disabled controls gray, concise explanatory tooltips, spacing and legible labels.
- [x] Collapsible sidebar, keyboard navigation and existing AI/automation APIs retained. Document actual machine interfaces; a new general MCP service is a separate integration.
- [ ] Double-click packaged launch, config selection, monitor mode and existing tracker connections continue to function. Do not constrain monitor count below the general 16-rig limit without an actual technical reason.

## P2 — follow-up work, ordered after core validation

These remain explicit work items when they cannot be validated in this pass; this list is not an assertion that they exist.

1. Object-picker rendered thumbnails and edit-only skybox preview using Unity's renderer, with no preview clutter during normal loading/acquisition.
2. Formal cross-platform runtime-model support matrix (external textures, material extensions, skeletal/morph clips) and packaging of assets bought from vendors.
3. Configurable **visibility matrix** mapping which VR sees which VR; default remains all peers, never self. This was explicitly requested as a much later feature.
4. Read-only replay acceleration/index persistence and bounded video caching, benchmarked on large datasets before choosing memory budgets.
5. End-to-end acknowledged bidirectional start/stop recording with **HejjéTrac and Rekkefly**, including failure/timeout behavior; requires compatible tracker implementations and physical acceptance. Preserve existing protocol limitations in docs until proven.
6. Broader MCP-client/accessibility acceptance; the opt-in local stdio MCP surface is implemented in this pass.
7. Native UI stress tests, screenshots, frame pacing and apparatus acceptance at all primary counts; platform walkthroughs and physical LED/tracker verification.
8. Further visual UI overhaul after functional controls stabilize.

## Branch and reuse audit

`git fetch origin --prune` completed before implementation. The current branch is two evidence commits ahead of `origin/main`/`v0.4.0`. Kannadi modernization, night sky, configuration studio and release-modernization tips are already ancestors. The ecosystem/reset audit commit is patch-equivalent to merged work (`git cherry` reports `-`). Historical replay-architecture changes are older/different implementation and must not replace current replay wholesale. Navrug, shade calibration and terrain-cache branch changes need a targeted feature comparison rather than blanket merges. Existing unrelated untracked Unity `.meta` files are preserved.

## Parallel ownership and integration order

1. **Plan**: commit this requirements list and README link before implementation.
2. **Replay agent**: replay Animator evidence/restoration, procedural animation, follow/trails/camera controls and replay-specific checks.
3. **Configuration agent**: titles/path labels, metadata compatibility, phase creation/grouping, dark UI, seed selection/scheduling, config checks.
4. **Rig/world agent**: shared rig capacity/visibility, Kannadi compatibility, build/scene recipes and rig checks.
5. **Primary agent**: external-object loading/archive/replay integration, shared world lighting, branch audit, integration and validation. Coordinate touched files with agents first.
6. Integrate all changes; regenerate relevant schemas/reference only after model changes settle. Run focused static checks, Unity compilation and targeted runtime/render checks in a disposable project.
7. Update this checklist with verified results and explicit remaining items, then build a local test application if compilation/runtime checks pass. No publication or apparatus equivalence is implied.

## Configuration implementation status

Implemented in source; integrated Unity/render validation is pending unless the acceptance report below says otherwise:

- Optional `title` and `comments` for the paradigm, individual sequence rows and experiment files. The editor/session overview use human titles with filename fallback; nested paths remain read-only beneath the title. Replay-library details show the saved paradigm and block titles when available.
- Explicit flat-to-phased conversion in the experiment editor; optional phases initially remain off. Existing heading observations move into an enabled pre-stimulus phase with their observation duration. All phases use the same settings tabs; the continuity switch controls stimulus/post position and rotation resets. A phased config remains one shuffle/loop unit. Optional contiguous `group` names are editable per outer row.
- Fixed/random seed controls with the integer field disabled in Random mode; the primary agent owns resolved-seed recording and scheduling.
- The apparatus editor accepts 1–16 rigs through one numeric count field, with unique identity validation and undoable count changes. Quick-count buttons were removed. Two vector icon buttons select LED panels or monitors. Animal IDs can advance by the current population size (including numeric suffixes and zero padding); a dedicated Undo ID increment action restores the previous labels.
- Frame-rate caps default to 60 FPS. Clicking the readout opens the exact setup setting or a live editor during acquisition; live changes append timestamped evidence and retain the archived initial config. Save changes stays disabled while clean, enables for config/metadata edits and pending delayed input, and commits those buffers before persisting. Advanced overlay controls expose the physical config path, automatic/manual placement and disabled-state behavior. `SetupUsabilityValidation.Run` passed all 28 production GUI checks in Unity 6000.3.16f1; screenshot review confirmed a readable layout and matching current cap readouts.
- Setup, apparatus and recording-library styles default to dark independently of OS appearance. Existing keyboard navigation, collapsible navigation rail, native Editor/runtime color+hex pickers, category tabs and disabled feature sections remain in use.
- Four explicit sky choices use the existing runtime fields. Panorama selection checks a local path before applying. The night-sky editor offers current/fixed time, disables the fixed-time field while current time is selected, and offers UTC offsets including fractional-hour zones. The Sky tab exposes optional sun settings.
- External-model fields list embedded GLB files found under the config/StreamingAssets root and retain a direct path field. Generic object arrays still use the existing item editor. Runtime model importing/archiving and scientific replay fidelity are validated separately.
- The recording library reads a saved PNG thumbnail with byte/pixel limits and releases its texture when selection changes or the library closes. It never writes into the recording.

Remaining UI work: rendered object/sky picker previews; a broader visual/layout redesign; and native pointer/keyboard acceptance across all primary counts and platforms. An opt-in local MCP interface and five-world sequence catalog are now included. There is no whole-session metadata undo chain; animal-number batch changes have their own undo action. Changing sky modes is an explicit undoable configuration edit and may clear inactive panorama/color values; disabled phase and nested enabled-feature values are retained.

Focused non-rendered regression checks passed during implementation: 55 apparatus profile checks; 7 sequence selection checks; 18 primary action checks; 16 pointer refresh checks; 15 recording-library UI checks; and 16 focus recovery checks plus 4 lifecycle guards. These use production helper/method code with controlled UI/native boundaries; they do not establish native input or rendering acceptance. `OverhaulConfigurationValidation.Run` supplies the new disposable-project parser/form/render checks and captures.

## Acceptance evidence

See [greenfield same-build validation](same-build-validation-20261029.md), plus [29 October validation and remaining limits](overhaul-validation-20261029.md) and its [machine-readable report](overhaul-validation-20261029.json). Core replay animation, GLB import/archive, seeded/grouped sequencing, dark configuration, titles, sixteen-rig setup, live phase continuity, five packaged world starters and local MCP control are implemented and checked at the scopes recorded there. This checklist remains the complete requirements inventory; composite requirements that include unverified or deferred parts remain unchecked.
