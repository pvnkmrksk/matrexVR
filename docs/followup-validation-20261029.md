# GUI and recording follow-up acceptance — 29 October 2026

The source now keeps feature controls in place, uses typed controls and the
bundled Lucide icons, and coordinates experiment recording with the ordinary
owned HejjéTrac/Rekkefly GUI workers. Preview and native build acceptance are
tracked separately below. No physical camera or LED equivalence is claimed.

| Executed check | Result and scope |
| --- | --- |
| GUI stability | 45 actual UI Toolkit assertions: control identity, geometry, focus, scroll, disabled fields, semantic enums, buffered edits, icons and matrix cells |
| Edit-only rendering | 24 GPU assertions: object/material/sky rendering, import cancellation/reopen, no source mutation, no acquisition scripts or archive writes |
| Existing setup behavior | 28 assertions, including dirty Save and cap controls |
| Actual native recording integration | 58 assertions across four MainController runs with both ordinary owned native GUIs: two normal, canceled preparation, manual pause |
| Native endpoint/transport | 30 actual two-take checks per backend; 20 transport/finalization checks |
| Same-build replay core | 43 assertions, including shader-clock GPU state and archived configuration |
| Actual record/replay round trip | 1,024 assertions; two scenes, all phases, forward/backward seeks, rendered view comparisons and unchanged source hashes; synthetic native-format input |
| Sixteen-rig phases | 96 assertions through actual MainController phase transitions, body visibility, continuity/reset and finalization |
| Appearance and visibility | 104 assertions: 1/2/4/8/16 six-face masks, enable/disable/phase restoration, rendered bodies and panorama orientation against Unity |
| Shared sky in Optomotor | Seven assertions through actual acquisition: solid color, custom panorama, same-image azimuth changes and archived sky images |
| Animation/serialization | 371 assertions, including original locust animation, transitions, imported GLB and read-only replay; performance agent's final safe source |
| Runtime GLB preflight | 22 production format/dependency/decoder/reference checks |
| Logger and writer | 690 archive/writer assertions plus 150 lifecycle cycles; 16 logger-seal assertions |
| Apparatus model | 58 production profile assertions; no physical display claim |

The [GUI evidence](gui-stability-validation-results.json), [native lifecycle
report](native-recording-control-validation-results.json) and [performance
report](population-performance-20261029.md) describe fixture and platform scope.
Detailed local Unity captures and run folders remain under `.local/`; source
configurations and the operator's existing recordings were not used as test
write targets.

## Performance outcome

The supplied 592-animal/96-camera mirror workload improves from about **20 to
30 FPS** on the Apple M2 Pro. The requested 60 FPS is still open. Logging and
world-evidence costs fell substantially; the remaining cost is concentrated in
per-camera native rendering. A shared-pose rendering prototype failed the real
packaged image comparison and was removed. Original renderers, animations,
camera outputs, evidence frequency and source quality settings remain intact.

## Build and remaining acceptance

The local build target is `Preview/MATREX VR.app`. Launch it with the repository's
**Open MATREX VR Preview.command**, which uses the visible `Preview/Config` and
`Preview/RunData` folders. Rebuilding preserves those folders. This is a local
development build, not a published release.

The Mac was locked when the native pointer/keyboard walkthrough was attempted.
Automated UI Toolkit checks do not replace that acceptance. Physical tracker
calibration/camera recording and LED timing, unsupported-platform native
recording control, large-data cache budgets and further many-camera rendering
work remain explicit in the [follow-up checklist](gui-recording-performance-followup.md).
