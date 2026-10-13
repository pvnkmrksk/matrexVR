# Experiment variants

Each JSON file is a complete example that can be copied and edited independently. Paths in sequences are relative to StreamingAssets. Select a matching system configuration, then copy a sequence to `sequenceConfig.json` or reference an experiment from your own sequence. Do not change the Kinefly bridge protocol.

| Folder | Variations |
|---|---|
| `System` | Explicit Kinefly six-camera flight; FicTrac four-camera and six-camera walking layouts. |
| `Flight` | Selwyn live sampled sky, fixed date/time sky, historical Adaminaby panorama, migration AGL 10/100/1000; the active 240 s stars → 240 s leftward dorsal swarm recipe. |
| `Swarm` | Physical generated moths in a moving 4 m cube, speeds 6.17 ± 1.24 m/s; per-rig mean-heading aligned (0°), crossed (+90°) and opposed (180°) presentations; dispersed 3D directions; angular flickering Bogong dots; recorded Selwyn moth playback. |
| `Choice` | Empty control, two cylinders at ±45°, moving band. |
| `DynamicChoice` | Time trigger, area trigger with timeout, independent per-rig sequences, adaptive gray discrimination. |
| `Optomotor` | Yaw, pitch, roll, and a frequency/speed sweep. |
| `Sequences` | Heading-angle comparisons; migration heights; grating axes; dynamic design; Choice scene environments; historical panorama; Bogong sky; all-paradigm wiring. |

`Sequences/all-paradigms.example.json` illustrates file wiring. Tracking input mode comes from the system configuration for the whole run; use FicTrac for walking numeric gains or Kinefly for flight yaw-rate gain. The file does not switch tracker hardware between steps.

Flat experiments retain their sequence presentation timer after heading measurement; phased experiments use their nested durations. Reset switches explicitly control continuity. Measurement remains visible and spawners opt in separately. Full phase details: [experiment-phases.md](../../../docs/experiment-phases.md). Telemetry: [heading-reference-telemetry.md](../../../docs/heading-reference-telemetry.md).

`Flight/selwyn-live-sky.example.json` samples a new image every 30 minutes. The image is static between samples. The fixed-sky example disables advancing time. The panorama example uses the existing image asset.

The preserved Kannadi recipes remain under `Kannadi/`; this collection does not replace or rewrite them.

`Sequences/kinefly-auto-trim.example.json` runs a 240-second Kinefly calibration over Selwyn terrain with the live Milky Way star sky. Its provisional flight threshold and bounded corrections are tuned from six past recordings. See [auto trim](../../../docs/kinefly-auto-trim.md) for controls, units, replay results and continuation with trimming off.

`Flight/selwyn-trajectory-playback.example.json` plays the converted October 7 recording as lightweight flapping moths inside an animal-centered periodic volume. Terrain coordinates, speeds and AGL use metres. `Sequences/selwyn-trajectory-playback.example.json` is an independent 1200-second looping recipe. The full-duration four-file reference is `Swarm/selwyn-milky-way-swarm-cycle.sequence.json`: 360 s setup, 240 s Milky Way measurement, 240 s Milky Way/swarm and 240 s swarm-only; setup runs once, then the three 240 s blocks repeat with trimming off. It uses 150 physical moths per rig and a 7 m/s observer. See [cycle configuration](../../../docs/selwyn-milky-way-swarm-cycle.md). See [generated swarm](../../../docs/generated-bogong-swarm.md) and [recorded playback](../../../docs/swarm-playback.md) for parameters, units and validation.

The active sequence retains your quick-test 3-second setup/measurement timings and gain 2.5 on those first two rows. The full-duration cycle uses separate `-full.example.json` setup/measurement files, retaining the requested 240-second mean window.
