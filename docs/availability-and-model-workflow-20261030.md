# Startup availability, simpler controls and portable models

This pass follows the operator feedback on 30 October 2026. It supersedes the
earlier policy that a requested tracker recorder could block VR acquisition.

## Startup behavior

VR starts when native tracking or camera recording is missing, failed or
unsupported. Available recorders are attempted independently with bounded waits;
unavailable rigs produce a compact warning and remain explicitly unacknowledged.
The run preserves the warnings and recorder report. Missing footage is never
described as a successful camera recording. An actual take still needs its exact
closure receipt before its files can be archived as complete.

Two other blockers were fixed: system tracker calibration paths were being
classified as required experiment files, and the preview HUD reread the system
file with the wrong archive role. Native calibration remains optional provenance;
the selected experiment, sky and model files remain required experiment evidence.

The startup clear camera is now disabled before its one manual render. It
preserves the prior render target and no longer issues an unrelated `GL.Clear`
that could erase a preview texture or contribute another black camera pass.

## Controls

Settings are organized into **Basics**, **Appearance**, **Motion** and
**Advanced**. Common controls stay visible; placement, surface details, camera
routing and calibration use explicit disclosures. The object card puts the
model, GLB path, color override and rendered preview together. Icons retain
short labels or tooltips. Save/Start remain visible at the tested window sizes.

Native tracker failure is a warning at the GUI preparation stage as well as the
runtime recording stage. VR startup does not depend on opening a native window.

## Portable GLB examples

The real bundled locust was exported with the pinned UnityGLTF exporter to
`Assets/StreamingAssets/Models/Examples/SimulatedLocust.glb`. It retains a skin,
embedded textures and the `Scene` animation. Source dependencies and the exact
export hash are recorded beside it.

Two unchanged external examples come from the pinned Khronos sample collection:
[`RiggedSimple`](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/8f492390ddc7a2ae221a1369ef560589cf8c70fb/Models/RiggedSimple)
(Cesium, CC BY 4.0) and
[`AnimatedMorphCube`](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/8f492390ddc7a2ae221a1369ef560589cf8c70fb/Models/AnimatedMorphCube)
(Microsoft credit, CC0). Their sources, licenses and SHA-256 hashes are included.

Load `Examples/Sequences/custom-models.sequence.json` for all three examples,
or `Examples/Sequences/kannadi-glb.sequence.json` for external social animals.
Replace a GLB path, choose a color and adjust Placement. A file can live under
the writable `Models/` folder or at an absolute path. No Unity rebuild is needed.

Appearance overrides clone every material slot per instance. They can retain
base-color textures for tinting or remove them for a uniform chosen color.
Metallic and smoothness remain under Surface details. Disabling the override
restores the original materials. Original model files and shared source
materials remain unchanged; acquisition archives the consumed GLB bytes.
When a GLB is selected, retained built-in type/material settings are inactive in
both the preview and the experiment. They become usable again when the GLB path
is cleared. Body appearance uses the same override in live Kannadi/generated
populations and in replay, including backward seeks.

## Evidence and limits

- [Actual startup failure cases and render-target checks](startup-availability-validation-20261030.json).
- [Best-effort native recording policy and actual native-video runs](native-recording-best-effort-validation-results.json).
- [Actual three-model experiment sequence](glb-example-sequence-validation-20261030.json).
- [Runtime imports, GPU colors and animation restoration](model-import-validation-20261030.json).
- [Population appearance continuity and actual replay](population-appearance-validation-20261030.json).
- [Responsive GUI and actual GUI startup](responsive-gui-validation-20261030.json).

| Current acceptance | Checks |
| --- | ---: |
| Responsive rendered screens at three practical window sizes | 165 |
| GUI Start continuing after native preparation failures | 12 |
| Existing stable controls / setup / primary VR counts | 45 / 28 / 95 |
| Isolated edit-time GPU previews | 26 |
| Actual Main startup failures and render-target protection | 50 |
| Actual native GUI recording, missing peer, cancellation and override | 76 |
| Recorder manager identity and independent preparation | 140 |
| Three real GLB runtime imports, colors and animation restoration | 46 |
| Actual three-model Main experiment and byte-identical archives | 46 |
| Population colors, continuity and real forward/reverse replay | 225 |
| Same-build replay core / original animation / sixteen-rig phases | 43 / 371 / 96 |
| Final combined rendered record/replay round trip | 1,028 |

Validation uses isolated profiles, test footage and synthetic input. Native OS
pointer delivery and physical camera/display acceptance remain separate. This
pass does not claim to resolve the dense many-camera 60 FPS limit documented in
the prior performance report.

## Final local preview

`Preview/MATREX VR.app` is rebuilt as **0.5.1-dev** from `253cdda8`, with recorded player build GUID `ac80604a332e44b7b5e254b76eaf6e14`. Signature verification passed. The actual standalone app passed 30 checks while its configured tracker executable/calibration were deliberately missing: VR started with an honest unavailable warning, executed all three models and their original animation clips, finalized the ZIP, and replayed it without changing source files. Only the owned test player was closed.

All 1,449 existing preview config files remain byte-identical; 16 new model/example/notice files were added without overwriting them. Relaunch through **Open MATREX VR Preview.command**, then load `Examples/Sequences/custom-models.sequence.json` and open Appearance. The new examples are also directly visible in `Preview/Config/Models/Examples`.

[Final machine-readable acceptance](availability-models-final-validation-20261030.json) records the exact build, scopes and remaining limits.
