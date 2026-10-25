# Animals, agents and reversible configuration

In 0.3, **animal** means the real animal controlling a VR rig. **Agent** means a simulated individual, a mirrored representation or a recorded individual shown during playback.

## Name the animals and species

The Session screen creates one animal card for each entry in `system_config.json` → `configs`. Enter any text in **Animal ID**, and a common or scientific name in **Species**. The species is saved as `speciesName` on that rig’s system configuration and as `SpeciesName` in the run’s animal metadata.

```json
{
  "configs": [
    {
      "vrId": "VR1",
      "speciesName": "Agrotis infusa",
      "closedLoopMode": "Kinefly",
      "zmqAddress": "localhost",
      "zmqPort": 9871,
      "displayOrder": "DRBLFU"
    }
  ]
}
```

This is a small system example, not a substitute for the complete hardware template. Set your actual panel dimensions, display number and panel coordinates in System config.

**Add rig** copies the previous rig’s settings, assigns the first unused `VRn` ID and tracking port, and places its panels in the next row or column. Review that layout against your equipment. Runtime scenes activate exactly the configured rigs, create additional rigs from their authored template, and allocate separate stimulus layers. IDs such as `VR10` are supported. Capacity is bounded by the build’s available Unity user layers; setup validation reports when more isolated rigs are requested than the build can provide.

## Metadata compatibility

New runs write `AnimalMetadata.json` with `Animals`, `AnimalID`, `SpeciesName` and `UsedAnimalIDs`. During the 0.3 transition, they also write `FlyMetaData.json` and include the equivalent `Flies`, `FlyID` and `UsedFlyIDs` aliases so existing analysis scripts continue to work. These aliases describe the same animals. Count the canonical `Animals` collection once.

Old metadata imports through those aliases. If a file supplies both formats, canonical values win regardless of the JSON property order, including explicitly empty collections or IDs. Unknown per-session and per-animal extension fields are retained. The setup backup loads `AnimalMetadata.json` first and falls back to `FlyMetaData.json` when the new file is absent.

Existing CSV column names and serialized Unity component/asset identities remain compatible. Source integrations may still use the `FlyData` and `Fly` type aliases; new integrations should use `AnimalData` and `AnimalMetadata`.

## Simulated-agent JSON names

| Preferred field | Compatible older field | Resolution |
| --- | --- | --- |
| `agentCount` | `numberOfLocusts` | Preferred field wins when non-null, including zero. |
| `agentSpeed` | `locustSpeed`, `locoSpeed` | `agentSpeed`, then `locustSpeed`, then `locoSpeed`. |
| `agentSpeedStdDev` | `locustSpeedStdDev` | Preferred field wins when non-null, including zero. |

These fields work in standalone Swarm/Kannadi configs, nested Choice `swarm` objects, and phased Swarm configs. All historical JSON remains readable. The UI shows **Agent count**, **Agent speed** and **Agent speed SD** while preserving the original keys in old files. Inspect **More → Edit JSON** to see the exact spelling being saved.

Generated agent speed uses scene units per second: cm/s in walking scenes and m/s on metre-scale terrain. The standard deviation is not variance. Recorded trajectory timing uses `playback.speed`; changing generated `agentSpeed` does not retime recorded trajectories. A non-null explicit count takes precedence over density-based generation. Omitting both count names preserves the existing authored behavior.

## JSON and UI share one document

System, sequence and experiment JSON remain the source of truth. Opening a form does not inject defaults. Missing, null, false and zero remain distinct. Unrecognized fields, array order and numeric spellings beyond floating-point precision survive edits; a no-op save preserves the original file bytes, including its BOM and whitespace.

**Undo** and **Redo** apply to the selected configuration file and retain the latest 32 editing transactions. Saving leaves that edit history available for the current setup session. **More → Revert to saved JSON** restores the saved values as an undoable edit. **Reload disk (undoable)** accepts external changes and retains your prior applied draft in Undo history. An unapplied raw JSON text draft must first be applied or discarded when saving or navigating.

Saving an edited file uses an atomic replacement and retains its previous exact bytes in the adjacent `.bak` file. An external edit to a dirty document blocks saving until it is resolved. A failed form callback rolls back its partial mutations and preserves existing undo/redo history. Each file saves atomically; saving an entire multi-file workspace is not a filesystem-wide atomic transaction.

Installed desktop builds copy bundled examples to a writable `Config` folder under Unity’s per-user application data directory and write recordings to its `RunData` folder. Application updates seed missing files and preserve existing user JSON. In the Editor, these remain `Assets/StreamingAssets` and `Assets/RunData`. Use `--config-dir <folder>` and `--data-dir <folder>` to choose other directories.

Run `python3 tools/test_config_compatibility.py` after importing the project in Unity to verify these JSON contracts without launching the Editor. It needs a local .NET SDK; `--newtonsoft <DLL>` can supply the dependency explicitly. Unity rendering, rig isolation and physical tracking require their separate runtime checks.
