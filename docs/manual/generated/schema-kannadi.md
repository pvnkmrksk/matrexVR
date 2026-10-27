# Kannadi schema fields

This atlas lists **637 emitted property paths** from the current `kannadi` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

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

## randomInitialRotation group

### `randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## initialHeadingDistribution group

### `initialHeadingDistribution`

Random starting yaw for Swarm/Kannadi. Cardinal independently samples 0, 90, 180 or 270 degrees for each rig; Continuous preserves the historical 0–360 degree draw. Used only when random rotation and reset rotation are enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Continuous"` |
| `enum` | `["Continuous","Cardinal",0,1]` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[true]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

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

## numberOfRings group

### `numberOfRings`

Hex rings: 0=one tile, 1=7, 3=37 per rig. Negative disables replicas. Maximum 100.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

## hexRadius group

### `hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## spacing group

### `spacing`

Legacy alias, overridden by hexRadius.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## kannadiTilePrefab group

### `kannadiTilePrefab`

Catalog name, e.g. SimulatedLocust or LocustBand_black. Catalog is a build-safe Resources asset.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## periodicBoundary group

### `periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

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

## vrConfigs group

### `vrConfigs`

Optional per-rig pose/gain overrides below.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

## vrConfigs[] group

### `vrConfigs[].vrIndex`

One-based animal rig index. VR1 is index 1, VR10 is index 10. The system configuration determines which rigs exist.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `vrConfigs[].initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `vrConfigs[].initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `vrConfigs[].initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `vrConfigs[].watchIndex`

One-based target animal rig index in Kannadi. Omit to see other configured animals; select the same index for self mirrors with the central copy omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `vrConfigs[].closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `vrConfigs[].closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

## closedLoopPosition group

### `closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

## closedLoopOrientation group

### `closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

## trajectoryFile group

### `trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

## playback group

### `playback`

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

### `playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `playback.speed`

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

### `playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `playback.despawnDistanceMetres`

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

### `playback.terminalVelocityWindowSeconds`

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

### `playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `playback.preRollSeconds`

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

## worldUnitsPerMetre group

### `worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## nearClipMetres group

### `nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## useHeadingReference group

### `useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## headingReferenceId group

### `headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

## directionDistribution group

### `directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

## spawnRelativeToAnimal group

### `spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## followAnimalPosition group

### `followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## numberOfLocusts group

### `numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

## agentCount group

### `agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

## density group

### `density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## spawnAreaSize group

### `spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## mu group

### `mu`

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

## kappa group

### `kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

## locustSpeed group

### `locustSpeed`

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

## locustSpeedStdDev group

### `locustSpeedStdDev`

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

## agentSpeed group

### `agentSpeed`

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

## agentSpeedStdDev group

### `agentSpeedStdDev`

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

## locoSpeed group

### `locoSpeed`

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

## volumeHalfExtentMetres group

### `volumeHalfExtentMetres`

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

## preserveBoundaryOvershoot group

### `preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

## dimension group

### `dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

## spawnCenter group

### `spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## spawnVolumeSize group

### `spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## muElevation group

### `muElevation`

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

## kappaElevation group

### `kappaElevation`

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

## boundaryHeight group

### `boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

## wrapY group

### `wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

## agentVisual group

### `agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

## bogongVisual group

### `bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `bogongVisual.shape`

Dot draws a sphere-like patch. Moth draws a cigar body and one coupled wing pair with simple GPU flapping.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `bogongVisual.bodyLengthMetres`

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

### `bogongVisual.bodyDiameterMetres`

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

### `bogongVisual.wingSpanMetres`

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

### `bogongVisual.flapFrequencyHz`

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

### `bogongVisual.wingOpacity`

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

### `bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `bogongVisual.sizeMode`

World uses normal perspective: agents become smaller with distance. Angular maintains the specified angular diameter independently of distance.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `bogongVisual.size`

Diameter of a Dot in scene units when sizeMode is World. Moth uses physical body/wing dimensions instead.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"sizeMode","oneOf":["World"]}`. This governs the form, not removal of stored values.

### `bogongVisual.angularSizeDegrees`

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

### `bogongVisual.color`

Normalized moth albedo RGB (0–1) and optional alpha. Rendering reduces effective contrast when the projected moth is smaller than a pixel.

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

### `bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

## boundaryLengthX group

### `boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## boundaryLengthZ group

### `boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## animateOnMove group

### `animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## animationSpeedThreshold group

### `animationSpeedThreshold`

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

## initialPosition group

### `initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## initialRotation group

### `initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `initialRotation.x`

Initial Euler pitch about Unity +X, in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `initialRotation.y`

