# System schema fields

This atlas lists **58 emitted property paths** from the current `system` runtime/UI schema. It is generated from the actual bundled JSON rather than a second handwritten parameter list.

The schema default is a serialized type default, not a guarantee that it overrides an authored scene/prefab or a field retained by an in-place controller. Production loaders also enforce cross-field rules. A UI slider is a convenience range, not a runtime clamp. See [parameter semantics](../../configuration-reference.md) and [the exact emitted schema](schema-source.json).

Every field has a deep link and is indexed by the manual search. Array items use `[]` in their path. Nested pre/stimulus/post fields are listed where the schema emits them; repeated names in different phases are distinct paths.

## targetDisplay group

### `targetDisplay`

Zero-based physical display for stimulus cameras. 0 is the primary monitor. The top-level setting overrides legacy per-rig values.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `0` |
| `maximum` | `2147483647` |

## targetFrameRate group

### `targetFrameRate`

Positive FPS, or -1 for uncapped. vSync overrides this value.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `120` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |
| UI soft slider range | `{"min":30,"max":240,"step":1}`; display/edit convenience, not an extra runtime constraint. |

UI `enabledWhen`: `{"property":"vSyncCount","oneOf":[0]}`. This governs the form, not removal of stored values.

## vSyncCount group

### `vSyncCount`

0 uses the FPS target; 1–4 synchronize to every Nth display refresh and override the target on desktop.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `0` |
| `maximum` | `4` |

## configs group

### `configs`

Array of per-rig entries. Use one entry for every rig that should render.

| Contract | Emitted value |
| --- | --- |
| Type | `array` |
| Required by parent schema | Yes |
| Default | No default emitted at this path. |
| `minItems` | `1` |

## configs[] group

### `configs[].desktopPreview`

Desktop preview: render this rig’s front view across the native application window. Off keeps calibrated LED panel rectangles. Use apparatus setup for physical panel geometry.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `configs[].closedLoopMode`

Tracker backend: Hejjetrac (3) for HejjéTrac walking, Rekkefly (4) for flight, or Tirbala (2). Legacy FicTrac (0) and Kinefly (1) remain accepted without rewriting saved files. HejjéTrac and hejjetrak name aliases are also accepted. Native FT rows select sphere-radian walking interpretation; legacy JSON retains its gain behavior.

| Contract | Emitted value |
| --- | --- |
| Type | `string or integer` |
| Required by parent schema | No |
| Default | `"FicTrac"` |
| `enum` | `["FicTrac","Kinefly","Tirbala","Hejjetrac","Rekkefly",0,1,2,3,4,"HejjéTrac","hejjetrak"]` |

### `configs[].manualControls`

Optional per-rig operator movement settings below. Omission preserves prefab tuning.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `configs[].manualControls.enabled`

Enable this feature. Its saved parameters remain in the JSON when disabled.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `configs[].manualControls.translateSpeed`

World units/s for full arrow-key/stick input: cm/s walking, m/s terrain.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `10.0` |
| UI unit label | scene units / s |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":100.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `configs[].manualControls.rotateSpeed`

degrees/s for full yaw input.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `50.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":0.0,"max":300.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `configs[].manualControls.maxTranslateSpeed`

World units/s cap for operator speed adjustment.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `100.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `configs[].manualControls.maxRotateSpeed`

degrees/s cap.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `300.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `configs[].manualControls.allowVerticalTranslation`

Allow C/Z vertical motion.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `configs[].manualControls.allowPitchAndRoll`

Allow W/S pitch and Q/E roll.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `configs[].sphereDiameter`

Walking ball diameter in centimetres. Hidden in wing-angle flight mode.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

UI `visibleWhen`: `{"property":"closedLoopMode","oneOf":["FicTrac","Hejjetrac","HejjéTrac","hejjetrak",0,3]}`. This governs the form, not removal of stored values.

### `configs[].ledPanelWidth`

Width of each stimulus camera viewport in pixels. For a 64 × 64 panel, use 64. Independent of the physical monitor resolution.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `128` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `configs[].ledPanelHeight`

Height of each stimulus camera viewport in pixels. For a 64 × 64 panel, use 64.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `128` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `configs[].startRow`

Zero-based panel cells, measured from the top-left.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `configs[].startCol`

Zero-based panel cells, measured from the top-left.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `configs[].horizontal`

Advance through columns when true, rows when false.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `configs[].zmqAddress`

Host running the pose publisher; no tcp:// prefix.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"localhost"` |

### `configs[].zmqPort`

TCP port; template uses 9871–9874.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `9872` |
| `minimum` | `1` |
| `maximum` | `65535` |

### `configs[].vrId`

Exact unique positive VR identifier, such as VR1 or VR10. Duplicate IDs are rejected; a rig is separate from its animal ID/species.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | Yes |
| Default | `"VR1"` |

### `configs[].speciesName`

Common or scientific name of the real animal controlling this rig. Saved with its AnimalID in run metadata.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].tracker`

Native tracker app settings. The ready-to-run starters launch a bundled tracker with Start; custom experiments can launch a configured app explicitly. Apps run independently and remain open when MATREX closes. Save calibration before Stop; stop the active tracker before changing its backend or input source.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| Default | `null` |
| `additionalProperties` | `true` |

### `configs[].tracker.executablePath`

Absolute installed native executable. On macOS choose Contents/MacOS inside the app. Nothing launches automatically.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].tracker.configFile`

Existing tracker calibration file. Relative paths resolve inside the MATREX config folder; MATREX never rewrites this file.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].tracker.device`

Optional Rekkefly device override. HejjéTrac source selection stays in its calibration config.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].tracker.headless`

