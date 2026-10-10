# Experiment variants

Each JSON file is a complete example that can be copied and edited independently. Paths in sequences are relative to StreamingAssets. Select a matching system configuration, then copy a sequence to `sequenceConfig.json` or reference an experiment from your own sequence. Do not change the Kinefly bridge protocol.

| Folder | Variations |
|---|---|
| `System` | Explicit Kinefly six-camera flight; FicTrac four-camera and six-camera walking layouts. |
| `Flight` | Selwyn live sampled sky, fixed date/time sky, historical Adaminaby panorama, migration AGL 10/100/1000; the active 240 s stars → 240 s leftward dorsal swarm recipe. |
| `Swarm` | Per-rig mean-heading aligned (0°), crossed (+90°) and opposed (180°) presentation after 180 seconds; dispersed 3D directions; constant-angular-size flickering Bogong dots. |
| `Choice` | Empty control, two cylinders at ±45°, moving band. |
| `DynamicChoice` | Time trigger, area trigger with timeout, independent per-rig sequences, adaptive gray discrimination. |
| `Optomotor` | Yaw, pitch, roll, and a frequency/speed sweep. |
| `Sequences` | Heading-angle comparisons; migration heights; grating axes; dynamic design; Choice scene environments; historical panorama; Bogong sky; all-paradigm wiring. |

`Sequences/all-paradigms.example.json` illustrates file wiring. Tracking input mode comes from the system configuration for the whole run; use FicTrac for walking numeric gains or Kinefly for flight yaw-rate gain. The file does not switch tracker hardware between steps.

Flat experiments retain their sequence presentation timer after heading measurement; phased experiments use their nested durations. Reset switches explicitly control continuity. Measurement remains visible and spawners opt in separately. Full phase details: [experiment-phases.md](../../../docs/experiment-phases.md). Telemetry: [heading-reference-telemetry.md](../../../docs/heading-reference-telemetry.md).

`Flight/selwyn-live-sky.example.json` samples a new image every 30 minutes. The image is static between samples. The fixed-sky example disables advancing time. The panorama example uses the existing image asset.

The preserved Kannadi recipes remain under `Kannadi/`; this collection does not replace or rewrite them.

`Sequences/kinefly-auto-trim.example.json` runs a 240-second Kinefly calibration over Selwyn terrain with the live Milky Way star sky. The active `sequenceConfig.json` uses this test. Its provisional flight threshold and bounded corrections are tuned from six past recordings. See [auto trim](../../../docs/kinefly-auto-trim.md) for controls, units, replay results and continuation with trimming off.
