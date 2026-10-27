# Terrain matrix visual review — 27 October 2026

The surface assignments work, but the current textured stimulus does **not** make the left/right relief reliably obvious. The recovered forest-floor photograph has its own apparent roughness, even on flat geometry, and dominates the small geometric cue. This review verifies mesh selection, camera height/tilt, and rendered pixels; it does not establish a perceptually strong roughness treatment.

## All sixteen screenshots

[Open the full gallery](../.local/terrain-review-20261027/baseline/index.html) · [Download all screenshots and raw frames](../.local/terrain-review-20261027/terrain-16-screenshots-and-raw-frames.zip) · [Exact 128×128 camera matrix](../.local/terrain-review-20261027/baseline/led128-16-contact-sheet.png)

![All sixteen treatment and scene cases](../.local/terrain-review-20261027/baseline/terrain-16-contact-sheet.jpg)

Rows are the four treatments: visible terrain/navigation terrain, visible terrain/navigation flat, visible flat/navigation terrain, and both flat. Columns are rough left/smooth right, smooth left/rough right, both smooth, and both rough.

Each full-size review image contains two genuine animal-camera views, followed by separately labelled clay diagnostic views of the visible and collision geometry and a measured height cross-section. The clay pass uses neutral material and common low-angle lighting to expose the geometry; it is **not** the experiment's appearance. Geometry scale and height remain unchanged. Materials, lighting, viewport, and render target are restored after capture. Original stimulus PNGs are retained separately.

The authored `VR1 / Main Camera F` uses a 90° field of view, near clip 0.01, and far clip 100000. The same actual camera is rendered at 768×768 for review and at 128×128 for the LED case, with identical projection verified. Probes use fixed X/Z positions (−53.125/+53.125, 28.125 cm) and fixed inward headings (+55/−55°) across every treatment. The actual `RugosityRig.Conform` supplies eye height and tilt from the selected navigation surface.

There are **6,241 checks, 16 review screenshots, and 128 raw camera PNGs**, including 32 exact 128×128 frames. The additional debug and low-resolution passes leave all earlier raw stimulus frames hash-identical. The captures use isolated Unity Editor rendering rather than a native all-sixteen-trial framebuffer run.

## What the pictures establish

The shared mesh family has maximum rough extrusion 0.8 cm and smooth extrusion 0.025 cm, with a 1 cm camera clearance. The start position is X=0, inside the blended center seam. At the left steep probe in the matched rough-left/smooth-right case, ground height is 0.3839 cm and camera tilt is 10.34°. At its right smooth probe, ground height is about 0.0064 cm and tilt about 0.20°. Flat navigation instead gives zero ground height and tilt while retaining the selected visible mesh.

Fixed world views contain **five** unique visible states: four spatial patterns plus Flat. Paired animal views contain **thirteen** unique states at both capture resolutions. The four both-flat trials intentionally use Flat for both visible and navigation geometry; they must have identical stimuli. Scene labels alone should not introduce a visual cue. Changing navigation while retaining the same visible surface also must leave a fixed world view unchanged, although the conformed animal-camera view can change.

The geometry and independent navigation roles are therefore functioning. The normal textured screenshots support the operator's concern about weak visual contrast. No trial mesh amplitude, clearance, material, or lighting was changed to manufacture a more distinct screenshot. Stronger relief or a different texture would be an explicit stimulus change.

## Incompatible legacy scene pairing

The open operator setup paired `Rugosity_RS` with `Examples/Rugosity/visual-flat_navigation-terrain.json`. That current config references `RoughSmooth`, while the stock legacy scene registers only `RS` and `Flat`. Setup validation now identifies the incompatible role and block, disables Start, and recommends `TerrainCueLab` or a `TerrainChoice` scene. Compatible legacy configurations and custom scene extensions remain accepted. Separate tracking initialization remains available for hardware preflight.

The compatibility change passed 82 focused production checks and four wiring checks. A native player capture visibly confirms the disabled Start button and actionable validation message before experiment or tracker launch. This mismatch is separate from the weak visual contrast in otherwise valid new terrain runs.

## Restored run diagnostics

The compact bottom-right panel shows the active scene, trial cycle, execution step/count, phase, elapsed/total/remaining time, terrain visual/navigation roles, and each rig's backend, live host/port, input freshness, and closed-loop state. More details retains advanced diagnostics. Live listener endpoints take precedence over stale configured values. The panel avoids calibrated LED rectangles.

The preview's root and saved apparatus profiles explicitly disabled the panel. Only `statusOverlay.visible` was enabled in those two files, with originals backed up under `.local/terrain-review-20261027/profile-before-debug-status`. Calibration, routing, and sequence contents were preserved. Other profiles' explicit false settings remain respected.

The HUD passed 25 focused checks and 190 existing overlay checks across seven layouts, with zero changed LED pixels. [The real Mac-player capture](../.local/terrain-review-20261027/native-fixtures/live-hud/native.png) also shows all four live endpoints (9871–9874), timing and terrain roles during a bounded terrain run. That run used disposable configs and no tracker application or physical camera input; the waiting/no-pose indicators are expected.

## Local build and handoff

The signed Mac app at `.local/MATREX VR.app` is `0.3.1-local-review`, built from `f276f818` with zero errors and 55 warnings. Native captures use a 3024×1898 framebuffer; the separate Full HD HUD layout test uses 1920×1080. The app remains open on Ready to run with the saved four-rig HejjéTrac LED setup, 128×128 faces, and the matched-relief sequence's four 30-second blocks. Diagnostics are enabled; tracking and the experiment are stopped. The existing launcher's startup selection is unchanged.

[Structured evidence](terrain-matrix-visual-review-20261027-results.json) · [HUD validation](runtime-status-hud-20261027-results.json). All work is local; nothing was pushed or released.
