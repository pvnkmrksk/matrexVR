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
7. Accept the recommended LTS version (e.g., 2024.x LTS)
   - Do NOT install Unity 6000 or other non-recommended versions

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

## Dependencies

- Unity Engine (version X.X.X)
- NetMQ (version X.X.X)

## Contributing

Contributions to the project are welcome! If you find any issues or have suggestions for improvements, please submit a pull request or create an issue.

## License

The project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- [Unity Engine](https://unity.com/)
- [NetMQ](https://github.com/zeromq/netmq)
