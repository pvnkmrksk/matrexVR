# Local Mac review — 27 October 2026

The local review app is `.local/MATREX VR.app`; launch `.local/Open MATREX Preview.command` to use the prepared profile. It opens **four saved HejjéTrac LED rigs**, ports **9871–9874**, and the four-block matched terrain sequence, with tracking/video options visible and the experiment stopped. Recordings for this installation belong in `.local/MATREX VR RunData`. No push, release or other operating-system build was made.

The preview startup JSON was backed up before selecting this apparatus and sequence. Original `Apparatus/system.json`, the social sequence and tracker calibration files were preserved. The shared terrain treatment now explicitly uses `rugosity.textureScale: 4`; omission still means authored scale 1. Both the source example and the prepared preview use 4.

## Terrain and recorded video

Actual Unity checks confirm **1 cm eye clearance**, local height/normal following and the correct walking conversion. The current terrain is a separately generated centimeter scene, rather than the Blender mesh with a mistaken meter conversion. Physical relief was not exaggerated. Texture comparisons at 1/2/4/8 preserve identical pose and geometry; frozen replay restores the recorded texture scale. See [terrain and recording evidence](terrain-tracking-recording-20261027.md).

Seven fresh native recordings passed 161 import/decode/seek assertions; a real terrain run passed another 43 ReplayMediaPanel GPU assertions using consumed-input frame association. Native Mac replay visibly displayed tracker video beside the animal camera. Generated/sample acquisition verifies the software path, not a physical camera's exposure timing. Automatic per-experiment recording Start/Stop remains follow-up work; the pinned trackers already provide control primitives.

## Replay and configuration UI

Replay now has a compact transport/timeline, camera card, disclosed media tools and refined dialogs. Preview resizing enlarges both images and respects the space above the transport. Four-rig selection draws a single label. Full HD and Retina validation passed 48 focused resize/selector assertions, in addition to the earlier 22 UI assertions and 80 source/layout/pointer checks.

Built-in Unity cameras and LineRenderer components provide distinct rig colors, visible markers/halos, recorded recent trails, heading arrows, front-camera frusta and offscreen direction labels. Marker sizing cannot scale saved camera offsets. Clipping adapts to current depth bounds. The 82 camera assertions cover reverse seeking, trial resets, actual close-object rendering and unchanged animal-view pixels. Native rendering confirmed the colors, lines, frusta and edge labels.

Native keyboard acceptance covered Space, forward/reverse arrows, N/M trial changes, camera translation/orbit, setup keyboard help and focus traversal. Native mouse automation repeatedly returned `noWindowsAvailable`, including after the Mac was unlocked. This is retained as a manual acceptance limitation; it is not represented as a completed standalone click/drag test. Actual Unity GUI click tests passed separately.

Choice configurations now offer **Available objects & materials** and exact-name Type/Material pickers. The catalog is generated from registered scene assets during builds; 54 Unity checks cover inventory parity, JSON writes, custom-value preservation and Undo. Open it through **Sequence → Edit config → Available objects & materials**.

## Throughput and remaining pacing work

The d154f1f6 standalone repeat rendered four six-face LED rigs with generated native input, native video encoding and pose saving. It reported 568 FPS uncapped, with no acquisition/recorder failures; all **6,492 accepted video frames** were written. Its short capped average was 59.63 FPS. Subsequent investigation found a boundary bias in that benchmark's averaging, so raw interval distributions and the corrected comparisons take precedence over interpreting that decimal as an exact cadence.

**Evenly paced 60 FPS is not resolved.** Minimal one-cube controls reproduce Unity/macOS pacing issues, and neither legacy VSync/2 nor the supported MetalDisplayLink path established a fix. The full application plus four encoders has additional tails requiring capped workload isolation. See [frame-pacing measurements and next tests](frame-pacing-20261027.md).

The FPS display now excludes startup and samples a consistent engine frame-start clock; it continues to show actual delayed frames and peaks. The complete source compiled against installed Unity APIs and passed 1,393 deterministic production-update assertions. This corrects measurement artifacts without claiming to fix physical presentation timing.

The existing **18:00 Berlin follow-up** now includes controlled pacing isolation before the acknowledged HejjéTrac/Rekkefly recording-control integration. It preserves unpublished upstream work and requires local Mac evidence before any further review. No push is authorized until the user reviews this build.

Machine-readable build identity, evidence paths, final handoff and limitations are recorded in [the review results](replay-recording-mac-review-20261027-results.json). Local images, videos, build logs and profile backups remain under `.local/replay-recording-review-20261027/` and `.local/recording-terrain-review-20261027/`.
