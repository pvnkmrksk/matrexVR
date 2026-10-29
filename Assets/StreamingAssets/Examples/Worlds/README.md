# Packaged world examples

Load these through **Load sequence → World examples**. The current apparatus and animal metadata are retained. Every example has a human-readable title, fixed seed 1, automatic startup off, and a single inspection block. World examples are editable copies of configurations, so changing one does not replace older recipes.

| Starter | Packaged scene | Ground and units |
| --- | --- | --- |
| Flat world · open base | Choice | Large flat textured grass/dirt plane; centimetres. A base for open/desert-style experiments, with no sand-artwork claim. |
| Forest floor · rugged ground | Rugosity_RS | Original rugged RS ground mesh, matching visual/navigation surfaces; centimetres and 1 cm clearance. |
| Dense forest · grounded trees | Choice_Selwyn | Existing terrain plus 90 grounded stock trees at scale 1, 8 m spacing and an open corridor; metres. |
| Real-world terrain · Selwyn | Choice_Selwyn | Packaged terrain without extra objects; metres. |
| Optomotor · yaw drum | Optomotor | Procedural yaw drum, 30 seconds at 20 degrees/s and 50% contrast. |

The walking gain in the two metre-scale terrain examples is 0.01 to convert centimetre tracker displacement to metres. Autopilot is off, so the apparatus controls tracked/manual movement. These defaults are starting points, not hardware calibrations.

All five use scenes already included in the desktop build. There are no source-only scene dependencies. Kannadi remains the population/mirror example and can provide swarm or social paradigms; these world examples describe environmental geometry. Custom GLB objects, sky choices and the directional sun can be changed through the configuration editor.

`python3 tools/test_world_starters.py` checks catalog references, build-scene availability, object registration/scales and basic configuration contracts. It does not replace a native render or apparatus test.
