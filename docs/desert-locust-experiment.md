# Desert locust experiment

Load [Desert locust · 16 rigs](../Assets/StreamingAssets/Examples/Sequences/desert-locust-16rig.sequence.json) in the sequence picker. Keep its three referenced files under `Examples/Kannadi/` in the configuration folder. Select your normal walking system configuration for VR1–VR16. The sequence opens in setup and starts when you press **Start experiment**.

| Condition | What each rig sees |
| --- | --- |
| [Blank control](../Assets/StreamingAssets/Examples/Kannadi/desert-16rig-blank.experiment.json) | Flat sand and a gray sky, with zero conspecifics. |
| [Ordered swarm](../Assets/StreamingAssets/Examples/Kannadi/desert-16rig-ordered-swarm.experiment.json) | A private population of 64 simulated locusts in a 1 m × 1 m periodic arena centered on the observer. They walk at 2 cm/s with highly aligned headings sampled from a von Mises distribution with κ = 35. |
| [Kannadi peers](../Assets/StreamingAssets/Examples/Kannadi/desert-16rig-kannadi.experiment.json) | The other 15 tracked locusts, with a central avatar and one hexagonal ring of six copies per peer. Each rig hides its own avatars and copies. |

Each condition runs for 10 seconds. MATREX reshuffles all three conditions after each cycle and presents each once per cycle. The fixed sequence seed makes the order reproducible across runs. Escape ends the loop and returns to setup.

The 16 rigs start on the same 4 × 4 grid in all three conditions, with 22 cm spacing and fixed jitter of up to 2 cm on each horizontal axis. All conditions share the same heading seed. Within a cycle, each rig starts at the same independently sampled yaw in every condition. The next cycle draws new headings.

## Change the ground

Open a condition and go to **Appearance → Ground**. Choose **Sand**, **Grass**, or **Scene default (dry grass)**. Drop PNG or JPEG files into `GroundTextures/` inside the active configuration folder and reopen the Appearance tab to list them. **Custom image…** can also select a file from another folder. MATREX archives custom ground images used in recorded runs.

**Tiling X** and **Tiling Y** set repeat counts independently. On the stock 200 m × 200 m plane, 200 gives 1 m per tile on that axis and 400 gives 0.5 m. Use the same values in all three condition files to keep ground cues matched.

## Repeat the desktop check

Run the following from the repository root with Unity 6000.3.16f1 and an unused `.local/desert-check` directory. The validator copies the packaged recipe to a test profile and uses a synthetic 16-rig system config.

```sh
python3 tools/build_local_desktop.py --prepare-only --project .local/desert-check/project

/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath .local/desert-check/project \
  -executeMethod DesertLocustValidation.Run \
  --config-dir "$PWD/.local/desert-check/Config" \
  --data-dir "$PWD/.local/desert-check/RunData" \
  --control-dir "$PWD/.local/desert-check/Control" \
  -logFile .local/desert-check/validation.log
```

The check writes a JSON report and three rendered rig views alongside the test profile. It exercises setup validation, the texture picker, two full sequence cycles, all 16 rig populations and visibility, saved pose streams, and same-build replay. The check uses desktop rendering with hardware inputs disabled.
