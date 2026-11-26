# Ant path-angle pilot: source checkout and Ubuntu testing

This branch contains the pilot runtime, procedural ground shader, twelve-condition protocol, adjustable Unity preview and MATREX sequence-picker integration. The historical six-condition reproduction is available in the same picker. macOS executables, generated desktop profiles, downloaded companion applications and recordings are not part of the source branch.

## Open on Ubuntu

1. Clone the `codex/ant-path-angle-pilot` branch (or fetch and switch to it in an existing checkout).
2. Install and activate **Unity 6000.3.16f1**, the version pinned in `ProjectSettings/ProjectVersion.txt`. Add this repository directory as a project in Unity Hub. Let the pinned packages restore.
3. Open `Assets/Scenes/ControlScene.unity` and enter Play mode. Alternatively use **Tools → Experiment setup → Open setup screen (Play)**, which also regenerates the configuration schemas.
4. Use **Configure** to select the actual walking-input backend, tracker host/port, sphere diameter, display mapping and gains for the apparatus. Save/complete the apparatus setup. Loading the pilot preserves these settings.
5. On **Start**, choose **Load sequence → Ant · path-angle pilot → Load sequence**. Review it, then return to **Start** and start the experiment when the rig is ready.

The repository also contains the editor keyboard launcher under **Ant experiments → Start path-angle pilot (keyboard)**. This is useful for a first visual check without a tracker; it explicitly selects the desktop keyboard system. Use the normal MATREX workflow above for the actual apparatus.

The keyboard preview system is marked as a completed desktop setup so that the standalone setup screen keeps the pilot selected and offers **Start experiment**. It remains a keyboard preview, as recorded by `previewPurpose`; this marker does not configure or calibrate an animal apparatus.

Existing tracker/calibration files from the Ubuntu apparatus should remain the reference for the hardware configuration. `Assets/StreamingAssets/Experiments/PathAnglePilot/system.apparatus-template.json` is only a starter: manual controls are disabled, but its localhost port 29951, front-camera display and sphere diameter are example values. It does not contain the Ubuntu rig's calibration. The keyboard preview uses `system.preview.json` and the same example port.

