# Recording pipeline

A session is a bundle of evidence. Animal pose CSVs alone cannot identify all presented stimuli, gains, original inputs or sky orientation. Preserve the whole directory or complete ZIP together with the software version.

## Before the experiment starts

MasterDataLogger establishes a unique timestamped session folder, adding a numeric suffix when a directory or ZIP already exists. RunConfigArchive captures the system and sequence and recursively follows the referenced config/input graph. The studio starts a fresh configured session from the selected system/sequence documents rather than appending new metadata to an old completed run.

The archive retains readable first copies and immutable content-addressed snapshots. If a file changes between actual loads, both versions remain; the manifest distinguishes captured files from the exact versions consumed and records scene/load attribution. It never overwrites the earlier bytes as if the new version had always been the experiment.

## The saved layout

```text
RunData/<session>/
  AnimalMetadata.json
  FlyMetaData.json              transitional equivalent metadata
  system_config.json
  sequenceConfig.json
  config_manifest.json
  Configs/
    Files/...                  readable first copies
    Snapshots/<SHA-256>.*       exact immutable versions
  Skyboxes/
    <skyboxId>.png
    <skyboxId>.json
    usage.jsonl
    loads.csv
  ... animal / population CSV and gzip logs
  runtime_trace.log
```

Resource catalog names for built prefabs/materials are not external files. Referenced experiment/design JSON, trajectory files with metadata sidecars and explicit sky images are captured as files where supported. Generated sky images have their own activation provenance.

## During the run

Animal/world loggers sample the rendered rig pose with scene, trial, sequence/inner-step and experiment-phase context. Optional sensor columns retain the receiver's input representation. Position is the virtual rig's world pose, which can contain autopilot, wind or terrain effects as well as measured tracking.

Swarm recording captures population snapshots in LateUpdate and sends immutable pooled data to a bounded worker. That worker formats, gzips and writes. Pending queue overflow/dropped frames are diagnostic evidence; sampling a transform every rendered frame does not establish an independent fixed-frequency acquisition clock.

Kannadi, band and Optomotor loggers add their own stimulus context. Do not interpret a drum transform or one replicated model root as another measured animal. Headers and data-column counts differ by format and historical version.

## Phase and stimulus attribution

Rows retain experimentPhase where supported. Heading-reference and phase-onset JSONL records describe actual transitions and frozen references. Managed sky IDs join animal/population rows to the exact archived panorama and manifest. Rugosity state records document effective pose/geometry choices and Inspector changes in source workflows.

The configuration manifest records load boundaries; it is not a per-frame world-state recorder. Playback cannot recover an unlogged random state or an exact historical pixel merely from a current file with the same basename.

## Shutdown and ZIP creation

The archive boundary closes ordinary loggers, drains asynchronous population writers and completes gzip trailers before ZIP creation. The original session folder remains if zipping fails. Stopping the process uncleanly can still lose buffered/in-flight work, so retain the trace and original directory when investigating an interrupted session.

## Interpretation rules

- Use column names and the actual header rather than assuming one fixed row width across versions.
- Use timestamp differences, inspect gaps/duplicates, and retain the workstation's timezone context for historical local wall-clock rows.
- Distinguish raw tracker units, converted sensor display values and virtual world units.
- Preserve resets/wraps as discontinuities before estimating speed or paths.
- Read canonical Animals once; transitional metadata aliases describe the same animals and must not be counted again.

## Source and detailed schema

[MasterDataLogger](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/Loggers/MasterDataLogger.cs), [RunConfigArchive](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Scripts/Loggers/RunConfigArchive.cs), [data formats](../data-formats.md), [run snapshots](../run-config-snapshots.md), [replay](../replay.md).
