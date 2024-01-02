**MATREX VR: Bridging Virtual Reality and Naturalistic Behaviors in Neuroscience**

**Overview:**

This repository hosts the code and resources for MATREX VR (MATREX Architecture Terraforming Realistic Environments in X), a groundbreaking project at the intersection of behavioral neuroscience, virtual reality, and ecological studies. Our system, inspired by the concept of 'Prakruti Maye', blends tangible reality with carefully crafted illusion, challenging traditional boundaries between actuality and artifice.

**Project Aim:**

The MATREX VR project aims to revolutionize our understanding of collective foraging behaviors, particularly in locusts and other species. We focus on creating simulated yet highly realistic environments that replicate the complexity of natural settings. This approach seeks to overcome the limitations of reductionist models historically used in neuroscience and behavioral studies.

**Key Features:**

- **Immersive Virtual Reality Environments:** Utilizes high refresh rate, commercial LED panels, and off-the-shelf components to create panoramic, naturalistic visual stimuli.
- **Parametric 3D Printable Modules:** Designed to accommodate a range of organisms, enabling scalable and cost-effective study of both walking and flying behaviors.
- **Advanced Sensory System:** Features a fully 3D printable, 6-axis force-torque sensor capturing nuanced dynamics like pitch, yaw, roll, and translational forces.
- **Open Source Contribution:** Simplifies the data capture process in VR research and democratizes access, promoting an inclusive approach in behavioral neuroscience.

**Applications and Impact:**

MATREX VR is not just a technological advancement but a paradigm shift in how we approach naturalistic behavior studies. By integrating complex, real-world stimuli into virtual environments, we open new avenues for understanding the neural basis of individual and collective decision-making. This repository serves as a resource for researchers and academicians interested in exploring similar paths or expanding upon our work.

**Note for Contributors and Users:**

This repository is a living document of our journey in understanding the complex tapestry of collective behavior. We encourage collaboration, suggestions, and constructive feedback. For detailed instructions, usage guidelines, and contribution policies, please refer to the documentation within.

---

This repository contains a Unity project that demonstrates various functionalities and features. The project includes scripts for closed-loop control, data logging, object rotation, keyboard controls, texture generation, viewport setup, and socket communication.

## Scripts

- [x] ClosedLoop.cs
- [x] DrumLogger.cs
- [x] DrumRotator.cs
- [x] Keyboard.cs
- [x] SinusoidalGrating.cs
- [x] ViewportSetter.cs
- [x] ZmqListener.cs
- [x] DataLogger.cs
- [] jsonLogger.cs
- [] replayscript.cs

### ClosedLoop.cs

- Controls the closed-loop position and orientation of an object based on input from a ZMQ listener and user input.
  - [x] Implement closed-loop position control.
  - [x] Implement closed-loop orientation control.
  - [ ] Add individual axis closed-loop control.
  - [ ] Add individual axis gain control.
  - [ ] Implement real-time physics for force-torque transform.

### DrumLogger.cs

- Logs the position, rotation, frequency, level, and other parameters of various objects in the scene to a compressed CSV file.
  - [x] Implement data logging functionality.
  - [ ] Improve drum rotation bug for different drum axes.
  - [ ] Add GUI elements for control.
  - [ ] Add GUI elements for data display.
  - [ ] Enhance README and documentation.

### DrumRotator.cs

- Rotates a drum object based on predefined configurations, allowing step-wise rotation and pausing functionality.
  - [x] Implement drum rotation based on configurations.
  - [x] Add step-wise rotation functionality.
  - [x] Add pausing functionality.
  - [ ] Fix drum rotation bug for different drum axes.
  - [ ] Add GUI elements for control.

### Keyboard.cs

- Enables keyboard controls for translating and rotating an object, with adjustable speed and maximum limits.
  - [x] Implement keyboard controls for translation.
  - [x] Implement keyboard controls for rotation.
  - [ ] Add better README and documentation.

### SinusoidalGrating.cs

- Generates a sinusoidal grating texture based on frequency and level parameters, applied to a cylindrical mesh object.
  - [x] Generate sinusoidal grating texture.
  - [ ] Add better README and documentation.

### ViewportSetter.cs

- Sets up multiple viewports for different cameras to render a portion of the LED panel, with options for horizontal or vertical arrangement.
  - [x] Implement viewport setup.
  - [ ] Add better README and documentation.

### ZmqListener.cs

- Listens to a ZeroMQ socket for pose messages and updates the position and rotation of an object accordingly.
  - [x] Implement ZMQ listener functionality.
  - [ ] Add better README and documentation.
  
### DataLogger.cs

It can be attached to any GameObject and will continuously log data to a .csv.gz file. The script will generate a new log file each time the application starts.
Features
The script buffers the log lines and flushes them in the file when it's optimal.
The script logs the following data:
Current Time: The current date and time at the time of logging.
VR: The name of the GameObject the script is attached to.
Scene: The name of the current active scene in Unity.
SensPosX, SensPosY, SensPosZ: The position of the sensor (from ZmqListener script).
SensRotX, SensRotY, SensRotZ: The rotation of the sensor (from ZmqListener script).
InsectPosX, InsectPosY, InsectPosZ: The position of the GameObject the script is attached to.
InsectRotX, InsectRotY, InsectRotZ: The rotation of the GameObject the script is attached to.

    Attach the DataLogger script to the GameObject you want to log data from.
    Ensure the GameObject also has the ZmqListener script attached.

## Dependencies

The project has the following dependencies:

- Unity Engine (version X.X.X)
- NetMQ (version X.X.X)

## Installation and Setup

1. Clone the repository or download the source code.
2. Open the project in Unity.
3. Ensure that Unity Engine and the NetMQ library are properly installed and configured.
4. Open the desired scene in the Unity Editor.
5. Attach the appropriate scripts to the relevant objects in the scene.
6. Adjust the script parameters and settings as needed.
7. Build and run the project.

## Usage

- The project showcases various functionalities through the different scenes and objects.
- Use the provided keyboard controls to interact with the objects and observe their behaviors.
- Refer to the specific script documentation for detailed usage instructions and customization options.

## Keyboard Controls

- `W` - Pitch down.
- `S` - Pitch up.
- `D` - Yaw right.
- `A` - Yaw left.
- `E` - Roll CW.
- `Q` - Roll CCW.
- `Up Arrow` - Move forward.
- `Down Arrow` - Move backward.
- `Right Arrow` - Move right.
- `Left Arrow` - Move left.
- `C` - Move up.
- `Z` - Move down.

- `O` - Toggle Closed Loop Orientation Control.
- `P` - Toggle Closed Loop Position Control.
- `M` - Toggle Closed Loop Momentum Control.



## Contributing

Contributions to the project are welcome! If you find any issues or have suggestions for improvements, please submit a pull request or create an issue.

## License

The project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- [Unity Engine](https://unity.com/)
- [NetMQ](https://github.com/zeromq/netmq)
