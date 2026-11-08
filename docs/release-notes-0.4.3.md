# MATREX VR 0.4.3 for Linux x86-64

- **Kannadi · entrainment then swarm** starts with 120 seconds of ordered movement, followed by a 300-second grid swarm with evenly spaced initial headings. Both phases place 64 locusts per rig in a 1 m × 1 m spawn area with a matching 1 m × 1 m periodic arena. The tracked animal retains its pose at the phase change. These are starter settings, and existing saved configurations keep their values.
- **Save tracker video** and **Skip tracker video** set the experiment-wide permission for automatic camera recording. Per-rig **Save tracker video** and **Recorder control** still apply. Compatible native endpoints receive Start and ownership-fenced Stop. The stock Linux HejjéTrac recorder does not expose that fenced endpoint and uses Manual control. Animal poses, virtual swarm settings, and tracking continue independently of the video switch.
- The update includes worker population capture, the fix for half-second scans by the runtime status overlay, compact swarm controls, model texture selection and replay, and acquisition-only fullscreen resolution.

[Setup and controls](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.3/docs/configuration-ui.md) · [Recording control](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.3/docs/native-recording-control.md) · [Performance history](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.3/docs/population-worker-performance.md)

The Linux archive lists the pinned companion packages. The bundled HejjéTrac 0.5.0 requires glibc 2.35 or newer. Keep the complete run and the creating player for replay.

## Frame pacing at the requested density

On Linux with Vulkan, 16 rigs each showed 64 locusts through four 128 × 128 faces. A controlled 65-second entrainment comparison with full VR recording measured 59.96 FPS, a 16.67 ms median, a 17.70 ms p99, and a 25.22 ms maximum with the software cap. The display-synced comparison reached 60.00 FPS with a 19.61 ms p99 and 22 intervals over 33.33 ms. The default stays at a 60 FPS software cap. The software phase had zero frames over 33.33 ms and zero population recorder drops. These are software frame intervals on a single host with synthetic input. [Frame and CPU series](https://github.com/pvnkmrksk/matrexVR/blob/v0.4.3/docs/pacing-0.4.3-results.json)

The 70-second candidate retests kept full VR recording enabled. Entrainment measured 59.95 FPS and a 17.58 ms p99. The grid swarm measured 59.92 FPS and a 17.42 ms p99. Both had zero intervals above 20 ms and zero population drops. The Linux HejjéTrac payload is the checksum-verified upstream v0.5.0 archive. [HejjéTrac release](https://github.com/pvnkmrksk/hejjetrac/releases/tag/v0.5.0)

## Independent HejjéTrac recording

The experiment-owned recording protocol is now maintained in HejjéTrac itself.
Install the compatible tracker from the upstream project. The archived Linux
0.4.3 MATREX package retains its original HejjéTrac 0.5.0 payload.
[HejjéTrac releases](https://github.com/pvnkmrksk/hejjetrac/releases) · [Original sample validation](hejjetrac-linux-recording-validation.json)
