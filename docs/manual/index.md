# MATREX VR manual

MATREX VR connects measured animal movement to controlled visual environments in Unity. Use this manual to operate an existing experiment, understand the path from a tracker packet to an LED viewport, interpret saved data, or extend the source.

The manual describes the **0.3 software and the current source**. Sections for modern native tracker decoding and the live browser bridge are explicitly marked as source updates after 0.3; those changes do not retroactively appear in the stable 0.3 downloads. The standalone application, this documentation website, external trackers and analysis tools have separate responsibilities. Documentation updates do not change an installed application or publish a new software release.

## Choose your starting point

| Goal | Start here |
| --- | --- |
| Run a supplied experiment | [Getting started](../getting-started.md), then [configuration studio](../configuration-ui.md) |
| Understand how the application works | [Runtime architecture](architecture.md) and [component index](components.md) |
| Route camera faces to an LED array | [Rigs and viewports](viewports.md), then [six-camera reference](../six-camera-rig.md) |
| Connect animal tracking | [Input contracts](input-contracts.md) and [ClosedLoop](closed-loop.md) |
| Find any exact schema field | [Full generated schema atlas](generated/schema-index.md) |
| Choose a visual task | [Scenes and capabilities](scenes.md) and [native gallery](gallery.md) |
| Inspect the original experiment | [Recording pipeline](recording-pipeline.md), [data formats](../data-formats.md) and [replay](../replay.md) |
| Add an experiment or asset | [Extension guide](extending.md) |
| Connect the wider toolchain | [Trackers and analysis](integrations.md) |

## Follow one experiment through the system

1. **Describe it.** The system file assigns hardware, tracker endpoints and rig identities; a sequence selects scenes and experiment files.
2. **Configure it.** The studio edits those same JSON documents. Opening a default or an unknown field does not rewrite the experiment.
3. **Run it.** MainController loads the selected scene or advances a compatible scene in place. The scene controller applies stimuli and tracking behavior.
4. **Present it.** ClosedLoop and manual/autopilot controls update rig poses. ViewportSetter maps the allowed camera faces to configured display rectangles.
5. **Retain it.** Recordings preserve animal poses, supported virtual populations, the consumed configuration graph and generated sky provenance.
6. **Inspect it.** Telemetry monitors the running process; replay reconstructs supported recorded evidence on a shared timeline.

An **animal** is the real subject providing movement input. An **agent** is a virtual, mirrored or replayed individual. `VR1`, `VR2`, and other positive `VRn` names are routing identities, not animal names or species.

## What is included

The compact desktop build includes ControlScene, Choice, Choice_desync, Choice_Selwyn, Swarm, Kannadi, Optomotor, TerrainCueLab, Rugosity_RS and ReplayScene. Optional forest/tree environments remain in source and must be enabled in a custom build. The [scene guide](scenes.md) distinguishes a controller family, a particular authored environment and its JSON recipe.

**Kannadi — ಕನ್ನಡಿ — means “mirror” in Kannada.** It presents a matrix of mirrored animal poses across a hexagonal arrangement. The [Kannadi section](scenes.md#kannadi-the-mirror-matrix) explains visibility and replication; it is more than an isolated animal model.

## Coordinates, units and timing

Unity's world uses +X right/east, +Y up and +Z forward/north. Positive yaw turns +Z toward +X. Walking and rugosity scenes use centimetres; Selwyn/migration terrain uses metres. Keep scene scale, tracker input units and physical moth dimensions distinct.

Presentation and rig logging follow rendered frames. They are not an independent hardware acquisition clock. The input contract identifies raw tracker units; the run's archived JSON records the actual gains and scale. Read [data formats](../data-formats.md) before converting recordings into physical quantities.

## Evidence and limits

The site links the actual runtime source and saved validation reports. Synthetic captures show the software's rendering, not a live animal session. Accelerated repeated-load checks establish behavior in their recorded workload; they do not prove that a long physical acquisition session or the historical Unity crash is resolved. [Hardware checks](hardware.md) and [lifecycle evidence](../scene-lifecycle.md) describe those boundaries.

For a citation, use [the software citation](citation.md). For ready-to-run applications, use the existing [OS downloads](../site/index.html#downloads).
