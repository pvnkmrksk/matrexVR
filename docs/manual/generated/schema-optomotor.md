# Optomotor schema fields

This atlas lists **187 emitted property paths** from the current `optomotor` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

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

## randomInitialRotation group

### `randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## loop group

### `loop`

Repeat the configured optomotor stimuli while this scene runs. The sequence block's duration still ends the scene.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

## stimuli group

### `stimuli`

Required nonempty stimulus list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

## stimuli[] group

### `stimuli[].duration`

Duration of the stimulus in seconds

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":1.0,"max":3600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].speed`

Angular drum speed in degrees per second. Use nonnegative speed and the clockwise switch to choose direction.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| UI unit label | degrees / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].clockwise`

Whether the rotation is clockwise

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimuli[].rotationAxis`

Axis of rotation (Yaw, Pitch, or Roll)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Yaw"` |
| `enum` | `["Yaw","Pitch","Roll"]` |

### `stimuli[].frequency`

Spatial frequency of the grating in cycles per revolution

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `4.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":40.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].contrast`

Contrast of the grating (0 = no contrast, 1 = maximum contrast)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].dutyCycle`

Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].color1`

First color of the grating in hex format (e.g. #000000)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#000000"` |

### `stimuli[].color2`

Second color of the grating in hex format (e.g. #FFFFFF)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#FFFFFF"` |

### `stimuli[].closedLoopOrientation`

Whether to use closed-loop orientation control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimuli[].closedLoopPosition`

Whether to use closed-loop position control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

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

### `preStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.loop`

Repeat the configured optomotor stimuli while this scene runs. The sequence block's duration still ends the scene.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.stimuli`

Required nonempty stimulus list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `preStimulus.stimuli[].duration`

Duration of the stimulus in seconds

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":1.0,"max":3600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].speed`

Angular drum speed in degrees per second. Use nonnegative speed and the clockwise switch to choose direction.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| UI unit label | degrees / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].clockwise`

Whether the rotation is clockwise

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.stimuli[].rotationAxis`

Axis of rotation (Yaw, Pitch, or Roll)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Yaw"` |
| `enum` | `["Yaw","Pitch","Roll"]` |

### `preStimulus.stimuli[].frequency`

Spatial frequency of the grating in cycles per revolution

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `4.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":40.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].contrast`

Contrast of the grating (0 = no contrast, 1 = maximum contrast)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].dutyCycle`

Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].color1`

First color of the grating in hex format (e.g. #000000)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#000000"` |

### `preStimulus.stimuli[].color2`

Second color of the grating in hex format (e.g. #FFFFFF)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#FFFFFF"` |

### `preStimulus.stimuli[].closedLoopOrientation`

Whether to use closed-loop orientation control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.stimuli[].closedLoopPosition`

Whether to use closed-loop position control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

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

## stimulus group

### `stimulus`

Main phase of this experiment. Duration, motion and stimuli are configured within this phase.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

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

### `stimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.loop`

Repeat the configured optomotor stimuli while this scene runs. The sequence block's duration still ends the scene.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.stimuli`

Required nonempty stimulus list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `stimulus.stimuli[].duration`

Duration of the stimulus in seconds

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":1.0,"max":3600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].speed`

Angular drum speed in degrees per second. Use nonnegative speed and the clockwise switch to choose direction.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| UI unit label | degrees / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].clockwise`

Whether the rotation is clockwise

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.stimuli[].rotationAxis`

Axis of rotation (Yaw, Pitch, or Roll)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Yaw"` |
| `enum` | `["Yaw","Pitch","Roll"]` |

### `stimulus.stimuli[].frequency`

Spatial frequency of the grating in cycles per revolution

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `4.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":40.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].contrast`

Contrast of the grating (0 = no contrast, 1 = maximum contrast)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].dutyCycle`

Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].color1`

First color of the grating in hex format (e.g. #000000)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#000000"` |

### `stimulus.stimuli[].color2`

Second color of the grating in hex format (e.g. #FFFFFF)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#FFFFFF"` |

### `stimulus.stimuli[].closedLoopOrientation`

Whether to use closed-loop orientation control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.stimuli[].closedLoopPosition`

Whether to use closed-loop position control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

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

## postStimulus group

### `postStimulus`

Optional phase after the main stimulus. It retains its own duration, movement, sky and stimulus settings when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

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

### `postStimulus.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.loop`

Repeat the configured optomotor stimuli while this scene runs. The sequence block's duration still ends the scene.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.stimuli`

Required nonempty stimulus list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |

### `postStimulus.stimuli[].duration`

Duration of the stimulus in seconds

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":1.0,"max":3600.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].speed`

Angular drum speed in degrees per second. Use nonnegative speed and the clockwise switch to choose direction.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| UI unit label | degrees / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].clockwise`

Whether the rotation is clockwise

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.stimuli[].rotationAxis`

Axis of rotation (Yaw, Pitch, or Roll)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"Yaw"` |
| `enum` | `["Yaw","Pitch","Roll"]` |

### `postStimulus.stimuli[].frequency`

Spatial frequency of the grating in cycles per revolution

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `4.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":40.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].contrast`

Contrast of the grating (0 = no contrast, 1 = maximum contrast)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].dutyCycle`

Duty cycle of the grating (0 = all dark, 1 = all light, 0.5 = equal dark/light)

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].color1`

First color of the grating in hex format (e.g. #000000)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#000000"` |

### `postStimulus.stimuli[].color2`

Second color of the grating in hex format (e.g. #FFFFFF)

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"#FFFFFF"` |

### `postStimulus.stimuli[].closedLoopOrientation`

Whether to use closed-loop orientation control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.stimuli[].closedLoopPosition`

Whether to use closed-loop position control

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

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

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
