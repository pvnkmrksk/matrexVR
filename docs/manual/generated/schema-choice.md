# Choice schema fields

This atlas lists **821 emitted property paths** from the current `choice` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## rugosity group

### `rugosity`

Independent visible, collision, height and normal surfaces. IDs refer to RugositySurface components in the scene; omitted values preserve scene tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `rugosity.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

### `rugosity.visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `rugosity.collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `rugosity.heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `rugosity.normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `rugosity.heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

### `rugosity.orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

### `rugosity.centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `rugosity.clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `rugosity.textureScale`

Ground albedo texture repetition multiplier. Authored terrain scenes use 1; 4 makes texture features four times smaller. Omit to retain scene tuning. This does not change terrain height, navigation or camera clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | × authored tiling |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `rugosity.absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `rugosity.normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `rugosity.tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `rugosity.normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

## resetPositionOnStart group

### `resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

## resetRotationOnStart group

### `resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

## uniformSkyColor group

### `uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `uniformSkyColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `uniformSkyColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `uniformSkyColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `uniformSkyColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

## swarm group

### `swarm`

Conspecific swarm stimulus. Contains population, size, direction, speed distribution, boundaries and optional recorded playback settings.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `swarm.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `swarm.playback`

Recorded swarm trajectory playback: time mapping, anchoring, looping and continuation beyond the recorded track.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "dataUnitsPerMetre": 1.0,
  "loop": true,
  "speed": 1.0,
  "startTimeSeconds": 0.0,
  "maxGapSeconds": 0.1,
  "yawOffsetDegrees": 0.0,
  "centerOnAnimal": true,
  "periodicBoundary": true,
  "continueAfterTrack": false,
  "despawnDistanceMetres": 100.0,
  "terminalVelocityWindowSeconds": 0.1,
  "continuationSpeedMetresPerSecond": null,
  "preRollSeconds": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"trajectoryFile","nonEmpty":true}`. This governs the form, not removal of stored values.

### `swarm.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `swarm.playback.speed`

Playback time multiplier. 1 reproduces recorded timing; 2 runs twice as fast. This is not a speed in metres per second.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | × time |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `swarm.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `swarm.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `swarm.playback.despawnDistanceMetres`

In continuation mode, retire a moth when its 3D distance from the current animal exceeds this many physical metres. Scaled correctly in centimetre scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `100.0` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `swarm.playback.terminalVelocityWindowSeconds`

Estimate outgoing velocity from approximately the final 0.1 s of each uninterrupted segment, and incoming velocity from its first 0.1 s. Short segments use available samples; zero uses the endpoint pair. Does not smooth or alter recorded positions.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `swarm.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `swarm.playback.preRollSeconds`

Seconds of straight-line incoming extrapolation before a segment's first sample. Requires continueAfterTrack: true. The passive Selwyn example uses 5 (about 30 m at 6 m/s); active/flying examples use 0. Preserve 0 for historical first-sample spawns. No prior recording cycle is invented at startup.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `swarm.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `swarm.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `swarm.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `swarm.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `swarm.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `swarm.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.locustSpeed`

Generated mean speed in world units/s: cm/s in walking scenes, m/s on terrain. Recorded CSV playback is unaffected.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.locustSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.agentSpeed`

Generated agent speed in scene units/s. Takes precedence over locustSpeed and locoSpeed. Recorded trajectory timing is controlled by playback.speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.agentSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.locoSpeed`

Legacy agent-speed spelling. Used only when agentSpeed and locustSpeed are omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.volumeHalfExtentMetres`

Physical half extent of the generated cube. 2 gives +/-2 m, a 4 m cube; overrides spawn/boundary sizes. Null preserves legacy sizes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `swarm.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `swarm.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `swarm.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `swarm.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.muElevation`

Unity X pitch in degrees: negative tilts upward, positive downward; ignored in 2D.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `swarm.kappaElevation`

3D elevation concentration. Legacy: 0 is uniform, >=10000 fixed. VonMises: concentration uses the corrected circular sampler without that cutoff.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `swarm.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `swarm.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `swarm.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `swarm.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `swarm.bogongVisual.bodyLengthMetres`

Length along local +Z.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0254` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.bodyDiameterMetres`

Physical diameter of the cigar-shaped moth body, in metres. 0.01 means 1 cm. Independent of terrain/walking scene units.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.wingSpanMetres`

Total two-wing width.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.04` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.flapFrequencyHz`

Flap cycles/s; zero gives a static pose.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `30.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.wingOpacity`

Faint wing blur opacity, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `swarm.bogongVisual.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["World"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.angularSizeDegrees`

Dot diameter, or nominal moth body length, in Angular mode; orientation still changes the projected shape.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["Angular"]}`. This governs the form, not removal of stored values.

### `swarm.bogongVisual.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "r": 0.12,
  "g": 0.12,
  "b": 0.12,
  "a": 1.0
}
```

</details>

### `swarm.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `swarm.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `swarm.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `swarm.animationSpeedThreshold`

cm/s planar translation; animate only strictly above threshold. Rotation alone does not animate.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"animateOnMove","oneOf":[true]}`. This governs the form, not removal of stored values.

## headingReference group

### `headingReference`

Measure each animal’s circular mean virtual yaw over an observation window. Save it for later heading-aligned presentations. Observation cannot extend the scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `headingReference.enabled`

Enable per-rig virtual-yaw circular observation.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `headingReference.windowSeconds`

Requested heading-observation window in seconds. The scene or phase duration is the hard limit; an oversized window is shortened for execution while JSON stays unchanged.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `180.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `headingReference.startOffsetSeconds`

Seconds to wait after the scene/phase starts before observing yaw. Must leave some time before the presentation ends.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |

### `headingReference.measureDuringPresentation`

Retained legacy setting. Observation now fits within the configured scene duration; it never extends a scene. False still permits spawners to reapply the completed reference.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `headingReference.saveAs`

Save this mean separately for each VR, for later swarm.headingReferenceId. Cleared at restart and each sequence loop.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

## objects group

### `objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

## objects[] group

### `objects[].groundOnTerrain`

Place this object on the active terrain at its X/Z position. Its configured Y becomes an extra height above terrain. Off uses the explicit world position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].position`

World zero if absent; position object described below.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `objects[].position.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].position.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].position.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].position.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].position.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].position.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].rotation`

Nullable Euler x/y/z degrees; all three present overrides mu.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `objects[].rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].speed`

cm/s on LocustMover/band movement components; example 2.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].meanBlueA`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].meanBlueB`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].switchInterval`

Color switching period, seconds; set positive for switching.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].numberOfInstances`

Band population; example 32.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `objects[].spawnLengthX`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].spawnLengthZ`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].gridType`

0 Hexagonal, 1 Manhattan, 2 Random.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Hexagonal"` |
| `enum` | `["Hexagonal","Manhattan","Random",0,1,2]` |

