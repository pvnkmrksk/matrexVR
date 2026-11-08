# Optomotor schema fields

This atlas lists **589 emitted property paths** from the current `optomotor` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## objects group

### `objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

## objects[] group

### `objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `objects[].appearance`

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `objects[].appearance.enabled`

Override this object's surface without changing the original model file.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `objects[].appearance.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

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

### `objects[].appearance.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].appearance.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].appearance.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].appearance.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `objects[].appearance.keepTexture`

Keep the model's base-color texture. Disable for a uniform chosen color.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `objects[].appearance.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |

### `objects[].appearance.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |

### `objects[].appearance.textureVariants`

Named replacement base-color images for this model's existing UVs. Use PNG or JPEG.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].appearance.textureVariants[].name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].appearance.textureVariants[].texturePath`

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `objects[].appearance.selectedTextureVariant`

Select a named base-color texture. An empty selection retains the original texture settings.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

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

Total periodic length along Z in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The length must accommodate the spawn area or volume.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `objects[].boundaryLengthX`

Total periodic width along X in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The width must accommodate the spawn area or volume.

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

### `preStimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `preStimulus.objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `preStimulus.objects[].appearance`

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `preStimulus.objects[].appearance.enabled`

Override this object's surface without changing the original model file.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.objects[].appearance.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

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

### `preStimulus.objects[].appearance.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].appearance.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].appearance.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].appearance.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.objects[].appearance.keepTexture`

Keep the model's base-color texture. Disable for a uniform chosen color.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.objects[].appearance.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |

### `preStimulus.objects[].appearance.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |

### `preStimulus.objects[].appearance.textureVariants`

Named replacement base-color images for this model's existing UVs. Use PNG or JPEG.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].appearance.textureVariants[].name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].appearance.textureVariants[].texturePath`

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `preStimulus.objects[].appearance.selectedTextureVariant`

Select a named base-color texture. An empty selection retains the original texture settings.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

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

Total periodic length along Z in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The length must accommodate the spawn area or volume.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.objects[].boundaryLengthX`

Total periodic width along X in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The width must accommodate the spawn area or volume.

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

### `preStimulus.sun`

Optional directional sunlight. Configure brightness, color and angle; position does not affect directional lighting. Omitted preserves the scene’s authored sun.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `preStimulus.sun.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `preStimulus.sun.intensity`

Brightness of the directional sun. Zero gives no direct illumination.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `preStimulus.sun.color`

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

### `preStimulus.sun.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.sun.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.sun.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.sun.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `preStimulus.sun.rotation`

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

### `preStimulus.sun.rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `preStimulus.sun.rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `preStimulus.sun.rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `preStimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `preStimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `preStimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

### `preStimulus.panoramaRotationDegrees`

Rotate an imported panorama horizontally. Zero preserves its original orientation; 360 is one complete turn.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |
| UI unit label | degrees |
| `minimum` | `0` |
| `maximum` | `360` |

### `preStimulus.visibilityMatrix`

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

### `preStimulus.visibilityMatrix.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `preStimulus.visibilityMatrix.rows`

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |
| `maxItems` | `16` |

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

### `stimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `stimulus.objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `stimulus.objects[].appearance`

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `stimulus.objects[].appearance.enabled`

Override this object's surface without changing the original model file.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.objects[].appearance.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

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

### `stimulus.objects[].appearance.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].appearance.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].appearance.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].appearance.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.objects[].appearance.keepTexture`

Keep the model's base-color texture. Disable for a uniform chosen color.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.objects[].appearance.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |

### `stimulus.objects[].appearance.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |

### `stimulus.objects[].appearance.textureVariants`

Named replacement base-color images for this model's existing UVs. Use PNG or JPEG.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].appearance.textureVariants[].name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].appearance.textureVariants[].texturePath`

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `stimulus.objects[].appearance.selectedTextureVariant`

Select a named base-color texture. An empty selection retains the original texture settings.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

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

Total periodic length along Z in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The length must accommodate the spawn area or volume.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `stimulus.objects[].boundaryLengthX`

Total periodic width along X in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The width must accommodate the spawn area or volume.

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

### `stimulus.sun`

Optional directional sunlight. Configure brightness, color and angle; position does not affect directional lighting. Omitted preserves the scene’s authored sun.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `stimulus.sun.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `stimulus.sun.intensity`

Brightness of the directional sun. Zero gives no direct illumination.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `stimulus.sun.color`

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

### `stimulus.sun.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.sun.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.sun.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.sun.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `stimulus.sun.rotation`

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

### `stimulus.sun.rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `stimulus.sun.rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `stimulus.sun.rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `stimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `stimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `stimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

### `stimulus.panoramaRotationDegrees`

