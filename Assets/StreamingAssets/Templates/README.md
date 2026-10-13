# Complete configuration templates

Recorded swarms and the lightweight flapping 3D moth are documented in [swarm-playback.md](../../../docs/swarm-playback.md). `swarm-playback.template.json` starts with a blank data path. `playback.preRollSeconds` defaults to 0; the passive Selwyn example uses 5 seconds of incoming extrapolation. Its stationary-observer settings and the separate 5 m/s flight example are documented in the playback guide. Terrain/migration uses metres; walking scenes use centimetres. Set `worldUnitsPerMetre` when moving a swarm between them.

Start a new **Kinefly flight** experiment with these three files:

1. `system_config.template.json` → `StreamingAssets/system_config.json`. Set the real tracking addresses, ports and displays. All four rigs explicitly select Kinefly; the template uses six cameras per rig.
2. `experiment.template.json` → your own experiment file. This contains a complete Choice/Selwyn flight configuration under `stimulus`, with pre/post disabled by default.
3. `sequence.template.json` → `StreamingAssets/sequenceConfig.json`. Change its `parameters.configFile` to your experiment file. Every sequence option is explicit; start is manual, gain is 1, and the example presentation lasts one hour.

The full-duration reference is the [four-block Selwyn cycle](../../../docs/selwyn-milky-way-swarm-cycle.md): setup 360 s, then Milky Way 240 s, Milky Way/swarm 240 s, swarm-only 240 s. Setup runs once, then the three 240 s blocks repeat in order with trimming off (`loopStartIndex: 1`). Individual files own the experiment parameters, with 150 moths per rig and a 7 m/s observer. Copy templates when intentionally creating a new setup. JSON `null` explicitly leaves an optional alternative/inherited value unset; it is not a numeric zero. Fields that do not apply to a controller are not invented or shared across schemas. Scene names and paths are case-sensitive.

| Template | Scene / paradigm | Notes |
|---|---|---|
| `system_config.template.json` | All scenes | Full per-rig hardware, Kinefly mode, manual control, frame timing, overview, telemetry and status settings. |
| `sequence.template.json` | Outer experiment sequence | Complete `autoStart`, `loop`, `loopStartIndex`, `randomise`, `sceneName`, `duration`, `gain`, `autoTrim`, `reloadScene` and file reference. |
| `experiment.template.json` | `Choice_Selwyn` flight | Complete canonical experiment with auto-trim tuning inside `stimulus`; autopilot 7 world units/s, 100-unit AGL, Selwyn sky. |
| `choice.template.json` | `Choice`, `Choice_Forrest`, all four `Choice_TwoTreeIndia_*` scenes | Full Choice schema, including every object property. The scene supplies its authored environment. |
| `choice-band.template.json` | `Choice` moving bands | Same Choice schema with active band geometry and motion. |
| `migration.template.json` | `Choice_Selwyn` / terrain Choice flight | Full Choice schema with wind/AGL/autopilot controls; procedural sky disabled. |
| `swarm.template.json` | `Swarm`, planar swarm | Complete Swarm controls, gains, per-rig poses, visuals and optional sky/reference blocks. |
| `bogong-swarm.template.json` | `Swarm`, 3D Bogong swarm | Physical flapping moths, generated speed distribution, moving 4 m cube and sky. Explicit metre scale; configurable count, mean and SD. |
| `swarm-playback.template.json` | `Swarm`, generated or recorded moths | Complete Swarm schema, metre-scale volume and physical 3D moth; blank `trajectoryFile` retains generated motion. |
| `optomotor.template.json` | `Optomotor` | Full grating stimulus and repeat settings. |
| `dynamic-choice.template.json` | `Choice_desync` | Full design, steps, trigger, intertrial, camera, object and adaptive controls. Use `parameters.design`, not `configFile`. |
| `kannadi.template.json` | `Kannadi`, multiplayer mirror | Existing Kannadi template preserved unchanged. |
| `kinematic.template.json` | `Kannadi`, single-tile calibration | Existing kinematic template preserved unchanged. |

`ControlScene` collects metadata and starts the sequence. `ReplayScene` consumes recorded data and can use a Dynamic Choice design to rebuild its environment; neither has a separate experiment JSON schema. Disabled legacy scenes are not additional supported paradigms.

## Variant examples

[Examples/README.md](../Examples/README.md) indexes complete, independent JSON variants and sequences. Examples are not partial overrides. Walking/FicTrac layouts are under `Examples/System`; the canonical system template explicitly selects Kinefly flight. Select the correct system setup before running walking or flight variants.

For flat Choice blocks, `headingReference.measureDuringPresentation: true` measures while the sequence timer runs; `saveAs` supplies a named per-rig reference to later `swarm.headingReferenceId`. Both default to legacy behavior (false/empty). Named references require fixed sequence order. `directionDistribution: "VonMises"` opts into corrected concentration; the default `"Legacy"` preserves historical swarms. See the cycle guide for measured kappa and visible count defaults.

For phased experiments, put heading assessment inside `preStimulus.headingReference`, never in sequence parameters. Spawners must separately opt in with `useHeadingReference: true`. Both pose reset switches default true; turn them off for continuity. See [experiment-phases.md](../../../docs/experiment-phases.md). `enabled: false` means no assessment delay. **Tab** restores the status/error panel. The sky is held between its configured image updates; `advanceWithRealTime` does not enable continuous interpolation.

The entire `Kannadi/` directory remains unchanged. Historical configurations and their original template versions are in [Archive/Legacy](../Archive/Legacy). References from preserved sequences can still resolve there when no current file exists at the requested path.

See [configuration-reference.md](../../../docs/configuration-reference.md), [heading-reference-telemetry.md](../../../docs/heading-reference-telemetry.md), [night-sky.md](../../../docs/night-sky.md) and [data-formats.md](../../../docs/data-formats.md) for units, field semantics and logging. Walking scenes use centimeters; imported flight terrain uses its authored world scale.

Your active sequence retains the 3-second setup/measurement and gain 2.5 quick-test edits. Copy `Examples/Swarm/selwyn-milky-way-swarm-cycle.sequence.json` over `StreamingAssets/sequenceConfig.json` before Play for the full-duration recipe; the reference uses separate `-full` setup/measurement files.
