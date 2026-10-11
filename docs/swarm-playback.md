# Bogong moths and recorded swarm playback

Use `Examples/Sequences/selwyn-trajectory-playback.example.json` to run `Choice_Selwyn` for 1200 seconds, looping the 599.46-second recording. It references an individual experiment file; settings belong to that file's `swarm` object. The active sequence selects playback for its second step, preserving your calibration step, duration, gain and auto-trim settings. A standalone Swarm recipe is under `Examples/Swarm`. `Templates/swarm-playback.template.json` has a blank data path and produces a generated swarm until one is supplied.

## Coordinates and units

The source uses millimetres: +X north, +Y west, +Z up, with Z=0 at 5 m AGL. Converted positions are **metres**, in Unity axes (+X east, +Y up, +Z north):

```
X = -source Y / 1000
Y = 5 + source Z / 1000
Z = source X / 1000
time = TimeExact - earliest TimeExact (seconds)
```

Naive timestamps are Australia/Sydney local time (UTC+11 on October 7, 2026). The shared relative clock preserves gaps between sightings. The converted file retains all 14,429 rows and 430 IDs, local 19:44:35.283–19:54:34.743. The raw source is untouched. Finite outliers, including heights below the stated ground origin, are reported in bounds and retained.

With the stated axes, aggregate heading is **225.64° (southwest)**. The canonical file preserves those axes. Examples explicitly use `playback.yawOffsetDegrees: -90` to present it at **135.64° (southeast)**. Set 0 to retain the measured cardinal direction. Flight direction cannot independently establish geographical calibration. Positive yaw is clockwise from above: N=0°, E=90°, S=180°, W=270°.

Terrain/migration uses metres (`worldUnitsPerMetre: 1`); older walking scenes use centimetres (100). Omitted scale defaults to 1 in a scene with active Terrain, otherwise 100. Prefer explicit values. `playback.dataUnitsPerMetre` describes CSV units, default 1. Positions are multiplied by `worldUnitsPerMetre / dataUnitsPerMetre`. Boundary sizes, offsets, generated swarm speed and autopilot speed remain in existing scene units. For terrain, speed 5 means 5 m/s. Moth dimensions and near clip are physical metres, so the body stays one inch long in either scale. The camera's original near plane is restored when the swarm is cleared.

Centering removes the absolute dataset position/height during presentation. For original georeferenced positions, disable both `centerOnAnimal` and `periodicBoundary`.

## Standalone converter

Python 3.9+ with timezone data is sufficient; no third-party dependencies or Unity are needed:

```sh
python3 tools/convert_swarm_trajectories.py /path/to/long-format.csv Assets/StreamingAssets/SwarmData/new-unity.csv
```

Input columns are Frame, ID, X, Y, Z, TimeExact. Output is exactly `ID,X,Y,Z,time`, sorted by time/ID. IDs stay text. Frame is validated and its range retained in metadata. The sidecar records local start, timezone, units, source hash, bounds, rejected rows and transform choices. Empty IDs, invalid frames/timestamps and nonfinite positions are rejected and reported. Duplicate ID/time pairs raise an error; `--duplicates first|last` explicitly changes that policy. Finite position outliers are retained.

Options: `--source-units mm|cm|m` (mm), `--output-units m|cm` (m), `--height-origin-m 5`, `--timezone Australia/Sydney`, `--yaw-degrees 0`. Keep converter yaw 0 when using playback yaw to avoid rotating twice.

## Playback settings

| Field | Default | Meaning |
|---|---|---|
| `trajectoryFile` | empty | StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors. |
| `worldUnitsPerMetre` | scene-dependent | Physical scene scale: 1 terrain or 100 walking. |
| `nearClipMetres` | 0.001 for Moth | 1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists. |
| `playback.dataUnitsPerMetre` | 1 | CSV distance scale. |
| `playback.loop` | true | Repeat until the outer experiment/phase ends. |
| `playback.speed` | 1 | Positive time multiplier. |
| `playback.startTimeSeconds` | 0 | Shared clock offset. |
| `playback.maxGapSeconds` | 0.1 | Interpolate across gaps no longer than this; longer missing intervals are invisible. |
| `playback.yawOffsetDegrees` | 0 | Clockwise presentation yaw independent of steering. |
| `playback.centerOnAnimal` | true | Align the full dataset's sample centroid with the animal at each track appearance. |
| `playback.periodicBoundary` | true | Recycle positions through the moving 3D boundary with overshoot preserved. |
| `spawnCenter` | zero in examples | World-unit presentation offset. |
| `spawnVolumeSize` | dataset bounds in playback | Explicit positive X/Y/Z periodic volume if supplied; does not rescale or retime tracks. |

