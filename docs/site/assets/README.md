# Preview provenance

`selwyn-night-sky.jpg` is a 1600×800 JPEG, downsampled from an actual Unity 6000.3.16f1 generated 4096×2048 panorama. No exposure or brightness alteration was applied. Its original image SHA-256 and complete stimulus description are retained in [the accompanying manifest](selwyn-night-sky.provenance.json). Source output ID: `9bc91c3c8f64532e802899f0a174e32453034c00d7c41918c4f58152901e6b8f`; sampled UTC `2026-10-07T07:14:38.3559010Z`; observer −35.894409°, 148.453948°; exposure 0.5; faint-detail cutoff 0.15; horizon mask false. This is a full-sphere equirectangular stimulus texture, not a camera screenshot or photometric calibration.

Source credit: NASA/Goddard Space Flight Center Scientific Visualization Studio; Ernie Wright (USRA); Gaia DR2: ESA/Gaia/DPAC. [NASA Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/). The original source hash and Astronomy Engine revision appear in the manifest.

`configuration-studio.jpg` derives from the actual 0.3 Session capture `docs/images/dashboard-v0.3/session-1920.png`, encoded as JPEG without cropping or color correction. It shows the current Animal ID/Species cards, hardware summary and sequence. The [0.3 capture manifest](https://github.com/pvnkmrksk/matrexVR/blob/v0.3.0/docs/images/dashboard-v0.3/provenance.json) records source hashes, Unity version, runner and transformation. The [GUI report](https://github.com/pvnkmrksk/matrexVR/blob/v0.3.0/docs/configuration-dashboard-v0.3-validation-results.json) records the native checks.

`flight-dashboard.jpg` and `kannadi-overview.jpg` derive from `docs/images/ux/flight-hud.png` and `docs/kannadi-overview.png`. The HUD is a 0.2 baseline capture; current selection also supports 1–9 and 0/Next animal. All UI/overview images use synthetic validation metadata/trajectories, not live animal collection data.

`terrain-cue-four-conditions.svg` is copied from `docs/terrain-cue-four-conditions.svg`. The schematic uses actual Unity mesh and rig samples exported by RugosityValidation; its vertical scale is exaggerated. See `docs/rugosity.md`.
