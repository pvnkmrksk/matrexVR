# Selwyn terrain in Git

`Assets/Scenes/Choice_Selwyn.unity` selects the expanded **Selwyn** terrain from `Assets/RWT_Result/2026-10-05 16-26/`. Both active tiles include height data, matching colliders, terrain layers, satellite textures and Unity `.meta` GUIDs. A clone can render them without downloading source tiles again.

| Active tile | Heightmap | Diffuse texture | Terrain size in Unity units |
|---|---|---|---|
| Terrain 0x0 | 257 × 257 | 4096 × 8192 JPEG | 29684 × 2074 × 51453 |
| Terrain 1x0 | 257 × 257 | 4096 × 8192 JPEG | 29684 × 2074 × 51453 |

The two tiles sit side by side under the `Selwyn` parent at `(-28246, -1929, -27082)`. The second tile is offset by 29684 on X. The scene also retains **Selwyn Old**, an inactive single-tile terrain from `2026-10-05 15-48/`, with a 257 × 257 heightmap and 4096 × 4096 texture. Its parent remains at `(-7538, -1711, -6226)`.

All three export folders are versioned:

- `2026-10-05 06-22/`: the original two tiles, their updated 1024 × 512 texture layers, the original larger textures, and the pre-existing spare TerrainData asset. This export is no longer selected by Choice_Selwyn.
- `2026-10-05 15-48/`: the earlier single-tile Selwyn export, including its full-size and reduced-size textures.
- `2026-10-05 16-26/`: the active two-tile high-resolution Selwyn export, about 72 MiB.

Exact file sizes, SHA-256 hashes, active/inactive scene references, height ranges and layer/texture bindings are in [the terrain manifest](selwyn-terrain-manifest.json). The unused spare TerrainData retains its pre-existing unassigned layer; it is not a scene dependency.

## Running the scene

Open `Assets/Scenes/ControlScene.unity`, enter Play mode, then press Start. The active sequence uses `Choice_Selwyn` and `Kannadi/selwyn-night-sky.json`, with manual startup (`autoStart: false`). The config uses Selwyn's coordinates, the current instant, UTC+11, 30-minute sky refreshes, exposure 0.5, and AGL 100. See [night-sky documentation](night-sky.md) for image exports and activation records.

The scene retains Real World Terrain authoring components for editing on the original machine. That optional authoring plugin is not included in Git. Without it, Unity reports missing authoring scripts; the generated Unity Terrain/TerrainCollider components, heights, textures and sky rendering still work.

## Validation

The 2026-10-05 disposable Unity 6000.3.16f1 project loaded both active tiles and the inactive old tile, verified non-flat heightmaps and matching colliders, and resolved every scene terrain layer and diffuse texture. All six exported TerrainData assets were inspected. The existing Selwyn night-sky/runtime suite passed **70 checks**, including four rigs, six camera faces per rig, sky generation/rendering, archive records and sky transitions. The static GUID/dependency and 22-template validators also passed. The current results are embedded in the manifest; the earlier [sky validation report](choice-night-sky-validation-results.json) remains as historical evidence.

## Local-only authoring state

`RWT_Cache/` contains downloaded source heightmaps and imagery used by the authoring tool. `RealWorldTerrainPrefs.xml` contains local authoring settings. Both remain on the original computer and are ignored by Git, along with Finder's `.DS_Store` files. Generated terrain in `Assets/RWT_Result/` remains tracked.

The original unpublished terrain commit included a 179,737,876-byte cached ASCII heightmap, exceeding GitHub's 100 MiB normal Git limit. It was repaired before publication by removing the cache from that commit; no terrain assets were removed. A local recovery branch, `codex/backup-terrain-cache-20261005`, retains the original commit. Do not push that recovery branch or use `git push --all`, which would resend the oversized cache.
