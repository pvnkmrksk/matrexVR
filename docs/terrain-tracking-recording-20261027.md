# Terrain, recording and local Mac measurements — 27 October 2026

The current centimeter terrain has the expected **1 cm camera clearance**, and production walking conversion is correct: one sphere radian with diameter 2.6 cm moves 1.3 scene units (1.3 cm). Terrain examples omit `walkingPositionGain` and inherit 1. Copying a meter-world gain of .01 into this centimeter scene would make translation 100× too slow.

The Blender source `/Users/pavan/Downloads/nav_rug_rr.blend` declares meters (`METRIC`, scale 1). Its baked mesh spans about 6.800 × 5.661 × 0.389 m and one UV tile. The current terrain is separately generated 200 × 200 cm geometry, with rough amplitude 0.8 cm and smooth amplitude 0.025 cm. It is not that original mesh scaled incorrectly. We preserved its physical heights: enlarging relief without raising a 1 cm flat-navigation eye can move the camera inside the visible terrain in a mismatch condition.

`rugosity.textureScale` now changes albedo repetition independently of visibility, navigation or eye height. Omission retains authored 1; the shared terrain treatment example explicitly uses 4. A fixed steep point rendered at 1/2/4/8 has identical position/tilt and exact 1 cm clearance. Restoring 1 restores the original pixels byte for byte. Frozen replay respects saved 4 and restores authored 1 for old files, including when `visualSurface` is inherited. **50 Unity assertions passed**, plus **168 terrain configuration checks**. Shared materials are neither modified nor instantiated.

The comparison images are in `.local/recording-terrain-review-20261027/texture-scale/comparison.png` and `comparison-led128.png`; raw actual camera renders and checksummed observations are alongside them. The latter uses native 128 × 128 frames.

## Standalone terrain measurements

Independent native Rekkefly processes generated 640 × 480 tracking at about 59.9 Hz, with native video encoding enabled or disabled. The player rendered six configured 128 × 128 LED cameras per rig and saved pose data. Each phase ran 4 seconds after 2 seconds settling. Every phase reported focus=true; the operator preview and desktop apps remained open. The build was **f276f818**, Unity 6000.3.16f1, before the new texture multiplier and replay redesign.

| Case | Capped60FPS | Uncapped, all active | Tracking listener off | Pose logging off | Capped60FPS repeated |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1-rig-video-off | 59.61 | 1119.72 | 1031.66 | 1165.95 | 59.64 |
| 1-rig-video-on | 59.60 | 1135.99 | 1043.08 | 1159.45 | 59.36 |
| 4-rig-video-off | 59.58 | 575.20 | 558.10 | 583.30 | 59.51 |
| 4-rig-video-on | 59.59 | 588.97 | 576.20 | 585.69 | 59.48 |

Native video did not produce a measurable throughput problem in these short comparisons. The runs overlap normal variation; encoding does not improve performance. Four rigs stayed near 60 with video on and reached 589 engineFPS uncapped. Turning off tracking listeners or pose logging did not reveal a large hidden tracking bottleneck. These are software measurements, not physical LED/exposure timing or a long-duration throughput guarantee.

At capped 60, average GPU time was 0.63–2.03 ms. The Unity process CPU cost was 7.67–12.59 ms summed across its threads per frame. Its pacing counters are not idle-capacity counters: a small `WaitForTargetFPS` value alone does not imply the GPU/main thread consumed the frame budget. Raw frame distributions, pacing counters, native counters, and observed layouts are retained in `performance/summary.json` and per-case `results.json`. GC allocation counters were unavailable and are reported as such. All twenty phases passed camera/tracking assertions; native UDP/ZMQ drops were 0.

## Native recording through archive and replay

Five fresh native Rekkefly clips saved **11,984 accepted frames**, all finalized and written, with no recorder error. The pinned bundled HJ backend also acknowledged two `record_start`/`record_stop` cycles: frames 0–44 and 50–89 were saved, with five intervening frames intentionally unrecorded. This used HJ's attributed prerecorded sample, without changing its source or existing recordings.

**161 assertions** imported all seven clips, checked completed receipts, encoded frame counts, native-media checksums, Unity decoding, and exact frame 0/mid/last/mid/0 pixel restoration. Those import-only fixtures explicitly use approximate operator-offset association. A separate **43-assertion** replay of the freshly acquired terrain pose CSV uses its actual consumed-input frame mapping. The production ReplayMediaPanel GPU texture matched the expected 640 × 480 pixels when scrubbing 303→952→2286→952→303, with God's-eye and animal preview active. Runtime errors were empty; original native files remained unchanged.

The batch Editor did not publish a complete GameView screenshot, so the final native replay GUI is inspected separately. The retained fresh-panel Editor log contains a `UnityEditor.Search.SearchDatabase` startup exception before Play; it did not recur as a runtime error and did not prevent the 43 checks.

The native trackers already expose record-control primitives. Unity still needs acknowledged per-run control of its detached tracker processes, independent manual recording override and recorder state reconciliation. This verification deliberately does not modify HJ/Rekkefly upstream; that integration is a follow-up after their other-device changes are available.

All machine-readable evidence, source hashes and exact artifact paths are recorded in [terrain-tracking-recording-20261027-results.json](terrain-tracking-recording-20261027-results.json). Local media and logs are under `.local/recording-terrain-review-20261027/`; they are not part of the committed source.