### `objects[].kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].visibleOffDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].visibleOnDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].lockBoundaryWithAnimalPosition`

Translate band boundary with tracked animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].lockAgentWithAnimalPosition`

Translate agents with their parent animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].prioritizeNumbers`

Prefer requested count over geometric grid filling.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].sectionLengthZ`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].sectionLengthX`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].rotationAngle`

Band layout rotation in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## closedLoopOrientation group

### `closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

## closedLoopPosition group

### `closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

## initialPosition group

### `initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

## initialRotation group

### `initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `initialRotation.x`

Initial Euler pitch about Unity +X, in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `initialRotation.y`

Initial Euler yaw about Unity +Y, in degrees. 0 faces +Z/north; +90 faces +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `initialRotation.z`

Initial Euler roll about Unity +Z, in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

## randomInitialRotation group

### `randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## walkingPositionGain group

### `walkingPositionGain`

Optional walking displacement multiplier. The tracker position is scaled by sphere radius into centimetres; use 0.01 for a terrain scene measured in metres. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## walkingOrientationGain group

### `walkingOrientationGain`

Optional signed multiplier for walking heading changes. 1 keeps the measured turn, negative reverses it, and 0 disables heading response. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## backgroundColor group

### `backgroundColor`

{r,g,b,a} (0–1); null keeps existing camera background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `backgroundColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `backgroundColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `backgroundColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `backgroundColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

## skyboxPath group

### `skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## nightSky group

### `nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `nightSky.enabled`

Render the astronomical night sky using the configured location and time. Disabled sky settings are retained in the file.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `nightSky.latitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `47.6896` |
| `minimum` | `-90` |
| `maximum` | `90` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `nightSky.longitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `9.1881` |
| `minimum` | `-180` |
| `maximum` | `180` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `nightSky.timeZoneId`

Optional legacy/advanced named-zone input for automatic daylight saving. Fallback is Europe/Berlin when no offset is supplied (Konstanz default). Coordinates do not infer a zone. Berlin and Sydney have cross-platform Windows aliases. No zone ID is required when supplying utcOffsetHours.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Europe/Berlin"` |

### `nightSky.utcOffsetHours`

Preferred fixed offset east of UTC, e.g. 11, -5, 5.5, or 5.75. Range −14…+14 in whole-minute increments. Overrides any named zone; no seasonal rules. Active Adaminaby config uses 11.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |

### `nightSky.utcOffsetMinutes`

Legacy equivalent, e.g. 660 for UTC+11. Use either hours or minutes, not both.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `nightSky.date`

Optional yyyy-MM-dd; omitted means today's date in the selected zone.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `nightSky.localTime`

Default "now"; omitted, null or blank also means now. Otherwise "22:30", "22:30:15", or "22:30:15.125" starts at that exact local time on date (or today).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"now"` |

### `nightSky.advanceWithRealTime`

true: advances from the resolved start using an unscaled monotonic clock. False freezes the sampled sky.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `nightSky.updateIntervalMinutes`

30; integer 1…60. Regenerates and archives a panorama each interval. The image is static between updates.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `30` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `nightSky.roundToInterval`

false: exact starting instant, then start+30 minutes, start+60 minutes, etc. Opt into true for nearest UTC interval, ties forward; this can round an explicitly supplied time.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `nightSky.northYawDegrees`

0: north +Z, east +X, zenith +Y. Positive yaw rotates north toward +X. Calibrate against the physical arena.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `nightSky.exposure`

1; linear-source multiplier in (0,100], before clipping and conversion to 8-bit sRGB.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `nightSky.faintDetailCutoff`

0 preserves all source detail. Range 0…1; active preview uses 0.15 to suppress faint texture detail. Pixels at/below the cutoff are black, those at twice the cutoff or above are unchanged, with a smooth fade between, measured on the maximum linear source RGB channel before exposure. Not a magnitude or Bortle scale.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `nightSky.maskBelowHorizon`

true: black below zero altitude. False shows the whole celestial sphere. Scene geometry can still occlude it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `nightSky.imageWidth`

4096; also accepts 2048 or 1024. Height is half width.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `4096` |
| `enum` | `[1024,2048,4096]` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

## windSpeed group

### `windSpeed`

Nonnegative world units/s, independent of FicTrac position gain. Zero disables drift.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":20.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

## windDirection group

### `windDirection`

Degrees wind comes FROM: 0 from +Z, 90 from +X. Motion is opposite.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

## aglHeight group

### `aglHeight`

Height above ground in world units. Zero disables height regulation. Examples: 10, 100, 1000.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

## groundLayerMask group

### `groundLayerMask`

Unity physics layer bitmask used to find terrain below the flying animal for altitude control. -1 includes every layer. Use a mask containing your ground/terrain layers; it is not a distance.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

## autopilotEnabled group

### `autopilotEnabled`

Move continuously forward while the animal controls steering through closed loop orientation. Default depends on the experiment; flying templates enable it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## autopilotSpeed group

### `autopilotSpeed`

Forward autopilot speed in scene units per second: m/s on terrain, cm/s for walking. Applies when autopilot is enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"autopilotEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

## autoTrimSettings group

### `autoTrimSettings`

Per-rig flight classification and automatic yaw-bias compensation. These settings apply while auto-trim is enabled for the sequence block.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `autoTrimSettings.windowSeconds`

Qualified L−R sampling window for yaw-bias compensation, in seconds. It can grow toward maxWindowSeconds when noise-aware trimming needs more data.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `15.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `autoTrimSettings.maxWindowSeconds`

Maximum yaw-trim observation window in seconds. Used by noise-aware compensation.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `120.0` |
| UI unit label | seconds |

### `autoTrimSettings.centerStatistic`

