# Five minutes: demo → recording → replay

Try MATREX VR without a camera, animal or LED panels. By the end, you will have run a visual environment, saved a short demonstration and replayed it. Allow about five minutes **after downloading and extracting a desktop archive**; source compilation is a separate task.

This guide follows the **0.4.1 desktop Home workflow**. Archived versions can use different labels or startup defaults; the `START-HERE.txt` in your archive is authoritative. [Release history and limitations](releases.md) identify version-specific capabilities.

## 1. Extract and open

[Download an archive for your operating system](https://github.com/pvnkmrksk/matrexVR/releases/latest). Extract the **entire ZIP** to a writable folder and keep its supporting files together.

| System | Open |
| --- | --- |
| Windows | The `.exe`, beside its `_Data` folder and DLLs. |
| macOS | `MATREX VR.app`. If macOS prompts because the app is not notarized, use Finder's Open action and the corresponding Privacy & Security option. |
| Linux | The `.x86_64` player in a graphical desktop. Restore executable permission if needed. |

You need no Unity Editor or Python to run the player. If you already built the local macOS preview, open **`Preview/Open MATREX VR.command`** instead; it selects the visible `Preview/Config` and `Preview/RunData` folders.

**Checkpoint:** Home/setup is visible. If a saved sequence auto-starts, press **Escape once** to return to setup. Escape from setup quits the player.

## 2. Start the hardware-free demo

On a fresh desktop profile, leave **Night sky** and the flying demonstration selected, then choose **Start demo**. Optional Forest and walking examples live under **Demo options**. Use the keyboard to move through either demonstration.

**Checkpoint:** the environment appears and the run controls are visible. Demonstration mode leaves installed tracking apps and cameras independent.

A configured apparatus can open a different Home screen. To try a disposable fresh profile, launch with separate absolute `--config-dir` and `--data-dir` paths; [launch examples](getting-started.md#configuration-and-data-locations) explain the options. Keep calibrated configurations in their existing directory.

## 3. Move, then stop after 30 seconds

Focus the player. Use the **arrow keys** to move and **A/D** to turn. **Ctrl+Space** toggles forward autopilot; **R** resets the rig. **Tab** hides/restores the run graphics if they obstruct the view.

After about 30 seconds, choose **Back to setup** or press **Escape once**. Wait for this recording to finish saving. In 0.4.1, wait for **Finishing recorders… / Saving recording…** to clear; inspect the error if **Recording needs attention** appears.

**Checkpoint:** setup returns and the recording completes. Pose/configuration recording does not require tracker video. A running scene alone does not confirm that a camera take was recorded.

## 4. Replay the saved run

Choose **Replay** in the sidebar (the page is titled **Replay recordings**), select the newest completed recording and choose **Replay selected**. In older builds, use **Replay recordings** or **Browse…** to open the complete saved folder. If the list has not updated, choose **Refresh** after saving completes.

**Space** pauses/resumes; **Left/Right** seeks one second. Hold **Shift** to seek ten seconds. The default **God's-eye view** shows the trajectory; switch to an animal view to inspect its reconstructed stimulus.

**Checkpoint:** the timeline advances and seeking moves the animal along the recorded trajectory. Missing video can be expected for a demo; inspect the saved evidence before interpreting any absent media. [Replay documentation](replay.md) explains reconstruction limits.

## 5. Find and keep the recording

The library displays its exact RunData root above the list. Normal players save beside the installation in **`MATREX VR RunData`**, with a per-installation application-support fallback if that location is protected. Explicit `--data-dir` overrides this. The local preview launcher uses **`Preview/RunData`**.

Keep the **whole run folder**, including saved configurations, manifests, logs and any media, together with the build that created it. Copying only a CSV loses context needed for replay. [Storage paths](getting-started.md#configuration-and-data-locations) and [data formats](data-formats.md) cover the files.

For a real experiment, continue with [apparatus and tracker setup](getting-started.md#launch-and-configure), verify physical calibration and display routing, then set animal/session labels and save the setup. For agent automation, start at [AGENTS.md](../AGENTS.md) and [JSON/MCP control](ai-control.md).

If a checkpoint fails, use the [startup troubleshooting table](getting-started.md#if-the-first-run-does-not-work). Include the build revision, operating system, visible error and saved run's `runtime_trace.log` when reporting a problem.
