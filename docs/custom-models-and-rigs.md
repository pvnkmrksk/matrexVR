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
population's visibility policy; private per-viewer world cues need the planned
visibility editor.

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
all populations once. A general visibility matrix remains a future feature.

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