Initial Euler yaw about Unity +Y, in degrees. 0 faces +Z/north; +90 faces +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `initialRotation.z`

Initial Euler roll about Unity +Z, in degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `preStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.initialHeadingDistribution`

Random starting yaw for Swarm/Kannadi. Cardinal independently samples 0, 90, 180 or 270 degrees for each rig; Continuous preserves the historical 0–360 degree draw. Used only when random rotation and reset rotation are enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Continuous"` |
| `enum` | `["Continuous","Cardinal",0,1]` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[true]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

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

### `preStimulus.numberOfRings`

Hex rings: 0=one tile, 1=7, 3=37 per rig. Negative disables replicas. Maximum 100.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spacing`

Legacy alias, overridden by hexRadius.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.kannadiTilePrefab`

Catalog name, e.g. SimulatedLocust or LocustBand_black. Catalog is a build-safe Resources asset.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

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

### `preStimulus.vrConfigs`

Optional per-rig pose/gain overrides below.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.vrConfigs[].vrIndex`

One-based animal rig index. VR1 is index 1, VR10 is index 10. The system configuration determines which rigs exist.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `preStimulus.vrConfigs[].initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.vrConfigs[].initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.vrConfigs[].initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.vrConfigs[].watchIndex`

One-based target animal rig index in Kannadi. Omit to see other configured animals; select the same index for self mirrors with the central copy omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `preStimulus.vrConfigs[].closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.vrConfigs[].closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `preStimulus.playback`

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

### `preStimulus.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.playback.speed`

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

### `preStimulus.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.playback.despawnDistanceMetres`

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

### `preStimulus.playback.terminalVelocityWindowSeconds`

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

### `preStimulus.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.playback.preRollSeconds`

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

### `preStimulus.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `preStimulus.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.mu`

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

### `preStimulus.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.locustSpeed`

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

### `preStimulus.locustSpeedStdDev`

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

### `preStimulus.agentSpeed`

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

### `preStimulus.agentSpeedStdDev`

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

### `preStimulus.locoSpeed`

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

### `preStimulus.volumeHalfExtentMetres`

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

### `preStimulus.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `preStimulus.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `preStimulus.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.muElevation`

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

### `preStimulus.kappaElevation`

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

### `preStimulus.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `preStimulus.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `preStimulus.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `preStimulus.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `preStimulus.bogongVisual.bodyLengthMetres`

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

### `preStimulus.bogongVisual.bodyDiameterMetres`

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

### `preStimulus.bogongVisual.wingSpanMetres`

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

### `preStimulus.bogongVisual.flapFrequencyHz`

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

### `preStimulus.bogongVisual.wingOpacity`

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

### `preStimulus.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `preStimulus.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `preStimulus.bogongVisual.size`

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

### `preStimulus.bogongVisual.angularSizeDegrees`

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

### `preStimulus.bogongVisual.color`

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

### `preStimulus.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.animationSpeedThreshold`

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

### `preStimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `preStimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `preStimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `stimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.initialHeadingDistribution`

Random starting yaw for Swarm/Kannadi. Cardinal independently samples 0, 90, 180 or 270 degrees for each rig; Continuous preserves the historical 0–360 degree draw. Used only when random rotation and reset rotation are enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Continuous"` |
| `enum` | `["Continuous","Cardinal",0,1]` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[true]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

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

### `stimulus.numberOfRings`

Hex rings: 0=one tile, 1=7, 3=37 per rig. Negative disables replicas. Maximum 100.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spacing`

Legacy alias, overridden by hexRadius.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.kannadiTilePrefab`

Catalog name, e.g. SimulatedLocust or LocustBand_black. Catalog is a build-safe Resources asset.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

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

### `stimulus.vrConfigs`

Optional per-rig pose/gain overrides below.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.vrConfigs[].vrIndex`

One-based animal rig index. VR1 is index 1, VR10 is index 10. The system configuration determines which rigs exist.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `stimulus.vrConfigs[].initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.vrConfigs[].initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.vrConfigs[].initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.vrConfigs[].watchIndex`

One-based target animal rig index in Kannadi. Omit to see other configured animals; select the same index for self mirrors with the central copy omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `stimulus.vrConfigs[].closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.vrConfigs[].closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `stimulus.playback`

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

### `stimulus.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.playback.speed`

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

### `stimulus.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.playback.despawnDistanceMetres`

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

### `stimulus.playback.terminalVelocityWindowSeconds`

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

### `stimulus.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.playback.preRollSeconds`

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

### `stimulus.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `stimulus.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.mu`

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

### `stimulus.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.locustSpeed`

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

