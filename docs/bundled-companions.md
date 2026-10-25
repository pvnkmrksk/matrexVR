# Bundled native tracking apps

Desktop packages include pinned native Rekkefly and HejjéTrac packages. The
quick-start workflow chooses flying or walking and distinguishes **demo input**
from a real camera. Flying demo uses the native synthetic source; walking demo
uses the attributed recorded sample shipped by HejjéTrac. Neither is a measured
animal. Live use needs the operator's camera and calibration.

Walking demo is a short, finite recorded video, so its motion stream stops at
end-of-file while the independent tracker window remains open. In HejjéTrac,
use **More → Rig manager… → Start all** to replay a finished source in the same app. **Reset
tracking** resets the estimator without rewinding the video. The bundled
v0.1.4 documentation does not expose an automatic replay-loop option; MATREX
does not simulate new walking measurements or spawn replacement apps at EOF.

Bundled flight camera mode requires a tracker that supports camera-only
acquisition. Earlier official Rekkefly packages can fall back to an example
video without identifying that substitution in their legacy ZMQ messages, so
MATREX refuses to launch those packages as camera input. Their explicitly
selected demo remains usable. An external tracker can still be configured by
an operator who verifies its actual source.

The tracker window is a separate native process. Closing or crashing Unity
does not close it, and a tracker failure cannot terminate Unity. Starting the
same rig again reuses its existing process, including after restarting Unity.
There is no stdin supervision pipe, parent-death signal, or automatic Stop on
scene/system changes. Complete Mac app bundles open through macOS Launch
Services, so the tracker has its own application/privacy identity and the
receipt records its actual native PID. Directly executing a camera app as a
Unity child can make macOS attribute camera access to Unity and terminate it
for a missing camera usage description. Camera-only launch therefore requires
the complete `.app`, and the manager never substitutes a child-process launch
when Launch Services reports an error. The implementation uses Apple’s [NSWorkspace app-launch API](https://developer.apple.com/documentation/appkit/nsworkspace/launchapplication(at:options:configuration:)) and its [literal argument/environment configuration](https://developer.apple.com/documentation/appkit/nsworkspace/launchconfigurationkey). Other POSIX native programs use a fixed
exec wrapper with literal positional arguments, stdin disconnected and output
in a per-rig diagnostic log; they do not inherit Unity's output pipe or terminal
hangup. Windows uses
the native ShellExecute process launch API, never a command interpreter. Close the tracker window, or use an explicit Stop, when
finished. A mode/source/configuration change while that rig's app is running
requires an explicit Stop first, so an old demo cannot silently become a live
session or compete for the same port.

The Mac launcher uses the synchronous
[`NSWorkspace.launchApplication(at:options:configuration:)`](https://developer.apple.com/documentation/appkit/nsworkspace/launchapplication(at:options:configuration:))
API, supported since macOS 10.6 and deprecated in favor of the asynchronous
configuration API. It passes a native string array and requests a new app
instance for each explicitly launched rig; persisted rig receipts prevent
duplicates. A windowless fixture verified literal arguments, actual PID,
explicit environment support and survival after launcher exit. Production
launches provide no custom environment override. This fixture does not request
camera permission.

## Package selection and provenance

`Assets/StreamingAssets/Companions/companions.lock.json` pins exact release
asset URLs and the SHA-256 digests published by GitHub. Runtime does not
contact GitHub, download executables, unpack archives repeatedly, or select a
moving latest version. A maintainer explicitly refreshes the lock, tests the
selected packages, then builds the desktop package:

```sh
python3 tools/companion_bundles.py --refresh-lock
python3 tools/build_release.py --ref HEAD --version VERSION --platform all
```

Bundling is the default; `--without-companions` is an advanced source-build
opt-out for trackers. The dashboard is independently bundled by default;
`--without-dashboard` omits it. Staging directly for a local Mac build is also supported:

```sh
python3 tools/companion_bundles.py --platform macOS \
  --output '/path/to/MATREX VR.app/Contents/Companions'
```

The universal Mac payload contains arm64 and x64 apps; the running operating
system chooses the matching entry. Windows and Linux packages contain their
x64 apps. Every archive and extracted executable is hashed. Whole upstream
packages, licenses, notices and attributed samples are retained. The initial
lock uses the latest stable Rekkefly release tag `v0.6.1` and HejjéTrac `v0.1.4`.
Rekkefly's `v0.6.1` release assets are named `0.6.0` and that executable reports
`0.6.0`; the catalog preserves the actual release URL, asset and executable
hash rather than inventing a binary version.

The released Mac flight app was tested with explicit synthetic input against
both the shipped NetMQ client and modern libzmq. It also passed 40 repeated
client connect/dispose cycles with fresh messages after every reconnect. These
are protocol/software tests, not physical-camera accuracy claims.

## Bundled live dashboard

The same desktop download contains Daari Deepa with its Python interpreter,
server and web assets. It starts as a separate process on explicit request and
can also be hosted on another computer. It does not run analysis or plotting
inside Unity's rendering loop.

`Assets/StreamingAssets/Companions/dashboard.lock.json` pins the official
continuous build from source revision `b0f36d231424af6ad19ce264376515fdaf2e78c6`.
This is a tested source build, not an invented release tag. The lock retains
the published `SHA256SUMS.txt`, its digest, and exact archive URLs and hashes.
The official download URLs can move; changed upstream bytes make packaging
fail verification until a maintainer deliberately updates and tests the lock.
App startup never downloads or selects a newer package.

```sh
# Defaults to both Mac architectures, preserving the local build command.
python3 tools/stage_local_dashboard.py --output '/path/to/Companions'
# Other build targets: Windows, Linux, all, or a single platform key.
python3 tools/stage_local_dashboard.py --platform Windows --output '/path/to/Companions'
```

The dashboard is staged under `Companions/dashboard/<platform>/`. Each marker
records source and checksum provenance plus the executable's SHA-256. Windows
uses `DaariDeepa/DaariDeepa.exe`; Mac and Linux use `DaariDeepa/DaariDeepa`.
The whole upstream application directory is preserved. Build caches contain
verified archives only. Existing staged binaries with changed digests are
rejected. The release manifest lists the bundled dashboard packages, and Mac
parent signing happens after both trackers and dashboard have been embedded.

## Local Mac camera correction

The local Apple Silicon build applies the checked-in
`tools/native_patches/rekkefly-macos.patch` to upstream source commit
`f9a9ba71619b86becf0fb1ce1fc2cf5aa3d851bb`. Its lock records the exact patch
digest. This produces a **local source build**, identified by a `local-…` tag
and source/patch provenance; it is not relabeled as an official release.
Other platform packages retain their original bytes and capabilities.

The correction enumerates Mac camera descriptors before constructing a camera
and retries exact ranked modes. The previous constructor selected a 15 FPS
range endpoint that its Mac binding rejected before reaching the retry loop.
It also adds `--no-camera-fallback`, which fails camera acquisition without
stdin supervision. A normal GUI fallback remains a preview with all outputs
disabled. Engine ZMQ messages append `source_kind` (`camera`, `video`, `demo`
or `network`) and `source_fallback`; legacy numerical fields retain their
meaning. Messages are emitted only after the actual source opens and a frame
is processed.

The patched Apple Silicon app was checked with the physical FaceTime HD
camera: an independent ZMQ receiver received 30 consecutive decoded messages
(frames 1117–1146 over 0.970308 seconds), all marked `source_kind="camera"`
and `source_fallback=false`. The signed executable digest was
`19353afdaaea489f3108aacca937e15ae5ed869a3d43d27d0dfb177ff03191f3`.
This validates camera acquisition, decoding and transport. The scene was not
an independently calibrated animal-wing experiment and establishes no
biological tracking accuracy. Windows, Linux and Intel Mac camera acquisition
are outside this local correction's physical test coverage.

Maintainers building this local correction need Git, the current stable Rust
toolchain and the matching Unity Editor/build module. Downloaded desktop apps
include the native binaries and need neither Rust nor Python installed.
`tools/build_local_desktop.py` builds this correction by default on Apple
Silicon, after staging the official base packages and before sealing the parent
app. It reads a suitable sibling `rekkefly` clone without modifying it, or
fetches the exact pinned commit into a disposable `.integration` cache.

```sh
python3 tools/build_local_rekkefly.py --companions '/path/to/Companions'
```

The helper verifies the patch digest and prepared source bytes, builds with
the locked Cargo dependencies, preserves upstream resources/licenses, seals
the modified nested app and updates only the Mac arm64 catalog entry. The
original upstream archive hash is retained as **upstream** provenance; the
assembled local package does not claim to match that official archive.
`--skip-native-camera-fix` is a development-only local-build opt-out and leaves
bundled flight camera mode unavailable. No new release is published by these
helpers.

## Stable app identity and editable calibration

On first use, the selected app is copied once into a versioned directory under
Unity's per-user persistent-data directory, inside `Companions/`. Subsequent
starts reuse the same verified executable path. Configurations are stored
separately under the writable config root's `Trackers/` folder. Each rig and
demo/camera source has its own configuration; an existing calibration is never
reseeded from a release template. A generated walking profile's local ZMQ host
and port follow the rig without renormalizing its geometry or other fields.
File creation/replacement is atomic, and concurrent calibration changes cause
an actionable error instead of being overwritten.

Persisted private process receipts include the exact PID, process start time,
executable identity and source/configuration. Launch is guarded by a per-rig
exclusive file lock. A PID alone is never sufficient authorization to stop a
process; an unrelated instance of the same executable remains untouched.

First-install copying and hashing run off Unity's UI thread through
`CompanionCatalog.PrepareAsync`. The UI renders its starting state before
awaiting preparation, then launches the prepared native app on the main thread.
Preparing files does not itself open a camera or start an app.

## Signing order

The upstream app bundles and their current signatures are preserved. Embedding
companions changes Unity's resource tree, so the parent Mac app is sealed only
after all companions and its catalog are in place. The build tool makes an
ad-hoc parent seal for local testing; this is not an Apple Developer ID or
notarization claim.

For a future Developer ID distribution, sign nested apps first. If intentional
signing changes their executable bytes, update catalog executable digests while
retaining the original archive hashes:

```sh
python3 tools/companion_bundles.py --seal-catalog \
  '/path/to/MATREX VR.app/Contents/Companions'
```

Then sign/seal the parent app and archive it. Do not modify nested executables
after hashing and sealing. Signing/Camera permission still belongs to macOS;
the application does not strip quarantine or bypass privacy settings.
