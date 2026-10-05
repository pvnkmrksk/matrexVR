# MATREX VR

Unity experiments for tracked animals and LED-panel virtual environments. This branch's current setup is **Bogong/Kinefly flight**, with Selwyn terrain and night sky. It also includes the JuliusTree infrastructure, Swarm and **Kannadi** (ಕನ್ನಡಿ, mirror) multiplayer scenes and their separate walking input path.

Use **Unity 6000.3.16f1**, matching ProjectVersion.txt. Packages are pinned in `Packages/manifest.json` and `packages-lock.json`; the operator controls now use Unity Input System 1.20.0. Restart an already open Editor after pulling the input-backend change.

## Start here

1. Open `Assets/Scenes/ControlScene.unity`.
2. Keep your existing `Assets/StreamingAssets/system_config.json`, or copy the complete [system template](Assets/StreamingAssets/Templates/system_config.template.json). Set addresses/displays and retain explicit `"closedLoopMode": "Kinefly"` for flight. Walking alternatives live in [Examples/System](Assets/StreamingAssets/Examples/System).
3. Use the complete [experiment](Assets/StreamingAssets/Templates/experiment.template.json) and [sequence](Assets/StreamingAssets/Templates/sequence.template.json) templates as a matched starting set. [Templates](Assets/StreamingAssets/Templates/README.md) covers every supported paradigm; [Examples](Assets/StreamingAssets/Examples/README.md) contains full variants. The active Choice_Selwyn sequence and all `Kannadi/` recipes are preserved. Old configs are in [Archive/Legacy](Assets/StreamingAssets/Archive/Legacy).
4. Enter Play mode from Control, fill the experiment metadata, and start the sequence. Focus Game for hotkeys. R resets; P toggles tracked position; O toggles tracked orientation. Escape returns to Control. **Tab shows/hides the bottom-right status/error panel.**
5. Each animal has a visible sex selector: **Unspecified** (fresh default), **Female**, **Male**. Reloading saved metadata restores the saved selection; old Unknown or missing values become Unspecified.
6. Find recordings under `Assets/RunData/<session>/`. Preserve the saved config files with the data.

Kinefly's existing single-frame JSON messages work without a publisher or config migration. The status panel identifies **Kinefly / flight**, each **Input SUB** address/port and reception state. **Telemetry PUB (output)** on port 9880 is a separate monitoring feed. See [Kinefly wire and gain compatibility](docs/bogong-gain-compatibility.md).

## Reference manuals

| Document | Contents |
|---|---|
| [Configuration reference](docs/configuration-reference.md) | Every current system, sequence, Choice, Swarm, Kannadi/kinematic, optomotor, migration and dynamic-design parameter; defaults, units, precedence and legacy no-op fields. |
| [Editable templates](Assets/StreamingAssets/Templates/README.md) | Complete JSON files and runnable example sequences, including migration AGL 10/100/1000. |
| [Unity 6 controls](docs/unity6-controls.md) | Action map, keyboard/mouse/gamepad bindings, UI migration, tracking/reset behavior and bench checks. |
| [Heading reference and telemetry](docs/heading-reference-telemetry.md) | Per-animal circular heading assessment, stimulus-relative angles, outbound ZMQ, and the restored Tab status/error panel. |
| [Data formats](docs/data-formats.md) | CSV/gzip/metadata columns, clocks, sensor-to-world conversion, coordinate conventions, replay and analysis limitations. |
| [Kannadi integration](docs/kannadi-modernization.md) | Scene integration, camera allow-list, overview, trails, animation and validation history. |
| [Experiment workflow](docs/experiment-workflow.md) | Controller lifecycle and adding experiments. |

## Coordinates and visual behavior

Current walking scenes use **one world unit = one centimeter**. Speeds are cm/s. Unity is **left-handed**: X right, Y up, Z forward. Positive yaw turns +Z toward +X, clockwise when viewed from above with +Z at the top. Rotations use degrees; log Euler angles wrap at 360. Imported flight terrain may have a different authored scale: check before interpreting world units as physical centimeters.

The system `displayOrder` is authoritative. A camera whose letter is missing is disabled even if it exists and is enabled in a scene. A rig absent from system config renders none of its stimulus cameras. The operator overview shows numbered heading arrows at actual rig positions with 60 s fading trajectories. It works in Swarm and Kannadi; Hide stops overview rendering and tracking work. FPS stays visible at the top right, alongside the configured target. Set top-level `targetFrameRate` (default 120) and `vSyncCount` (default 0) in system_config.json. Walking translation and yaw default to 1:1 gain; explicit numeric Swarm/Kannadi overrides scale displacement/angle.

Locust animation gating uses planar translation: at or below the default 0.5 cm/s threshold, legs stop; above it they animate. Rotation alone does not trigger walking. Configure `animateOnMove` and `animationSpeedThreshold` per experiment.

## Linux tracking workstation installation

### Prerequisites
- Ubuntu for the setup commands below (the Unity project is also tested on macOS)
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
7. Install Unity 6 (`6000.3.16f1`), matching `ProjectSettings/ProjectVersion.txt`.

### Step 6: Run the Project

Once the correct Unity version is installed, open the project and you're ready to go!

## Validation

Run `python3 tools/validate_kannadi_assets.py` and `python3 tools/validate_config_templates.py` for static checks. Unity regression testing uses `Assets/Editor/KannadiValidation.cs` on a disposable project copy; it refuses to run without the `KANNADI_VALIDATION_COPY` sentinel. See [the saved report](docs/kannadi-validation-results.json). Physical FicTrac, gamepad devices, LED timing and multi-monitor mapping still need a bench check.

## Contributing

Contributions to the project are welcome! If you find any issues or have suggestions for improvements, please submit a pull request or create an issue.

## License

The previous README identifies this project as MIT-licensed; this checkout has no root LICENSE file. Third-party assets retain their own license terms.

## Acknowledgements

- [Unity Engine](https://unity.com/)
- [NetMQ](https://github.com/zeromq/netmq)