Rotate an imported panorama horizontally. Zero preserves its original orientation; 360 is one complete turn.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |
| UI unit label | degrees |
| `minimum` | `0` |
| `maximum` | `360` |

### `stimulus.visibilityMatrix`

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

### `stimulus.visibilityMatrix.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `stimulus.visibilityMatrix.rows`

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |
| `maxItems` | `16` |

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

### `postStimulus.objects`

Omitted, null or [] means environment-only flight/migration; sky and movement settings still apply.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.objects[].modelPath`

Optional embedded .glb model path relative to StreamingAssets. Overrides the built-in object/model. Model bytes are archived with the recording for replay.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `postStimulus.objects[].animationClip`

Optional animation clip in the imported model. Leave blank to use its first clip.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].animationSpeed`

Model animation playback multiplier. 1 uses the model's authored timing; 0 freezes it.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

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

### `postStimulus.objects[].appearance`

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `postStimulus.objects[].appearance.enabled`

Override this object's surface without changing the original model file.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.objects[].appearance.color`

Normalized color channels from 0 to 1. Use the JSON object or array format shown for this parameter; alpha is optional where supported. These are stimulus values, not calibrated luminance.

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

### `postStimulus.objects[].appearance.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].appearance.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].appearance.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].appearance.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.objects[].appearance.keepTexture`

Keep the model's base-color texture. Disable for a uniform chosen color.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.objects[].appearance.metallic`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |

### `postStimulus.objects[].appearance.smoothness`

Legacy compatibility; unlit visual ignores PBR lighting.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.5` |
| `minimum` | `0` |
| `maximum` | `1` |

### `postStimulus.objects[].appearance.textureVariants`

Named replacement base-color images for this model's existing UVs. Use PNG or JPEG.

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].appearance.textureVariants[].name`

Logged step label; keep names unique and avoid CSV delimiters.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].appearance.textureVariants[].texturePath`

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

### `postStimulus.objects[].appearance.selectedTextureVariant`

Select a named base-color texture. An empty selection retains the original texture settings.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

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

Total periodic length along Z in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The length must accommodate the spawn area or volume.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.objects[].boundaryLengthX`

Total periodic width along X in scene units. With a fixed agent count, a smaller arena keeps more agents close to an observer inside it. The width must accommodate the spawn area or volume.

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

### `postStimulus.sun`

Optional directional sunlight. Configure brightness, color and angle; position does not affect directional lighting. Omitted preserves the scene’s authored sun.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
| `additionalProperties` | `true` |

### `postStimulus.sun.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `postStimulus.sun.intensity`

Brightness of the directional sun. Zero gives no direct illumination.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

### `postStimulus.sun.color`

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

### `postStimulus.sun.color.r`

Red channel, normalized from 0 (none) to 1 (full intensity). This is a stimulus value, not a calibrated luminance.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.sun.color.g`

Green channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.sun.color.b`

Blue channel, normalized from 0 (none) to 1 (full intensity).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.sun.color.a`

Alpha channel, from 0 (transparent) to 1 (opaque).

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `1` |
| UI soft slider range | `{"min":0.0,"max":1.0,"step":0.01}`; display/edit convenience, not an extra runtime constraint. |

### `postStimulus.sun.rotation`

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

### `postStimulus.sun.rotation.x`

X component. Unity +X points right/east. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `postStimulus.sun.rotation.y`

Y component. Unity +Y points up. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `postStimulus.sun.rotation.z`

Z component. Unity +Z points forward/north. Distances use scene units: metres on terrain, centimetres for walking.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.0` |
| `minimum` | `0` |
| `maximum` | `360` |

### `postStimulus.nightSky`

Generated astronomical sky and RunData archive, with the same location/time settings as Swarm. Used when skyboxPath is empty. Omitted/disabled restores the scene background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `postStimulus.uniformSkyColor`

Uniform sky background with RGB channels in 0–1. Replaces the Milky Way when the sky configuration requests a uniform background.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | No default emitted at this path. |
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

### `postStimulus.skyboxPath`

Panorama path under StreamingAssets; takes precedence over nightSky. Uses Choice's legacy image loader.

| Contract | Emitted value |
| --- | --- |
| Type | `string` |
| Required by parent schema | No |
| Default | `""` |

### `postStimulus.panoramaRotationDegrees`

Rotate an imported panorama horizontally. Zero preserves its original orientation; 360 is one complete turn.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0` |
| UI unit label | degrees |
| `minimum` | `0` |
| `maximum` | `360` |

### `postStimulus.visibilityMatrix`

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

### `postStimulus.visibilityMatrix.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `postStimulus.visibilityMatrix.rows`

| Contract | Emitted value |
| --- | --- |
| Type | `array or null` |
| Required by parent schema | No |
| Default | `null` |
| `maxItems` | `16` |

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
