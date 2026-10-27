# Ordered swarm and social coupling

Load either sequence in the configuration workspace:

- `Examples/Sequences/swarm-entrainment-social.sequence.json`: 120 seconds in an ordered, observer-centered periodic swarm, then 300 seconds of Kannadi social coupling.
- `Examples/Sequences/social-coupling-control.sequence.json`: the same 300-second social presentation without the preceding swarm.

The first sequence uses `Examples/Swarm/ordered-entrainment.example.json`. It generates 256 moving locusts per observer, with a fixed northward mean direction and Von Mises concentration 100. The 4 m wide periodic area follows that observer's position, so walking cannot take the observer out of the swarm. Wrapping preserves overshoot even after large movements. These walking scenes use 100 scene units per metre; the example's speed 2 is **2 scene units/s**, matching the existing walking swarm's speed. Change `agentSpeed` to set a different stimulus speed.

Both sequences reference **one** `Examples/Kannadi/social-coupling.example.json`. Change `numberOfRings` there to control additional clonal copies in both sequences: **0** means one center representation of each tracked animal and no extra copies; **1** means a center plus six copies; **2** means nineteen representations. Every animal sees the other tracked animals and their copies, while its own layer is hidden. Real interaction requires at least two independently tracked rigs. With one configured rig and zero rings there are no peers to see. These files never invent independent animals from a single tracker.

The social config includes starting positions for VR1–VR4. Configure the corresponding rig count and independent input ports under Apparatus before running; unused VR entries do not create trackers. `watchIndex` is intentionally omitted, enabling all-to-all peer visibility. The positions, arena size, durations, swarm count and copy count remain ordinary editable JSON fields.

Every presentation resets each rig to an independently sampled cardinal heading: 0°, 90°, 180° or 270°. Repeated headings are possible. This is selected with `randomInitialRotation: true` and `initialHeadingDistribution: "Cardinal"`; omitted distribution retains the historical continuous yaw sampler. The Swarm → Kannadi transition creates a new presentation and resets its visual heading. This example does not claim to carry a virtual yaw through a scene reload. It also does not infer biological polarization: use the saved independent rig trajectories/headings to measure it.

The existing `Swarm` scene is the generated/recorded population runtime used by this recipe and current playback examples. It is reused rather than duplicating a legacy swarm scene. Earlier root `Kannadi/` recipes are preserved for archival compatibility; this experiment uses only the two explicit current example configs above.

`SocialSwarmValidation.Run` is a guarded Unity validation for a disposable project. It tests actual four-rig Swarm → Kannadi loading, cardinal yaw resets, ordered motion, moving periodic boundaries, all-to-all camera layer masks and changing the one ring-count setting. It disables live inputs and makes no hardware claim. `tools/test_config_compatibility.py` exercises the production heading parser/sampler and legacy defaults offline.
