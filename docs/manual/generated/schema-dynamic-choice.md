# Dynamic-Choice schema fields

This atlas lists **176 emitted property paths** from the current `dynamic-choice` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## seed group

### `seed`

Shuffle seed; negative uses time. Example 42 for initial reproducibility; later controller loops reseed using time.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `-1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

## repetitions group

### `repetitions`

Number of shuffled repetitions before reaching the end.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

## sync group

### `sync`

Legacy serialized field; currently not consulted by execution. Do not infer rig synchronization from it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

## intertrial group

### `intertrial`

Optional step inserted before each trial. Same schema as a step.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `intertrial.resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `intertrial.resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `intertrial.name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `intertrial.trigger`

Null advances immediately; explicit time or area trigger is recommended.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `intertrial.trigger.type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["time","area"]` |

### `intertrial.trigger.seconds`

Time trigger duration.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["time"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.areaTag`

Tag assigned to trigger objects (fallback Untagged).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.vrId`

Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"box"` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.boxSize`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.timeoutSeconds`

Positive timeout, otherwise wait indefinitely for area event.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.triggerOnExit`

Use exit instead of entry.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `intertrial.trigger.advanceOnTrigger`

false resets the rig on contact and keeps waiting for timeout.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `intertrial.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `intertrial.objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `intertrial.objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `intertrial.objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].polar`

{radius,angle,height} as above, defaults zero for each scalar. Takes precedence over Cartesian pos.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `intertrial.objects[].polar.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].polar.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].polar.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `intertrial.objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].swapColor`

Normalized [r,g,b,a]; RGB allowed. Explicit swapColor sets swap target. Otherwise first two colors exchange.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `intertrial.objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `intertrial.objects[].randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `intertrial.objects[].mu`

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

### `intertrial.objects[].prefab`

Name of a registered scene prefab to instantiate. The asset must be present in the scene registry.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].pos`

Legacy [x,y,z], fallback zero.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].rot`

Legacy serialized array, currently ignored. Use mu for yaw.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].mat`

Legacy alias for a registered material name. The asset must be present in the scene’s registry.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.objects[].role`

black or gray for adaptive decisions.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.camera`

Array of {vrId,clearFlags,bgColor}. clearFlags defaults SolidColor; bgColor is [r,g,b,a] (alpha optional). Changes clear/background, never system camera order.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.camera[].vrId`

Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.camera[].clearFlags`

Camera background clearing mode, such as Skybox or SolidColor. SolidColor uses the configured background color.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"SolidColor"` |
| `enum` | `["Skybox","Color","SolidColor","Depth","Nothing",1,2,2,3,4]` |

### `intertrial.camera[].bgColor`

Dynamic Choice camera background as normalized RGB or RGBA channels (0–1). Used with SolidColor camera clearing.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.skybox`

Material name loaded by Resources.Load<Material>. This differs from Choice's panorama file path.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.resetVR`

Legacy serialized list; currently resets the executing rig regardless of this list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `intertrial.closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `intertrial.closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `intertrial.initialPosition`

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

### `intertrial.initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.initialRotation`

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

### `intertrial.initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `intertrial.randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `intertrial.swapAfterSeconds`

Positive delay before swapping object colors; 0 disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## steps group

### `steps`

Array of steps.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

## steps[] group

### `steps[].resetPositionOnStart`

Apply the configured initial position when this experiment begins. When disabled, retain the current position.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `steps[].resetRotationOnStart`

Apply the initial orientation when this experiment begins. When disabled, retain the current orientation unless the experiment requests randomization.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `steps[].name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `steps[].trigger`

Null advances immediately; explicit time or area trigger is recommended.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `steps[].trigger.type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["time","area"]` |

### `steps[].trigger.seconds`

Time trigger duration.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["time"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.areaTag`

Tag assigned to trigger objects (fallback Untagged).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.vrId`

Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.shape`

box or cylinder (capsule collider).

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"box"` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.boxSize`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.timeoutSeconds`

Positive timeout, otherwise wait indefinitely for area event.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.triggerOnExit`

Use exit instead of entry.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

UI `visibleWhen`: `{"property":"type","oneOf":["area"]}`. This governs the form, not removal of stored values.

### `steps[].trigger.advanceOnTrigger`

false resets the rig on contact and keeps waiting for timeout.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `steps[].objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `steps[].objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `steps[].objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].useHeadingReference`

Add the measured per-rig heading to mu and rotate its spawn/boundary frame accordingly.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `steps[].objects[].type`

Required prefab name, e.g. tree01 or LocustBand_black.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].polar`

{radius,angle,height} as above, defaults zero for each scalar. Takes precedence over Cartesian pos.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `steps[].objects[].polar.radius`

Cylinder dimensions in world units; controller applies fallback/minimum geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].polar.angle`

Polar angle in degrees. Zero points along Unity +Z/north; positive 90 points +X/east. Positive yaw is clockwise from above.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].polar.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].scale`

Omitted object resolves to (1,1,1); scalar fields in a supplied object default to 0. Use {x:1,y:1,z:1} for unit scale.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `steps[].objects[].scale.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].scale.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].scale.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].material`

Registered material name; empty keeps prefab material.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].swapColor`

