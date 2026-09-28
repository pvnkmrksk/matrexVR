# Historical configuration provenance

These exact JSON examples are references, not runnable templates for this checkout. Modern runnable adaptations live in `Assets/StreamingAssets/Templates`.

- `bogong-*`: `origin/BogongAustralia` (2026-03-15), the AGL 10/100/1000 LSM examples and Bogong migration sequence.
- `smrmah-*`: `origin/smrmah-optomotor-updated` (2026-08-04), matching examples from the SmrMah line. No branch explicitly named Deathhead was present in the inspected remote refs; do not infer a species identity from this name.
- `optomotor_config_arc.json`: `origin/Optomotor` (2026-03-15), grating frequency/speed sweep.

The old migration sequence refers to Choice_Bogong, Photosphere/LSM.png, and a top-level gain field. The modern Choice adaptations use the existing environment and retain wind/AGL meanings; they do not activate the older force/torque or yaw-rate tracking modes. The old sequence's gain is ignored by the modern outer sequence parser and was removed from runnable examples.
