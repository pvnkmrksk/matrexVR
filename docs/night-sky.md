# Bogong astronomical skyboxes

Swarm (including Bogong), Kannadi and Choice can generate a night-sky stimulus inside Unity. NASA's existing 4096 × 2048 **Deep Star Maps 2020** panorama supplies the stars and Milky Way. The C# **Astronomy Engine** library handles observer orientation, Earth's rotation, precession and nutation. A small GPU shader resamples the panorama. Both dependencies are bundled: no runtime internet, Stellarium process, Python, or pre-generated calendar is required.

## Run and configure

The current active sequence runs **Choice_Selwyn**, started manually from Control, with `Examples/Flight/selwyn-stars-left-swarm.example.json`: four minutes of stars then four minutes of uniform gray and a leftward dorsal swarm. See [experiment phases](experiment-phases.md). The original `Kannadi/selwyn-night-sky.json` remains unchanged. The complete starting files are `Templates/system_config.template.json`, `Templates/experiment.template.json` and `Templates/sequence.template.json`.

The earlier **Adaminaby sky and aligned Bogong swarm preview** is preserved as a recipe. Settings are in `Assets/StreamingAssets/Kannadi/adaminaby-night-sky.json`: approximately **35.996113° S, 148.773895° E**, `utcOffsetHours: 11` (fixed UTC+11). `localTime: "now"` captures the current instant each run; no date/time is hardcoded. The first image uses the exact startup instant and subsequent images advance in 30-minute increments from that instant. The preview has **256 solid Bogong circular patches per rig**, in a **120 × 60 × 120 world-unit volume**, moving at **2 units/s** toward heading **60°** with **5° upward pitch** (`mu: 60`, `muElevation: -5`). Both concentrations are 10000, so headings stay aligned. The volume wraps at its edges. The active markers use a **2° angular diameter**, solid RGB `(1, 0.5, 0.5)`, and have **flicker disabled**. Both angular and world-size modes are unlit, flat circles; their orientation does not change movement heading. Autopilot and tracker-driven camera movement remain disabled. The six-face layout uses `DRBLFU`; choose `Templates/system_config.template.json` for Kinefly flight. Keyboard controls still apply. North is Unity +Z, east +X.

In the Editor, exports are in `Assets/RunData/<run timestamp>/Skyboxes/`. Generated star panoramas use the configured width (normally **4096 × 2048**); uniform-color skies are **2 × 1**. `loads.csv` records the actual load time and represented sky time in UTC and the configured local time (including its UTC offset), the previous/new skybox IDs, and image filename. `usage.jsonl` contains the same activation details plus the resolved start and scene. The computer clock supplies the instant; the configured offset determines the local timestamp. A fixed offset does not change with daylight saving: use +11 for AEDT or +10 for AEST. This changes local calendar input/output; `"now"` always resolves to the actual instant.

Escape returns to Control without immediately auto-starting again. To restore the previous Swarm → Kannadi sequence, copy `Kannadi/sequenceConfig.before-adaminaby-preview.json` back to `sequenceConfig.json`. For manual startup of any sequence, omit `autoStart` or set it false. This automatic preview bypasses the Control form's manual Start action; use manual startup when collecting animal metadata.

Copy `Assets/StreamingAssets/Examples/Sequences/bogong-night-sky.example.json` to `Assets/StreamingAssets/sequenceConfig.json` to run the one-hour Bogong example, or reference `Templates/bogong-swarm.template.json` in a Swarm sequence step. The same sky is shared by all rigs. Existing sequences retain their settings.

Add `"nightSky": { "enabled": true }` to an individual Swarm, Kannadi or Choice experiment JSON for Konstanz and the current date/time. To set a local date and time elsewhere:

```json
"nightSky": {
  "latitude": -36.45,
  "longitude": 148.26,
  "date": "2026-12-01",
  "localTime": "22:30",
  "utcOffsetHours": 11,
  "updateIntervalMinutes": 30,
  "advanceWithRealTime": true
}
```