### `stimulus.locustSpeedStdDev`

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

### `stimulus.agentSpeed`

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

### `stimulus.agentSpeedStdDev`

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

### `stimulus.locoSpeed`

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

### `stimulus.volumeHalfExtentMetres`

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

### `stimulus.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `stimulus.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `stimulus.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.muElevation`

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

### `stimulus.kappaElevation`

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

### `stimulus.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `stimulus.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `stimulus.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `stimulus.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `stimulus.bogongVisual.bodyLengthMetres`

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

### `stimulus.bogongVisual.bodyDiameterMetres`

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

### `stimulus.bogongVisual.wingSpanMetres`

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

### `stimulus.bogongVisual.flapFrequencyHz`

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

### `stimulus.bogongVisual.wingOpacity`

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

### `stimulus.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `stimulus.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `stimulus.bogongVisual.size`

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

### `stimulus.bogongVisual.angularSizeDegrees`

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

### `stimulus.bogongVisual.color`

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

### `stimulus.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.animationSpeedThreshold`

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

### `stimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `stimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `stimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `postStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.initialHeadingDistribution`

Random starting yaw for Swarm/Kannadi. Cardinal independently samples 0, 90, 180 or 270 degrees for each rig; Continuous preserves the historical 0–360 degree draw. Used only when random rotation and reset rotation are enabled.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"Continuous"` |
| `enum` | `["Continuous","Cardinal",0,1]` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[true]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

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

### `postStimulus.numberOfRings`

Hex rings: 0=one tile, 1=7, 3=37 per rig. Negative disables replicas. Maximum 100.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":10.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.hexRadius`

Band grid spacing parameter in world units; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spacing`

Legacy alias, overridden by hexRadius.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.kannadiTilePrefab`

Catalog name, e.g. SimulatedLocust or LocustBand_black. Catalog is a build-safe Resources asset.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

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

### `postStimulus.vrConfigs`

Optional per-rig pose/gain overrides below.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.vrConfigs[].vrIndex`

One-based animal rig index. VR1 is index 1, VR10 is index 10. The system configuration determines which rigs exist.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `postStimulus.vrConfigs[].initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.vrConfigs[].initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.vrConfigs[].initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.vrConfigs[].watchIndex`

One-based target animal rig index in Kannadi. Omit to see other configured animals; select the same index for self mirrors with the central copy omitted.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `2147483647` |

### `postStimulus.vrConfigs[].closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.vrConfigs[].closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `number or boolean or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.trajectoryFile`

StreamingAssets-relative or absolute CSV path. Blank preserves generated motion. Supplied missing/malformed files raise errors.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `postStimulus.playback`

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

### `postStimulus.playback.dataUnitsPerMetre`

Number of CSV distance units per metre. Use 1 for sanitized metre data and 1000 for millimetres. Combined with worldUnitsPerMetre during playback.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.playback.loop`

Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.playback.speed`

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

### `postStimulus.playback.startTimeSeconds`

Shared clock offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.playback.maxGapSeconds`

Interpolate across gaps no longer than this; longer missing intervals are invisible.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.playback.yawOffsetDegrees`

Clockwise presentation yaw independent of steering.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.playback.centerOnAnimal`

Align the full dataset's sample centroid with the animal at each track appearance.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.playback.periodicBoundary`

Wrap animal and replicas in X/Z.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.playback.continueAfterTrack`

Continue each uninterrupted recorded segment at its terminal velocity. Requires periodicBoundary: false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.playback.despawnDistanceMetres`

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

### `postStimulus.playback.terminalVelocityWindowSeconds`

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

### `postStimulus.playback.continuationSpeedMetresPerSecond`

Physical speed override for extrapolated incoming/outgoing lines; positive m/s, independent of CSV/scene units. Null retains each measured endpoint velocity. Active recorded examples use 6. Recorded positions/times are unchanged. A singleton or zero-velocity endpoint stays stationary.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `enabledWhen`: `{"property":"continueAfterTrack","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.playback.preRollSeconds`

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

### `postStimulus.worldUnitsPerMetre`

Physical scene scale: 1 terrain or 100 walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.nearClipMetres`

1 mm near plane; permits seeing a moth 1 cm ahead. Applied to stimulus cameras, preserving system camera allow-lists.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | metres |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.headingReferenceId`

Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

UI `enabledWhen`: `{"property":"useHeadingReference","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.directionDistribution`

Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Legacy"` |
| `enum` | `["Legacy","VonMises"]` |

### `postStimulus.spawnRelativeToAnimal`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.followAnimalPosition`