Normalized [r,g,b,a]; RGB allowed. Explicit swapColor sets swap target. Otherwise first two colors exchange.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].flip`

Negate X scale.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `steps[].objects[].visualAngleDegrees`

Angular size for prefabs with ScaleWithDistance; example 10.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | degrees |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `steps[].objects[].randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `steps[].objects[].mu`

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

### `steps[].objects[].prefab`

Name of a registered scene prefab to instantiate. The asset must be present in the scene registry.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].pos`

Legacy [x,y,z], fallback zero.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].rot`

Legacy serialized array, currently ignored. Use mu for yaw.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].mat`

Legacy alias for a registered material name. The asset must be present in the scene’s registry.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].objects[].role`

black or gray for adaptive decisions.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].camera`

Array of {vrId,clearFlags,bgColor}. clearFlags defaults SolidColor; bgColor is [r,g,b,a] (alpha optional). Changes clear/background, never system camera order.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].camera[].vrId`

Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].camera[].clearFlags`

Camera background clearing mode, such as Skybox or SolidColor. SolidColor uses the configured background color.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"SolidColor"` |
| `enum` | `["Skybox","Color","SolidColor","Depth","Nothing",1,2,2,3,4]` |

### `steps[].camera[].bgColor`

Dynamic Choice camera background as normalized RGB or RGBA channels (0–1). Used with SolidColor camera clearing.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].skybox`

Material name loaded by Resources.Load<Material>. This differs from Choice's panorama file path.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].resetVR`

Legacy serialized list; currently resets the executing rig regardless of this list.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `steps[].closedLoopOrientation`

Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot. A numeric value sets the orientation gain; true uses gain 1 and false disables it.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `steps[].closedLoopPosition`

Use the animal’s measured translation to move its virtual rig. Orientation remains independent. A numeric value sets the translation gain; true uses gain 1 and false disables it. Walking uses scene units (centimetres).

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |
| UI soft slider range | `{"min":0.0,"max":5.0,"step":0.1}`; display/edit convenience, not an extra runtime constraint. |

### `steps[].initialPosition`

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

### `steps[].initialPosition.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].initialPosition.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].initialPosition.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].initialRotation`

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

### `steps[].initialRotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].initialRotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].initialRotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |

### `steps[].randomInitialRotation`

Random starting yaw; Choice shares that sampled yaw across its rigs.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `steps[].swapAfterSeconds`

Positive delay before swapping object colors; 0 disables.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## size group

### `size`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.01,"max":5.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

## boxSize group

### `boxSize`

Legacy fields not consulted by trigger construction; use trigger fields.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

## adaptiveDecision group

### `adaptiveDecision`

Optional adaptive staircase below.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `adaptiveDecision.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `adaptiveDecision.startGray`

Initial normalized gray for adaptive decision trials. 0 is black and 1 is white.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `adaptiveDecision.grayStep`

Normalized change in adaptive gray after each decision (0–1 luminance channel units). The resulting gray is clamped to 0–1.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.05` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `adaptiveDecision.controlEvery`

Run a no-bar control every N adaptive decision trials. Zero disables these control trials.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `5` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `adaptiveDecision.noBarControlSeconds`

Duration, in seconds, of a no-bar adaptive control trial.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## title group

### `title`

Optional human-readable experiment or paradigm name. The file path remains visible separately.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

## comments group

### `comments`

Optional notes retained in this configuration and the run archive.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

## sun group

### `sun`

Optional directional sunlight. Configure brightness, color and angle; position does not affect directional lighting. Omitted preserves the scene’s authored sun.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `sun.enabled`

Enable the directional sun. Disabled settings stay in this configuration.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `sun.intensity`

Brightness of the directional sun. Zero gives no direct illumination.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `sun.color`

Sunlight color. Choose a color or enter its hexadecimal RGB value.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "r": 1.0,
  "g": 1.0,
  "b": 1.0,
  "a": 1.0
}
```

</details>

### `sun.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sun.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sun.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sun.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `sun.rotation`

Sun direction as X/Y/Z Euler angles in degrees. Position does not affect directional lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "x": 50.0,
  "y": 330.0,
  "z": 0.0
}
```

</details>

### `sun.rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `sun.rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `sun.rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

## nightSky group

### `nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

## uniformSkyColor group

### `uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

## skyboxPath group

### `skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

## panoramaRotationDegrees group

### `panoramaRotationDegrees`

Rotate an imported panorama horizontally. Zero preserves its original orientation; 360 is one complete turn.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |
| UI unit label | degrees |
| `minimum` | `0` |
| `maximum` | `360` |

## visibilityMatrix group

### `visibilityMatrix`

Rows are observers VR1–VR16; columns are the VR population layers they see. The matrix changes no ground, sky, or private Choice-object layers. Disabled preserves the normal scene visibility.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "enabled": false,
  "rows": [
    [
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false,
      true
    ],
    [
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      true,
      false
    ]
  ]
}
```

</details>

### `visibilityMatrix.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `visibilityMatrix.rows`

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |
| `maxItems` | `16` |

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
