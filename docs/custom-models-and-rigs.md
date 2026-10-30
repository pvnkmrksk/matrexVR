# Custom models and sixteen-rig worlds

This overhaul uses **Kannadi** for both mirrored animals and simulated/recorded
populations. Choose `populationMode: "Mirror"` or `"Swarm"` in the experiment
configuration. Existing sequence rows named `Swarm` resolve to Kannadi in the
runtime and preserve their historical centimetre scale. The old scene asset is
retained for source history and is disabled in desktop build settings.

## Add a model without rebuilding

Export a model as a **self-contained GLB 2.0 file**, with textures and animation
clips embedded. Copy it into the configuration folder beside the experiment
files, normally under `Models/`. In an Editor checkout this is
`Assets/StreamingAssets/Models/`; desktop profiles show their writable
configuration folder in the studio. Paths are relative to that folder.

GLB is an interchange format: a Unity package, FBX file, Blender project or
asset-store download must first be exported to GLB. Runtime importing does not
execute scripts, construct Unity-specific shaders, or import gameplay components
from a purchased Unity package. Model cameras and lights are disabled so the
experiment owns its illumination and physical camera configuration.

Choice and Kannadi world objects share the same transform fields:

```json
{
  "objects": [
    {
      "enabled": true,
      "modelPath": "Models/tree.glb",
      "position": { "x": 0, "y": 0, "z": 100 },
      "rotation": { "x": 0, "y": 45, "z": 0 },
      "scale": { "x": 100, "y": 100, "z": 100 },
      "animationClip": "Wind",
      "animationSpeed": 1
    }
  ]
}
```

`modelPath` takes precedence over a built-in `type`. Omit `animationClip` to use
the first embedded clip. Its clock starts at zero; `animationSpeed: 0` freezes it.
Disable an object to leave its configuration in place without importing or
rendering it. Most walking worlds use centimetres and terrain worlds use metres;
choose the scale for the destination world. Absolute Kannadi objects stay fixed
in the shared world. Heading-relative Kannadi objects use the mirrored
population's visibility policy. The optional observer/target visibility matrix
described below changes population visibility; an independently routed private
world-cue editor remains a separate extension.

To use an external model for the mirrored animals, set
`kannadiModelPath: "Models/animal.glb"`. For generated/recorded swarm agents,
`modelPath` is also supported in the swarm settings. A Kannadi-specific
`kannadiModelPath` overrides the general population selection. Built-in
`kannadiTilePrefab` names remain available.

Models preload **before experiment clocks start**. Missing, invalid or
externally referenced model data stops startup with a diagnostic. Each consumed
GLB is saved byte-for-byte in the run's hashed `Configs/Snapshots` archive, with
the original reference and hash in `config_manifest.json`. Replay resolves that
saved copy; it does not need the original model file and does not write to it.
Exported materials and animation clips remain subject to the embedded content
and importer capabilities; preservation of the bytes is separate from a claim
of pixel-perfect import across future Unity versions.

### Runtime model support in this build

The player uses the UnityGLTF revision pinned by `Packages/packages-lock.json`
with Unity 6000.3.16f1. A vendor's source format or Unity package is not a claim
that its shaders, components and animation will survive interchange export.
Keep the original license and export a small test asset before a large purchase.

| Content | Current contract | Verified scope |
| --- | --- | --- |
| GLB 2.0 scene, geometry and base-color material | Embedded, uncompressed `.glb`; choose transform and scale in the experiment | Actual Mac Unity import, rendered animation and archived-byte replay |
| Embedded transform animation | Named clip, speed, loop; sampled from experiment/replay time | Actual GLB translation-clip round trip and edit-only preview |
| Embedded PNG/JPEG and standard glTF material properties | Importer supported; use embedded buffer views or data URIs | Preview/material rendering checked; no comprehensive vendor material corpus |
| Skeletal and morph-target clips | Export to standard glTF animation | Exported locust, downloaded RiggedSimple skin and AnimatedMorphCube morphs pass actual deformed-geometry and forward/backward replay checks on Mac; other assets need their own acceptance |
| Optional material extensions | Subject to this pinned importer and shader set | No blanket appearance-equivalence claim; check the exported asset |
| Draco, Meshopt or BasisU required compression | Rejected before import; matching optional decoders are absent from this player | Production preflight regression |
| External `.bin`/texture/URL dependencies | Rejected, including local relative paths; embed all dependencies | Production preflight regression |
| FBX, `.gltf`, Blender project, Unity package | Convert/export to embedded GLB before copying into the config folder | Deliberate file-format boundary |
| Asset scripts, custom Unity shaders, lights and cameras | No scripts imported; imported lights/cameras disabled; experiment owns lighting and views | Visual previews copy only rendering/animation data and never activate experiment scripts |
| Windows/Linux import and rendering | Same source contract and archived bytes | Not established by the Mac rendering checks; validate on the acquisition build/platform |

