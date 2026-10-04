# Selwyn terrain in Git

`Assets/Scenes/Choice_Selwyn.unity` and its generated terrain data are versioned together. A clone includes the height data, colliders, terrain layers, satellite textures and Unity `.meta` GUIDs. The terrain folder is `Assets/RWT_Result/2026-10-05 06-22/` (about 56 MiB). It does not require downloading the source tiles again to render.

| Tile | Heightmap | Diffuse texture | Terrain size in Unity units |
|---|---|---|---|
| Terrain 0x0 | 65 × 65 | 8192 × 4096 JPEG | 15685 × 1821 × 11764 |
| Terrain 0x1 | 65 × 65 | 8192 × 4096 JPEG | 15685 × 1821 × 11764 |

The additional generated TerrainData asset and earlier local terrain/layer assets are retained too. Exact file sizes and SHA-256 hashes are in [the terrain manifest](selwyn-terrain-manifest.json).

## Run

Open `Assets/Scenes/ControlScene.unity`, enter Play mode, then press Start. The active sequence uses `Choice_Selwyn` and `Kannadi/selwyn-night-sky.json`, with manual startup (`autoStart: false`). The current config uses Selwyn's coordinates, the current instant, UTC+11, 30-minute sky refreshes, exposure 0.5, and AGL 100. See [night-sky documentation](night-sky.md) for image exports and activation records.

The scene retains three Real World Terrain authoring components for editing on the original machine. That optional authoring plugin is not included in Git. Without it, Unity reports missing authoring scripts; the generated Unity Terrain/TerrainCollider components, heights, textures and sky rendering still work. The validation copy ran without that plugin and verified both terrain tiles plus 70 sky/runtime checks; see [results](choice-night-sky-validation-results.json).

## Local files and push cleanup

`RWT_Cache/` contains downloaded source heightmaps and imagery used by the authoring tool. `RealWorldTerrainPrefs.xml` contains local authoring settings. Both remain on the original computer and are ignored by Git, along with Finder's `.DS_Store` files. Generated terrain in `Assets/RWT_Result/` remains tracked.

The original unpublished terrain commit included a 179,737,876-byte cached ASCII heightmap, exceeding GitHub's 100 MiB normal Git limit. It was repaired before publication by removing the cache from that commit; no terrain assets were removed. A local recovery branch, `codex/backup-terrain-cache-20261005`, retains the original commit. Do not push that recovery branch or use `git push --all`, which would resend the oversized cache.
