# Path pilot integration with main — 2026-11-05

Integrated `origin/main` at `5fbfc05` into `codex/ant-path-angle-pilot`, based on
pilot commit `63d1a98`. The merge retains the ant sequence entries, three-hour
session clock and randomized blocks alongside main's world starters, asynchronous
startup, native recording finalization and recorded world state.

Pilot world checkpoints now include the effective ground appearance. The original
dynamic-choice controller recreates that ground during replay, including supplied
textures. Missing recorded appearance fails explicitly. The existing rendering
runner covers acquisition snapshot, ground reconstruction and checkpoint restore;
this is not a complete player record/replay walkthrough.

The first runtime run rejected a measured no-bars duration of 19.9999323 seconds
against an exact 20-second lower bound. Duration assertions now allow 50 ms for
Unity frame-clock versus monotonic-clock scheduling; production trial timing is
unchanged. Deadline assertions retain their original bounds.

## Verification

Unity 6000.3.16f1 runs use a disposable snapshot prepared with:

```sh
python3 tools/build_local_desktop.py --prepare-only \
  --project ../validation/main-merge-20261105/project --library Library
```

The copy also has the `ANT_PILOT_VALIDATION_COPY` marker. Run Unity with
`-batchmode -projectPath <copy> -executeMethod <runner> -logFile <log>` and a
separate `--data-dir`. Add `-quit` only for the synchronous appearance runner;
the runtime and UI runners exit themselves. `PathPilotValidation.Run` accepts
`-pilot-output <directory>`.

- `PathPilotValidation.Run`: 45 checks passed with no experiment-runtime errors;
  randomized blocks, real left/right physics contacts, actual 5/20/120-second
  timings, and deadline shutdown during trial/intertrial, editor pause and zero
  time scale. Unity's known search-index startup diagnostic is recorded separately.
- `PathPilotAppearanceValidation.Run`: 43 checks passed, including ground replay.
- `PathPilotUiValidation.Run`: 16 checks passed; selection, three-hour labels,
  historical-experiment switching and exact apparatus preservation.
- The regenerated configuration schema equals the merged schema.
- Ten Python regression suites passed: configuration compatibility, effective
  config, sequence selection, primary workbench action, world starters, terrain
  treatments, native recording control, tracker contracts, replay archives and
  recorded world state reader. These cover 1,777 assertions, including the
  reader's .NET and Unity Mono runs; they do not establish physical hardware behavior.
- `validate_config_templates.py` passed 112 template/example checks, then failed
  an archive byte-hash assertion. `core.autocrlf=true` changes local archived JSON
  line endings. The first failing file is identical in both parent commits and
  its committed bytes match the manifest. No archive files were changed for this test.
- Whitespace checks pass for the integration edits. The full incoming diff has
  existing Unity-generated `.meta` trailing spaces from main.

Logs, JSON results, screenshots and original local-file backups are in the
workspace's `validation/main-merge-20261105/` directory. Pre-existing local changes
remain outside the merge commit. The generated choice catalog had only local
line-ending differences before receiving main's updated catalog.

No standalone app was rebuilt, no physical tracker/LED apparatus was tested,
and no three-hour endurance run was performed. The separate `pilot/` project
and previously built apps retain their earlier versions.
