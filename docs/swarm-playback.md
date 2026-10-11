# Bogong moths and recorded swarm playback

Open `Assets/Scenes/ControlScene.unity` and press Play. The active `Assets/StreamingAssets/sequenceConfig.json` auto-starts **Swarm** immediately for 1200 seconds, with a uniform gray sky, the recorded swarm's aggregate direction set to **north (0°)**, and each animal starting at yaw 0°, height 5 m. This standalone experiment explicitly uses metres, flight autopilot 5 m/s, extrapolated moth speed 6 m/s and gain 1. Auto-trim is off for this direct playback recipe. The 599.46-second recording repeats its **birth schedule**, while previously spawned moths keep flying. Set `autoStart: false` to restore the manual Start button.

The individual experiment is `Examples/Swarm/selwyn-trajectory-playback.example.json`; edit its `playback` and `bogongVisual` settings. The terrain alternative `Examples/Sequences/selwyn-trajectory-playback.example.json` uses Choice_Selwyn and an embedded `swarm` object, with southeast presentation. `Templates/swarm-playback.template.json` has a blank data path and produces a generated swarm until one is supplied. Hardware settings remain in your existing `system_config.json`; these edits preserve camera order and tracker endpoints.

## Coordinates and units

The source uses millimetres: +X north, +Y west, +Z up, with Z=0 at 5 m AGL. Converted positions are **metres**, in Unity axes (+X east, +Y up, +Z north):

```
X = -source Y / 1000
Y = 5 + source Z / 1000
Z = source X / 1000
time = TimeExact - earliest TimeExact (seconds)
```

Naive timestamps are Australia/Sydney local time (UTC+11 on October 7, 2026). The shared relative clock preserves gaps between sightings. The converted file retains all 14,429 rows and 430 IDs, local 19:44:35.283–19:54:34.743. The raw source is untouched. Finite outliers, including heights below the stated ground origin, are reported in bounds and retained.

With the stated axes, aggregate heading is **225.64° (southwest)**. The canonical file preserves those axes. The active standalone Swarm recipe uses `playback.yawOffsetDegrees: -225.64127709737375` to present its aggregate displacement at **0° (north)**. The terrain/flight examples use `-90` for **135.64° (southeast)**. Generated motion uses `mu`; recorded motion uses this explicit yaw offset, preserving each measured trajectory and its spread. Set 0 to retain the measured cardinal direction. Flight direction cannot independently establish geographical calibration. Positive yaw is clockwise from above: N=0°, E=90°, S=180°, W=270°.

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
| `playback.continueAfterTrack` | false; true in Selwyn examples | Continue each uninterrupted recorded segment at its terminal velocity. Requires `periodicBoundary: false`. |
| `playback.despawnDistanceMetres` | 100 | In continuation mode, retire a moth when its 3D distance from the current animal exceeds this many physical metres. Scaled correctly in centimetre scenes. |
| `playback.terminalVelocityWindowSeconds` | 0.1 | Estimate tail velocity from displacement over approximately the final 0.1 seconds of that segment; short segments use their available samples. Zero uses the last sample pair. Does not smooth or alter recorded positions. |
| `playback.continuationSpeedMetresPerSecond` | null | Physical speed override for extrapolated tails only; positive m/s, independent of CSV/scene units. Null retains measured terminal speed. Active recorded examples use 6. Recorded samples and birth times are unchanged. A singleton or zero-velocity segment stays stationary. |
| `spawnCenter` | zero in examples | World-unit presentation offset. |
| `spawnVolumeSize` | dataset bounds in playback | Explicit positive X/Y/Z periodic volume if supplied; does not rescale or retime tracks. |

With `continueAfterTrack: false`, IDs are pooled at startup and visible only during their recorded lifetimes, retaining the earlier playback behavior. Position is interpolated, heading follows trajectory velocity/elevation, and constant-speed movers are disabled. Data is parsed once and shared across rigs. Tracks receive world anchors at appearance, loop, or gap reappearance. Existing agents do not translate with the animal or rotate with steering; only an enabled periodic boundary recycles crossings. Each rig retains its stimulus layer.

### Spawn-centred continuation (active Selwyn recipe)

The animal is at the **full recording's sample centroid** of the new spawn volume, not necessarily at the centroid of the moths currently visible. The measured centroid is (-0.2697495303, 5.7251529268, 0.1078053752) metres before presentation yaw. At each birth:

```
anchor = animalPositionAtSpawn + spawnCenter - yaw * (recordingCentroid * unitsScale)
worldPosition(t) = anchor + yaw * (recordedPosition(t) * unitsScale)
after the final sample: recordedPosition(t) = lastPosition + finalVelocity * (t - lastTime)
```

This is a translation of the recorded volume, with an explicit presentation yaw (north in the active recipe, southeast in the terrain examples); it does not rescale, randomize or retime measured observations. The anchor is frozen at spawn. Turning or moving the animal later changes its view of that moth but cannot drag or steer the moth. New births centre a new copy of the same sampled volume on the animal's current position. The old births then fly outside the small mocap volume without any teleportation or periodic wrapping. Following the same direction and speed can keep a moth alongside the animal beyond the original track's endpoint. It is retired only when it exceeds the 100m distance cutoff or the experiment stops. Matched velocities can retain moths across multiple recording loops; the pool grows only when all existing slots are busy, preserves wing phases and continues to use one batch renderer per rig.

Birth times, entry positions, recorded trajectories and inter-arrival intervals are measured; **positions after each recorded segment ends are extrapolated**, so infinite continuation does not establish unmeasured motion or density beyond the capture volume. Empty CSV intervals create no new moths, but earlier tails can remain visible. The median recorded track length is 0.15 seconds (range 0.02–1.83 seconds). Seven gaps exceed 0.1 seconds; they split into separate recorded segments, each with its own birth and continuation, rather than drawing a false connecting trajectory. A segment with only one sample has no measured velocity and remains stationary until the animal leaves the cutoff. Backward seeking clears tails and starts only tracks observed at the seek time, since the animal's past movement is unavailable. Forward frame steps emit every crossed birth, anchoring them at the current frame's animal position.

The active recipe sets `continuationSpeedMetresPerSecond: 6`. This keeps the measured terminal direction but normalizes its speed after the final sample. The short recorded portion still uses its measured velocity (median 4.10 m/s); the original measured terminal-speed median is 5.02 m/s. Set this override to null to reproduce the earlier measured-speed continuation. `locustSpeed` controls generated motion only, and `playback.speed` changes the entire data clock including arrival rate; neither is a tail-only physical speed control.

The recording's row-weighted centroid centres its sampled cloud. It is not a survey of the geometric mocap boundary or an instantaneous population centre. With zero `spawnCenter`, `SwarmTrajectoryPlayback.SpawnVolumeCentroid` tracks the animal; the world centroid of existing moths is free to move away. With `centerOnAnimal: false`, canonical absolute data is retained, without the spawn-centred translation.

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

Near animals use actual low-poly bodies and rudimentary wings. Far animals use an area-weighted bilinear point footprint, fading toward the background rather than failing subpixel triangle rasterization. It distributes the true projected area over one to four pixels, preserving total subpixel contrast as the point crosses pixel boundaries. The previous Gaussian spread this contrast across roughly eight pixels, making individual pixels unnecessarily faint. There is no artificial minimum opacity or increase in physical size. Extreme distances still reach finite 8-bit display precision. No Animator runs per insect. For 30 Hz flapping, 120 FPS is preferable; 60 FPS shows two phases, and 30 FPS can alias. Panel refresh limits available temporal detail.

Flight AGL is settled before the first CSV birth is anchored, preventing first-frame moths from being left behind at the terrain scene’s authored height.

The moth shader uses separate RGB/alpha blending so composited camera textures remain opaque over an opaque background. This preserves the intended faint RGB contrast when exported or displayed in a UI instead of applying transparency a second time. The RGB stimulus rendering is unchanged by the alpha fix.

Existing pooled asynchronous gzip logging is retained. Swarm-scene logs identify agents by `_ID_<source ID>` names and record presented world positions; repeated births add `_C<recording-cycle>_S<segment>` so simultaneous instances are distinguishable. Pool reuse refreshes logger identity caches. Terrain Choice retains its rig logging. Loading a CSV adds no duplicate raw-data writes. Converted data and metadata retain provenance.

## Arrival rate and visibility audit

The supplied recording has 430 IDs over 599.46 seconds: **43.04 unique arrivals/minute**, or 1.39 seconds per arrival on average. Arrival times are bursty: 26–71 IDs per minute, with a longest gap of 12.67 seconds. The 430 allocated root objects are a pool, not 430 simultaneous visible animals. Seven missing-data gaps produce 437 uninterrupted segments, as already documented above.