Start and retain the volume around the current observer. Individual moths move at their own world velocity; they are not dragged by observer motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.numberOfLocusts`

Population per rig; template 256 means 1024 total.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.agentCount`

Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.density`

Optional agents per world-unit area (2D) or volume (3D). Used only when numberOfLocusts is omitted; count is rounded to the nearest whole agent.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnAreaSize`

Square spawn extent in world units.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.mu`

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

### `postStimulus.kappa`

Dimensionless heading concentration: 0 uniform, 10 concentrated, 100000 aligned.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1000.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.locustSpeed`

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

### `postStimulus.locustSpeedStdDev`

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

### `postStimulus.agentSpeed`

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

### `postStimulus.agentSpeedStdDev`

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

### `postStimulus.locoSpeed`

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

### `postStimulus.volumeHalfExtentMetres`

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

### `postStimulus.preserveBoundaryOvershoot`

Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"trajectoryFile","oneOf":[""]}`. This governs the form, not removal of stored values.

### `postStimulus.dimension`

2D preserves the historical X/Z swarm. 3D samples a volume and can wrap Y.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"2D"` |
| `enum` | `["2D","3D"]` |

### `postStimulus.spawnCenter`

Optional {x,y,z} center of the spawn volume. In 2D only X/Z are used.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.spawnCenter.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnCenter.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnCenter.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnVolumeSize`

Positive {x,y,z} volume dimensions. Omit for spawnAreaSize in every dimension.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.spawnVolumeSize.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnVolumeSize.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.spawnVolumeSize.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.muElevation`

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

### `postStimulus.kappaElevation`

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

### `postStimulus.boundaryHeight`

3D periodic Y extent. Required only when wrapY is true; otherwise Y is not wrapped.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.wrapY`

Enable periodic Y wrapping for a 3D swarm.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"dimension","oneOf":["3D"]}`. This governs the form, not removal of stored values.

### `postStimulus.agentVisual`

Existing prefab, or Bogong for an opaque, unlit circular patch.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"ScenePrefab"` |
| `enum` | `["ScenePrefab","Bogong"]` |

### `postStimulus.bogongVisual`

sizeMode is World (diameter size) or Angular (diameter angularSizeDegrees, 0–180 exclusive). Both draw a camera-facing circular patch of solid color RGB; alpha, metallic and smoothness have no visual effect. No lighting, fog, shadows, textures or soft edges. flickerFrequencyHz 0 disables flicker; flickerDutyCycle is the visible fraction.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `visibleWhen`: `{"property":"agentVisual","oneOf":["Bogong"]}`. This governs the form, not removal of stored values.

### `postStimulus.bogongVisual.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Dot"` |
| `enum` | `["Dot","Moth"]` |

### `postStimulus.bogongVisual.bodyLengthMetres`

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

### `postStimulus.bogongVisual.bodyDiameterMetres`

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

### `postStimulus.bogongVisual.wingSpanMetres`

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

### `postStimulus.bogongVisual.flapFrequencyHz`

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

### `postStimulus.bogongVisual.wingOpacity`

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

### `postStimulus.bogongVisual.subpixelVisibility`

Soft proxy below a few pixels, with opacity proportional to projected body area plus faint wings; retains subpixel flap motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

UI `visibleWhen`: `{"property":"shape","oneOf":["Moth"]}`. This governs the form, not removal of stored values.

### `postStimulus.bogongVisual.sizeMode`

Physical distance scaling, or Angular for constant apparent nominal body length.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"World"` |
| `enum` | `["World","Angular"]` |

### `postStimulus.bogongVisual.size`

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

### `postStimulus.bogongVisual.angularSizeDegrees`

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

### `postStimulus.bogongVisual.color`

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

### `postStimulus.bogongVisual.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.bogongVisual.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.bogongVisual.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.bogongVisual.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.bogongVisual.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.bogongVisual.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.25` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.bogongVisual.flickerFrequencyHz`

Whole-agent flicker separate from flap. Zero disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.bogongVisual.flickerDutyCycle`

Visible fraction, 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.boundaryLengthX`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.boundaryLengthZ`

Periodic arena lengths in world units; example 200 each.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `200.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.animateOnMove`

Enable leg-animation gating; templates set true.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.animationSpeedThreshold`

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

### `postStimulus.initialPosition`

World x/y/z, cm. Template ground-eye height 0.5; migration example 100.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"property":"resetPositionOnStart","oneOf":[true]}`. This governs the form, not removal of stored values.

### `postStimulus.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.initialRotation`

Euler x/y/z degrees.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"all":[{"property":"randomInitialRotation","oneOf":[false]},{"property":"resetRotationOnStart","oneOf":[true]}]}`. This governs the form, not removal of stored values.

### `postStimulus.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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