Optional headless Rekkefly launch. HejjéTrac uses its native desktop window.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `configs[].tracker.strictCameraFallback`

Optional headless Rekkefly launch. HejjéTrac uses its native desktop window.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `false` |

### `configs[].tracker.saveVideo`

Save supported bundled tracker camera footage for replay; enabled when omitted. Explicit false disables it. Close an already open tracker normally before changing this option, then restart it. Footage remains pending until the independent recorder finishes saving.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `configs[].tracker.recordingDirectory`

Optional headless Rekkefly launch. HejjéTrac uses its native desktop window.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].tracker.recordingPrefix`

Optional headless Rekkefly launch. HejjéTrac uses its native desktop window.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].displayOrder`

Ordered camera allow-list. R=Right, B=Back, L=Left, F=Front, U=Up, D=Down. RBLF draws those four views in that order. Omitted letters disable their cameras; an empty order renders no stimulus cameras.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `""` |

### `configs[].targetDisplay`

An explicit rig display takes precedence. Omission inherits the top-level or command-line display; effective JSON preserves that inheritance.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

UI `readOnly`: `true`. This governs the form, not removal of stored values.

## telemetry group

### `telemetry`

Publish live experiment state and frame timing over a ZeroMQ PUB output. Independent of the incoming closed loop data. Enabled by default.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "enabled": true,
  "bindAddress": "127.0.0.1",
  "port": 9880,
  "rateHz": 20.0,
  "topic": "matrex.telemetry.v2",
  "contextIntervalSeconds": 2.0
}
```

</details>

### `telemetry.enabled`

Publish live state and frame timing. Default ON; this output is separate from the closed loop input.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `telemetry.bindAddress`

Local interface used by the telemetry publisher. 127.0.0.1 allows local subscribers; 0.0.0.0 allows network subscribers.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"127.0.0.1"` |

### `telemetry.port`

TCP port for telemetry output. Default 9880. Choose a free port, distinct from all closed loop input ports.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `9880` |
| `minimum` | `1` |
| `maximum` | `65535` |

### `telemetry.rateHz`

Maximum telemetry snapshots per second. Default 20 Hz; captured on the main thread and serialized/published on a worker.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `20.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |
| UI soft slider range | `{"min":1.0,"max":120.0,"step":1.0}`; display/edit convenience, not an extra runtime constraint. |

### `telemetry.topic`

ZeroMQ topic prefix for experiment telemetry. Default matrex.telemetry.v2.

| Contract | Emitted value |
| --- | --- |
| Type | `string or null` |
| Required by parent schema | No |
| Default | `"matrex.telemetry.v2"` |

### `telemetry.contextIntervalSeconds`

Compact session context heartbeat for late-joining dashboards. Topic matrex.telemetry.v2.context uses schema 1/kind matrex.context; normal frame snapshots retain schema 2.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `2` |
| UI unit label | seconds |
| `minimum` | `0.25` |
| `maximum` | `60` |

## statusOverlay group

### `statusOverlay`

Operator status panel showing flight state, run progress and diagnostics. Visible by default. Tab shows or hides it during the experiment.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "visible": true,
  "targetDisplay": 0
}
```

</details>

### `statusOverlay.visible`

Show the operator status panel when the experiment starts. Default ON. Tab toggles it during the run.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `statusOverlay.targetDisplay`

Zero-based monitor for the operator status panel. Default 0 is the primary monitor; separate from stimulus camera output.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `0` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

## overheadCamera group

### `overheadCamera`

Operator overview in Swarm/Kannadi; independent of animal camera letter order.

| Contract | Emitted value |
| --- | --- |
| Type | `object or null` |
| Required by parent schema | No |
| `additionalProperties` | `true` |

<details><summary>Exact emitted default JSON</summary>

```json
{
  "enabled": true,
  "targetDisplay": -1,
  "x": 0.58,
  "y": 0.02,
  "width": 0.4,
  "height": 0.4,
  "resolution": 512,
  "markerSizePixels": 14.0,
  "trailDurationSeconds": 60.0,
  "trailSampleInterval": 0.1,
  "trailWidthPixels": 1.5,
  "trailBreakDistance": 50.0
}
```

</details>

### `overheadCamera.enabled`

Create the overview.

| Contract | Emitted value |
| --- | --- |
| Type | `boolean` |
| Required by parent schema | No |
| Default | `true` |

### `overheadCamera.targetDisplay`

Zero-based monitor for the overview. -1 follows the stimulus display. Default -1.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `-1` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `overheadCamera.x`

Normalized bottom-left of panel.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.58` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.y`

Normalized bottom-left of panel.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.02` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.width`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.4` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.height`

Fraction of display width/height.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.4` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.resolution`

Square render texture pixels; 512 is a practical starting point.

| Contract | Emitted value |
| --- | --- |
| Type | `integer` |
| Required by parent schema | No |
| Default | `512` |
| `minimum` | `-2147483648` |
| `maximum` | `2147483647` |

### `overheadCamera.markerSizePixels`

Fixed screen-size arrow, visible while zooming out.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `14.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.trailDurationSeconds`

Fading history; use 30 for shorter trails.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `60.0` |
| UI unit label | seconds |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.trailSampleInterval`

Seconds between samples.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `0.1` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.trailWidthPixels`

Screen-space trail width.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `1.5` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

### `overheadCamera.trailBreakDistance`

World-unit jump that breaks a trail at reset/wrap.

| Contract | Emitted value |
| --- | --- |
| Type | `number` |
| Required by parent schema | No |
| Default | `50.0` |
| `minimum` | `-3.40282347e+38` |
| `maximum` | `3.40282347e+38` |

## Source

[Bundled schema source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Resources/ConfigurationUI/schemas.json) · [Schema generator](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs)
