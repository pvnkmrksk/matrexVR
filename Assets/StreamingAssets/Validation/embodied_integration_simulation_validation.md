# Embodied integration simulated-controller validation

Generated: 2026-07-30

Result: **PASS** for deterministic protocol logic.

- PASS — fixed goal geometry, 3.5-cm radial contact, and first-crossing trigger
- PASS — all eleven cue transitions and P/H target equations
- PASS — four asynchronous VR queues, timeout/failure reinsertion, nonchoice consumption
- PASS — eleven-cell renewal through block ten and symmetric pseudo-side pair balance
- PASS — 5-s ITI, 20-s write/read, 3-h stop, and one-frame start budget
- PASS — intertrial stimulus matches the reference skybox plus `glassplane` floor
- PASS — attempt sensor fields serialize as finite numeric arrays

## Required hardware/render validation before animal 1

- Freeze `rotYPlusZDegrees`, `yawSign`, the sensor-axis mapping, and measured latency; replace the pending calibration ID in the resolved design.
- Capture all eleven pre/post rendered transitions on each of four VRs and verify no partial frame.
- Replay asynchronous sensor streams and verify the transaction-spanning delta is logged but never applied.
- Verify achieved-state tolerances at the actual display refresh rate and test explicit neutral stop behavior.
- Record the display color-space setting and code commit in the frozen animal-1 archive.
