# Editable experiment templates

Copy a JSON file, rename it, and change its values. All paths inside a sequence are relative to StreamingAssets. Do not edit the reference copy during routine experiments.

| File | Use |
|---|---|
| system_config.template.json | Copy to StreamingAssets/system_config.json; four RBLF rigs, 2.6 cm balls, local test addresses, overview and manual controls. |
| system-config-six-cameras.template.json | Six directions per rig (DRBLFU), four non-overlapping rows, 128×128 panels; copy to system_config.json. |
| sequence.template.json | Copy to StreamingAssets/sequenceConfig.json; 20 s Swarm then 1000 s Kannadi. |
| sequence-all-modes.example.json | Example wiring for every supported experiment controller. |
| choice.template.json | All ordinary Choice fields; tree example and optional migration settings disabled. |
| choice-band.template.json | Practical moving-band example with explicit geometry and visibility. |
| swarm.template.json | Dense aligned swarm, 256 models per rig at 2 cm/s, including individual-file autopilot settings. |
| bogong-swarm.template.json | Procedural 3D Bogong swarm with volume, alignment, optional Y wrapping, solid circular markers with world or angular size, and a live Konstanz star sky. |
| bogong-night-sky-sequence.template.json | One-hour Bogong sky experiment; copy to sequenceConfig.json to run. See docs/night-sky.md for location/time and image audit settings. |
| kannadi.template.json | Four-animal mirror experiment with every current Kannadi field, including individual-file autopilot settings. |
| kinematic.template.json | Kannadi motion calibration with one tile; use sceneName Kannadi. |
| optomotor.template.json | All current grating fields and their default stimulus values. |
| optomotor-arc.example.json | Frequency/speed sweep from origin/Optomotor. |
| migration.template.json | Choice flight/migration experiment with 100-unit AGL; autopilot is enabled at 10 world units/s by default. |
| migration-agl*.example.json | 10, 100 and 1000 world-unit AGL examples adapted from Bogong/SmrMah; autopilot is enabled by default. |
| sequence-migration.example.json | Runs the three AGL examples. |
| dynamic-choice.template.json | Dynamic Choice design; all fields, including documented legacy no-op aliases. |

Read [configuration-reference.md](../../../docs/configuration-reference.md) for every field, omission defaults, units, supported ranges, precedence, and historical limitations. Read [unity6-controls.md](../../../docs/unity6-controls.md) for controls and [data-formats.md](../../../docs/data-formats.md) before analysis. One world unit is one centimeter in current walking scenes; Unity axes are X right, Y up, Z forward, with positive yaw clockwise viewed from above.
