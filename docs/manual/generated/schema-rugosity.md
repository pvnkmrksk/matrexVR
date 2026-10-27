# Rugosity schema fields

This atlas lists **14 emitted property paths** from the current `rugosity` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## enabled group

### `enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean or null` |
| Required by parent schema | No |
| Default | `null` |

## visualSurface group

### `visualSurface`

Registered surface ID rendered to the animal, for example Flat or RoughSmooth in TerrainCueLab. This can differ from the navigation surface.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## collisionSurface group

### `collisionSurface`

Registered navigation/collision surface ID. Height and orientation follow this surface unless a separate override is supplied.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## heightSurface group

### `heightSurface`

Optional registered surface ID for AboveGround height sampling. Empty inherits collisionSurface, allowing height and tilt cues to be isolated.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## normalSurface group

### `normalSurface`

Optional registered surface ID for orientation normals. Empty inherits collisionSurface, allowing tilt to be independent of rendered terrain and height.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `null` |

## heightMode group

### `heightMode`

AboveGround follows the sampled terrain plus vertical clearanceCm. Absolute uses a fixed world Y from absoluteHeight.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["AboveGround","Absolute",0,1,null]` |

## orientationMode group

### `orientationMode`

WorldUp keeps the rig level. SurfaceNormal tilts its up direction to the sampled navigation normal while retaining its heading.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `enum` | `["WorldUp","SurfaceNormal",0,1,null]` |

## centimetersPerUnit group

### `centimetersPerUnit`

1 for centimeter scenes; 100 for meter terrain scenes.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| `exclusiveMinimum` | `0` |

## clearanceCm group

### `clearanceCm`

Vertical camera/animal clearance above the height surface, in centimetres. This is measured along world Y, not along the surface normal.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

## textureScale group

### `textureScale`

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

## absoluteHeight group

### `absoluteHeight`

Fixed world Y in scene units when heightMode is Absolute. It is not an above-ground clearance.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | world Y (scene units) |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## normalRadiusCm group

### `normalRadiusCm`

Radius in centimetres used to sample nearby terrain normals. Zero samples the local normal directly, without spatial smoothing.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| UI unit label | cm |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

## tiltResponse group

### `tiltResponse`

Tilt response rate in inverse seconds. Zero follows the sampled normal immediately; positive values smooth orientation over time.

| Contract | Emitted value |
| --- | --- |
| Type | `number or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `0` |
| `maximum` | `3.40282347e+38` |

## normalSamples group

### `normalSamples`

Number of surrounding terrain-normal samples, from 1 to 32, in addition to the centre sample. Set normalRadiusCm to zero to disable spatial averaging.

| Contract | Emitted value |
| --- | --- |
| Type | `integer or null` |
| Required by parent schema | No |
| Default | `null` |
| `minimum` | `1` |
| `maximum` | `32` |

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