mean removes expected heading drift; median retains median centering.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"mean"` |
| `enum` | `["mean","median"]` |

### `autoTrimSettings.noiseAware`

Extend measurements and suppress corrections within the approximate noise band.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `autoTrimSettings.flightWindowSeconds`

0.2–30 s raw-signal variance window.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":0.2,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `autoTrimSettings.flightCheckEnabled`

Set false to bypass nonflight while retaining measured flight status.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `autoTrimSettings.flightVarianceThreshold`

0–1,000,000 rad²; entry requires variance strictly above it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `autoTrimSettings.flightConfirmationSeconds`

0–30 s continuously above entry threshold before declaring flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | seconds |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `autoTrimSettings.flightVarianceHysteresis`

0–1 multiplier of entry threshold for exiting flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `autoTrimSettings.updateIntervalSeconds`

0.1–60 s between center evaluations.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| UI unit label | seconds |

### `autoTrimSettings.aggressiveness`

0.001–1 fraction of selected residual removed per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI soft slider range | `{"min":0.001,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `autoTrimSettings.maxStepRadians`

0.000001–10 rad maximum DC change per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `autoTrimSettings.maxOffsetRadians`

0.000001–10 rad maximum absolute automatic offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI soft slider range | `{"min":0.01,"max":2.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `autoTrimSettings.settleSeconds`

0–300 s adaptation time after corrections or gain/manual-offset changes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `3.0` |
| UI unit label | seconds |

### `autoTrimSettings.toleranceDegPerSecond`

0–1,000,000 deg/s effective center tolerance after gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

## preStimulus group

### `preStimulus`

Optional phase before the main stimulus. It retains its own duration, movement, sky and stimulus settings when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `preStimulus.rugosity`

Independent visible, collision, height and normal surfaces. IDs refer to RugositySurface components in the scene; omitted values preserve scene tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.rugosity.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.rugosity.visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.rugosity.collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.rugosity.heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.rugosity.normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.rugosity.heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

### `preStimulus.rugosity.orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

### `preStimulus.rugosity.centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `preStimulus.rugosity.clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.rugosity.textureScale`

Ground albedo texture repetition multiplier. Authored terrain scenes use 1; 4 makes texture features four times smaller. Omit to retain scene tuning. This does not change terrain height, navigation or camera clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | × authored tiling |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `preStimulus.rugosity.absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.rugosity.normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.rugosity.tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.rugosity.normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

### `preStimulus.resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.uniformSkyColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.uniformSkyColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.uniformSkyColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.uniformSkyColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm`

Conspecific swarm stimulus. Contains population, size, direction, speed distribution, boundaries and optional recorded playback settings.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.swarm.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `preStimulus.swarm.playback`

Recorded swarm trajectory playback: time mapping, anchoring, looping and continuation beyond the recorded track.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "dataUnitsPerMetre": 1.0,
  "loop": true,
  "speed": 1.0,
  "startTimeSeconds": 0.0,
  "maxGapSeconds": 0.1,
  "yawOffsetDegrees": 0.0,
  "centerOnAnimal": true,
  "periodicBoundary": true,
  "continueAfterTrack": false,
  "despawnDistanceMetres": 100.0,
  "terminalVelocityWindowSeconds": 0.1,
  "continuationSpeedMetresPerSecond": null,
  "preRollSeconds": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"trajectoryFile","nonEmpty":true}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.swarm.playback.speed`

Playback time multiplier. 1 reproduces recorded timing; 2 runs twice as fast. This is not a speed in metres per second.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | × time |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.swarm.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.swarm.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.swarm.playback.despawnDistanceMetres`

In continuation mode, retire a moth when its 3D distance from the current animal exceeds this many physical metres. Scaled correctly in centimetre scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `100.0` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.playback.terminalVelocityWindowSeconds`

Estimate outgoing velocity from approximately the final 0.1 s of each uninterrupted segment, and incoming velocity from its first 0.1 s. Short segments use available samples; zero uses the endpoint pair. Does not smooth or alter recorded positions.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.playback.preRollSeconds`

Seconds of straight-line incoming extrapolation before a segment's first sample. Requires continueAfterTrack: true. The passive Selwyn example uses 5 (about 30 m at 6 m/s); active/flying examples use 0. Preserve 0 for historical first-sample spawns. No prior recording cycle is invented at startup.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.swarm.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `preStimulus.swarm.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.swarm.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.swarm.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.locustSpeed`

Generated mean speed in world units/s: cm/s in walking scenes, m/s on terrain. Recorded CSV playback is unaffected.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.locustSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.agentSpeed`

Generated agent speed in scene units/s. Takes precedence over locustSpeed and locoSpeed. Recorded trajectory timing is controlled by playback.speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.agentSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.locoSpeed`

Legacy agent-speed spelling. Used only when agentSpeed and locustSpeed are omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.volumeHalfExtentMetres`

Physical half extent of the generated cube. 2 gives +/-2 m, a 4 m cube; overrides spawn/boundary sizes. Null preserves legacy sizes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `preStimulus.swarm.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.swarm.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.swarm.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.muElevation`

Unity X pitch in degrees: negative tilts upward, positive downward; ignored in 2D.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.kappaElevation`

3D elevation concentration. Legacy: 0 is uniform, >=10000 fixed. VonMises: concentration uses the corrected circular sampler without that cutoff.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `preStimulus.swarm.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `preStimulus.swarm.bogongVisual.bodyLengthMetres`

Length along local +Z.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0254` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.bodyDiameterMetres`

Physical diameter of the cigar-shaped moth body, in metres. 0.01 means 1 cm. Independent of terrain/walking scene units.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.wingSpanMetres`

Total two-wing width.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.04` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.flapFrequencyHz`

Flap cycles/s; zero gives a static pose.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `30.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.wingOpacity`

Faint wing blur opacity, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `preStimulus.swarm.bogongVisual.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["World"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.angularSizeDegrees`

Dot diameter, or nominal moth body length, in Angular mode; orientation still changes the projected shape.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["Angular"]}`. This governs the form, not removal of stored values.

### `preStimulus.swarm.bogongVisual.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "r": 0.12,
  "g": 0.12,
  "b": 0.12,
  "a": 1.0
}
```

</details>

### `preStimulus.swarm.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.swarm.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.swarm.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.swarm.animationSpeedThreshold`

cm/s planar translation; animate only strictly above threshold. Rotation alone does not animate.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"animateOnMove","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.headingReference`

Measure each animal’s circular mean virtual yaw over an observation window. Save it for later heading-aligned presentations. Observation cannot extend the scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `preStimulus.headingReference.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.headingReference.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `180.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.headingReference.startOffsetSeconds`

Nonnegative finite delay after configuration, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |

### `preStimulus.headingReference.measureDuringPresentation`

Retained legacy setting. Observation now fits within the configured scene duration; it never extends a scene. False still permits spawners to reapply the completed reference.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.headingReference.saveAs`

Save this mean separately for each VR, for later swarm.headingReferenceId. Cleared at restart and each sequence loop.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `preStimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `preStimulus.objects[].groundOnTerrain`

Place this object on the active terrain at its X/Z position. Its configured Y becomes an extra height above terrain. Off uses the explicit world position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].position`

World zero if absent; position object described below.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.objects[].position.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].position.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].position.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].position.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].position.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].position.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].rotation`

Nullable Euler x/y/z degrees; all three present overrides mu.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.objects[].rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].speed`

cm/s on LocustMover/band movement components; example 2.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].meanBlueA`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].meanBlueB`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].switchInterval`

Color switching period, seconds; set positive for switching.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].numberOfInstances`

