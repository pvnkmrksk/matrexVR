# Working in MATREX VR

MATREX VR is a Unity application for closed-loop animal behaviour experiments.
The GitHub repository is `pvnkmrksk/matrexVR`; a local checkout may still be named
`ledpanelVR`. Use Unity **6000.3.16f1**, as pinned in
`ProjectSettings/ProjectVersion.txt`.

## Start each task

1. Read the user's request, this file, and the relevant [role guide](docs/agents/README.md).
2. Inspect `git status --short --branch`, recent commits, and the diff for files
   you intend to change. Existing edits and untracked files may belong to ongoing
   work. Preserve them; never reset, clean, stash, or broadly stage them to make a
   task easier.
3. Read the implementation before changing its documentation. Use `rg` with
   explicit source directories; exclude `Library`, `.local`, `.integration`,
   `.release-work`, `Preview`, and third-party trees from general searches.
4. Choose the smallest complete change and the checks that establish its outcome.
   Coordinate ownership before parallel agents edit shared files.
5. Implement, run the relevant checks, inspect the final diff, and update affected
   instructions. Carry out already authorized commits, pushes, and releases after
   verification; do not stop at a plan or ask the user to repeat authorization.

Make routine implementation decisions from source and established conventions.
Ask only when missing information changes the requested outcome, an action needs
new authorization, or a real blocker cannot be resolved independently. Report
what was completed, what was tested, and any remaining concrete limitation.

## Source map

| Area | First places to read |
| --- | --- |
| Startup, sequences, phases | `Assets/Scripts/MainController.cs`, `RuntimePaths.cs`, `DesktopQuickStart.cs`, `ExperimentPhases.cs` |
| Configuration and GUI | `Assets/Scripts/ConfigurationUI/ConfigWorkspace.cs`, `ConfigDocument.cs`, `LosslessJson.cs`, `ConfigWorkbench*.cs` |
| Local agent control | `tools/matrex_control_mcp.py`, its adjacent lock, `Assets/Scripts/ExperimentControlEndpoint*.cs`, [MCP guide](docs/ai-control.md) |
| Tracking and motion | `Assets/Scripts/ZmqListener.cs`, `TrackerWireDecoder.cs`, `ClosedLoop.cs`, `TrackerProcessManager*.cs` |
| Recording and replay | `Assets/Scripts/Loggers/`, `Assets/Scripts/Replay/`, [replay contract](docs/same-build-replay-contract.md) |
| Scenes and populations | `Assets/Scenes/`, `Assets/Scripts/Swarm/`, `Assets/Scripts/Rugosity/`, `RuntimeRigRegistry.cs` |
| Templates and examples | `Assets/StreamingAssets/Templates/`, `Assets/StreamingAssets/Examples/` |
| Native companions and packaging | `tools/companion_bundles.py`, `tools/native_patches/README.md`, `tools/build_local_desktop.py`, `tools/build_release.py` |
| Manual and website | `docs/manual/navigation.json`, `tools/build_docs_site.py`, `docs/site/`, `.github/workflows/pages.yml` |

## Contracts to preserve

- GUI, JSON, and MCP share one configuration workspace and production validators.
  Preserve unknown fields, untouched numeric text, explicit null/false/zero,
  revisions, undo history, and external-file conflict detection.
- Keep Unity object and rendering work on the Unity thread. Workers own their
  sockets/files and exchange bounded snapshots with that thread.
- Replay uses the creating build and complete archived evidence. It is read-only:
  no live tracking, recording, or mutation of the source run. See the contract
  before changing recording formats or controller restoration.
- Accepted asynchronous commands do not prove completion. Observe startup,
  recording ownership/acknowledgement, drain/finalization, and replay readiness.
- Preserve operator calibration and recordings. Use separate explicit config,
  data, and control directories for tests. Unity validation runners may rewrite
  fixtures; run them only in marked disposable projects.
- Keep Unity asset `.meta` files paired with their assets. Do not hand-edit
  generated schemas or HTML as a substitute for updating their sources.

## Validation and publication

Use the [role-specific validation commands](docs/agents/README.md). There is no
single test command that establishes GUI, native-platform, replay, and physical
apparatus correctness. Record the scope actually checked; synthetic inputs and
cross-built players do not establish camera calibration or LED scanout timing.

`build_local_desktop.py` snapshots working-tree source, including untracked source
files. `build_release.py --ref COMMIT --version X.Y.Z` packages a pinned revision
in a fresh output directory and does not upload it. Read the
[release guide](docs/agents/release.md) before publishing. Stage explicit paths,
review the staged diff, and keep release notes aligned with the files actually
committed and assets actually uploaded.
