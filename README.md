# Unity Project Readme

This repository contains a Unity project that demonstrates various functionalities and features. The project includes scripts for closed-loop control, data logging, object rotation, keyboard controls, texture generation, viewport setup, and socket communication.

## Scripts

- [x] ClosedLoop.cs
- [x] DataLogger.cs
- [x] DrumRotator.cs
- [x] Keyboard.cs
- [x] SinusoidalGrating.cs
- [x] ViewportSetter.cs
- [x] ZmqListener.cs

### ClosedLoop.cs

- Controls the closed-loop position and orientation of an object based on input from a ZMQ listener and user input.
  - [x] Implement closed-loop position control.
  - [x] Implement closed-loop orientation control.
  - [ ] Add individual axis closed-loop control.
  - [ ] Add individual axis gain control.
  - [ ] Implement real-time physics for force-torque transform.

### DataLogger.cs

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