IDs are pooled at startup and visible only during their recorded lifetimes. Position is interpolated, heading follows trajectory velocity/elevation, and constant-speed movers are disabled. Data is parsed once and shared across rigs. Tracks receive world anchors at appearance, loop, or gap reappearance. Existing agents do not translate with the animal or rotate with steering; only the boundary follows it and recycles crossings. New appearances use the current animal position. Empty recording intervals remain empty; count/density do not invent observations. Each rig retains its stimulus layer.

## Moth visual

Set `agentVisual: "Bogong"`, `bogongVisual.shape: "Moth"`. Dot is still the old default. `Assets/Resources/Bogong/Moth.prefab` is the reusable metre-scale asset. Unity's **GameObject → Experiments → Bogong moth (metres)** creates another editable `BogongMoth` root. Geometry regenerates on enable; edit physical dimensions on the component. Swarms use a shared renderer directly, avoiding one renderer per prefab instance.

| `bogongVisual` field | Default | Meaning |
|---|---|---|
| `shape` | Dot | Moth selects a 3D cigar body and two GPU wing patches. |
| `bodyLengthMetres` | 0.0254 | Length along local +Z. |
| `bodyDiameterMetres` | 0.01 | Body thickness. |
| `wingSpanMetres` | 0.04 | Total two-wing width. |
| `flapFrequencyHz` | 30 | Flap cycles/s; zero gives a static pose. |
| `wingOpacity` | 0.25 | Faint wing blur opacity, 0–1. |
| `subpixelVisibility` | true | Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion. |
| `sizeMode` | World | Physical distance scaling, or Angular for constant apparent body diameter. |
| `angularSizeDegrees` | 1 | Dot diameter, or nominal moth body length, in Angular mode; orientation still changes the projected shape. |
| `size` | 0.5 world units | Legacy dot diameter; Moth uses physical dimensions. |
| `color` | RGBA .12,.12,.12,1 | Dark gray unlit albedo with cheap body shading. Body alpha stays opaque; use wing opacity for blur. |
| `metallic`, `smoothness` | 0, .25 | Legacy compatibility; unlit visual ignores PBR lighting. |
| `flickerFrequencyHz` | 0 | Whole-agent flicker separate from flap. Zero disables. |
| `flickerDutyCycle` | .5 | Visible fraction, 0–1. |

Near animals use actual low-poly bodies and rudimentary wings. Far animals use an area-weighted Gaussian proxy, fading toward the background rather than failing subpixel triangle rasterization. Extreme distances still reach finite 8-bit display precision. No Animator runs per insect. For 30 Hz flapping, 120 FPS is preferable; 60 FPS shows two phases, and 30 FPS can alias. Panel refresh limits available temporal detail.

Existing pooled asynchronous gzip logging is retained. Swarm-scene logs identify agents by `_ID_<source ID>` names and record presented world positions; terrain Choice retains its rig logging. Loading a CSV adds no duplicate raw-data writes. Converted data and metadata retain provenance.

## Verification

Converter tests: `python3 -m unittest discover -s tools -p test_convert_swarm_trajectories.py`. `SwarmTrajectoryValidation.Run` on a disposable Unity copy checks 64×64 renders, 1 cm visibility, subpixel fading, flap poses, interpolation, lifetimes, world anchors, loops and metre/centimetre scale. Existing `BogongDotValidation.Run` and `SelwynSwarmValidation.Run` retain reference raster/timing checks. Timing results describe the test machine, not guaranteed LED workstation FPS.

[Saved results](swarm-playback-validation-results.json): 39 render/playback/prefab checks, 30 real Selwyn sequence checks, 4,743 strict catalog/UI checks, and 798,525 generated-swarm/reference checks passed in Unity 6000.3.16f1. The four-rig test verifies metre dimensions, 5 m/s autopilot, 5 m AGL, per-rig visibility, disabled camera letters and near-plane restoration. The isolated 1,024-agent test across 24 cameras at 64×64 measured median CPU times of 2.27 ms for dots and 2.89 ms for moths on an M2 Pro; these measurements exclude real display hardware and the full experiment frame.
