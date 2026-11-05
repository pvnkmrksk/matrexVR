# Portable model examples

These are real files loaded by the same runtime GLB importer as your own models.

| File | Example |
| --- | --- |
| `SimulatedLocust.glb` | Existing MATREX locust re-exported with its skeleton, textures and animation. Source and hash: `SimulatedLocust.provenance.json`. |
| `RiggedSimple.glb` | Downloaded skeletal-animation sample by Cesium, CC BY 4.0. |
| `AnimatedMorphCube.glb` | Downloaded morph-animation sample credited to Microsoft, CC0. |

The downloaded files are unchanged. Their exact upstream revision, URLs, hashes,
credits and license references are in `downloaded-models.provenance.json` and
the adjacent `.source.md` files. The locust retains its original asset rights.

Load `Examples/Sequences/custom-models.sequence.json`, then open **Appearance**
and its object card. Choose another GLB, enable its color override, or open
Placement for size, position and orientation. Copy your own embedded `.glb`
under `Models/` to list it here, or enter its absolute file path. Keep textures
enabled for tinting, or disable them for a uniform chosen color. No rebuild is
needed. Geometry, textures and animation must be embedded in the GLB.

`Examples/Sequences/kannadi-glb.sequence.json` shows the same locust file used
for the mirrored animals and a downloaded animated object in the shared world.
