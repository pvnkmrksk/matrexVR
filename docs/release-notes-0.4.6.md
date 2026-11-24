# MATREX VR 0.4.6

**Desert locust · 16 rigs** is a ready-to-load walking sequence. Select it in the sequence picker and choose your calibrated VR1–VR16 system configuration.

- **Blank control** presents an empty desert to each rig.
- **Ordered swarm** gives each rig 64 highly aligned simulated locusts in a private square metre.
- **Kannadi** shows each rig the other 15 tracked locusts, with one hexagonal ring of copies per peer. Each rig hides its own copies.

The sequence presents each condition once in a shuffled 30-second cycle. The rig positions use a jittered 4 × 4 grid. A shared seed matches each rig's heading across conditions within a cycle and draws new headings for the next cycle. All three conditions use the same flat sand and gray sky.

Use **Appearance → Ground** in Kannadi to select sand, grass, or a custom PNG/JPEG texture and adjust X and Y tiling separately. Put reusable images in the active configuration folder's `GroundTextures/` directory. Custom ground images are archived with the run for same-build replay. [Setup and validation](desert-locust-experiment.md)

Desktop packages use Unity 6000.3.16f1. Install Rekkefly or HejjéTrac separately for live tracking. Keep the player that created each recording for same-build replay.
