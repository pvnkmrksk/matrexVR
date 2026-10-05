# Historical configuration provenance

These exact JSON examples are references, not runnable templates for this checkout. Modern runnable adaptations live in `Assets/StreamingAssets/Templates`.

- `bogong-*`: `origin/BogongAustralia` (2026-03-15), the AGL 10/100/1000 LSM examples and Bogong migration sequence.
- `smrmah-*`: `origin/smrmah-optomotor-updated` (2026-08-04), matching examples from the SmrMah line. No branch explicitly named Deathhead was present in the inspected remote refs; do not infer a species identity from this name.
- `optomotor_config_arc.json`: `origin/Optomotor` (2026-03-15), grating frequency/speed sweep.

The old migration sequence refers to Choice_Bogong, Photosphere/LSM.png, and a top-level gain field. The modern Choice adaptations use the existing environment and retain wind/AGL meanings; the historical Kinefly yaw-rate calculation and top-level sequence gain have now been restored. Earlier modernization commits ignored that gain and removed it from adapted examples; an example without `gain` uses the historical default 1, not the archived migration example's explicit gain 10. See [the compatibility audit](../bogong-gain-compatibility.md).