Band population; example 32.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `preStimulus.objects[].spawnLengthX`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].spawnLengthZ`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].gridType`

0 Hexagonal, 1 Manhattan, 2 Random.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Hexagonal"` |
| `enum` | `["Hexagonal","Manhattan","Random",0,1,2]` |

### `preStimulus.objects[].kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].visibleOffDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].visibleOnDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].lockBoundaryWithAnimalPosition`

Translate band boundary with tracked animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].lockAgentWithAnimalPosition`

Translate agents with their parent animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].prioritizeNumbers`

Prefer requested count over geometric grid filling.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].sectionLengthZ`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].sectionLengthX`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].rotationAngle`

Band layout rotation in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `preStimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `preStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.walkingPositionGain`

Optional walking displacement multiplier. The tracker position is scaled by sphere radius into centimetres; use 0.01 for a terrain scene measured in metres. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.walkingOrientationGain`

Optional signed multiplier for walking heading changes. 1 keeps the measured turn, negative reverses it, and 0 disables heading response. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.backgroundColor`

{r,g,b,a} (0–1); null keeps existing camera background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.backgroundColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.backgroundColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.backgroundColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.backgroundColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.nightSky.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.nightSky.latitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `47.6896` |
| `minimum` | `-90` |
| `maximum` | `90` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.nightSky.longitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `9.1881` |
| `minimum` | `-180` |
| `maximum` | `180` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.nightSky.timeZoneId`

Optional legacy/advanced named-zone input for automatic daylight saving. Fallback is Europe/Berlin when no offset is supplied (Konstanz default). Coordinates do not infer a zone. Berlin and Sydney have cross-platform Windows aliases. No zone ID is required when supplying utcOffsetHours.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Europe/Berlin"` |

### `preStimulus.nightSky.utcOffsetHours`

Preferred fixed offset east of UTC, e.g. 11, -5, 5.5, or 5.75. Range −14…+14 in whole-minute increments. Overrides any named zone; no seasonal rules. Active Adaminaby config uses 11.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.nightSky.utcOffsetMinutes`

Legacy equivalent, e.g. 660 for UTC+11. Use either hours or minutes, not both.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `preStimulus.nightSky.date`

Optional yyyy-MM-dd; omitted means today's date in the selected zone.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.nightSky.localTime`

Default "now"; omitted, null or blank also means now. Otherwise "22:30", "22:30:15", or "22:30:15.125" starts at that exact local time on date (or today).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"now"` |

### `preStimulus.nightSky.advanceWithRealTime`

true: advances from the resolved start using an unscaled monotonic clock. False freezes the sampled sky.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.nightSky.updateIntervalMinutes`

30; integer 1…60. Regenerates and archives a panorama each interval. The image is static between updates.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `30` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `preStimulus.nightSky.roundToInterval`

false: exact starting instant, then start+30 minutes, start+60 minutes, etc. Opt into true for nearest UTC interval, ties forward; this can round an explicitly supplied time.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.nightSky.northYawDegrees`

0: north +Z, east +X, zenith +Y. Positive yaw rotates north toward +X. Calibrate against the physical arena.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.nightSky.exposure`

1; linear-source multiplier in (0,100], before clipping and conversion to 8-bit sRGB.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.nightSky.faintDetailCutoff`

0 preserves all source detail. Range 0…1; active preview uses 0.15 to suppress faint texture detail. Pixels at/below the cutoff are black, those at twice the cutoff or above are unchanged, with a smooth fade between, measured on the maximum linear source RGB channel before exposure. Not a magnitude or Bortle scale.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.nightSky.maskBelowHorizon`

true: black below zero altitude. False shows the whole celestial sphere. Scene geometry can still occlude it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.nightSky.imageWidth`

4096; also accepts 2048 or 1024. Height is half width.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `4096` |
| `enum` | `[1024,2048,4096]` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `preStimulus.windSpeed`

Nonnegative world units/s, independent of FicTrac position gain. Zero disables drift.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":20.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `preStimulus.windDirection`

Degrees wind comes FROM: 0 from +Z, 90 from +X. Motion is opposite.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `preStimulus.aglHeight`

Height above ground in world units. Zero disables height regulation. Examples: 10, 100, 1000.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `preStimulus.groundLayerMask`

Unity physics layer bitmask used to find terrain below the flying animal for altitude control. -1 includes every layer. Use a mask containing your ground/terrain layers; it is not a distance.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `preStimulus.autopilotEnabled`

Move continuously forward while the animal controls steering through closed loop orientation. Default depends on the experiment; flying templates enable it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.autopilotSpeed`

Forward autopilot speed in scene units per second: m/s on terrain, cm/s for walking. Applies when autopilot is enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"autopilotEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.autoTrimSettings`

Per-rig flight classification and automatic yaw-bias compensation. These settings apply while auto-trim is enabled for the sequence block.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `preStimulus.autoTrimSettings.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `15.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.autoTrimSettings.maxWindowSeconds`

1–300 s maximum noise-verification window, at least the initial window in effect.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `120.0` |
| UI unit label | seconds |

### `preStimulus.autoTrimSettings.centerStatistic`

mean removes expected heading drift; median retains median centering.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"mean"` |
| `enum` | `["mean","median"]` |

### `preStimulus.autoTrimSettings.noiseAware`

Extend measurements and suppress corrections within the approximate noise band.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.autoTrimSettings.flightWindowSeconds`

0.2–30 s raw-signal variance window.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":0.2,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.autoTrimSettings.flightCheckEnabled`

Set false to bypass nonflight while retaining measured flight status.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.autoTrimSettings.flightVarianceThreshold`

0–1,000,000 rad²; entry requires variance strictly above it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.autoTrimSettings.flightConfirmationSeconds`

0–30 s continuously above entry threshold before declaring flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | seconds |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.autoTrimSettings.flightVarianceHysteresis`

0–1 multiplier of entry threshold for exiting flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.autoTrimSettings.updateIntervalSeconds`

0.1–60 s between center evaluations.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| UI unit label | seconds |

### `preStimulus.autoTrimSettings.aggressiveness`

0.001–1 fraction of selected residual removed per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI soft slider range | `{"min":0.001,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.autoTrimSettings.maxStepRadians`

0.000001–10 rad maximum DC change per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `preStimulus.autoTrimSettings.maxOffsetRadians`

0.000001–10 rad maximum absolute automatic offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI soft slider range | `{"min":0.01,"max":2.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.autoTrimSettings.settleSeconds`

0–300 s adaptation time after corrections or gain/manual-offset changes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `3.0` |
| UI unit label | seconds |

### `preStimulus.autoTrimSettings.toleranceDegPerSecond`

0–1,000,000 deg/s effective center tolerance after gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `preStimulus.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.durationSeconds`

