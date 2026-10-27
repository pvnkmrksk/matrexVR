# Local Mac review — 27 October 2026

This review follows the operator feedback on the first local preview. Work is on `codex/seamless-desktop-experience`, with local commits only. No release, push, or Windows/Linux build was made.

## Open the app

Use `.local/Open MATREX Preview.command`, which opens `.local/MATREX VR.app` with the isolated `.local/Preview Config` profile. Its active recording root is `.local/MATREX VR RunData`; the recordings library displays that exact root. Existing untouched terrain example files in the preview profile were upgraded with backups under `.local/review-20261027/profile-backup`. Existing calibration and apparatus routing are retained. Video recording was enabled for the preview profile's explicitly disabled VR1 setting; omitted supported tracker settings now default to recording video.

The launcher currently opens the saved demo workspace. For LED experiments, choose **Configure → Review saved apparatus**, retain the existing LED profile, then **Load sequence** to choose a terrain treatment. A demo desktop profile deliberately uses one fullscreen view; terrain now honors that choice too.

Initialize tracking before starting an experiment. Close and reopen a previously running tracker when changing video recording so its native recorder receives the new setting. The always-visible recording summary shows the requested recording mode before start.

## Four terrain scenes, sixteen combinations

Load one of the four **Terrain** treatment sequences. Each contains the four distinct scenes: **Rough left, smooth right**, **Smooth left, rough right**, **Both smooth**, and **Both rough**. Each treatment independently chooses the visible and navigation surface. There are four scene assets, with one shared environment prefab, rather than sixteen duplicated worlds.

Edit `Assets/TerrainCueLab/TerrainChoiceWorld.prefab` once for common geometry or target positions, and `TerrainChoiceTarget.prefab` once for both placeholder trees. The two trees are at X = −40/+40 cm, Z = +60 cm, equally distant from the centered animal. The recovered forest-floor texture is included. Motion and terrain-controller defaults are shared in `Examples/Rugosity/terrain-treatment.json`; selected sequence overrides take precedence. The UI distinguishes the shared file from the selected block's effective roles.

The original fullscreen camera bypassed the apparatus and had no pose logger. The standard configured rig now provides LED face routing, dimensions, display selection, tracked clones, and pose logging. The [terrain report](rugosity.md) records 3,193 runtime checks, 64 real sequence trials, and 22,485 playable pose rows. Standalone profiling additionally verifies actual 128×128 LED cameras for one and four rigs.

## Design and interaction review

The dedicated design pass replaces cramped dark controls with a neutral workspace, compact input cards, a restrained teal hierarchy, natural font spacing, clear focus states, and explicit hover/pressed/disabled colors. Runtime help has measured height and separate readable descriptions and JSON facts. The apparatus map keeps square previews and bounded size. Replay controls use the same typography and semantic colors.

The gallery passed **370 checks**, with **34 actual screenshots** and zero errors. The screenshot gallery includes the main pages, expanded settings, recording library, modal dialogs, hover, keyboard focus, disabled controls, and terrain sequences/settings at Full HD and larger backing scales. The selected terrain block's effective overrides are tested without modifying the shared JSON. Screenshots and exact source hashes are retained under `.local/review-20261027/ui`.

## Replay video and performance

The recent operator runs requested no saved tracker video; their missing footage cannot be recovered. Supported bundled trackers now record by default, with explicit false respected. The native HejjéTrac headless layout is also imported correctly. Real Rekkefly generated video and the bundled HejjéTrac sample both passed acquisition-to-replay checks and exact forward/backward visible-frame comparisons. See [camera evidence](replay-camera-validation-20261027.md).

The apparent 60 FPS ceiling was the configured frame cap. On this M2 Pro, the standalone Choice case measured 1,326–1,414 uncapped engine FPS; one terrain LED rig measured 1,591–1,898; four terrain LED rigs measured 443–492. The simple Choice case consumed about 2.51 ms of process CPU per capped frame across all Unity threads. The HUD now labels process CPU work separately from raw presentation wait and explicitly shows the pacing cap. See [the profiling report](performance-20261027.md), including the retained one-second outlier and hardware/background-run limits. Native video encoding was tested separately from those throughput measurements.

The test inputs are generated or previously recorded. Physical camera calibration, animal experiments, displayed LED timing, and acceptance of mouse gestures remain operator checks unless explicitly noted in the final player capture results.

## Final packaged-player verification

`0.3.1-local-review` was built from source commit `54206323` with zero errors and 56 warnings, and its app signature passed verification. The final native setup capture is 3024×1898 with direct framebuffer rendering at 2× UI scale. The app is launched at `.local/MATREX VR.app` with `.local/Preview Config`; run data resolves to `.local/MATREX VR RunData`.

Both native replay screenshots visibly show their saved tracker video together with the animal front view and overview. A separate foreground test of the packaged player, while a generated native tracker recorded MJPEG, held 59.6 FPS at its 60 FPS cap and reached 2,293 FPS uncapped. Every one of its six cameras was 128×128 on display 0, tracking input stayed fresh, and all 1,800 accepted native video frames were written without recording errors. These captures/profiling used `774ebfa2`; the final `54206323` adds only the setup Escape input correction. The full results preserve this distinction.

Native coordinate navigation and numeric text entry worked. The duration field was restored to its original value before leaving; no profile settings were saved by that check. The review also fixed setup Escape being swallowed by the generic text-input guard; 17 checks now exercise the actual runtime gate. F1/Tab injection through the computer-control tool did not establish visible acceptance. The Mac then locked before the final physical keyboard check, so that check remains explicit rather than reported as passed. The app was left launched for operator review.

[Structured final-player evidence](local-player-review-20261027-results.json) · [UI gallery/source hashes](ui-design-review-20261027-results.json). All screenshots and the gallery ZIP remain under `.local/review-20261027`.
