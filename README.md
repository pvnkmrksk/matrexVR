# MATREX VR: Bridging Virtual Reality and Naturalistic Behaviors in Neuroscience

## Overview

This repository hosts the code and resources for MATREX VR (MATREX Architecture Terraforming Realistic Environments in X), a groundbreaking project at the intersection of behavioral neuroscience, virtual reality, and ecological studies. Our system, inspired by the concept of 'Prakruti Maye', blends tangible reality with carefully crafted illusion, challenging traditional boundaries between actuality and artifice.

## Project Aim

The MATREX VR project aims to revolutionize our understanding of collective foraging behaviors, particularly in locusts and other species. We focus on creating simulated yet highly realistic environments that replicate the complexity of natural settings. This approach seeks to overcome the limitations of reductionist models historically used in neuroscience and behavioral studies.

## Key Features

- **Immersive Virtual Reality Environments:** Utilizes high refresh rate, commercial LED panels, and off-the-shelf components to create panoramic, naturalistic visual stimuli.
- **Parametric 3D Printable Modules:** Designed to accommodate a range of organisms, enabling scalable and cost-effective study of both walking and flying behaviors.
- **Advanced Sensory System:** Features a fully 3D printable, 6-axis force-torque sensor capturing nuanced dynamics like pitch, yaw, roll, and translational forces.
- **Open Source Contribution:** Simplifies the data capture process in VR research and democratizes access, promoting an inclusive approach in behavioral neuroscience.

## Applications and Impact

MATREX VR is not just a technological advancement but a paradigm shift in how we approach naturalistic behavior studies. By integrating complex, real-world stimuli into virtual environments, we open new avenues for understanding the neural basis of individual and collective decision-making. This repository serves as a resource for researchers and academicians interested in exploring similar paths or expanding upon our work.

## Installation Guide

### Prerequisites
- Ubuntu operating system
- Terminal access
- Unity account

### Step 1: Install FicTrac

```bash
# Create a folder for source code
mkdir src
cd src

# Clone the FicTrac repository
git clone https://github.com/pvnkmrksk/fictrac.git

# Enter the FicTrac folder
cd fictrac

# Make the install script executable and run it
chmod +x install_ubuntu.sh
./install_ubuntu.sh
```



### Step 2: Install MatrexVR

```bash
# Go back to source folder
cd ~/src

# Clone MatrexVR repository
git clone https://github.com/pvnkmrksk/matrexVR.git
```

### Step 3: Install Unity Hub

```bash
# Add Unity's Public Key
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null

# Add Unity Hub Repository
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'

# Update and Install Unity Hub
sudo apt update
sudo apt-get install unityhub
```

### Step 4: Launch Unity Hub

1. Run Unity Hub from terminal or applications:
   ```bash
   unityhub
   ```
2. Login or create a Unity account
3. Do not install Unity yet
4. Click Add or Open
5. Navigate to: `~/src/matrexVR`
6. Click Open
7. Install the editor version recorded in `ProjectSettings/ProjectVersion.txt`. The terrain work was validated locally with **Unity 6000.3.16f1 (Unity 6.3 LTS)**; the local editor/package upgrade is separate from the terrain commits.

### Step 6: Run the Project

Once the correct Unity version is installed, open the project and you're ready to go!

## Scripts Overview

| Script | Status | Description |
|--------|--------|-------------|
| ClosedLoop.cs | ✅ | Controls closed-loop position and orientation |
| DrumLogger.cs | ✅ | Logs position, rotation, and parameters |
| DrumRotator.cs | ✅ | Rotates drum object with configurations |
| Keyboard.cs | ✅ | Enables keyboard controls |
| SinusoidalGrating.cs | ✅ | Generates sinusoidal grating texture |
| ViewportSetter.cs | ✅ | Sets up multiple viewports |
| ZmqListener.cs | ✅ | Listens to ZeroMQ socket |
| DataLogger.cs | ✅ | Logs data to CSV |
| jsonLogger.cs | ❌ | Not implemented |
| replayscript.cs | ❌ | Not implemented |

## Keyboard Controls

### Movement
- `W` - Pitch down
- `S` - Pitch up
- `D` - Yaw right
- `A` - Yaw left
- `E` - Roll CW
- `Q` - Roll CCW
- `↑` - Move forward
- `↓` - Move backward
- `→` - Move right
- `←` - Move left
- `C` - Move up
- `Z` - Move down

### Control Toggles
- `O` - Toggle Closed Loop Orientation Control
- `P` - Toggle Closed Loop Position Control
- `M` - Toggle Closed Loop Momentum Control

## Terrain appearance and navigation experiments

The existing `navrug` scenes support JSON-controlled terrain conditions, with **1 Unity unit = 1 cm** and a default **1 cm** ground clearance. The first scene suffix letter is appearance; the second is navigation.

| Condition | Scene | Choice config (`Assets/StreamingAssets/`) |
| --- | --- | --- |
| Looks rough, feels rough | `navrug_rr` | `choice_navrug_looks_rough_feels_rough.json` |
| Looks rough, feels smooth | `navrug_rs` | `choice_navrug_looks_rough_feels_smooth.json` |
| Looks smooth, feels rough | `navrug_sr` | `choice_navrug_looks_smooth_feels_rough.json` |
| Looks smooth, feels smooth | `navrug_ss` | `choice_navrug_looks_smooth_feels_smooth.json` |

The active `Assets/StreamingAssets/sequenceConfig.json` already runs all **16 combinations**, 30 seconds each, looping every eight minutes. Open `ControlScene` and press **Play**: top-level `autoStart: true` starts the sequence automatically. Conditions are selected by JSON, with no terrain GUI controls.

For the shorter four-condition experiment, back up the active sequence and replace it with `sequenceConfig_terrainNavigation.json`. That example uses manual startup; add `"autoStart": true` at the top level if desired.

Use `sequenceConfig_terrainNavigation_allModes.json` to compare all 16 combinations of appearance, navigation surface, AGL/absolute height and level/normal-following orientation. Choice configs define condition defaults; sequence parameters can override individual fields.

See [Terrain navigation README](docs/terrain-navigation.md) for complete JSON examples, configuration precedence, boundary behavior and Unity CLI tests on all four actual scenes.

## Running Experiments

- The sequence of scenes is defined in `Assets/StreamingAssets/sequenceConfig.json`. Each entry lists a `sceneName`, a `duration`, optional `parameters`, and whether to reload the scene between steps.
- Scenes that implement `IInSceneSequencer` (e.g., `Choice_desync`) can run their own internal step list when `reloadScene` is `false`, using parameters such as `design` to pick a sequence design JSON.
- Editor menu items under `Tools/…` generate the design JSONs into `Assets/StreamingAssets`. After generating, point `sequenceConfig.json` to the desired design filename for the scene you want to run.
- See `docs/experiment-workflow.md` for a concise walkthrough of the scene/sequence pipeline and how to add new experiments.
- See [docs/json-config-schema.md](/home/flyvr01/src/matrexVR/docs/json-config-schema.md) for the JSON schema used by sequence, system, choice, and optomotor configs, plus template files in `Assets/StreamingAssets`.

## Dependencies

- Unity Engine: see `ProjectSettings/ProjectVersion.txt`
- NetMQ: bundled in `Assets/Plugins/NetMQ.dll`

## Contributing

Contributions to the project are welcome! If you find any issues or have suggestions for improvements, please submit a pull request or create an issue.

## License

The project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- [Unity Engine](https://unity.com/)
- [NetMQ](https://github.com/zeromq/netmq)
