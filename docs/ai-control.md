# Agent control through JSON and MCP

MATREX now exposes its configuration and experiment workflow through **27 MCP tools and three JSON resources**, using the [official MCP Python SDK](https://py.sdk.modelcontextprotocol.io/). Configuration, validation, defaults, history and Start use the same C# `ConfigWorkspace`, `ConfigDocument`, schema and GUI controller methods. The adapter does not drive the mouse or keyboard and does not maintain another copy of experiment settings.

This guide describes the **current source and compatible local preview**. An older desktop download does not acquire MCP support from a documentation update. Use the adapter shipped with the app, or the adapter from its matching source revision. The app must expose control protocol **2**; discovery reports its actual methods and compiled scenes.

Normal app launches enable no command endpoint. Control is an explicit local opt-in. MCP-capable desktop packages include the server script and its dependency lock at the paths below. Source users can use `tools/matrex_control_mcp.py` and the adjacent lock instead. The tested SDK version is **2.3.0**, requiring Python **3.11 or newer**; Python and the SDK are needed on the MCP host, not for ordinary desktop use.

| Package | Adapter location |
| --- | --- |
| macOS release / local preview | `MATREX VR.app/Contents/Resources/MCP/matrex_control_mcp.py` |
| Windows / Linux release | `MCP/matrex_control_mcp.py`, beside the player executable |
| Source checkout | `tools/matrex_control_mcp.py` |

The 0.4.1 release packager includes the script and adjacent `.py.lock` from the player's pinned source revision. `release-manifest.json` records `mcpBundled`, the adapter's source revision and SHA-256 hashes. Older archives can lack these files; check their actual contents.

**First connection:** launch an MCP-capable app with a private control directory, point your client's stdio server at that same directory, then call `matrex_discover` and `matrex_status`. Success means `ok: true`, protocol 2 and the intended config/data roots. Connecting does not initialize a tracker or start acquisition. See the [copyable client configuration](mcp/mcp-client.example.json) and [read-only connection check](mcp/inspect_connection.py).

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

On Windows or Linux, launch the actual player executable with the same arguments instead of the macOS `open` command:

```sh
"/absolute/path/to/MATREX VR.x86_64" \
  --control-dir "/absolute/path/MATREX-Control" \
  --config-dir "/absolute/path/Config" \
  --data-dir "/absolute/path/RunData" -configure
```

The Windows executable may be named differently; use the `.exe` supplied in that archive. Keep the player visible and responsive: Unity processes commands on its main thread. After launch, `MATREX-Control/endpoint.json` identifies the process, instance, config root and run-data root. A fresh config root is seeded from the app's bundled examples. The MCP adapter connects to this running app; it does not launch the player itself.

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

Replace every `/absolute/path/...` with a real absolute path, including the path to `uv` if your GUI client cannot find it. This `mcpServers` JSON is for clients that accept that configuration shape; clients with another format still need the same executable and argument array. For a source checkout, substitute `/absolute/path/matrexVR/tools/matrex_control_mcp.py`; retain its adjacent `.lock` file. Let the client launch the adapter as a stdio subprocess; running the adapter in an interactive terminal waits for protocol input and shows no application UI.

Alternatively, from the repository root, prepare a Python 3.11+ virtual environment:

```sh
python3 --version
python3 -m venv .local/mcp-venv
.local/mcp-venv/bin/python -m pip install 'mcp==2.3.0'
```

On Windows use `.local\mcp-venv\Scripts\python.exe` in place of `.local/mcp-venv/bin/python`. Use that environment's **absolute Python executable path** as the client `command`, with only the adapter path, `--control-dir` and the control path in `args`. No shell activation is needed. The `uv --locked` route also locks transitive dependencies; a plain pip installation pins the SDK alone. Prepare dependencies before an offline experiment. Quit and relaunch the app without `--control-dir` to disable control.

## Check the connection without starting a run

With the player launched in setup, run this from the repository root using the prepared environment:

```sh
.local/mcp-venv/bin/python docs/mcp/inspect_connection.py \
  --adapter "/absolute/path/matrexVR/tools/matrex_control_mcp.py" \
  --control-dir "/absolute/path/MATREX-Control"
```

For a packaged adapter, change `--adapter` to the package path above. The check uses the real SDK stdio client, lists tools/resources, and reads discovery, status and the GUI workspace. It sends no mutation. JSON output includes the actual root paths, scene and selected document revisions; exit status 0 means all requested reads succeeded. If setup is closed or an experiment is preparing/running/stopping, status/discovery can still succeed, but workspace inspection fails with the ordinary idle-setup requirement and the script exits 1. Inspect the reported state and return to idle setup when appropriate before rerunning; a working transport does not make that workspace read successful.

In an agent client, the equivalent first request is: “Inspect MATREX with discovery, status and the current workspace. Report the config and run-data roots, selected sequence, validation errors and current run state.” Supply the intended experiment task once; an agent can then carry its authorized steps through validation and completion without asking for approval at each tool call.

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

Use these argument shapes when turning that workflow into tool calls. Paths come from discovery and workspace results; revisions come from the latest document read or mutation result.

| Step | Tool and arguments | Completion check |
| --- | --- | --- |
| Inspect | `matrex_discover {}`, `matrex_status {}`, `matrex_workspace_get {}` | Confirm intended roots, selected files and state. Workspace reads require idle setup. |
| Read/edit | `matrex_config_get {"path":"/absolute/path/Config/example.json"}`, then `matrex_config_replace {"path":"…","revision":"…","jsonText":"…"}` | Supply the complete edited JSON text, preserving unknown fields and numeric text. Check returned validation errors. |
| Save | `matrex_config_save {"path":"…","revision":"…"}` | Use the revision returned by replacement; confirm `dirty:false`, `conflict:false`. |
| Validate saved files | `matrex_validate_setup {"systemFile":"…","sequenceFile":"…"}` | `valid:true` and no errors. This validates saved files; unsaved workspace changes are separate. |
| Preflight, when needed | `matrex_tracking_initialize {}` | Wait for `diagnostics.setupPreparing:false`; inspect `diagnostics.rigs[].freshPose` and `inputState` for every intended rig. |
| Start | `matrex_start_current {}` | Wait for `sequenceRunning:true` and the expected scene; inspect `sequenceError` and recorder state. |
| Stop | `matrex_stop {}` | Wait for `sequenceRunning:false`, `sequencePreparing:false`, `sequenceStopping:false` and `diagnostics.runFinalizing:false`; inspect errors/warnings and the run's saved evidence. |
| Find/open | `matrex_recordings_list {}`, then `matrex_replay_open {"directory":"…"}` | Use an entry's returned directory; wait for `replayReady:true`, `replayLoading:false`, with no `replayError`. |
| Inspect replay | `matrex_replay_play {"playing":false}`, `matrex_replay_seek {"seconds":1}`, `matrex_replay_view {}` | `replayTime` reflects the seek; empty view arguments select overview. Use a recorded `vrId` for animal view. |

The ellipses in this table are placeholders, not literal values. One shared workspace is visible to the operator and agent: finish a focused text/JSON edit or close an open dialog before requesting conflicting changes. Inspect a validation failure and correct its actual cause; repeated Start calls do not repair a setup.

For example, with the official SDK client connected to the adapter:

```python
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
        if workspace.is_error:
            raise RuntimeError(workspace.structured_content)
        print(workspace.structured_content["result"])
        # Only as part of the requested experiment task:
        accepted = await client.call_tool("matrex_start_current", {})
        if accepted.is_error:
            raise RuntimeError(accepted.structured_content)
        print(accepted.structured_content["result"])
        # Poll matrex_status; accepted is not running or recorder acknowledgement.
```

This function is an SDK usage fragment; the [connection check](mcp/inspect_connection.py) is a complete executable example. The wire reply is `{"ok":true,"result":{...}}`; tool results expose that envelope as `structured_content`. Read state fields under `result`. Failures set `is_error:true`, `ok:false`, `errorCode` and `error`. A successful tool result can still describe a failed validation (`result.valid:false`) or an asynchronous operation that has not completed.

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

## Troubleshooting

| Symptom | Action |
| --- | --- |
| Client cannot start `uv`/Python, or reports missing `mcp` | Use absolute executable paths and the prepared Python 3.11+ environment. Keep the script and lock together for `uv --locked`. |
| Missing `endpoint.json` / unavailable control directory | Launch an MCP-capable player with `--control-dir`; check the exact same absolute path on both sides. Ordinary launches expose no endpoint. |
| `Unsupported endpoint` / method unavailable | Match the adapter to the app revision. Read discovery before assuming a scene or operation exists in an older build. |
| Control directory already owned | Choose a separate directory for another running player. Do not delete an active instance's files to take ownership. |
| Timeout / unknown execution outcome | Read status and any returned operation ID before deciding whether to retry. Check that the player is responsive and not blocked by a native dialog. The adapter's `--timeout` defaults to 15 seconds and allows up to 60; extending it does not make Start synchronous. |
| Revision changed / external conflict | Read the current draft again and reconcile the operator's changes. Preserve both versions when resolving a disk conflict. |
| Pending input, unsaved draft or modal dialog | Finish/apply the active editor or dialog. Save the current draft before selecting other files; use `start_current` when the intended task is to run that draft. |
| `accepted:true` but no experiment/video | Inspect `sequenceError`, preparation flags, `recordingError`, `recordingWarnings` and each rig's recorder acknowledgement. A live tracker process alone does not show fresh measurements. |
| Stop returned but files are still saving | Follow sequence/finalization state and recording evidence before opening a replay, moving files or quitting. A timeout is not evidence of a safe finalization. |
| Replay controls rejected | Wait for ready/loading/error fields. Use the creating build and complete run folder, following the [same-build replay contract](same-build-replay-contract.md). |

## Validation and scope

`tools/test_matrex_control_mcp.py` exercises the actual official SDK client/server, standard tools/resources, structured results/errors, strict argument rejection, precision-safe JSON text, instance-fenced mailbox and timeout cleanup. `ExperimentControlValidation.Run` exercises the real Unity endpoint and GUI DOM/history/validators in a marked disposable project. Tracker regression tests cover exact owned-take manual stop and single-stop finalization reuse.

The standalone acceptance uses a real Mac player and the **packaged** MCP adapter, generated tracker input on an isolated localhost port, two distinct experiment runs, read-only replay and recorded input/configuration provenance. Hardware camera/exposure timing and other-platform native runtime acceptance remain outside that test. The [review report](mcp-control-validation-20261031.json) records exact evidence and limits. The Mac preview is a local review build, not a published release.
