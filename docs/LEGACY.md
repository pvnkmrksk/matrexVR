# Legacy assets and disabled experiments

## `Assets/StreamingAssets/Archive/`

Hundreds of older sequence configs, choice JSONs, and helper Python scripts. **Active experiments do not reference `Archive/` paths** in the top-level `StreamingAssets/*.json` files shipped as defaults.

Keep Archive for historical reproduction; delete or move only after confirming your lab’s `sequenceConfig.json` does not point there.

## Disabled Unity scenes

`ProjectSettings/EditorBuildSettings.asset` lists many scenes with `enabled: 0` (terrain variants, old choice layouts, Swarm, etc.). They remain in `Assets/Scenes/` for reference.

Enabled by default: `ControlScene`, `Choice`, `Choice_3d`, `ReplayScene` (as of `feature/replay-architecture`).

## Swarm pipeline

`Assets/Scripts/Swarm/*` and `Swarm.unity` are not wired into the current boot → sequence flow. Re-enable the scene in Build Settings and attach `SwarmController` if reviving swarm experiments.

## Optional runtime code

- **`ZmqSender.cs`** — publishes sequence/trial metadata; not placed on prefabs today.
- **`Build*.cs` / `Editor/Build*.cs`** — design JSON generators for offline use.
