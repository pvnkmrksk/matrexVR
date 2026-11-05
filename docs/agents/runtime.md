# Runtime and replay agent

Read [repository instructions](../../AGENTS.md), the
[ownership map](../manual/architecture.md), and the
[same-build replay contract](../same-build-replay-contract.md) before changing
acquisition or replay. New configuration behavior must flow through the original
experiment controllers and complete archived inputs.

## Choose the relevant checks

The commands below are entry points, not a requirement to run every suite for
every change. Inspect a script's prerequisites before launching it. Production
C# contract runners generally need a local .NET SDK and the Newtonsoft DLL from
an imported Unity checkout; they fetch no NuGet packages. Some tests are source
guards, while others execute production methods with stubs. Their scope matters.

| Change | Useful starting checks |
| --- | --- |
| Config documents, templates, defaults | `python3 tools/test_config_compatibility.py`; `python3 tools/test_effective_config.py`; `python3 tools/validate_config_templates.py` |
| Apparatus, display routing, paths | `python3 tools/test_apparatus_profile.py`; `python3 tools/test_system_display_routing.py`; `python3 tools/test_desktop_profile_paths.py` |
| Tracker input or recording ownership | `python3 tools/test_tracker_contracts.py`; `python3 tools/test_native_recording_control.py`; `python3 tools/test_native_tracker_interop.py --help` for configured native integration prerequisites |
| Archive, saved runs, replay | `python3 tools/test_replay_archive.py`; `python3 tools/test_recording_summary.py`; `python3 tools/test_recording_seal.py`; `python3 tools/test_replay_media.py` |
| GUI actions, selection, focus | `python3 tools/test_primary_workbench_action.py`; `python3 tools/test_sequence_selection.py`; `python3 tools/test_workbench_keyboard_focus.py` |
| Scene/terrain contracts | `python3 tools/test_terrain_scene_compatibility.py`; `python3 tools/test_terrain_treatments.py`; `python3 tools/test_world_starters.py` |

Use `rg --files tools -g 'test_*'` to find additional targeted regressions for the
specific component. Read the related `Assets/Editor/*Validation.cs` runner for
real Unity behavior/rendering coverage.

## Disposable Unity work

The local builder can prepare a marked copy of current source without launching
Unity or building companions. It snapshots current tracked and untracked source
files, so inspect the input worktree first and choose a fresh output path:

```sh
python3 tools/build_local_desktop.py --prepare-only --project .local/agent-check/project
```

The copy receives `KANNADI_VALIDATION_COPY`, a safe system template, an empty
non-autostart sequence, and build identity. Do not run fixture-writing validation
against the real source project or an operator's calibrated profile.

For example, on the standard maintainer Mac installation:

```sh
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath .local/agent-check/project \
  -executeMethod SameBuildReplayValidation.Run \
  -logFile .local/agent-check/unity.log
```

Use the installed Editor path on other machines. This runner exits itself; do
not add `-quit` to a runner that waits asynchronously for Play Mode. Graphics
checks require a graphics-capable session: `-nographics` changes their scope.
Inspect the runner's JSON result and Unity log, including compilation errors.
Use another prepared project or close Unity before a second process uses it.

For record/replay changes, the graphics-enabled end-to-end runner is:

```sh
python3 tools/run_replay_roundtrip.py \
  --project .local/agent-check/project --output .local/agent-check/roundtrip
```

Read `--help` for extended coverage and explicit external-publisher mode. A
successful synthetic run establishes its fixture's behavior; physical tracker,
LED timing, and other operating systems need their own acceptance.

## Integration checklist

Preserve one-frame identity across consumed input, poses, animation, populations,
and world state. Drain writers before sealing archives. Keep replay readers
read-only and fail clearly for missing/foreign required evidence. Retain scene,
material, socket, and worker cleanup across both full reload and in-scene steps.

If fields change, regenerate schemas with
`ConfigurationSchemaBuilder.Generate` in Unity, inspect the generated JSON, then
regenerate the manual. If assets change, preserve `.meta` identity and verify
build-scene/shader/model inclusion. Finish with the relevant player interaction
or rendered-frame check when the change affects UI, lifecycle, or presentation.