Duration of this phase, in seconds. Its clock takes priority over the heading observation window; oversized windows are shortened with a warning.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10` |
| UI unit label | seconds |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":1,"max":3600,"step":1}`; display/edit convenience, not an extra runtime constraint. |

## stimulus group

### `stimulus`

Main phase of this experiment. Duration, motion and stimuli are configured within this phase.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `stimulus.rugosity`

Independent visible, collision, height and normal surfaces. IDs refer to RugositySurface components in the scene; omitted values preserve scene tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.rugosity.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.rugosity.visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.rugosity.collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.rugosity.heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.rugosity.normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.rugosity.heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

### `stimulus.rugosity.orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

### `stimulus.rugosity.centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `stimulus.rugosity.clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `stimulus.rugosity.textureScale`

Ground albedo texture repetition multiplier. Authored terrain scenes use 1; 4 makes texture features four times smaller. Omit to retain scene tuning. This does not change terrain height, navigation or camera clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | × authored tiling |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `stimulus.rugosity.absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.rugosity.normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `stimulus.rugosity.tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `stimulus.rugosity.normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

### `stimulus.resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.uniformSkyColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.uniformSkyColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.uniformSkyColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.uniformSkyColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm`

Conspecific swarm stimulus. Contains population, size, direction, speed distribution, boundaries and optional recorded playback settings.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.swarm.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `stimulus.swarm.playback`

Recorded swarm trajectory playback: time mapping, anchoring, looping and continuation beyond the recorded track.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "dataUnitsPerMetre": 1.0,
  "loop": true,
  "speed": 1.0,
  "startTimeSeconds": 0.0,
  "maxGapSeconds": 0.1,
  "yawOffsetDegrees": 0.0,
  "centerOnAnimal": true,
  "periodicBoundary": true,
  "continueAfterTrack": false,
  "despawnDistanceMetres": 100.0,
  "terminalVelocityWindowSeconds": 0.1,
  "continuationSpeedMetresPerSecond": null,
  "preRollSeconds": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"trajectoryFile","nonEmpty":true}`. This governs the form, not removal of stored values.

### `stimulus.swarm.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.swarm.playback.speed`

Playback time multiplier. 1 reproduces recorded timing; 2 runs twice as fast. This is not a speed in metres per second.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | × time |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.swarm.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.swarm.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.swarm.playback.despawnDistanceMetres`

In continuation mode, retire a moth when its 3D distance from the current animal exceeds this many physical metres. Scaled correctly in centimetre scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `100.0` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.playback.terminalVelocityWindowSeconds`

Estimate outgoing velocity from approximately the final 0.1 s of each uninterrupted segment, and incoming velocity from its first 0.1 s. Short segments use available samples; zero uses the endpoint pair. Does not smooth or alter recorded positions.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.playback.preRollSeconds`

Seconds of straight-line incoming extrapolation before a segment's first sample. Requires continueAfterTrack: true. The passive Selwyn example uses 5 (about 30 m at 6 m/s); active/flying examples use 0. Preserve 0 for historical first-sample spawns. No prior recording cycle is invented at startup.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.swarm.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `stimulus.swarm.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.swarm.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.swarm.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.locustSpeed`

Generated mean speed in world units/s: cm/s in walking scenes, m/s on terrain. Recorded CSV playback is unaffected.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.locustSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.agentSpeed`

Generated agent speed in scene units/s. Takes precedence over locustSpeed and locoSpeed. Recorded trajectory timing is controlled by playback.speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.agentSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.locoSpeed`

Legacy agent-speed spelling. Used only when agentSpeed and locustSpeed are omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.volumeHalfExtentMetres`

Physical half extent of the generated cube. 2 gives +/-2 m, a 4 m cube; overrides spawn/boundary sizes. Null preserves legacy sizes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `stimulus.swarm.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.swarm.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.swarm.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.muElevation`

Unity X pitch in degrees: negative tilts upward, positive downward; ignored in 2D.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.kappaElevation`

3D elevation concentration. Legacy: 0 is uniform, >=10000 fixed. VonMises: concentration uses the corrected circular sampler without that cutoff.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `stimulus.swarm.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `stimulus.swarm.bogongVisual.bodyLengthMetres`

Length along local +Z.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0254` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.bodyDiameterMetres`

Physical diameter of the cigar-shaped moth body, in metres. 0.01 means 1 cm. Independent of terrain/walking scene units.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.wingSpanMetres`

Total two-wing width.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.04` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.flapFrequencyHz`

Flap cycles/s; zero gives a static pose.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `30.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.wingOpacity`

Faint wing blur opacity, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `stimulus.swarm.bogongVisual.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["World"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.angularSizeDegrees`

Dot diameter, or nominal moth body length, in Angular mode; orientation still changes the projected shape.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["Angular"]}`. This governs the form, not removal of stored values.

### `stimulus.swarm.bogongVisual.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "r": 0.12,
  "g": 0.12,
  "b": 0.12,
  "a": 1.0
}
```

</details>

### `stimulus.swarm.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.swarm.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.swarm.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.swarm.animationSpeedThreshold`

cm/s planar translation; animate only strictly above threshold. Rotation alone does not animate.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"animateOnMove","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.headingReference`

Measure each animal’s circular mean virtual yaw over an observation window. Save it for later heading-aligned presentations. Observation cannot extend the scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `stimulus.headingReference.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.headingReference.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `180.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.headingReference.startOffsetSeconds`

Nonnegative finite delay after configuration, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |

### `stimulus.headingReference.measureDuringPresentation`

Retained legacy setting. Observation now fits within the configured scene duration; it never extends a scene. False still permits spawners to reapply the completed reference.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.headingReference.saveAs`

Save this mean separately for each VR, for later swarm.headingReferenceId. Cleared at restart and each sequence loop.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `stimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `stimulus.objects[].groundOnTerrain`

Place this object on the active terrain at its X/Z position. Its configured Y becomes an extra height above terrain. Off uses the explicit world position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].position`

World zero if absent; position object described below.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.objects[].position.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].position.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].position.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].position.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].position.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].position.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].rotation`

Nullable Euler x/y/z degrees; all three present overrides mu.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.objects[].rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].speed`

cm/s on LocustMover/band movement components; example 2.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].meanBlueA`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].meanBlueB`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].switchInterval`

Color switching period, seconds; set positive for switching.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].numberOfInstances`

Band population; example 32.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `stimulus.objects[].spawnLengthX`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].spawnLengthZ`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].gridType`

0 Hexagonal, 1 Manhattan, 2 Random.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Hexagonal"` |
| `enum` | `["Hexagonal","Manhattan","Random",0,1,2]` |

### `stimulus.objects[].kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].visibleOffDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].visibleOnDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].lockBoundaryWithAnimalPosition`