`python3 tools/test_runtime_model_contract.py` exercises 22 production preflight
and active-reference checks. These establish file/dependency validation, not
renderer correctness. A given acquisition build records its own identity and
the model bytes; use that same build for scientifically faithful replay.

Load `Examples/Sequences/custom-models.sequence.json` for three real GLB object
examples. **Appearance → Objects** combines the model/path, optional color
override and preview. Placement and detailed surface controls stay collapsed.
The original model file is unchanged. `appearance.enabled` opts an object into
`appearance.color`; `keepTexture` chooses tint versus a uniform color. The
population equivalent is `modelAppearance` in Kannadi/generated-model configs.
`Examples/Sequences/kannadi-glb.sequence.json` points social animals at the
visible exported locust file. Sources, exact hashes and license notices are
beside the files in `Models/Examples/`.

### Appearance and visibility

Custom panoramas accept `panoramaRotationDegrees` from 0 to 360. Omitted means
zero; 360 normalizes to zero. This rotates azimuth without changing the image or
the archived source. The Sky tab offers this field only for a custom panorama.

An optional `visibilityMatrix` has `enabled` and a square boolean `rows` array
up to 16×16. Rows are observing VRs; columns are visible population VRs, indexed
by VR number minus one. Disabled preserves standard scene behavior. Enabling it
replaces only population-layer bits on the six animal cameras, preserving the
world layers and overhead camera. The editor keeps a fixed 16×16 grid and grays
out unconfigured individuals. Defaults are all peers visible and self hidden.
Archived configuration supplies the identical matrix to individual replay views.

The object and sky editors show a bounded rendered preview while editing. They
cancel obsolete imports when selection changes or acquisition starts. Built-in
visual aliases are generated during the player build; for source-editor play,
use **Tools → Experiment setup → Refresh available choice objects** once after
changing the object catalog. Preview copies never run acquisition controllers
or create sky/model evidence inside a recording.

## One editable rig template

The six-face camera prefab remains the authoring source. The runtime derives
only the configured **VR1 through VR16** instances, with canonical F, B, R, L, U
and D cameras at 90 degrees. Unconfigured authored instances are inactive. This
avoids maintaining sixteen diverging prefab variants while keeping cameras
editable in Unity.

For development, open an experiment scene and choose **Tools → MATREX VR →
Materialize VR1 through VR16 in current scene**. The command makes the instances
visible in the hierarchy, preserves the active subset, and leaves saving the
scene under the developer's control. At runtime the system configuration still
owns which instances and camera faces are enabled. Old manual worlds with one
camera on the tracking root are adapted to six child cameras before expansion.

Each rig has a deterministic stimulus layer. In standard Mirror mode every
camera hides its own animal population and sees the other active populations;
`watchIndex` provides the existing single-population override. In Swarm mode
each viewer sees its own configured stimulus population. The overview can show
all populations once. An enabled visibility matrix overrides those population
bits explicitly; disabling it restores the scene's usual policy.

Identical population settings with `resetPositionOnStart: false` retain the
existing swarm/mirror instances between phases. The tracked animal pose also
continues. Changing a population's configuration rebuilds that population;
`resetPositionOnStart: true` requests the configured starting pose and a fresh
population. Rotation reset remains independently controlled by
`resetRotationOnStart`.

## Configure the sun

All experiment families accept an optional `sun` block:

```json
{
  "sun": {
    "enabled": true,
    "intensity": 1,
    "color": { "r": 1, "g": 0.95, "b": 0.85, "a": 1 },
    "rotation": { "x": 50, "y": 330, "z": 0 }
  }
}
```

The block selects one directional light, disables extra directional lights, and
sets its color, brightness and orientation. Position has no effect on a
directional source. Omit `sun` to retain the world's authored illumination.
Replay applies the saved values to the imported world.

## Reproducibility boundary

The stored experiment seed drives sequence grouping/shuffling, phase
initialization, per-component random streams and particle seeds. Color drift
uses a private stream; its random draws cannot be consumed by another animal.
Spawner initialization and delayed band setup also have independent streams.

A fixed seed does not make variable frame steps, physics integration, arbitrary
third-party scripts or a different engine build identical. Recorded positions,
stimulus events, animation states and saved model/configuration bytes remain
the authority for replay. The new animation evidence supports faithful seeking
without asking a second independent simulation to reproduce an unsaved history.

The [overhaul plan](overhaul-0.5-plan.md) tracks the remaining terrain/world
catalog, previews, visibility matrix and automation-interface work separately.