| Field | Default and meaning |
|---|---|
| `enabled` | `true` within a supplied object. Omit `nightSky`, or set false, for the existing background. |
| `latitude`, `longitude` | Approximate University of Konstanz campus location, **47.6896° N, 9.1881° E**, Universitätsstraße 10. Latitude −90…90; longitude −180…180; east positive. |
| `date` | Optional `yyyy-MM-dd`; omitted means today's date in the selected zone. |
| `localTime` | Default `"now"`; omitted, null or blank also means now. Otherwise `"22:30"`, `"22:30:15"`, or `"22:30:15.125"` starts at that exact local time on `date` (or today). |
| `utcOffsetHours` | Preferred fixed offset east of UTC, e.g. `11`, `-5`, `5.5`, or `5.75`. Range −14…+14 in whole-minute increments. Overrides any named zone; no seasonal rules. Active Adaminaby config uses `11`. |
| `timeZoneId` | Optional legacy/advanced named-zone input for automatic daylight saving. Fallback is `Europe/Berlin` when no offset is supplied (Konstanz default). **Coordinates do not infer a zone.** Berlin and Sydney have cross-platform Windows aliases. No zone ID is required when supplying `utcOffsetHours`. |
| `utcOffsetMinutes` | Legacy equivalent, e.g. `660` for UTC+11. Use either hours or minutes, not both. |
| `advanceWithRealTime` | `true`: advances from the resolved start using an unscaled monotonic clock. False freezes the sampled sky. |
| `updateIntervalMinutes` | `30`; integer 1…60. Regenerates and archives a panorama each interval. The image is static between updates. |
| `roundToInterval` | `false`: exact starting instant, then start+30 minutes, start+60 minutes, etc. Opt into `true` for nearest UTC interval, ties forward; this can round an explicitly supplied time. |
| `northYawDegrees` | `0`: north +Z, east +X, zenith +Y. Positive yaw rotates north toward +X. Calibrate against the physical arena. |
| `exposure` | `1`; linear-source multiplier in (0,100], before clipping and conversion to 8-bit sRGB. |
| `faintDetailCutoff` | `0` preserves all source detail. Range 0…1; active preview uses `0.15` to suppress faint texture detail. Pixels at/below the cutoff are black, those at twice the cutoff or above are unchanged, with a smooth fade between, measured on the maximum linear source RGB channel before exposure. Not a magnitude or Bortle scale. |
| `maskBelowHorizon` | `true`: black below zero altitude. False shows the whole celestial sphere. Scene geometry can still occlude it. |
| `imageWidth` | `4096`; also accepts 2048 or 1024. Height is half width. |

With `localTime: "now"` (or omitted/blank) and no date, the actual current UTC instant is captured **at each initialization**, independent of the computer's zone. Restarts do not retain an earlier date. If only `date` is set, the current local clock time is used on that date. An explicit time starts exactly as specified by default, and advances at real-time speed unless frozen; Unity timeScale does not affect it. Repeated/skipped local times at daylight-saving transitions are rejected: supply an explicit offset to identify the intended instant. A daytime request still displays stars at that time's orientation.

The first image is generated synchronously at initialization. Sampling boundaries trigger a bake, GPU readback, PNG encoding and disk writes before activation. This introduces main-thread work once per interval; bench-test acquisition timing on the experiment computer. Between boundaries the sky is a static texture. No persistent disk cache is trusted on restart.

## Explicit image override

A top-level `"skyboxPath": "Photosphere/my-sky.png"` takes precedence over `nightSky`. Use a 2:1, 360° equirectangular PNG/JPEG, relative to StreamingAssets or an absolute path. It uses Unity's `Skybox/Panoramic` convention: +X at u=0.5, +Z at u=0.25, zenith at v=1. The image stays fixed and is archived as PNG. Externally prepared Stellarium skies can use this after conversion to the same convention. Missing/invalid files never silently reuse the last trial's sky. Choice supports generated `nightSky` settings and the same audit records, including 30-minute regeneration. Its explicit `skyboxPath` retains the separate legacy image loader and takes precedence over generation; that loader does not create skybox audit records and accepts historical non-equirectangular images. The [restored historical Adaminaby preset](adaminaby-historical-scene.md) uses the legacy loader. Choice accepts omitted or null `objects` for sky-only terrain experiments.

## Audit and data

Each run's `Skyboxes/` directory contains:

* `<skyboxId>.png`: the exact displayed texture after orientation, faint-detail filtering, exposure and horizon masking.
* `<skyboxId>.json`: image SHA-256, source provenance, configuration, sampled UTC, transformation matrix, renderer version, colour space and Unity version.
* `usage.jsonl`: each activation, including previous/new IDs, image filename, application and sky times in UTC/local time, Unity frame and resolved starting instant.
* `loads.csv`: a tabular load history with `appliedUtc,appliedLocal,sampleUtc,sampleLocal,previousSkyboxId,skyboxId,imageFile,frame`.

The ID is SHA-256 over the stimulus description and PNG hash. Identical settings/output identify the same stimulus; each run has its own image copy. Saving must succeed before activation. Failed updates clear the sky and ID and emit an error. Direct scene play without MasterDataLogger saves under `Application.persistentDataPath/NightSkyRuns/<session>/Skyboxes/` and prints the path.

Rig, Swarm population and Kannadi clone CSVs contain **`skyboxId`** and **`skyboxSampleUtc`**. Join the ID to its manifest/image. Sample UTC is blank for a supplied image; both fields are blank outside a managed sky stimulus. The ID identifies the scene sky, even if geometry occludes it in a particular view. ReplayController does not yet restore skies automatically; archived PNGs can be supplied through `skyboxPath`.

## Scope and sources

This is a **stars and Milky Way** stimulus, without Sun, Moon, planets, twilight, atmospheric refraction/extinction, light pollution, weather or stellar proper motion. It is not photometrically calibrated. Source brightness/star sizes are a visualization; output clips HDR values. Resolution, exposure, faint-detail filtering and LED calibration affect visibility. The faint-detail cutoff reduces diffuse background and dim pixels together; stars are already baked into the source, so it cannot independently select stars by catalog magnitude or control Milky Way intensity. Set it to `0` to recover the original source appearance. For actual Bortle/magnitude/planet controls, use a prepared Stellarium image via `skyboxPath` or a future Stellarium renderer.

* [NASA Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/): ICRF/J2000 plate carrée, RA zero at centre and increasing leftward. Bundled EXR is unchanged; hash and credit are in `Assets/Resources/NightSky/source.txt`. Credit: NASA/Goddard Space Flight Center Scientific Visualization Studio; Ernie Wright (USRA); Gaia DR2: ESA/Gaia/DPAC. [Usage information](https://svs.gsfc.nasa.gov/help/).
* [Astronomy Engine C#](https://github.com/cosinekitty/astronomy/tree/865d3da7d8112bbc7911238052c6af4aaf877181/source/csharp): unchanged source pinned to commit `865d3da7d8112bbc7911238052c6af4aaf877181`; MIT license retained in `Assets/ThirdParty/AstronomyEngine/LICENSE.txt`.
* [Stellarium–Unity bridge](https://github.com/Stellarium/stellarium-unity): evaluated; its skybox mode exports six tiles and its live Spout mode requires a separate Windows process. No code from that GPL bridge was imported.
* [University of Konstanz visitor address](https://www.uni-konstanz.de/universitaet/ueber-die-universitaet-konstanz/anreise-lageplan-und-oeffnungszeiten/).

## Validation

Run the static repository checks, then use Unity 6000.3.16f1 with graphics enabled on a **disposable project copy** containing `KANNADI_VALIDATION_COPY`:

```text
Unity -batchmode -projectPath <copy> -executeMethod NightSkyValidation.Run -logFile <log>
```

Do not add `-quit` or `-nographics`. This tests clock/offset/now/DST handling, coordinate conventions, GPU filtering/output, checksums, explicit-image precedence, restart/update/freeze behavior, camera restoration and CSV attribution. `KannadiValidation.Run` separately covers the existing experiment stack. Physical north alignment and LED acquisition timing still need a bench check.

Use `NightSkyValidation.RunControlPreview` on a disposable copy with the preserved Adaminaby preview sequence to test the real Control-scene launch, current-time export, actual aligned Bogong movement and flicker-free visibility, simulated next half-hour image/load record, and Escape behavior. The general Kannadi suite installs the saved pre-preview sequence into the disposable copy before running.

Use `ChoiceNightSkyValidation.Run` on a disposable copy for the Choice sky-only regression: omitted/null objects, generated-to-legacy image transitions, camera overrides, current-time panorama export, 30-minute refresh and cleanup. `RunSelwyn` additionally tests the local `Choice_Selwyn` terrain scene and `Kannadi/selwyn-night-sky.json` when those assets are present. Both runners install a temporary sequence and six-camera system config in the disposable copy.
