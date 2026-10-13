# Visual geometry, clearance and horizon orientation

The current `codex/config-ui-test` branch already contains all commits from `origin/codex/bogong-night-sky` (verified after fetching on 14 October 2026). Its night-sky, Swarm, input, archive and configuration-workbench improvements are retained. There was no missing night-sky history to merge.

The earlier September `navrug` experiment lived on a different branch. This implementation is integrated with the current Choice/phased-config workflow and Input System instead of replacing those newer systems with the old controllers.

## What RS means here

**RS is one landscape containing rough and smooth regions.** It does not encode a visual-versus-navigation pairing. In the supplied `Rugosity_RS` lab the left half (negative X) is rough and the right half is much smoother, with a continuous transition around X = 0. `Flat` is a separate, exactly planar mesh covering the same footprint.

Four assignments are independent:

| Assignment | What it controls |
| --- | --- |
| `visualSurface` | Which registered surface is rendered |
| `collisionSurface` | Which registered surface has active physical colliders |
| `heightSurface` | The geometry used for above-ground clearance; empty inherits collision |
| `normalSurface` | The geometry used for tilt; empty inherits collision |

This distinction resolves an important ambiguity: **flat collision geometry cannot itself produce an up/down AGL trajectory over visible hills**. To retain visible-terrain AGL with level orientation, select RS as the height reference separately. The physical collider can still be Flat.

| Experiment | Visual | Collision | Height reference/mode | Orientation |
| --- | --- | --- | --- | --- |
| Traditional matching geometry | RS | RS | RS, AGL | RS normal |
| Visible bumps, level camera | RS | Flat | RS, AGL | World up |
| Hidden roughness, flat visible plane | Flat | RS | RS, AGL | RS normal |
| Flat control | Flat | Flat | Flat, AGL | World up |
| Visible bumps, no camera height/tilt variation | RS | Flat | Fixed world Y | World up |
| Flat visible plane, tilt only | Flat | RS | Fixed world Y | RS normal |

In the second row, camera height still varies; the ground silhouette can be irregular, but the camera does not acquire terrain-induced pitch/roll. In the third row, the visible plane's boundary remains straight while its position and angle in the image change. Pitch shifts the horizon vertically; roll slopes it. The last two rows help separate vertical motion from orientation changes.

## Tune in the Unity Inspector first

1. Open **Tools → Rugosity → Open RS tuning scene**, or `Assets/Scenes/Rugosity_RS.unity`.
2. Select **Rugosity — select to tune**. The custom Inspector provides surface dropdowns, height/orientation modes, clearance and normal-response controls.
3. Press Play. Arrow keys move; A/D turn. Tracking is disabled in the lab examples so manual tests are not driven by incoming hardware data.
4. Change the Inspector fields while walking between rough and smooth regions. These changes take effect without restarting. No runtime condition-selection GUI is required.
5. If a sequence is running, choose **InspectorOnly** to prevent its next trial from replacing your tuning. This policy applies only in the Editor; builds always apply JSON.
6. **Keep current settings in scene after Stop** captures a snapshot at the time of the click and restores it after leaving Play mode. Save the scene afterward. Click it again if you make further changes before Stop.
7. **Copy effective rugosity JSON** copies the complete current settings. **Save rugosity into choice JSON…** writes the settings into a choice file while preserving its other fields. For phased documents it updates `stimulus.rugosity`.

In Edit mode, changing the Inspector updates visibility/collider roles. **Preview pose at current rig positions** also applies the selected height and tilt to the camera rig. It records Undo for the pose. These are ordinary scene-authoring edits; save or undo them as appropriate.

The lab contains no MainController, so opening it directly is an Inspector-only walkthrough. The same scene has a ChoiceController and can be used by the normal ControlScene/sequence workflow.

## Clean configuration

Choice configs use one optional `rugosity` object. For the visible RS/flat collision/level-camera condition:

```json
{
  "objects": [],
  "closedLoopPosition": false,
  "closedLoopOrientation": false,
  "initialPosition": { "x": -25, "y": 1, "z": -60 },
  "initialRotation": { "x": 0, "y": 0, "z": 0 },
  "rugosity": {
    "enabled": true,
    "visualSurface": "RS",
    "collisionSurface": "Flat",
    "heightSurface": "RS",
    "normalSurface": "",
    "heightMode": "AboveGround",
    "orientationMode": "WorldUp",
    "centimetersPerUnit": 1,
    "clearanceCm": 1,
    "normalRadiusCm": 0.5,
    "normalSamples": 5,
    "tiltResponse": 0
  }
}
```

Surface names refer to `RugositySurface.surfaceId` values registered on the scene's `RugosityWorld`. They are not asset filenames, layers, or object-name searches. Unknown or duplicate IDs fail validation instead of falling back to arbitrary ground.

Six ready-to-run choice files and a sequence are in `Assets/StreamingAssets/Examples/Rugosity/`:

- `traditional-rs.json`
- `visual-rs-level-agl.json`
- `visual-flat-hidden-rs.json`
- `flat-control.json`
- `visual-rs-fixed-height.json`
- `visual-flat-tilt-only.json`
- `sequence.json`: all six for 30 seconds each, in-place Choice transitions, looping, automatic sequence start.

