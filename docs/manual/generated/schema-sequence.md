# Sequence schema fields

This atlas lists **43 emitted property paths** from the current `sequence` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## loopStartIndex group

### `loopStartIndex`

Zero-based row to restart after the final row: 0 repeats everything; 1 runs row 0 once, then repeats rows 1 onward. Requires fixed order when nonzero.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `0` |
| `maximum` | `2147483647` |

## autoStart group

### `autoStart`

Start the sequence when Play begins in ControlScene. Returning with Escape does not auto-start again. Canonical templates explicitly use false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## randomise group

### `randomise`

Shuffle outer scene execution order.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## loop group

### `loop`

Repeat outer sequence; templates explicitly use false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

## sequences group

### `sequences`

Required nonempty array of scene steps.

| Contract | Emitted value |
| --- | --- |
| Type | `array` |
| Required by parent schema | Yes |
| Default | `null` |

## sequences[] group

### `sequences[].sceneName`

Exact enabled scene name, e.g. Swarm, Kannadi, Choice, Optomotor, Choice_desync. No Matrix scene exists.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | Yes |
| Default | `null` |

### `sequences[].duration`

Outer step time, seconds; use a positive duration.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | Yes |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |
| UI soft slider range | `{"min":1.0,"max":3600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `sequences[].gain`

Historical Bogong/Kinefly signed yaw gain, applied on scene load and in-place trial transitions. Zero disables yaw response; negative reverses it. It does not replace the separate walking angular multiplier.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":-10.0,"max":10.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `sequences[].autoTrim`

Opt-in per-row Kinefly median centering; pauses outside flight/closed-loop yaw. Learned DC offsets persist when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `sequences[].autoTrimSettings`

Per-rig flight classification and automatic yaw-bias compensation. These settings apply while auto-trim is enabled for the sequence block.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

UI `enabledWhen`: `{"property":"autoTrim","oneOf":[true]}`. This governs the form, not removal of stored values.

### `sequences[].autoTrimSettings.windowSeconds`

Positive finite observation window, seconds.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `15.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":1.0,"max":180.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `sequences[].autoTrimSettings.maxWindowSeconds`

1–300 s maximum noise-verification window, at least the initial window in effect.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `120.0` |
| UI unit label | seconds |

### `sequences[].autoTrimSettings.centerStatistic`

mean removes expected heading drift; median retains median centering.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"mean"` |
| `enum` | `["mean","median"]` |

### `sequences[].autoTrimSettings.noiseAware`

Extend measurements and suppress corrections within the approximate noise band.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `sequences[].autoTrimSettings.flightWindowSeconds`

0.2–30 s raw-signal variance window.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI unit label | seconds |
| UI soft slider range | `{"min":0.2,"max":30.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `sequences[].autoTrimSettings.flightCheckEnabled`

Set false to bypass nonflight while retaining measured flight status.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `sequences[].autoTrimSettings.flightVarianceThreshold`

0–1,000,000 rad²; entry requires variance strictly above it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.01` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `sequences[].autoTrimSettings.flightConfirmationSeconds`

0–30 s continuously above entry threshold before declaring flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI unit label | seconds |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `sequences[].autoTrimSettings.flightVarianceHysteresis`

0–1 multiplier of entry threshold for exiting flight.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |

UI `enabledWhen`: `{"property":"flightCheckEnabled","oneOf":[true]}`. This governs the form, not removal of stored values.

### `sequences[].autoTrimSettings.updateIntervalSeconds`

0.1–60 s between center evaluations.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| UI unit label | seconds |

### `sequences[].autoTrimSettings.aggressiveness`

0.001–1 fraction of selected residual removed per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| UI soft slider range | `{"min":0.001,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sequences[].autoTrimSettings.maxStepRadians`

0.000001–10 rad maximum DC change per pass.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `sequences[].autoTrimSettings.maxOffsetRadians`

0.000001–10 rad maximum absolute automatic offset.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2.0` |
| UI soft slider range | `{"min":0.01,"max":2.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sequences[].autoTrimSettings.settleSeconds`

0–300 s adaptation time after corrections or gain/manual-offset changes.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `3.0` |
| UI unit label | seconds |

### `sequences[].autoTrimSettings.toleranceDegPerSecond`

0–1,000,000 deg/s effective center tolerance after gain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |

### `sequences[].parameters`

Controller-specific dictionary.

| Contract | Emitted value |
| --- | --- |
| Type | `object` |
| Required by parent schema | Yes |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `sequences[].parameters.rugosity`

Independent visual and navigation terrain cues. Requires a RugosityWorld in the selected scene; omitted/null settings retain its authored tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `sequences[].parameters.rugosity.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

### `sequences[].parameters.rugosity.visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `sequences[].parameters.rugosity.collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `sequences[].parameters.rugosity.heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `sequences[].parameters.rugosity.normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `sequences[].parameters.rugosity.heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

### `sequences[].parameters.rugosity.orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

### `sequences[].parameters.rugosity.centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

### `sequences[].parameters.rugosity.clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `sequences[].parameters.rugosity.absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `sequences[].parameters.rugosity.normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `sequences[].parameters.rugosity.tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `sequences[].parameters.rugosity.normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

### `sequences[].parameters.configFile`

Choice, Optomotor, Kannadi or Swarm file relative to StreamingAssets.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `"Templates/experiment.template.json"` |

UI `visibleWhen`: `{"ancestorProperty":"sceneName","notEquals":"Choice_desync"}`. This governs the form, not removal of stored values.

### `sequences[].parameters.design`

Dynamic Choice design file.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `"Templates/dynamic-choice.template.json"` |

UI `visibleWhen`: `{"ancestorProperty":"sceneName","equals":"Choice_desync"}`. This governs the form, not removal of stored values.

### `sequences[].reloadScene`

When false and the scene stays the same, call its in-scene sequencer. Use true for Dynamic Choice: its AdvanceStep is intentionally empty.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