Translate band boundary with tracked animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].lockAgentWithAnimalPosition`

Translate agents with their parent animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].prioritizeNumbers`

Prefer requested count over geometric grid filling.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].sectionLengthZ`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].sectionLengthX`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].rotationAngle`

Band layout rotation in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `stimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `stimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.walkingPositionGain`

Optional walking displacement multiplier. The tracker position is scaled by sphere radius into centimetres; use 0.01 for a terrain scene measured in metres. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.walkingOrientationGain`

Optional signed multiplier for walking heading changes. 1 keeps the measured turn, negative reverses it, and 0 disables heading response. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.backgroundColor`

{r,g,b,a} (0–1); null keeps existing camera background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.backgroundColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.backgroundColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.backgroundColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.backgroundColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.nightSky.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.nightSky.latitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `47.6896` |
| `minimum` | `-90` |
| `maximum` | `90` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.nightSky.longitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `9.1881` |
| `minimum` | `-180` |
| `maximum` | `180` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.nightSky.timeZoneId`

Optional legacy/advanced named-zone input for automatic daylight saving. Fallback is Europe/Berlin when no offset is supplied (Konstanz default). Coordinates do not infer a zone. Berlin and Sydney have cross-platform Windows aliases. No zone ID is required when supplying utcOffsetHours.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Europe/Berlin"` |

### `stimulus.nightSky.utcOffsetHours`

Preferred fixed offset east of UTC, e.g. 11, -5, 5.5, or 5.75. Range −14…+14 in whole-minute increments. Overrides any named zone; no seasonal rules. Active Adaminaby config uses 11.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.nightSky.utcOffsetMinutes`

Legacy equivalent, e.g. 660 for UTC+11. Use either hours or minutes, not both.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `stimulus.nightSky.date`

Optional yyyy-MM-dd; omitted means today's date in the selected zone.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.nightSky.localTime`

Default "now"; omitted, null or blank also means now. Otherwise "22:30", "22:30:15", or "22:30:15.125" starts at that exact local time on date (or today).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"now"` |

### `stimulus.nightSky.advanceWithRealTime`

true: advances from the resolved start using an unscaled monotonic clock. False freezes the sampled sky.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.nightSky.updateIntervalMinutes`

30; integer 1…60. Regenerates and archives a panorama each interval. The image is static between updates.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `30` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `stimulus.nightSky.roundToInterval`

false: exact starting instant, then start+30 minutes, start+60 minutes, etc. Opt into true for nearest UTC interval, ties forward; this can round an explicitly supplied time.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.nightSky.northYawDegrees`

0: north +Z, east +X, zenith +Y. Positive yaw rotates north toward +X. Calibrate against the physical arena.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.nightSky.exposure`

1; linear-source multiplier in (0,100], before clipping and conversion to 8-bit sRGB.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.nightSky.faintDetailCutoff`

0 preserves all source detail. Range 0…1; active preview uses 0.15 to suppress faint texture detail. Pixels at/below the cutoff are black, those at twice the cutoff or above are unchanged, with a smooth fade between, measured on the maximum linear source RGB channel before exposure. Not a magnitude or Bortle scale.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.nightSky.maskBelowHorizon`

true: black below zero altitude. False shows the whole celestial sphere. Scene geometry can still occlude it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.nightSky.imageWidth`

4096; also accepts 2048 or 1024. Height is half width.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `4096` |
| `enum` | `[1024,2048,4096]` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `stimulus.windSpeed`

Nonnegative world units/s, independent of FicTrac position gain. Zero disables drift.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":20.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `stimulus.windDirection`

Degrees wind comes FROM: 0 from +Z, 90 from +X. Motion is opposite.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `stimulus.aglHeight`

Height above ground in world units. Zero disables height regulation. Examples: 10, 100, 1000.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `stimulus.groundLayerMask`

Unity physics layer bitmask used to find terrain below the flying animal for altitude control. -1 includes every layer. Use a mask containing your ground/terrain layers; it is not a distance.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `stimulus.autopilotEnabled`

Move continuously forward while the animal controls steering through closed loop orientation. Default depends on the experiment; flying templates enable it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.autopilotSpeed`

Forward autopilot speed in scene units per second: m/s on terrain, cm/s for walking. Applies when autopilot is enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"autopilotEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.autoTrimSettings`

Per-rig flight classification and automatic yaw-bias compensation. These settings apply while auto-trim is enabled for the sequence block.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `stimulus.autoTrimSettings.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `15.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.autoTrimSettings.maxWindowSeconds`

1–300 s maximum noise-verification window, at least the initial window in effect.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `120.0` |
| UI unit label | seconds |

### `stimulus.autoTrimSettings.centerStatistic`

mean removes expected heading drift; median retains median centering.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"mean"` |
| `enum` | `["mean","median"]` |

### `stimulus.autoTrimSettings.noiseAware`

Extend measurements and suppress corrections within the approximate noise band.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.autoTrimSettings.flightWindowSeconds`

0.2–30 s raw-signal variance window.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":0.2,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.autoTrimSettings.flightCheckEnabled`

Set false to bypass nonflight while retaining measured flight status.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.autoTrimSettings.flightVarianceThreshold`

0–1,000,000 rad²; entry requires variance strictly above it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.autoTrimSettings.flightConfirmationSeconds`

0–30 s continuously above entry threshold before declaring flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | seconds |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.autoTrimSettings.flightVarianceHysteresis`

0–1 multiplier of entry threshold for exiting flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.autoTrimSettings.updateIntervalSeconds`

0.1–60 s between center evaluations.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| UI unit label | seconds |

### `stimulus.autoTrimSettings.aggressiveness`

0.001–1 fraction of selected residual removed per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI soft slider range | `{"min":0.001,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.autoTrimSettings.maxStepRadians`

0.000001–10 rad maximum DC change per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `stimulus.autoTrimSettings.maxOffsetRadians`

0.000001–10 rad maximum absolute automatic offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI soft slider range | `{"min":0.01,"max":2.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.autoTrimSettings.settleSeconds`

0–300 s adaptation time after corrections or gain/manual-offset changes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `3.0` |
| UI unit label | seconds |

### `stimulus.autoTrimSettings.toleranceDegPerSecond`

0–1,000,000 deg/s effective center tolerance after gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `stimulus.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.durationSeconds`

Duration of this phase, in seconds. Its clock takes priority over the heading observation window; oversized windows are shortened with a warning.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10` |
| UI unit label | seconds |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":1,"max":3600,"step":1}`; display/edit convenience, not an extra runtime constraint. |

## postStimulus group

### `postStimulus`

Optional phase after the main stimulus. It retains its own duration, movement, sky and stimulus settings when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `postStimulus.rugosity`

