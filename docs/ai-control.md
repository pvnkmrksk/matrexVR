# Agent control through JSON and MCP

MATREX now exposes its configuration and experiment workflow through **27 MCP tools and three JSON resources**, using the [official MCP Python SDK](https://py.sdk.modelcontextprotocol.io/). Configuration, validation, defaults, history and Start use the same C# `ConfigWorkspace`, `ConfigDocument`, schema and GUI controller methods. The adapter does not drive the mouse or keyboard and does not maintain another copy of experiment settings.

Normal app launches enable no command endpoint. Control is an explicit local opt-in. The local Mac preview includes the self-contained server script and its dependency lock under `MATREX VR.app/Contents/Resources/MCP/`. Source users can use `tools/matrex_control_mcp.py` and the adjacent lock instead. The tested SDK version is **2.3.0**, requiring Python **3.11 or newer**; the SDK provides protocol negotiation, initialization, standard stdio framing, tools/resources and structured results.

## Connect a client

Create a private command directory and launch an independently reviewable local profile:

```sh
mkdir -m 700 /absolute/path/MATREX-Control
open -n "/absolute/path/MATREX VR.app" --args \
  --control-dir "/absolute/path/MATREX-Control" \
  --config-dir "/absolute/path/Config" \
  --data-dir "/absolute/path/RunData" -configure
```

Use a separate command directory for each running player. Config and RunData should be explicit for test profiles so they cannot affect a calibrated installation. The command directory must remain outside saved recordings.

With `uv`, the adapter's inline dependency declaration and adjacent lock install the pinned SDK:

```json
{
  "mcpServers": {
    "matrex": {
      "command": "uv",
      "args": [
        "run", "--locked", "--script",
        "/absolute/path/MATREX VR.app/Contents/Resources/MCP/matrex_control_mcp.py",
        "--control-dir", "/absolute/path/MATREX-Control"
      ]
    }
  }
}
```

Alternatively install `mcp==2.3.0` once in a Python 3.11+ virtual environment and use that environment's Python executable as `command`, omitting `uv run --locked --script` from the arguments. Prepare dependencies before an offline experiment. The current review environment is `.local/mcp-venv`; nothing was installed into the system Python. Quit and relaunch without `--control-dir` to disable control.

## Available operations

| Area | Tools |
| --- | --- |
| Discovery | `matrex_discover`, `matrex_schemas` |
| Shared GUI draft | `matrex_workspace_get`, `matrex_config_get`, `matrex_config_create`, `matrex_config_replace`, `matrex_config_save`, `matrex_config_history` |
| Select and validate | `matrex_setup_select`, `matrex_validate_setup` |
| Tracking preflight | `matrex_tracking_initialize`, `matrex_tracker_control` |
| Session/animal metadata | `matrex_metadata_get`, `matrex_metadata_set` |
| Experiment lifecycle | `matrex_start_current`, `matrex_start_files`, `matrex_stop`, `matrex_status` |
| Recorder override | `matrex_recorder_stop`, `matrex_operation_get` |
| Live controls | `matrex_runtime_control` |
| Run data and replay | `matrex_recordings_list`, `matrex_replay_open`, `matrex_replay_play`, `matrex_replay_seek`, `matrex_replay_view`, `matrex_replay_options` |

The resources `matrex://status`, `matrex://catalog` and `matrex://schemas` return read-only JSON snapshots. Poll the status tool for live progress. Discovery/initialization never implicitly starts an experiment, tracking, or a recorder. Tool calls are intended for a user-authorized workflow; the server contains no autonomous experiment loop.

Discovery includes the scenes actually compiled into the build, their selectable/legacy status, exact built-in object and material names, example/treatment/sequence JSON paths, connected display names, and configured camera selections. **Physical camera enumeration, camera formats and calibration remain owned by the native companion APIs.** MATREX does not open another camera stream to invent an inventory. External GLB object paths and their settings remain ordinary configuration fields, with the same production asset validation/preload at Start.

## JSON and GUI parity

`matrex_workspace_get` and `matrex_config_get` report the current GUI DOM, including unsaved form edits, `revision`, `dirty`, `conflict`, parse errors, undo/redo availability and `effective` defaults. Effective defaults use the exact GUI export path and never silently fill the source file. Damaged input retains its original text in `damagedSourceText`; the GUI and agent can inspect inherited defaults while the explicit invalid input remains an error.

Use **`jsonText`**, rather than rebuilding a file from the effective-defaults object, when editing. It preserves unknown keys and lossless numeric literals, including integers beyond JavaScript's exact number range. `matrex_config_replace` requires the revision just read. It validates the candidate through the production workspace without changing the draft or undo history on failure. A successful replacement is one normal GUI undo transaction and does not save automatically. `config_save` uses the same external-conflict checks and atomic replacement/`.bak` preservation as the GUI.

To repair a multi-file setup with pre-existing errors, `allowInvalidDraft:true` explicitly allows an invalid GUI draft. Its validation errors remain visible and Start still refuses it. This is useful when building system and sequence JSONs incrementally; it never coerces invalid values or reports the draft runnable. New files are created only below the configured config root and cannot overwrite an existing file. New unreferenced files initially receive document-schema validation; production cross-file validation occurs when the selected sequence references them.

Selecting different primary files refuses unsaved GUI changes, focused/pending editor input and open dialogs. A conflict or stale revision requires inspecting the current state before retrying. Shared metadata can also be read/replaced with a revision; `save:true` persists it through the ordinary GUI metadata backup routine, otherwise Start saves it.

Saved-setup validation and GUI Start share the same readiness method: production document/runtime validation, apparatus/profile checks and connected-output layout checks. It does not silently ignore missing panel dimensions just because the scene can load.

## End-to-end workflow

1. Discover the catalog and schemas; inspect the selected workspace.
2. Select existing system/sequence JSON paths, or create JSON files under the config root and reference them from the sequence. Read a document's revision, replace its JSON draft and save it.
3. Set session metadata if needed.
4. Call `matrex_tracking_initialize`. It saves the apparatus draft, applies the selected input ports and routing, then uses the ordinary tracker-preparation workflow. It does **not** create an experiment recording. Poll `status.diagnostics.setupPreparing` and each rig's `inputState`/`freshPose`; a process being alive does not establish valid acquisition.
5. Call `matrex_start_current` for the current GUI draft, or `matrex_start_files` for already saved files with no pending GUI changes. Both use the ordinary Start action and preserve metadata/configuration provenance.
6. Poll status until the intended scene is running or a preparation/sequence error is reported. Status distinguishes preparation, running and finalization, and exposes trial/step/phase timing, actual ports, live performance, run directory, native recorder requested/acknowledged state and warnings.
7. Call `matrex_stop`, then wait for both experiment and recording finalization to settle. Discover new runs under this installation's RunData root with `matrex_recordings_list`; supply another directory only when intentionally browsing elsewhere.
8. Open a run, wait for `replayReady:true` and `replayLoading:false`, then control play/pause, seek and overview/animal views. `replayError` reports a failed load. Replay operations do not edit saved data.

For example, with the official SDK client connected to the adapter:

```python
import json
from mcp import Client
from mcp.client.stdio import StdioServerParameters

server = StdioServerParameters(
    command="/absolute/path/venv/bin/python",
    args=["/absolute/path/matrex_control_mcp.py",
          "--control-dir", "/absolute/path/MATREX-Control"],
)

async def configure_and_start():
    async with Client(server) as client:
        workspace = await client.call_tool("matrex_workspace_get", {})
        # Read structured_content; an is_error result must be handled first.
        print(workspace.structured_content)
        accepted = await client.call_tool("matrex_start_current", {})
        print(accepted.structured_content)
        # Poll matrex_status; accepted is not running or recorder acknowledgement.
```

Replay options are a validated partial JSON object, for example:

```json
{
  "playbackSpeed": 0.5,
  "trailSeconds": 8,
  "trailProminence": 2,
  "perspective": true,
  "pivot": {"x": 0, "y": 0, "z": 0},
  "yaw": 35,
  "pitch": 25,
  "distance": 12
}
```

Send that object as the `jsonText` string to `matrex_replay_options`, after selecting the overview with `matrex_replay_view`. Omitting `jsonText` reads the current state. The same playback/trail/projection/camera variables drive the GUI sliders and mouse controls. Other options include `loop`, `panelPixels`, `followRig` (rig ID or null), and orthographic `zoom`. All fields are validated before any option changes.

`matrex_runtime_control` exposes the ordinary live auto-trim, flight-check and frame-rate-cap controls. Frame-rate changes use the existing controller, respect VSync, and retain its runtime display-setting evidence. These are transient live controls, distinct from editing the next run's saved system JSON.

## Recording acknowledgement and override

Starting/stopping an experiment delegates to the existing [native recording controller](native-recording-control.md). `Experiment` versus `Manual` mode and video opt-out remain system JSON settings. Manual/continuous native recording is never commandeered by an automatic experiment command. A missing or incompatible companion is reported honestly; best-effort VR start does not imply video success.

`matrex_recorder_stop` only stops an **acknowledged experiment-owned take** for the requested rig. It does not terminate tracking or the VR experiment. It sends the same ownership-fenced stop/finalization operation, latches operator override and returns an `operationId`; inspect `matrex_operation_get` for completion/error. A later experiment Stop reuses that task rather than sending a second native stop. The existing quality/finalization policy still reports an interrupted requested recording in run evidence. A rig in Manual mode or with no acknowledged active take yields a failed operation, without a false recorder acknowledgement. Native GUI pause/stop/replacement overrides remain respected.

## Transport and concurrency

The SDK uses stdio, with no listening HTTP/network port. A private local directory carries bounded atomic request/reply files to Unity's main thread. An exclusive owner lock prevents two app instances from serving the same directory. Each request/reply includes the application instance token and random request ID. Expiry and instance fencing prevent an old queued mutation from executing after a later launch. Requests are limited to 1 MiB.

Configuration writes use optimistic document revisions, retain normal GUI history and reject external edit conflicts. State-changing requests are not automatically retried. A timeout can occur after an operation began, so the outcome is **unknown** until status/operation evidence establishes it. Accepted Start means asynchronous preparation began, not that a scene is running or camera video is recording. The same distinction applies to tracker launch and recorder Stop.

Any process that can write the selected directory can request operations. Keep it private to the intended local account. No command transport is written into a recording, and replay remains read-only. Shared status reflects observed runtime state; the workspace tools reflect selected/unsaved GUI configuration.

## Validation and scope

`tools/test_matrex_control_mcp.py` exercises the actual official SDK client/server, standard tools/resources, structured results/errors, strict argument rejection, precision-safe JSON text, instance-fenced mailbox and timeout cleanup. `ExperimentControlValidation.Run` exercises the real Unity endpoint and GUI DOM/history/validators in a marked disposable project. Tracker regression tests cover exact owned-take manual stop and single-stop finalization reuse.

The standalone acceptance uses a real Mac player and the **packaged** MCP adapter, generated tracker input on an isolated localhost port, two distinct experiment runs, read-only replay and recorded input/configuration provenance. Hardware camera/exposure timing and other-platform native runtime acceptance remain outside that test. The [review report](mcp-control-validation-20261031.json) records exact evidence and limits. The Mac preview is a local review build, not a published release.