For external tracking, start the tracker already used on the apparatus and point MATREX at its publisher. The local Mac companion bundle was not committed; selecting a sequence does not install Linux tracker applications. See [tracker integration](tracker-integration.md), [source/tracker setup](getting-started.md#source-and-tracker-setup) and [input contract](data-formats.md#zmq-input-contract).

For a standalone Linux executable, add Linux desktop build support to that Unity installation and build the enabled scenes for Linux from Unity's Build Profiles. The local Linux player has been built and checked with synthetic inputs; see the Linux validation below. A Mac `.app` is not needed for source testing and will not run there.

## Protocol and appearance

The pilot repeats randomized complete blocks of these twelve conditions:

| Bars | Path opening angles |
| --- | --- |
| Two black scaling bars | 40°, 55°, 70°, 85°, 100°, 120°, 150°, 180°, 210° |
| No bars | 85° |
| Two black scaling bars, no path | None |
| No bars, no path | None |

Goals remain at radius 60 cm and bearings −20°/+20°. Scaling bars maintain 10° apparent size. Two-bar trials end at historical goal contact or 120 seconds; both no-bars trials last 20 seconds regardless of position and have no goal triggers. The two-bars/no-path control retains the same goal contacts and 120-second timeout. Every no-path trial retains the same grass ground; only the path marking is hidden. Each trial is preceded by a five-second intertrial.

The hard three-hour deadline uses monotonic elapsed time from session start, including scene loading and intertrials. It overrides an ongoing trial or intertrial, independent of Unity time scale. An editor watchdog also enforces it during Play-mode pause. It is checked on the first available update at/after the deadline; measured overshoot is recorded.

The path is a visual marking without colliders. For opening angle A, the fork is at `z = 60 cos(20°) − 60 sin(20°) / tan(A/2)` cm. The 40° condition splits at the origin, 180° makes a T at goal depth, and 210° forks beyond the goals with branches leading back.

Use **Ant experiments → Adjust path-angle pilot appearance** in the Unity editor to inspect every condition from above and from the ant's start pose. Width, darkness, brightness, texture scale, ground height and extent are adjustable. **Save appearance** changes the shared appearance file for the next session. Choosing a preview angle does not change the protocol. The current Diacamma appearance uses width 3 cm, darkness 0.5, a 1 cm edge fade, brightness 1 and a 50 cm grass tile; extent remains 400 cm and ground height −1 cm. See the reproducible extraction below.

All pilot configuration files are in `Assets/StreamingAssets/Experiments/PathAnglePilot/`:

- `design.json`: conditions, triggers, ground angles, deadline and seed. Seed `-1` chooses and records a new seed.
- `appearance.json`: shared appearance, loaded once per session.
- `sequence.json`: the prepared sequence listed in MATREX.
- `system.preview.json`: desktop keyboard preview.
- `system.apparatus-template.json`: apparatus starter settings, to configure for the rig.

For a supplied grass texture, set `appearance.json`'s `texturePath` relative to StreamingAssets. The texture repeats once per `textureScaleCm` and is multiplied by ground brightness. For a prepared ground/path image, set `preparedGroundImage: true`; it covers the whole square, centre at the start, top toward +z, right toward +x. Prepared imagery bypasses procedural painting and brightness. A condition's `pathGround.preparedTexturePath` overrides the shared image. Supplied images must match the intended geometry. Use relative paths when moving between computers; consumed image bytes are archived with the recording.

### Reproduce the Diacamma reference appearance

The shared appearance now uses grass cropped from the supplied `85deg_both.png`,
with a 3 cm rounded path, 0.5 darkness, a 1 cm soft edge fade and ground brightness 1. The existing
procedural shader draws the paths, including 120° and 150°, using the original
goal positions. This appearance recipe changes only visual settings; it preserves the selected protocol.

The 1024 × 1024 crop at `(0, 3072, 1024, 4096)` contains grass only. It repeats
every 50 cm; this approximates a 200 cm square for the full reference. The crop
is not resized or recoloured. Its repeat boundary can be visible; the physical
texture scale is inferred from the reference geometry, not calibrated from an
independent ruler. Inspect it on the apparatus before acquisition.

`pathEdgeSoftnessCm` controls the full fade width around the nominal path edge.
The current 1 cm setting blends over 5 mm on each side of the boundary; the
centre stays dark. Zero restores the previous antialiased edge. Values must be
finite, nonnegative and no larger than `pathWidthCm`. The Unity appearance
window exposes **Edge fade width (cm)**. This applies to procedural paths only;
prepared images retain their own painted edges. Older appearance records omit
the field and retain the previous edge. Softness is archived with the effective
appearance for replay. A rebuilt player is required to support the new field.

The generated tile is included, so using this appearance needs no Python. To
recreate it from the original image, install Pillow and run from the checkout:

```bash
python3 tools/prepare_diacamma_appearance.py "/path/to/Diacamma Paths/85deg_both.png"
```

To also update an existing standalone pilot profile, append
`--install-config "/path/to/Config"`. This updates only the seven appearance
fields and installs the tile/provenance; it preserves local ground extent,
height, protocol and system calibration. The first installation saves
`appearance.before-diacamma.json` beside `appearance.json`; copy that backup
over `appearance.json` to restore the prior appearance. Restart the experiment
to load changes. The local **Ant Path Pilot** desktop launcher uses
`Preview/AntPathPilot/Config`.

`diacamma-grass.provenance.json` records the original checksum, crop, generated
checksum, Pillow version and settings. The script rejects a different source
image. The original PNGs and XCF remain untouched and are not needed at runtime.

## Records and checks on the actual rig

The normal pose recordings and configuration archive are supplemented by `path_pilot_events.jsonl`: seed, block order, condition/angle/fork, trial/intertrial starts and ends, goal side and elapsed session time. Bar timeouts are `no_choice_timeout`; no-bars completions are `duration_complete`. Interrupted trials/intertrials and incomplete final blocks are explicit. Equal-black and no-bars conditions have no accuracy score.

Before animal acquisition, use a labelled setup test to inspect the ground and all angles on the real display, verify walking directions/gains and start resets, and test both goal contacts. Check that the no-bars trial continues across former goal locations. Keep these synthetic test recordings separate from animal data. Physical display appearance, latency and tracking calibration have not been validated by the Mac checks.

Editor recordings default to `Assets/RunData`. Standalone configuration lives in the application's persistent Config folder and existing files are preserved; recordings normally live beside the executable. `--config-dir` and `--data-dir` override these locations. See [run snapshots](run-config-snapshots.md).

## Reproduce the software validation

From a committed checkout, run:

```bash
python3 tools/run_path_pilot_validation.py --unity /path/to/Unity
```

The runner creates a temporary source copy from the selected Git revision, marks it as disposable, runs the runtime, appearance and UI checks there, and copies results into `validation-output/`. It does not change the current checkout's protocols or start the apparatus. It needs an activated Unity installation, package restoration access and a working graphics environment. The output records logs and synthetic recordings.

The [current Linux validation](path-angle-pilot-linux-validation.json) records
70 appearance/rendering/replay checks, 50 runtime checks, and 16 setup-screen
checks, plus a successful Linux player build. It covers all twelve conditions,
synthetic goal contacts, actual 5/20/120-second durations and short deadline
tests. The source fingerprint identifies the tested runtime snapshot.

The historical [recorded macOS validation](path-angle-pilot-validation.json) contains 45 runtime checks, 39 configuration/rendering/texture checks and 16 UI checks. Real 5/20/120-second timings were tested; session-deadline tests used short durations, including paused Unity and zero time scale. A full three-hour soak and Ubuntu hardware validation remain to be done.

The local Mac standalone build initially hit a native Unity Burst compiler crash, then built successfully with `--burst-disable-compilation`. The ant trial code does not use Burst. No equivalent workaround is assumed necessary on Ubuntu.

The local Ant Path Pilot apparatus profile uses only the four side faces,
`RBLF` (right, back, left, front), for VR1–VR4. Top (`U`) and bottom (`D`)
are omitted. With horizontal packing and start column 1, the side faces occupy
columns 1–4 in that order, preserving the original `DRBLFU` wiring. Columns 0
and 5 are unused black slots for bottom and top. Removing those faces without
offsetting the start column shifts every side view left by one panel. The previous system profile is saved locally as
`Preview/AntPathPilot/Config/system_config.before-side-panels-only.json`.

### Multiple Bifurcation appearance variant

A separate local profile, `Preview/AntPathPilot-MultipleBifurcation/Config`,
uses an appearance derived from the supplied Unity 2022.3.58f1
`DiacammaMultipleBifurcation/MultipleBifurcation_Data` build. Start it using
**Ant Path Pilot - Multiple Bifurcation** or
`Preview/Open Ant Path Pilot - Multiple Bifurcation.sh`. It uses the current
twelve-condition player with legacy-style edge support. The original profile
and launcher remain available. The new profile has separate RunData and Control
folders and initially copies the original profile's four-side-panel apparatus
configuration, tracking references and unchanged twelve-condition protocol.

Inspection found `DataBifurcation`, `EarlyBifurcation`, and `LateBifurcation`
textures painted onto 1,000 × 1,000 cm terrains using
`Nature/Terrain/Standard`. The middle-bifurcation stem is about 18 pixels wide
in a 4096-pixel image, or 4.4 cm; its RGB intensity is about 34–36% of the grass.
The initial preset used 4.5 cm width and a 1 cm fade. A closer comparison
normalizing the painted image by its underlying grass refines the half-darkness
width to about **5 cm** and the 10–90% transition to about **1.8 cm**. The
current preset uses **5 cm path width**, **0.65 darkness** and a **3 cm full
smoothstep fade** (about 1.8 cm from 10% to 90%), with **0.3 cm edge
irregularity** and **0.02 relative darkness mottling**. Its clean 1024²
grass crop comes directly from `DataBifurcation` at `(0,3072,1024,4096)` and
repeats every **250 cm**, matching the old terrain texture scale and orientation.
The pilot's existing goal locations, opening angles and trial timings remain
unchanged. This is a texture/scale/contrast adaptation: the old terrain's ambient
lighting is not reproduced by the pilot's unlit ground shader.

The source preset is `Experiments/PathAnglePilot/appearance.multiple-bifurcation.json`.
The preset and its grass tile are included; the original legacy build is needed
only for re-extraction. In a fresh source checkout, select the legacy appearance
by copying the preset over the shared appearance file before starting the pilot:

```bash
cp Assets/StreamingAssets/Experiments/PathAnglePilot/appearance.multiple-bifurcation.json \
  Assets/StreamingAssets/Experiments/PathAnglePilot/appearance.json
```


`multiple-bifurcation.provenance.json` records input checksums, packed texture
identity, crop coordinates, measurements and extraction versions. The included
tile works without extraction dependencies. For exact regeneration, use Python
with `UnityPy==1.25.2` and `Pillow==12.3.0`, then from this checkout:

```bash
python3 tools/prepare_multiple_bifurcation_appearance.py \
  "/path/to/MultipleBifurcation_Data" \
  --base-config "Preview/AntPathPilot/Config" \
  --output-config "Preview/AntPathPilot-MultipleBifurcation/Config"
```

The script rejects different reference files and unrelated existing output
profiles. On first use it copies the base configuration into the separate output
profile; on regeneration it updates only the appearance assets and nine visual
settings, preserving subsequent apparatus configuration changes. It does not
copy recordings or mutate the reference build. On another machine, launch a
current pilot player that supports `pathGround.pathVisible`, edge irregularity
and mottling, with `--config-dir` pointing to the new
profile, and separate `--data-dir` and `--control-dir` directories.

The legacy-match shader adds fixed noise in world centimetres rather than
an animation. `pathEdgeRoughnessCm` controls contour displacement, and
`pathMottling` controls relative darkness variation. Both default to zero for
older configurations and recordings. Irregularity must be finite and between
zero and one quarter of the path width; mottling must be finite and between
zero and one. The Unity preview exposes both controls. These fields are
archived with the effective appearance and restored during replay. The separate
Multiple Bifurcation launcher uses the rebuilt legacy-match player; the normal
Ant Path Pilot launcher retains its separate appearance. Both launchers use
the twelve-condition player after the no-path controls are installed.

The twelve-condition revision adds `PathPilot_blackBlack_noPath` and
`PathPilot_noBars_noPath`. Both set `pathGround.pathVisible: false`; the
`openingAngle` placeholder is ignored visually and trial events record null
angle/fork coordinates with explicit `pathVisible: false`. The flag defaults to
true in older designs and recordings. No-path trials require grass-only imagery,
not a prepared image with a baked path. Visibility is captured and restored in
replay. Both local desktop launchers now use the twelve-condition player with
their own appearance profiles. Their prior protocols and launchers are backed up
locally. Each random complete block contains all twelve conditions once.

Pilot trial and intertrial durations use the monotonic session clock after
visual setup. This prevents ground texture loading within a Unity frame from
shortening the recorded 5/20/120-second durations. The existing strict timing
checks retain their 50 ms clock tolerance.