Independent visible, collision, height and normal surfaces. IDs refer to RugositySurface components in the scene; omitted values preserve scene tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.rugosity.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.rugosity.visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.rugosity.collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.rugosity.heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.rugosity.normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.rugosity.heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

### `postStimulus.rugosity.orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

### `postStimulus.rugosity.centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `postStimulus.rugosity.clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.rugosity.textureScale`

Ground albedo texture repetition multiplier. Authored terrain scenes use 1; 4 makes texture features four times smaller. Omit to retain scene tuning. This does not change terrain height, navigation or camera clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | × authored tiling |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `postStimulus.rugosity.absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.rugosity.normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.rugosity.tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.rugosity.normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

### `postStimulus.resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.uniformSkyColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.uniformSkyColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.uniformSkyColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.uniformSkyColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm`

Conspecific swarm stimulus. Contains population, size, direction, speed distribution, boundaries and optional recorded playback settings.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.swarm.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `postStimulus.swarm.playback`

Recorded swarm trajectory playback: time mapping, anchoring, looping and continuation beyond the recorded track.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "dataUnitsPerMetre": 1.0,
  "loop": true,
  "speed": 1.0,
  "startTimeSeconds": 0.0,
  "maxGapSeconds": 0.1,
  "yawOffsetDegrees": 0.0,
  "centerOnAnimal": true,
  "periodicBoundary": true,
  "continueAfterTrack": false,
  "despawnDistanceMetres": 100.0,
  "terminalVelocityWindowSeconds": 0.1,
  "continuationSpeedMetresPerSecond": null,
  "preRollSeconds": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"trajectoryFile","nonEmpty":true}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.swarm.playback.speed`

Playback time multiplier. 1 reproduces recorded timing; 2 runs twice as fast. This is not a speed in metres per second.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | × time |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.swarm.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.swarm.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.swarm.playback.despawnDistanceMetres`

In continuation mode, retire a moth when its 3D distance from the current animal exceeds this many physical metres. Scaled correctly in centimetre scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `100.0` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.playback.terminalVelocityWindowSeconds`

Estimate outgoing velocity from approximately the final 0.1 s of each uninterrupted segment, and incoming velocity from its first 0.1 s. Short segments use available samples; zero uses the endpoint pair. Does not smooth or alter recorded positions.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.playback.preRollSeconds`

Seconds of straight-line incoming extrapolation before a segment's first sample. Requires continueAfterTrack: true. The passive Selwyn example uses 5 (about 30 m at 6 m/s); active/flying examples use 0. Preserve 0 for historical first-sample spawns. No prior recording cycle is invented at startup.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.swarm.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `postStimulus.swarm.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.swarm.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.swarm.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.locustSpeed`

Generated mean speed in world units/s: cm/s in walking scenes, m/s on terrain. Recorded CSV playback is unaffected.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.locustSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.agentSpeed`

Generated agent speed in scene units/s. Takes precedence over locustSpeed and locoSpeed. Recorded trajectory timing is controlled by playback.speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.agentSpeedStdDev`

Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.locoSpeed`

Legacy agent-speed spelling. Used only when agentSpeed and locustSpeed are omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | scene units / s |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.volumeHalfExtentMetres`

Physical half extent of the generated cube. 2 gives +/-2 m, a 4 m cube; overrides spawn/boundary sizes. Null preserves legacy sizes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `postStimulus.swarm.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.swarm.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.swarm.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.muElevation`

Unity X pitch in degrees: negative tilts upward, positive downward; ignored in 2D.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.kappaElevation`

3D elevation concentration. Legacy: 0 is uniform, >=10000 fixed. VonMises: concentration uses the corrected circular sampler without that cutoff.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `postStimulus.swarm.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `postStimulus.swarm.bogongVisual.bodyLengthMetres`

Length along local +Z.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0254` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.bodyDiameterMetres`

Physical diameter of the cigar-shaped moth body, in metres. 0.01 means 1 cm. Independent of terrain/walking scene units.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.wingSpanMetres`

Total two-wing width.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.04` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.flapFrequencyHz`

Flap cycles/s; zero gives a static pose.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `30.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.wingOpacity`

Faint wing blur opacity, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `postStimulus.swarm.bogongVisual.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["World"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.angularSizeDegrees`

Dot diameter, or nominal moth body length, in Angular mode; orientation still changes the projected shape.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["Angular"]}`. This governs the form, not removal of stored values.

### `postStimulus.swarm.bogongVisual.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "r": 0.12,
  "g": 0.12,
  "b": 0.12,
  "a": 1.0
}
```

</details>

### `postStimulus.swarm.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.swarm.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.swarm.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.swarm.animationSpeedThreshold`

cm/s planar translation; animate only strictly above threshold. Rotation alone does not animate.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"animateOnMove","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.headingReference`

Measure each animal’s circular mean virtual yaw over an observation window. Save it for later heading-aligned presentations. Observation cannot extend the scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `postStimulus.headingReference.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.headingReference.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `180.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.headingReference.startOffsetSeconds`

Nonnegative finite delay after configuration, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |

### `postStimulus.headingReference.measureDuringPresentation`

Retained legacy setting. Observation now fits within the configured scene duration; it never extends a scene. False still permits spawners to reapply the completed reference.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.headingReference.saveAs`

Save this mean separately for each VR, for later swarm.headingReferenceId. Cleared at restart and each sequence loop.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `postStimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `postStimulus.objects[].groundOnTerrain`

Place this object on the active terrain at its X/Z position. Its configured Y becomes an extra height above terrain. Off uses the explicit world position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].position`

World zero if absent; position object described below.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.objects[].position.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].position.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].position.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].position.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].position.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].position.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].rotation`

Nullable Euler x/y/z degrees; all three present overrides mu.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.objects[].rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].speed`

cm/s on LocustMover/band movement components; example 2.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].mu`

Mean/explicit yaw in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].meanBlueA`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].meanBlueB`

Blue-channel means for supported color-drift stimuli; normalized 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].switchInterval`

Color switching period, seconds; set positive for switching.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].numberOfInstances`

Band population; example 32.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `postStimulus.objects[].spawnLengthX`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].spawnLengthZ`

Band spawn extent in world units; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].gridType`

0 Hexagonal, 1 Manhattan, 2 Random.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Hexagonal"` |
| `enum` | `["Hexagonal","Manhattan","Random",0,1,2]` |

### `postStimulus.objects[].kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].visibleOffDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].visibleOnDuration`

Band invisibility/visibility durations in seconds; example 0/10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].lockBoundaryWithAnimalPosition`

Translate band boundary with tracked animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].lockAgentWithAnimalPosition`

Translate agents with their parent animal.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].prioritizeNumbers`

Prefer requested count over geometric grid filling.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].sectionLengthZ`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].sectionLengthX`

Band grid section dimensions; example 100 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].rotationAngle`

