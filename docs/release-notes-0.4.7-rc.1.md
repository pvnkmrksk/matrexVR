# MATREX VR 0.4.7-rc.1

This release candidate improves replay of large recording folders and makes the saved evidence easier to find and interpret. It retains the 16-rig desert sequence and archived sky replay introduced in 0.4.6 and 0.4.5. [Desert sequence](desert-locust-experiment.md) · [Replay views](replay.md)

## Recordings and replay

- **Portable folder layout.** The Linux launcher uses `Config/`, `RunData/`, `Cache/`, and `Preferences/` beside the launcher. Run `./Open\ MATREX\ VR.sh --show-paths` to print the absolute defaults without starting the player. Replay displays the active recording root and can open older saved folders in place.
- **A guide with the data.** The package includes `RUN-DATA.md`. The recording library and newly created run folders include a `README.md` that explains the files, manifest fields, CSV columns, binary streams, clocks, optional video, and verification and export commands. [Run-data guide](../Assets/StreamingAssets/RunData_README.md)
- **Faster repeated playback.** Replay shows progress through each loading stage. It can reuse a local, lossless `.matpose` projection of the required animal fields. Animal CSVs remain the full permanent record. The local cache is generated during finalization or on demand when opening a saved recording.
- **Smaller population streams.** New `MATPOP02` recordings use independently compressed blocks with a frame index. `MATPOP01` and historical CSV/gzip population streams remain readable. The Python exporter can write ordinary CSV from either binary version. [Recorded data and units](data-formats.md)
- **Preserving evidence when a run is incomplete.** Population readers open files as needed, and playback can use surviving tracks from a partial copy. The player reports gaps and missing evidence. The creating build remains the best reference for full replay. [Replay contract](same-build-replay-contract.md)

## Validation scope

Pre-integration Linux workstation validation used existing real recording folders and separate writable profiles. On the full 2-hour, 16-rig November 24 run, all 7,362,176 decoded replay pose rows matched their CSV counterparts. The local index occupied about 174 MB. The cached desktop replay opened in about 39 seconds. The 1,090 source files matched the original SHA-256 inventory after validation. The earlier preview also exercised historical runs and a reduced recording copy. Synthetic population tests checked lossless decode, frame seeking, and bounded recording with 16 streams of 200 agents at 60 Hz.

The package's release verification records its exact source revision and archive checksums. Physical tracker calibration, camera and LED timing, and long-duration acquisition remain apparatus-specific checks.
