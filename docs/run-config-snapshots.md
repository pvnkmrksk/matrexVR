# Run configuration snapshots

Every new run folder saves its configuration during `MasterDataLogger.Awake`, before scene data loggers start. MainController creates a logger if one is absent. Saving no longer depends on which component happened to wake first, and repeated config loads cannot overwrite an earlier version or fail because a destination already exists.

```text
RunData/<session>/
  system_config.json                 initial system snapshot
  sequenceConfig.json                initial sequence snapshot
  config_manifest.json               sources, versions, roles, hashes and errors
  Configs/
    Files/Examples/Swarm/...json      readable first copies, preserving source paths
    Files/Archive/Legacy/...json      resolved legacy config copies
    Files/SwarmData/...csv            referenced trajectory inputs and metadata
    Files/External/<path hash>/...    absolute files outside StreamingAssets
    Snapshots/<SHA-256>.<extension>   immutable bytes of every captured version
```

The timestamp/scene-prefixed sequence and system copies remain at the run root for existing tools. The readable copies and root canonical files retain the first captured version. If a file changes between loads, both versions remain under `Snapshots`; consult the manifest's `consumed` and `loads` fields to identify which version was actually read, when and in which scene. Nothing overwrites the earlier bytes.

The sequence graph is captured recursively: every `configFile` and `design`, nested/phased references, `trajectoryFile` plus an available metadata sidecar, and explicit `skyboxPath` images. Duplicate references are deduplicated; identical basenames in different directories remain distinct. Archive/Legacy fallback uses the resolved source location. Resource names for scene prefabs/materials are not file references; generated night-sky images and their provenance retain the existing `Skyboxes` archive.

The shared reader snapshots the exact bytes returned to system, sequence, Choice, Swarm, Kannadi, Dynamic Choice and Optomotor loaders. It also captures loaded trajectory/sky files. Recorded CSV data is cached by content instead of only file timestamp/length, so a different input cannot silently reuse a previously parsed trajectory. Archive IO and hashing happen at configuration/input-load boundaries, not every rendered frame.

`config_manifest.json` includes `schemaVersion`, Unity version, `complete`, each source path/reference, SHA-256, byte length, readable/snapshot paths, role and actual-load timestamps. Missing required files, failed writes or a missing/corrupt saved snapshot make the archive incomplete. Sequence start and experiment/phase application check it and report the failure through the existing error panel. A missing reference in a disabled phase, or a sky file overridden by `uniformSkyColor`, is recorded as unavailable optional input and does not block presentation.

## Audit existing runs

```sh
python3 tools/audit_run_config_snapshots.py Assets/RunData --recent-since 2026-10-01 --report /tmp/run-config-audit.json
```

The audit searches nested legacy copies and verifies hashes for new manifest snapshots. It accepts the comments/trailing commas supported by Json.NET without changing the original files. The report lists each session's missing sequence/system copies, missing referenced experiment files, nested copies and snapshot-integrity failures.

The October 12 audit covered 474 existing folders. All 60 October folders had sequence/system copies; 59 had their referenced experiment configs. One run, `20261011_152342`, referenced `Examples/heading-aligned.example.json` without a saved copy. Across the full older collection, 118 folders lacked at least one referenced config and four lacked a saved sequence. Many older folders predate system config, so absence there does not establish a contemporary system-saving regression. See [historical audit](run-config-historical-audit.json).

The audit is read-only. It never fills an old folder with today's config: that would mislabel current settings as the original experiment. An exact missing historical version can only be recovered from independent evidence such as a contemporaneous backup or known matching source revision. New snapshot behavior requires restarting Play to create a new session; existing folders remain unchanged.

## Verification

`RunConfigArchiveValidation.Run` in a disposable Unity project checks startup copies, nested references, duplicate basenames, absolute/legacy references, optional files, repeat reloads, changed versions, missing files, cyclic references, custom system filenames and snapshot corruption. It also starts the current ControlScene experiment, checks saved hashes and confirms that a missing snapshot prevents sequence start. The existing five-scene phase suite and swarm trajectory/rendering suite check compatibility. [Validation results](run-config-archive-validation-results.json) records the actual results.