Band layout rotation in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

</details>

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `postStimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `postStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.walkingPositionGain`

Optional walking displacement multiplier. The tracker position is scaled by sphere radius into centimetres; use 0.01 for a terrain scene measured in metres. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.walkingOrientationGain`

Optional signed multiplier for walking heading changes. 1 keeps the measured turn, negative reverses it, and 0 disables heading response. Omitted keeps the existing walking gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.backgroundColor`

{r,g,b,a} (0–1); null keeps existing camera background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.backgroundColor.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.backgroundColor.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.backgroundColor.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.backgroundColor.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.nightSky.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.nightSky.latitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `47.6896` |
| `minimum` | `-90` |
| `maximum` | `90` |
| UI soft slider range | `{"min":-90.0,"max":90.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.nightSky.longitude`

Approximate University of Konstanz campus location, 47.6896° N, 9.1881° E, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `9.1881` |
| `minimum` | `-180` |
| `maximum` | `180` |
| UI soft slider range | `{"min":-180.0,"max":180.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.nightSky.timeZoneId`

Optional legacy/advanced named-zone input for automatic daylight saving. Fallback is Europe/Berlin when no offset is supplied (Konstanz default). Coordinates do not infer a zone. Berlin and Sydney have cross-platform Windows aliases. No zone ID is required when supplying utcOffsetHours.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Europe/Berlin"` |

### `postStimulus.nightSky.utcOffsetHours`

Preferred fixed offset east of UTC, e.g. 11, -5, 5.5, or 5.75. Range −14…+14 in whole-minute increments. Overrides any named zone; no seasonal rules. Active Adaminaby config uses 11.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.nightSky.utcOffsetMinutes`

Legacy equivalent, e.g. 660 for UTC+11. Use either hours or minutes, not both.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `postStimulus.nightSky.date`

Optional yyyy-MM-dd; omitted means today's date in the selected zone.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.nightSky.localTime`

Default "now"; omitted, null or blank also means now. Otherwise "22:30", "22:30:15", or "22:30:15.125" starts at that exact local time on date (or today).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"now"` |

### `postStimulus.nightSky.advanceWithRealTime`

true: advances from the resolved start using an unscaled monotonic clock. False freezes the sampled sky.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.nightSky.updateIntervalMinutes`

30; integer 1…60. Regenerates and archives a panorama each interval. The image is static between updates.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `30` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `postStimulus.nightSky.roundToInterval`

false: exact starting instant, then start+30 minutes, start+60 minutes, etc. Opt into true for nearest UTC interval, ties forward; this can round an explicitly supplied time.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.nightSky.northYawDegrees`

0: north +Z, east +X, zenith +Y. Positive yaw rotates north toward +X. Calibrate against the physical arena.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.nightSky.exposure`

1; linear-source multiplier in (0,100], before clipping and conversion to 8-bit sRGB.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.1,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.nightSky.faintDetailCutoff`

0 preserves all source detail. Range 0…1; active preview uses 0.15 to suppress faint texture detail. Pixels at/below the cutoff are black, those at twice the cutoff or above are unchanged, with a smooth fade between, measured on the maximum linear source RGB channel before exposure. Not a magnitude or Bortle scale.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.nightSky.maskBelowHorizon`

true: black below zero altitude. False shows the whole celestial sphere. Scene geometry can still occlude it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.nightSky.imageWidth`

4096; also accepts 2048 or 1024. Height is half width.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `4096` |
| `enum` | `[1024,2048,4096]` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `postStimulus.windSpeed`

Nonnegative world units/s, independent of FicTrac position gain. Zero disables drift.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":20.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `postStimulus.windDirection`

Degrees wind comes FROM: 0 from +Z, 90 from +X. Motion is opposite.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":360.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `postStimulus.aglHeight`

Height above ground in world units. Zero disables height regulation. Examples: 10, 100, 1000.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `postStimulus.groundLayerMask`

Unity physics layer bitmask used to find terrain below the flying animal for altitude control. -1 includes every layer. Use a mask containing your ground/terrain layers; it is not a distance.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

UI `visibleWhen`: `{"contextProperty":"flight","equals":true}`. This governs the form, not removal of stored values.

### `postStimulus.autopilotEnabled`

Move continuously forward while the animal controls steering through closed loop orientation. Default depends on the experiment; flying templates enable it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.autopilotSpeed`

Forward autopilot speed in scene units per second: m/s on terrain, cm/s for walking. Applies when autopilot is enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"autopilotEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.autoTrimSettings`

Per-rig flight classification and automatic yaw-bias compensation. These settings apply while auto-trim is enabled for the sequence block.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `postStimulus.autoTrimSettings.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `15.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.autoTrimSettings.maxWindowSeconds`

1–300 s maximum noise-verification window, at least the initial window in effect.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `120.0` |
| UI unit label | seconds |

### `postStimulus.autoTrimSettings.centerStatistic`

mean removes expected heading drift; median retains median centering.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"mean"` |
| `enum` | `["mean","median"]` |

### `postStimulus.autoTrimSettings.noiseAware`

Extend measurements and suppress corrections within the approximate noise band.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.autoTrimSettings.flightWindowSeconds`

0.2–30 s raw-signal variance window.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":0.2,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.autoTrimSettings.flightCheckEnabled`

Set false to bypass nonflight while retaining measured flight status.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.autoTrimSettings.flightVarianceThreshold`

0–1,000,000 rad²; entry requires variance strictly above it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.autoTrimSettings.flightConfirmationSeconds`

0–30 s continuously above entry threshold before declaring flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | seconds |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.autoTrimSettings.flightVarianceHysteresis`

0–1 multiplier of entry threshold for exiting flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.autoTrimSettings.updateIntervalSeconds`

0.1–60 s between center evaluations.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| UI unit label | seconds |

### `postStimulus.autoTrimSettings.aggressiveness`

0.001–1 fraction of selected residual removed per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI soft slider range | `{"min":0.001,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.autoTrimSettings.maxStepRadians`

0.000001–10 rad maximum DC change per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `postStimulus.autoTrimSettings.maxOffsetRadians`

0.000001–10 rad maximum absolute automatic offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI soft slider range | `{"min":0.01,"max":2.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.autoTrimSettings.settleSeconds`

0–300 s adaptation time after corrections or gain/manual-offset changes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `3.0` |
| UI unit label | seconds |

### `postStimulus.autoTrimSettings.toleranceDegPerSecond`

0–1,000,000 deg/s effective center tolerance after gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `postStimulus.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.durationSeconds`

Duration of this phase, in seconds. Its clock takes priority over the heading observation window; oversized windows are shortened with a warning.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10` |
| UI unit label | seconds |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":1,"max":3600,"step":1}`; display/edit convenience, not an extra runtime constraint. |

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
