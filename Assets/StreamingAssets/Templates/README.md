# Complete configuration templates

Start a new **Kinefly flight** experiment with these three files:

1. `system_config.template.json` → `StreamingAssets/system_config.json`. Set the real tracking addresses, ports and displays. All four rigs explicitly select Kinefly; the template uses six cameras per rig.
2. `experiment.template.json` → your own experiment file. This contains a complete Choice/Selwyn flight configuration under `stimulus`, with pre/post disabled by default.
3. `sequence.template.json` → `StreamingAssets/sequenceConfig.json`. Change its `parameters.configFile` to your experiment file. Every sequence option is explicit; start is manual, gain is 1, and the example presentation lasts one hour.

The active sequence selects the repeating Selwyn stars-to-left-swarm experiment. The hardware file is unchanged. Copy templates when intentionally creating a new setup. JSON `null` explicitly leaves an optional alternative/inherited value unset; it is not a numeric zero. Fields that do not apply to a controller are not invented or shared across schemas. Scene names and paths are case-sensitive.

| Template | Scene / paradigm | Notes |
|---|---|---|
| `system_config.template.json` | All scenes | Full per-rig hardware, Kinefly mode, manual control, frame timing, overview, telemetry and status settings. |
| `sequence.template.json` | Outer experiment sequence | Complete `autoStart`, `loop`, `randomise`, `sceneName`, `duration`, `gain`, `autoTrim`, `autoTrimSettings`, `reloadScene` and file reference. |
| `experiment.template.json` | `Choice_Selwyn` flight | Complete canonical experiment; autopilot 7 world units/s, 100-unit AGL, Selwyn sky. |
| `choice.template.json` | `Choice`, `Choice_Forrest`, all four `Choice_TwoTreeIndia_*` scenes | Full Choice schema, including every object property. The scene supplies its authored environment. |
| `choice-band.template.json` | `Choice` moving bands | Same Choice schema with active band geometry and motion. |
| `migration.template.json` | `Choice_Selwyn` / terrain Choice flight | Full Choice schema with wind/AGL/autopilot controls; procedural sky disabled. |
| `swarm.template.json` | `Swarm`, planar swarm | Complete Swarm controls, gains, per-rig poses, visuals and optional sky/reference blocks. |
| `bogong-swarm.template.json` | `Swarm`, 3D Bogong swarm | Same Swarm schema with a 3D volume, Bogong visual and generated sky. |
| `optomotor.template.json` | `Optomotor` | Full grating stimulus and repeat settings. |
| `dynamic-choice.template.json` | `Choice_desync` | Full design, steps, trigger, intertrial, camera, object and adaptive controls. Use `parameters.design`, not `configFile`. |
| `kannadi.template.json` | `Kannadi`, multiplayer mirror | Existing Kannadi template preserved unchanged. |
| `kinematic.template.json` | `Kannadi`, single-tile calibration | Existing kinematic template preserved unchanged. |

`ControlScene` collects metadata and starts the sequence. `ReplayScene` consumes recorded data and can use a Dynamic Choice design to rebuild its environment; neither has a separate experiment JSON schema. Disabled legacy scenes are not additional supported paradigms.

## Variant examples

[Examples/README.md](../Examples/README.md) indexes complete, independent JSON variants and sequences. Examples are not partial overrides. Walking/FicTrac layouts are under `Examples/System`; the canonical system template explicitly selects Kinefly flight. Select the correct system setup before running walking or flight variants.

For phased experiments, put heading assessment inside `preStimulus.headingReference`, never in sequence parameters. Spawners must separately opt in with `useHeadingReference: true`. Both pose reset switches default true; turn them off for continuity. See [experiment-phases.md](../../../docs/experiment-phases.md). `enabled: false` means no assessment delay. **Tab** restores the status/error panel. The sky is held between its configured image updates; `advanceWithRealTime` does not enable continuous interpolation.

The entire `Kannadi/` directory remains unchanged. Historical configurations and their original template versions are in [Archive/Legacy](../Archive/Legacy). References from preserved sequences can still resolve there when no current file exists at the requested path.

See [configuration-reference.md](../../../docs/configuration-reference.md), [heading-reference-telemetry.md](../../../docs/heading-reference-telemetry.md), [night-sky.md](../../../docs/night-sky.md) and [data-formats.md](../../../docs/data-formats.md) for units, field semantics and logging. Walking scenes use centimeters; imported flight terrain uses its authored world scale.
