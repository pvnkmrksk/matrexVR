# GUI, native recording and population performance follow-up

This is the implementation and acceptance checklist for the 29 October 2026
operator feedback. It extends [the overhaul requirements](overhaul-0.5-plan.md).
The priorities below are ordered; unchecked acceptance is not a released claim.

## P0 — stable controls and acknowledged recording

- [x] Keep control geometry, focus and scroll stable when changing a value or
  toggling a feature. Retain inactive fields in place and gray them out.
- [x] Use the bundled Lucide icon family with readable labels/tooltips. Use
  checkboxes for booleans, including legacy numeric flags; use typed enum,
  number, color and file controls for the corresponding data.
- [x] Enable Save only for actual changes, including pending text buffers;
  commit those buffers before switching options or starting acquisition.
- [x] Start the native recorder before the stimulus clock. Require an owned,
  capable endpoint and proof of the first frame in a fresh take directory.
- [x] Stop the stimulus immediately; finish the matching native take, drain its
  writers and verify its completion receipt before archiving the run. Show
  preparation/finalization progress and meaningful failures without reflow.
- [x] Handle cancellation during asset loading/native startup, repeated
  Start/Stop, native manual overrides and application quit. Keep independent
  tracker/calibration windows usable. Manual recording is an explicit mode.
- [x] Validate HejjéTrac and Rekkefly production GUI workers with synthetic
  footage, plus the combined MATREX lifecycle. Physical camera acceptance remains
  a separate apparatus check.

## P1 — measured performance without changing the experiment

- [x] Profile the supplied dense Kannadi setup on the Mac, recording camera,
  clone, animation, logging, CPU/GPU timing and draw-call counts.
- [x] Remove repeated scene/bone traversals and avoid serialization/render-clock
  allocations; move population compression/CSV writes off the render thread.
- [x] Evaluate shared rendering only where full local bone poses, material,
  layer and rendering configuration match. Retain native rendering otherwise.
  Preserve original animation, evidence, shadows, visibility and replay.
- [x] Recheck production player pixels/geometry and repeat the benchmark with
  unchanged source quality settings. Report results separately for 1/4/16 rigs
  and mirrored versus independently animated populations. The target is 60 FPS;
  a measured improvement alone does not establish that target.

- [ ] Sustain 60 FPS in the supplied dense 16-rig workload. Safe changes achieve about 30 FPS; the remaining many-camera render cost requires a separately validated rendering change.

## P2 — deferred items that fit this pass

- [x] Render object and sky previews only while editing. Bound their memory,
  cancel obsolete imports and never activate acquisition behaviours or write
  recording evidence from a preview.
- [x] Add optional panorama azimuth rotation, default zero, bounded 0–360°.
  Match Unity's panoramic sky orientation in actual GPU checks.
- [x] Add an optional 16×16 observer/target visibility matrix. Retain the
  standard self-hidden/peers-visible default when disabled; preserve all
  non-population camera layers and replay the archived matrix.
- [x] Publish a precise runtime GLB support matrix separating verified behavior
  from importer capability. Reject required decoders absent from this build.
- [ ] Complete native pointer/keyboard checks at 1/2/4/8/16 and package a visible local preview. Automated GUI coverage is recorded separately.

## Remaining acceptance and larger work

These require their own evidence before being marked complete: persistent
large-recording indexes and video-cache budgets; broader MCP-client and screen
reader acceptance; native monitor orientation/calibration across platforms;
physical tracking, recording and LED timing; independently animated extreme
population performance; and broader visual redesign after controls stabilize.
They remain in the overhaul backlog and are not hidden behind this checklist.

## Ownership and order

The GUI agent owns control stability, icons and isolated rendered previews.
The native-recording agent owns both tracker bridges, receipts, packaging and
their tests. The performance agent owns profiling and fidelity-guarded rendering
and logging changes. The primary agent integrates lifecycle changes, appearance
and visibility, documentation, replay regression checks and the local build.
Measured GPU windows are serialized to avoid invalid performance comparisons.

## Evidence

- [Stable GUI and previews](gui-stability-validation-results.json)
- [Native recording lifecycle and software acceptance](native-recording-control.md)
- [Population measurements, fidelity gate and remaining 60 FPS gap](population-performance-20261029.md)
- [Runtime model contract and appearance controls](custom-models-and-rigs.md)

Physical camera/LED acceptance and untested platforms remain pending. The local build and final combined regression evidence are recorded in the follow-up acceptance report.