When multiple IDs have the same recorded timestamp, their median nearest-neighbour distance is **2.16 m**; only four IDs are simultaneously recorded at most. This conditional sample is not a complete census of the surrounding population and does not establish sub-1m spacing. Playback retains the original metre coordinates and spacings. It does not compress the volume or add extra births to create a denser synthetic stimulus.

[Density audit](swarm-density-audit.json) separates measured speeds/spacing from the explicit 6m/s continuation. For a northward observer at 5m/s, an exact-time geometric calculation predicts about 6.2 passes/minute within 1m, 24.4 within 2m and 40.9 within 3m. An arrival is not necessarily a close pass.

The real Unity scene audit sees every source ID, 49 births in the first minute, and about 31 live moths on average with 100m retirement. With the corrected point footprint, 42 first-minute births within 3m produce at least three bytes of gray contrast in native 128px panels; 34 do at 64px. These are numerical detection counts in 5×5 patches, sampled at 0.1s, not a biological visibility threshold. Overlapping moths can contribute to the same patch. Gray sky, body dimensions, source positions and birth times are retained. The low-poly body's diameter now reaches the specified 1cm instead of 0.87cm.

Reproduce the data audit without Unity:

```sh
python3 tools/audit_swarm_playback.py Assets/StreamingAssets/SwarmData/selwyn-oct7-unity.csv --config Assets/StreamingAssets/Examples/Swarm/selwyn-trajectory-playback.example.json --observer-speed 5 --report /tmp/swarm-audit.json
```

`SwarmTrajectoryPlayback.TotalBirths` reports arrivals since the last stream reset/seek; `ActiveCount` reports live moths. `SwarmStartupValidation.Run` tests ControlScene and runs the complete ten-minute scene audit in a disposable project.

## Verification

[Current visibility/speed validation](swarm-visibility-validation-results.json) records the latest rendering, arrival and config checks. [Earlier continuation validation](swarm-continuation-validation-results.json) records 61 playback/render/pooling checks, 30 real four-rig terrain checks, 4,833 config/UI checks, 46 ControlScene → Swarm startup checks and 7,866 legacy dot checks passing. [Source verification](swarm-source-verification.json) independently compares every ID, position and relative TimeExact value against the supplied raw CSV: all 14,429 rows match. Reproduce that audit with `python3 tools/verify_swarm_source.py /path/to/Selwyn_Oct_7_2026_LongFormat.csv Assets/StreamingAssets/SwarmData/selwyn-oct7-unity.csv`.

The local 18-second preview at `Builds/BogongSwarm-preview/swarm-playback-and-close-pass.mp4` is captured from Unity Play Mode at deterministic 120fps. It shows the asset at real time and 0.25x, six recorded-swarm camera faces, then ID 158's measured 17.7cm close pass and its extrapolated tail. Slow motion and the direction-following diagnostic camera are labelled. An 8×8 centre crop enlarges pixels without changing contrast; MP4 decoding was checked to retain the faint 248/255 and 254/255 pixels. Capture background/observer settings are diagnostic only; the video uses the earlier southeast presentation. The active main experiment now uses a gray standalone Swarm sky, north presentation and 5m/s flight autopilot.

Converter tests: `python3 -m unittest discover -s tools -p test_convert_swarm_trajectories.py`. `SwarmTrajectoryValidation.Run` on a disposable Unity copy checks 64×64 renders, 1 cm visibility, subpixel fading, flap poses, interpolation, lifetimes, world anchors, loops and metre/centimetre scale. Existing `BogongDotValidation.Run` and `SelwynSwarmValidation.Run` retain reference raster/timing checks. Timing results describe the test machine, not guaranteed LED workstation FPS.

[Saved results](swarm-playback-validation-results.json): 39 render/playback/prefab checks, 30 real Selwyn sequence checks, 4,743 strict catalog/UI checks, and 798,525 generated-swarm/reference checks passed in Unity 6000.3.16f1. The four-rig test verifies metre dimensions, 5 m/s autopilot, 5 m AGL, per-rig visibility, disabled camera letters and near-plane restoration. The isolated 1,024-agent test across 24 cameras at 64×64 measured median CPU times of 2.27 ms for dots and 2.89 ms for moths on an M2 Pro; these measurements exclude real display hardware and the full experiment frame.