The active `Assets/StreamingAssets/sequenceConfig.json` runs this six-condition sequence automatically: open ControlScene and press Play. The previous active Selwyn sequence is preserved at `Examples/Sequences/selwyn-before-rugosity.json`; copy it back to restore that experiment. Existing night-sky and Swarm choice fields can coexist with `rugosity`.

A sequence entry can override individual fields:

```json
{
  "sceneName": "Rugosity_RS",
  "duration": 30,
  "reloadScene": false,
  "parameters": {
    "configFile": "Examples/Rugosity/visual-flat-hidden-rs.json",
    "rugosity": {
      "heightMode": "Absolute",
      "absoluteHeight": 5
    }
  }
}
```

Precedence at each trial/phase boundary is **authored scene settings → choice/phase rugosity patch → sequence rugosity patch**. Omitted or null fields preserve the authored value. `""` on the height/normal surface explicitly means “inherit collision.” A later trial starts from the authored baseline, so previous trial overrides do not leak. Between trial boundaries, config is not reapplied every frame: Inspector changes remain live. Set `enabled: false` to restore the authored renderer/collider states and let the prior motion system control the rig.

## Parameters and units

| Parameter | Meaning |
| --- | --- |
| `heightMode` | `AboveGround` adds clearance to the selected height surface; `Absolute` fixes world Y |
| `orientationMode` | `WorldUp` removes pitch/roll while preserving yaw; `SurfaceNormal` aligns up to the selected normal surface |
| `clearanceCm` | Vertical clearance in centimeters; default 1. This is not distance along the normal |
| `centimetersPerUnit` | 1 for centimeter scenes, 100 for meter terrain scenes such as Selwyn |
| `absoluteHeight` | Fixed **world Y in scene units**, not a captured initial height |
| `normalRadiusCm` | Radius for smoothing sampled normals spatially; 0 uses the center alone |
| `normalSamples` | Number of ring samples (1–32), in addition to the center |
| `tiltResponse` | 0 gives exact alignment; positive values use exponential temporal response in inverse seconds |

The lab uses 1 cm per Unity unit and a 200 × 200 cm footprint. All height fields require finite values. Clearance and response are nonnegative. Height is corrected exactly each frame; smoothing applies only to orientation. Heading is preserved during normal alignment, and manual/FicTrac yaw uses world up only while this system owns the rig pose.

`MigrationMotion` still applies its configured wind, but yields AGL correction to Rugosity while active. The old terrain follower is temporarily disabled on controlled rigs and restored when Rugosity is disabled. Existing Bogong behavior remains unchanged in scenes without an active RugosityWorld.

## Use your own meshes or terrain tiles

Create a `RugositySurface` for each alternative and assign a unique ID:

- `terrains`: one or more Unity Terrain components, including adjacent tiles;
- `meshes`: one or more MeshFilters used for sampling (enable Read/Write for imported meshes);
- `renderers`: the renderers belonging to that visible alternative;
- `colliders`: the actual colliders belonging to that physical alternative.

Register the alternatives on one `RugosityWorld`. Assign rig transforms explicitly, or leave `rigTargets` empty to find this scene's ClosedLoop rigs. Keep surface objects active: the system changes renderers and colliders independently rather than deactivating their GameObjects. Do not include unrelated scenery, trees or actors in these arrays.

Unity Terrain data is sampled directly, including holes and bounds, regardless of renderer/collider enable state. Terrain must remain world-up. Mesh sampling uses a cached world-XZ triangle grid with interpolated normals and supports transformed/nonuniformly scaled meshes. Transform or mesh-reference changes rebuild the cache; call `InvalidateGeometry()` after editing mesh vertex data in code. This is a vertical ground-height model: the highest intersecting triangle is selected, not a cave/overhang traversal solver. Walking outside the reference footprint or into missing ground retains the last valid position.

Each surface keeps its own mesh/data. Neither choosing a different visible surface nor selecting a flat collider rewrites source geometry. Assign equivalent origins/footprints for clean comparisons. Independently chosen surfaces can intersect the camera; no hidden visible-terrain avoidance changes the requested experimental height.

## Recording and validation

With a MasterDataLogger, effective settings changes—including Inspector changes—are appended to `rugosity_states.jsonl` in the run directory, with UTC time and frame number. `RUGOSITY_STATE` records also appear in the runtime trace. The existing config archive still preserves original JSON; the state records explain deviations made during tuning.

`Assets/Editor/RugosityValidation.cs` tests six conditions, hidden terrain/mesh sampling, world transforms, centimeter conversion, bounds, yaw preservation, Inspector/config precedence and atomic invalid-config rejection. It also walks across rough and smooth regions through actual LateUpdate frames, renders native camera images, and checks the Keep-after-Stop workflow. Run on a disposable project copy with a `KANNADI_VALIDATION_COPY` marker:

```sh
Unity -batchmode -projectPath /path/to/disposable-copy \
  -executeMethod RugosityValidation.Run -logFile /tmp/rugosity-validation.log
```

Do not add `-quit`; the runner exits after Play-mode and Edit-mode checks. Results are written to `docs/rugosity-validation-results.json`, with renders in `docs/rugosity-renders/`. Hardware timing and live animal tracking are not covered.

Verified with Unity 6000.3.16f1: **2,390 rugosity checks passed**, including 60 movement frames per condition and retention after Stop. The inherited Choice/night-sky regression also passed **70 checks**. Static config validation passed for **74 JSON templates/examples**, including scene references and preserved archive hashes.
