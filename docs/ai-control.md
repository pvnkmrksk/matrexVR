# Optional local AI control

Current source includes an opt-in stdio MCP adapter. Normal launches create no command endpoint. Start MATREX with a separate absolute command directory, for example:

```sh
open "/path/to/MATREX VR.app" --args --control-dir "/Users/me/MATREX-Control"
```

Configure an MCP client to launch the Python standard-library adapter:

```json
{
  "mcpServers": {
    "matrex": {
      "command": "python3",
      "args": ["/path/to/matrexVR/tools/matrex_control_mcp.py", "--control-dir", "/Users/me/MATREX-Control"]
    }
  }
}
```

The client must support local stdio MCP servers. The adapter exposes status, saved-setup validation, starting the current GUI draft, selecting/starting saved system and sequence JSONs, stopping, and replay open/play/pause/seek. Setup paths and recording paths are absolute. Every state-changing tool describes its requirement for an explicit user request. Initialization and tool discovery send no experiment commands; the adapter has no autonomous experiment loop.

Starting uses the same GUI Start action, including production schema/runtime validation, apparatus/display checks, metadata, tracker preparation and previous-recording finalization. Keep setup open for start commands. `start_current` saves the current GUI draft as the ordinary button does. `start_files` refuses unsaved JSON/config edits, focused text fields, open dialogs and in-progress preparation. It validates saved files before selecting them. Starting is asynchronous: an accepted response means preparation began; read status to establish running/error state. Validation is available only while idle, preserving acquisition configuration provenance. Status remains available during acquisition and replay.

Requests and responses are small JSON files atomically renamed inside the selected directory, polled on Unity's main thread. There is no HTTP server or listening network port. Use a directory accessible only to the intended local account; any process that can write there can request operations. The directory must be separate from saved recordings, and the endpoint rejects recording manifests and CSV-containing mailbox folders. Viewing/controlling replay never writes commands into the recording folder. Explicit media import/offset features remain separate.

Requests expire. A timeout can occur after an operation began, so inspect status before retrying a start/stop/open command. There is no automatic retry of state-changing operations. The adapter removes its transport files after completion or timeout. Quit and relaunch without `--control-dir` to disable control.

Run `python3 tools/test_matrex_control_mcp.py` for protocol/transport tests. `ExperimentControlValidation.Run` exercises the real Unity endpoint and production validators in a marked disposable project. Physical apparatus timing, tracker availability and a native MCP-client walkthrough remain separate acceptance checks.
