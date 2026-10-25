# Hardware and calibration

The system configuration maps software rigs to tracker endpoints and display rectangles. It does not calibrate a camera, measure ball diameter, infer an LED wiring order or certify scanout timing.

## What the software needs

| Item | Configuration / responsibility |
| --- | --- |
| Real animal label/species | Session Animal ID and per-rig speciesName; does not choose a tracker mode automatically |
| Tracker source | Address, port, effective motion mode and documented input units |
| Ball calibration | Actual sphere diameter and publisher convention for cumulative displacement |
| Display output | Physical target display, pixel resolution, panel dimensions and offsets |
| Camera-face arrangement | Valid displayOrder matching the real surrounding panel order |
| Scene scale | Centimetres for walking/rugosity, metres for supported Selwyn/migration terrain |
| Timing | Workstation frame pacing, tracker acquisition timestamps and physical LED scanout measurements |
| Storage | Writable recording root with capacity for population/sky/config evidence |

## Before animal collection

Confirm rig identities individually so one animal cannot receive another's tracking or virtual population. Verify face order, panel location and camera orientation using an identifiable test stimulus on the real output. Check Up/Down faces when using a six-face layout.

Compare one known measured displacement/yaw against the rendered world with explicit gain and reset settings. Test missing/stale inputs and operator overrides under the same mode that will be used for acquisition. Record the physical north alignment for astronomical experiments.

Frame-rate targets and frame-time dashboards report software timing. They cannot alone establish LED scanout phase, camera-to-display latency, stimulus luminance or projector/panel calibration. Retain physical synchronization/calibration records with the experiment.

## Multi-rig planning

The number of configured software animals is bounded by available independent stimulus layers and workstation rendering/recording capacity. More configured rigs do not create tracker fanout or physical video outputs. External tracker fanout, display driving and network topology must be set explicitly.

The [integration guide](integrations.md) links the tracker projects. [Viewports](viewports.md) gives the exact pixel mapping. Use a representative population/sky load when checking performance; a blank Choice scene is not a substitute for a dense moth/terrain experiment.

## Hardware documentation scope

This manual documents the current software contract and the apparatus-dependent checks that follow from it. Rig-specific wiring drawings, parts lists, measured scanout/latency results and physical calibration procedures must come from the actual apparatus. They are not inferred from an example system_config.json. Those hardware records can be added alongside this manual as they are measured.
