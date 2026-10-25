# Preview provenance

`selwyn-night-sky.jpg` is a 1600×800 JPEG, downsampled from an actual Unity 6000.3.16f1 generated 4096×2048 panorama. No exposure or brightness alteration was applied. Its original image SHA-256 and complete stimulus description are retained in [the accompanying manifest](selwyn-night-sky.provenance.json). Source output ID: `9bc91c3c8f64532e802899f0a174e32453034c00d7c41918c4f58152901e6b8f`; sampled UTC `2026-10-07T07:14:38.3559010Z`; observer −35.894409°, 148.453948°; exposure 0.5; faint-detail cutoff 0.15; horizon mask false. This is a full-sphere equirectangular stimulus texture, not a camera screenshot or photometric calibration.

Source credit: NASA/Goddard Space Flight Center Scientific Visualization Studio; Ernie Wright (USRA); Gaia DR2: ESA/Gaia/DPAC. [NASA Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/). The original source hash and Astronomy Engine revision appear in the manifest.

`configuration-studio.jpg` is an unchanged copy of the actual current-source Session JPEG `docs/images/dashboard-ecosystem/session-1920.jpg`. It shows Animal ID/Species cards, modern tracker labels, hardware summary and sequence after 0.3. The [ecosystem capture manifest](https://github.com/pvnkmrksk/matrexVR/blob/main/docs/images/dashboard-ecosystem/provenance.json) records hashes, Unity version and fixture; the [2,970-check GUI report](https://github.com/pvnkmrksk/matrexVR/blob/main/docs/configuration-dashboard-ecosystem-validation-results.json) records native verification. Stable 0.3 binaries retain their archived UI.

`flight-dashboard.jpg` and `kannadi-overview.jpg` derive from `docs/images/ux/flight-hud.png` and `docs/kannadi-overview.png`. The HUD is a 0.2 baseline capture; current selection also supports 1–9 and 0/Next animal. All UI/overview images use synthetic validation metadata/trajectories, not live animal collection data.

`terrain-cue-four-conditions.svg` is copied from `docs/terrain-cue-four-conditions.svg`. The schematic uses actual Unity mesh and rig samples exported by RugosityValidation; its vertical scale is exaggerated. See `docs/rugosity.md`.


The nine actual scene JPGs under `gallery/` are native Unity demonstration renders. [Their unmodified provenance](gallery/provenance.json) identifies scene, camera, sample context and matching config sidecars. The forest capture explicitly instantiates `Assets/Prefabs/Landscape.prefab` into Choice_Forrest as source customization. The manual adds image/config SHA-256 hashes when built.
