# Releases and compatibility

[GitHub Releases](https://github.com/pvnkmrksk/matrexVR/releases) is the publication record. A local build or source change is not a published binary. The [website](https://pvnkmrksk.github.io/matrexVR/) discovers real uploaded ZIPs; it never constructs a download URL for an absent asset.

## 0.4 — terrain, recording and replay

Release target: [`v0.4.0`](https://github.com/pvnkmrksk/matrexVR/releases/tag/v0.4.0). Publication is established by the uploaded GitHub Release assets, not by this source document or a local build. The release manifests identify the exact source revision and packaged companions. Earlier 0.3 release descriptions below retain their historical scope.

- Four **TerrainChoice** spatial layouts share one environment prefab and two equidistant placeholder trees. Four ready-to-load treatment sequences independently select visible and navigation surfaces across all four layouts, covering sixteen scene/treatment combinations without sixteen scene copies. The shared centimeter recipe uses 1 cm eye clearance and texture scale 4; both-flat conditions deliberately look identical. Configured LED panel geometry and camera routing remain authoritative. See [terrain recipes](rugosity.md#load-one-treatment-and-run-all-four-spatial-patterns).
- **Initialize tracking** opens the independent tracker for calibration/input checks before the experiment. The setup and JSON retain the same values and inherited defaults. Choice configurations expose **Available objects & materials**, with exact-name Type/Material controls.
- Recordings default to **MATREX VR RunData beside each player**; protected installations use `Application.persistentDataPath/Installations/<installation-id>/RunData`. Configuration remains in `Application.persistentDataPath/Config`. Explicit `--data-dir` and `--config-dir` overrides remain supported. The recording library shows its root, discovers completed runs and opens external folders without moving them.
- Replay combines saved poses, trial context, supported scene reconstruction and available tracker video on one seekable clock. A compact transport, media card, disclosed media tools, bounded preview resizing and refined dialogs accompany colored rig markers, recent recorded trails, heading arrows, front-camera cones and offscreen labels. The God’s-eye camera supports orbit, pan, translation, projection changes and adaptive clipping. Animal-camera evidence retains its saved projection/aspect.
- The reviewed Mac arm64 Daari Deepa **1486b81e** payload adds linked recorded-trajectory plots and replay control. Older pinned Intel Mac, Windows and Linux dashboard archives lack that API/assets and report the capability limit. Unity’s internal pose/video replay is independent of the external viewer.

The compact build enables **14 scenes**: ControlScene, Choice, Choice_desync, Choice_Selwyn, Swarm, Kannadi, Optomotor, TerrainCueLab, Rugosity_RS, ReplayScene, TerrainChoice_RoughLeft_SmoothRight, TerrainChoice_SmoothLeft_RoughRight, TerrainChoice_BothSmooth and TerrainChoice_BothRough. Existing scene GUIDs remain compatible. Other forest/two-tree source examples still require enabling their scenes and rebuilding.

### 0.4 verification and known limits

The [local Mac review](replay-recording-mac-review-20261027.md) records actual native rendering and keyboard checks. [Terrain/video verification](terrain-tracking-recording-20261027.md) includes fresh native recordings and Unity import/decode/seek checks using generated or attributed prerecorded input. [Camera checks](replay-camera-review-20261027-results.json) passed 82 assertions; [preview resize/header checks](replay-preview-resize-20261027-results.json) passed 48 assertions at Full HD and supplied Retina scale, with actual production IMGUI events. They are distinct from native window input acceptance.

**Evenly paced 60 FPS remains unresolved.** High uncapped throughput does not establish uniform presentation intervals. The [pacing investigation](frame-pacing-20261027.md) reproduces engine/platform effects in minimal controls and records additional tails under the four-recorder workload. FPS measurement fixes retain real delayed frames and peaks; they do not certify LED presentation timing.

**Acknowledged per-experiment tracker recording Start/Stop, state reconciliation and manual override are not implemented.** Save tracker video requests recording when an eligible bundled tracker is launched. Recorders remain independent and may span multiple experiment runs; finish them normally, then collect completed media. A VR experiment stopping does not by itself stop its video recorder. See [video workflow](replay.md#record-and-replay-tracker-video).

Latest standalone pointer/drag acceptance is incomplete: native automation returned `noWindowsAvailable`, including after unlock. Native keyboard/rendering checks and injected Unity GUI-event tests passed within their separate scopes. The earlier 0.3.1 walkthrough does not establish acceptance of every changed 0.4 mouse interaction.

Windows/Linux archives are cross-built on Mac and have not received native execution walkthroughs. Mac packages are universal, ad hoc signed and not notarized. Only the reviewed Mac arm64 Rekkefly correction supports the strict camera path; older pinned packages on other architectures remain blocked for that path. Bundled Linux HejjéTrac requires **glibc 2.39 or newer**. Physical tracker calibration, LED scanout/exposure synchronization, representative long-run acquisition and hardware-specific behavior remain apparatus checks.

Build the pinned release source with both reviewed Mac payload options:

```sh
python3 tools/build_release.py --ref v0.4.0 --version 0.4.0 --platform all \
  --local-native-camera-fix \
  --local-dashboard-archive .integration/local-dashboard/DaariDeepa-macos-arm64.zip
```

The helper emits three complete OS ZIPs, per-archive SHA-256 files and embedded build/release manifests. It never uploads them. Hosted CI currently deploys only the static website; Unity desktop archives are built locally with the installed target modules. A published stable release is discovered by the website only after its actual ZIP assets exist.

## 0.1 — archived main

Source: [`779926a80b02190d6dd0410f260e52bba105a04e`](https://github.com/pvnkmrksk/matrexVR/tree/779926a80b02190d6dd0410f260e52bba105a04e), release tag `v0.1.0`.

Preserves the `main` runtime before the configuration studio and telemetry integration. Includes its historical Control metadata form, Choice, Swarm, Kannadi, Optomotor, replay and night-sky behavior. Packaging adds a public hardware starting template, build tooling and `START-HERE.txt`; runtime experiment scripts and JSON retain the archived revision.

The actual shipped sequence is manual-start `Choice_Selwyn`, 86,400 seconds per block, looping with `reloadScene: false`, using `Kannadi/selwyn-night-sky.json`. Its forward autopilot is 7 m/s, tracked orientation enabled, generated Selwyn sky at startup's current instant with fixed UTC+11 and 30-minute updates. The old revision's README described an earlier Swarm→Kannadi default; the JSON establishes actual startup.

The release keeps old metadata names, application-relative files and older replay/reset limitations. It does not include the later Tab dashboard or outbound experiment telemetry.

[Archived 0.1 shortcuts and launch guide](archival/START-HERE-0.1.txt).

## 0.2 — integrated Config UI Test baseline

Source: [`c13d33dd`](https://github.com/pvnkmrksk/matrexVR/tree/c13d33dd), release tag `v0.2.0`.

Preserves the already integrated Config UI Test branch: modern Kannadi, Bogong stars and moth/swarm behavior, generated/recorded swarms, configuration studio, live diagnostics/telemetry, recursive run snapshots and the updated terrain cue experiments. The branch already contains the Kannadi modernization, heading telemetry and Bogong night-sky histories; these are not additional disconnected feature copies.

Startup retains `autoStart: true` with seven 30-second TerrainCueLab conditions, looping and advancing in place. Escape returns to setup; `-configure` bypasses auto-start. The earlier Selwyn sequence remains `Examples/Sequences/selwyn-before-rugosity.json`.

The studio already preserves unknown JSON, exact untouched numbers, omissions/nulls and original bytes on unchanged saves. It retains historical metadata/speed spelling and engine scene rigs authored as VR1–VR4. It still edits bundled StreamingAssets and records under the application's data directory.

[Archived 0.2 shortcuts and launch guide](archival/START-HERE-0.2.txt).

## 0.3 — holistic improvements

Release tag: [`v0.3.0`](https://github.com/pvnkmrksk/matrexVR/releases/tag/v0.3.0). The source revision and platform build manifests are recorded with the release. This version brings reversible configuration, writable profiles, configurable animals, saved-run replay and explicit lifecycle cleanup together.

- A fresh profile opens the setup studio (`autoStart: false`); existing user sequence flags are preserved.
- Editable configs and recordings use a writable profile (`Config` and `RunData` under `Application.persistentDataPath`), with `--config-dir` and `--data-dir` overrides. Editor defaults remain unchanged. Initial seeding copies missing files only and never replaces existing user edits.
- Real subjects are **animals**; simulated/replayed individuals are **agents**. Session metadata adds species and animal IDs, and the system file stores per-rig `speciesName`.
- `agentSpeed` and `agentSpeedStdDev` describe simulated motion, retaining existing aliases. Canonical animal metadata retains old aliases for analysis during transition.
- Runtime rig provisioning follows configured VR IDs within available rendering-layer capacity. [Scene lifecycle](scene-lifecycle.md) gives trial materials, generated Optomotor assets, writer handles and tracking workers explicit cleanup. [Saved-run replay](replay.md) adds folder/config discovery, a common timeline, archived skies/populations and a default God’s-eye view. See release-specific verification before relying on a behavior for acquisition.

Replay includes a native **Browse…** folder picker (Windows/macOS, Editor, and Linux when zenity/kdialog is installed), folder-path input and `--replay-session`. Editor folder drag/drop works; standalone OS-level folder-drop is not implemented.

The compact build contains ten scenes: ControlScene, Choice, Choice_desync, Choice_Selwyn, Swarm, Kannadi, Optomotor, TerrainCueLab, Rugosity_RS and ReplayScene. Forest/two-tree examples marked `sourceBuildOnly` need their scenes enabled in a custom source build. Custom level authoring belongs in Unity source, not in the configuration studio.

## JSON and naming migration

| New/current field | Accepted older field | Rule |
| --- | --- | --- |
| `agentCount` | `numberOfLocusts` | Canonical non-null count takes precedence; a population of zero remains valid. |
| `agentSpeed` | `locustSpeed`, `locoSpeed` | First non-null value wins in that order; units remain the scene's world units per second. |
| `agentSpeedStdDev` | `locustSpeedStdDev` | New field takes precedence; existing distribution meaning is retained. |
| `Animals` | `Flies` | Canonical metadata wins when both are present, regardless of property order. |
| `AnimalID` | `FlyID` | Canonical animal ID wins. Hardware `VR` remains a routing key. |
| `UsedAnimalIDs` | `UsedFlyIDs` | Canonical list wins. |
| Per-rig system `speciesName` | — | Saved as `SpeciesName` in animal metadata. |

Loading or viewing a configuration does not mass-rewrite legacy fields. See [animal/agent compatibility](animal-agent-compatibility.md) for the complete mapping. The lossless editor preserves unknown extensions, untouched exact numeric text and missing values. If you explicitly set a canonical alias, it takes precedence; remove it to restore a legacy value's influence. Existing species-specific prefab names, Unity asset GUIDs, serialized component fields and historical CSV headers are preserved where renaming would break recordings or scenes. The operator UI uses animal/agent terminology.

## Platform packaging and verification

Release archives target Windows x86-64, macOS universal (Apple silicon and Intel), and Linux x86-64. Packages carry complete Unity players, public starter config and version-specific `START-HERE` instructions. macOS packages are ad hoc signed and are not notarized. Cross-building a target on macOS establishes the build result; Windows/Linux runtime behavior still needs a check on those operating systems and physical acquisition hardware.

Keep the whole extracted player directory together. Versions 0.1/0.2 require a writable extraction/app bundle because their historical code writes there. 0.3 uses a writable profile instead. See [getting started](getting-started.md) for exact paths and migration.

Each version is pinned separately so archive, integrated baseline and new improvements can be compared without moving an older tag. Build status, actual uploaded assets, hashes and runtime test evidence belong to the release itself. Tests using synthetic tracker data do not establish physical LED scanout timing or animal-tracking calibration.

## 0.3.1 — native desktop workflow

Release tag: [`v0.3.1`](https://github.com/pvnkmrksk/matrexVR/releases/tag/v0.3.1). This freezes the desktop workflow accepted in the [manual Mac test](manual-mac-workflow-validation.md), before the subsequent recording-library and synchronized video work.

The player opens fullscreen at native resolution with Retina-aware controls. A fresh apparatus profile offers Start demo and Configure; daily sequence and animal/session editing remain separate from hardware setup. Gain sliders span 0–50 with unrestricted numeric entry. The replay toolbar supports bidirectional timeline dragging, view switching, and an app-owned Mac folder picker that remains visible over fullscreen.

Bundled trackers and Daari Deepa launch independently, with stable per-user installations. Apple Silicon includes the reviewed Rekkefly source correction (`f9a9ba71` plus the checked-in patch) and Daari Deepa `f122518a`. Their exact source revisions, patches and executable hashes accompany the packages. Other architectures retain pinned upstream companions; the strict live-camera Rekkefly path requires the correction and is unavailable there. Demo input and manual/external tracker workflows remain available. No new upstream tracker release is implied.

The [manual Mac workflow report](manual-mac-workflow-validation.md) records Configure, Start, physical webcam input, live plots, acquisition, replay and 43 two-second scene transitions. An earlier build passed 300 full two-second reloads; the original historical native crash was not reproduced, and these results do not establish its sole cause or eliminate every hardware-specific failure. Windows/Linux are cross-built and not manually tested on those operating systems.

For this release's tested Apple Silicon payloads, build with:

```sh
python3 tools/build_release.py --ref v0.3.1 --version 0.3.1 --platform all \
  --local-native-camera-fix \
  --local-dashboard-archive .integration/local-dashboard/DaariDeepa-macos-arm64.zip
```

See [local companion build provenance](../tools/native_patches/README.md) for reproducing the reviewed dashboard archive. Omit those flags only for an explicit upstream-companion build; it does not reproduce the tested Apple Silicon package.

Run the archived 0.3.1 command from its matching checkout, including its companion locks and quickstart. Current-source locks and the 0.4 quickstart describe different payloads; they must not be substituted into a claimed reproduction of 0.3.1.

## Software verification

| Evidence | Result and scope |
| --- | --- |
| [Unity lifecycle/replay report](lifecycle-replay-validation-results.json) | **1,293 checks passed**: 150 accelerated native ColorDrift/material/grating/socket teardown cycles; 150 actual MainController Choice↔Swarm full scene loads at 0.1-second configured duration, with four local tracking SUB listeners and two generated agents; six-animal asynchronous layer/camera/reference checks; real ReplayScene folder, saved geometry/sky/population, timeline and default overview checks. |
| [Offline replay checks](../tools/test_replay_archive.py) | **630 checks passed** against the production archive/cache/writer code. |
| [JSON compatibility report](config-compatibility-validation-results.json) | **43 contracts passed** for lossless documents, animal aliases/species, canonical agent count/speed precedence and validation. Unity-only collaborators are stubs in these contract checks. |
| [Rig readiness report](release-readiness-results.json) | **71 checks passed**: configured rig creation, omitted-rig disabling, sparse VR10 IDs, isolated layers and camera masks, including additional asynchronous cameras excluding authored private rigs. |
| [0.3 GUI report](configuration-dashboard-v0.3-validation-results.json) / [capture manifest](images/dashboard-v0.3/provenance.json) | **2,946 checks passed** with graphics-enabled Unity runtime panels and fresh Full HD captures: animal/species edits, field geometry, raw JSON Apply/Discard visibility, replay launcher and actual UI callbacks, using synthetic metadata. |
| [Website download tests](../tools/test_release_downloads.cjs) | **15 assertions passed**: actual uploaded OS ZIP selection, incomplete/missing assets, universal macOS preference, version sorting and fallback. |

These accelerated software regressions establish cleanup and replay behavior under their recorded fixtures. They do not reproduce a 30-second, 100-reset physical acquisition run, certify LED timing or prove the sole cause of the historical Unity crash. Windows/Linux archives are cross-built; runtime checks on those operating systems and the actual apparatus remain separate.

## Rebuild the desktop archives

Use an activated Unity 6000.3.16f1 Editor with macOS, Windows Mono and Linux Mono support. The repository has no Unity license credentials configured for hosted CI. GitHub Actions can build Unity through GameCI when a suitable license is supplied; the published archives here were built locally.

```sh
python3 tools/build_release.py --ref v0.4.0 --version 0.4.0 --platform all \
  --local-native-camera-fix \
  --local-dashboard-archive .integration/local-dashboard/DaariDeepa-macos-arm64.zip
```

Supply `--unity /absolute/path/to/Unity` on another workstation. The helper creates a fresh ignored `.release-work/release-0.4.0/` folder, prepares tracked files from the requested Git revision, injects the release builder, supplies the public hardware template, and emits ZIPs, source manifests and SHA-256 files. It refuses to reuse an existing output folder. It never uploads releases. Archival tags can be rebuilt with a matching `--quickstart` text file containing their historical controls and storage locations; exact companion reproduction also requires the corresponding locks and reviewed payloads. On macOS, copy-on-write project/cache copies keep disk use manageable; other platforms need room for a complete source checkout.
