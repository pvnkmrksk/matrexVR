# Historical Adaminaby sky preset

The Adaminaby experiment was added on `BogongAustralia` in commit `ef8a68b2ad2ff690e803007511666296a31eda5a` (also present as `d9f9810bb5eb9da31392dc2017ce82f5692b2bb5`). That commit contains an image and a Choice configuration, not a separate Adaminaby Unity scene. Both files are restored here:

- `Assets/StreamingAssets/Photosphere/adaminaby.png`: original image bytes, Git blob `92d7a4e0d0cfcd3bd297bf56d62905aee59aa3d3`. The image's metadata GUID is restored from `f36f318c88186f646f61e8fdb9bbea9fd03ab0f8`.
- `Assets/StreamingAssets/choice__SM_adaminaby.json`: original settings, with the trailing comma removed for valid JSON. It has no spawned objects, enables closed-loop orientation and position, starts at `(0, 10.7, 0)`, and randomizes the initial yaw.

## Run from ControlScene

1. Save your current `Assets/StreamingAssets/sequenceConfig.json` if you want to return to it later.
2. Copy `Assets/StreamingAssets/Examples/Sequences/adaminaby-historical.example.json` over `Assets/StreamingAssets/sequenceConfig.json`.
3. Use the local system configuration for your rig. To enable Front, Back, Left, Right, Up and Down, use the [six-camera setup](six-camera-rig.md).
4. Open `Assets/Scenes/ControlScene.unity` and press Play. The template automatically starts the current `Choice` scene for one hour. Escape returns to ControlScene. Edit `duration` or set `autoStart` to `false` as needed.

The new sequence is an adaptation for the current scene and loader. It does not depend on the old `Choice_Bogong` terrain scene and its missing terrain assets. Adding this preset does not change the active automatic Bogong preview sequence.

Choice applies the selected skybox to every rig face, including Up cameras with legacy local skybox overrides. Clearing the skybox or destroying the Choice controller restores the previous camera clear flags, local overrides and scene material. Loading an image does not change which faces the system config enables.

## Source image limitations

The historical image is a **3024 × 1890 Stellarium screenshot**, including its toolbar and status text. The status text says **Canberra**, **2025-12-08 23:30:28 UTC+11:00**, and **180° FOV**. Its filename is Adaminaby, but it is not a calibrated Adaminaby panorama or a 2:1 equirectangular export. The existing Choice loader maps the whole screenshot onto Unity's panoramic skybox, so the UI and projection distortion remain visible.

This is a fixed historical image: it does not follow the current date, time or location, regenerate every 30 minutes, or create the astronomical skybox archive. For the current Adaminaby sky, exported panoramas and load-time records, use the [Bogong astronomical skybox setup](night-sky.md).

## Validation

Run `AdaminabyValidation.Run` through Unity 6000.3.16f1 with graphics enabled on a disposable project copy containing the marker file `KANNADI_VALIDATION_COPY`. The check installs this sequence and the six-camera system template into that copy, starts ControlScene in Play mode, verifies the loaded image pixels, renders all 24 camera views, and checks restoration after repeated loads, an empty skybox and controller destruction. It also covers an inactive face and a camera with a solid-color background. Results and a six-face contact sheet are written to the copy's project root.

The restoration passed 78 checks with all 24 views rendered; see [validation results](adaminaby-validation-results.json).
