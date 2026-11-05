# MCP agent

Read [repository instructions](../../AGENTS.md) and the [operator/client guide](../ai-control.md).
MATREX exposes local opt-in control over its existing Unity workflow. The adapter
must remain a transport layer, with experiment decisions and validation owned by
the same production C# code used by the GUI.

## Implementation map

| File or area | Responsibility |
| --- | --- |
| `tools/matrex_control_mcp.py` and `.py.lock` | Pinned Python SDK adapter, tool/resource schemas, stdio server, fenced mailbox |
| `Assets/Scripts/ExperimentControlEndpoint*.cs` | Unity-thread command handling, discovery, status, shared workspace and runtime operations |
| `Assets/Scripts/ConfigurationUI/ConfigWorkbench.Control.cs` | GUI document selection, draft/revision/history/save and normal Start integration |
| `Assets/Scripts/ConfigurationUI/ConfigWorkspace.cs`, `ConfigDocument.cs` | Authoritative configuration documents and validation |
| `Assets/Editor/ExperimentControlValidation.cs` | Real Unity endpoint and GUI integration checks |
| `tools/test_matrex_control_mcp.py` | Official SDK tools/resources and mailbox regressions |

## Rules and checks

- Initialization and discovery are read-only. Control stays disabled without
  `--control-dir`. Use a private directory for each player, outside recordings.
- Keep SDK dependencies and the adjacent lock synchronized. The source guide
  documents Python and SDK requirements; verify them against the script's inline
  metadata and lock rather than assuming another MCP SDK API.
- Edit original `jsonText` with the revision just read. Preserve precision and
  unknown fields; effective defaults are descriptive, not replacement JSON.
- Keep argument schemas strict, errors structured, and resources read-only.
  Invalid/stale requests must not partially mutate drafts or create undo entries.
- Retain request expiry, application-instance fencing, ownership locks, bounded
  payloads, and atomic file replacement. Never blindly retry a timed-out mutation:
  inspect status/operation evidence because its outcome may be unknown.
- Start acceptance is preparation, not proof of running or video recording. Stop
  acceptance is not proof of finalization. Preserve recorder ownership and manual
  override behavior. Replay calls must leave saved data unchanged.

Run adapter regressions in an environment with the locked SDK, for example:

```sh
uv run --locked --script tools/matrex_control_mcp.py --help
/absolute/path/to/mcp-venv/bin/python tools/test_matrex_control_mcp.py
python3 tools/test_native_recording_control.py
```

The first command verifies adapter startup/arguments; it does not install a
persistent test environment. The second requires an environment prepared with the
adapter's pinned dependencies. `ExperimentControlValidation.Run` is the Unity
integration gate; it requires a disposable project carrying
`KANNADI_VALIDATION_COPY`. Prepare a test project as described in the
[runtime guide](runtime.md), and inspect its `control-validation/results.json`.

If packaging changes, inspect the completed app/archive for both the script and
lock. Do not assume a locally packaged adapter is present in an older release.
Report protocol tests, Unity integration, standalone acceptance, and real hardware
checks separately.
