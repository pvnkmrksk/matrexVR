# Reviewed local companion builds

`build_local_desktop.py` installs the pinned Mac arm64 camera correction and the
reviewed local dashboard before signing the parent app. These are local source
builds, not published upstream releases. Other platform dashboard packages retain
the official lock in `Assets/StreamingAssets/Companions/dashboard.lock.json`.

The current dashboard archive is cached at
`.integration/local-dashboard/DaariDeepa-macos-arm64.zip`. Its complete archive
and executable hashes must match `dashboard-macos.lock.json`; staging fails on a
mismatch. The installed `dashboard-bundle.json` records the source revision,
hashes, `buildType: local-source`, and `published: false`. Runtime users need no
Python installation and the app downloads nothing.

To rebuild the dashboard for maintainer review, use a clean checkout of the
`sourceRevision` in that lock from
https://github.com/pvnkmrksk/trajectory-dashboard. On an arm64 Mac, create a
CPython 3.10 environment, install `requirements-native.txt` and
`PyInstaller==6.22.3`, then run from that checkout:

```sh
python -m PyInstaller --noconfirm --clean --distpath /tmp/daari-review/dist --workpath /tmp/daari-review/build daari-deepa.spec
python tools/smoke_native.py /tmp/daari-review/dist/DaariDeepa/DaariDeepa
ditto -c -k --keepParent /tmp/daari-review/dist/DaariDeepa /tmp/DaariDeepa-macos-arm64.zip
```

Review the live plots and native smoke results before updating the lock's two
hashes and copying that archive into the cache. PyInstaller archives are not
assumed to be byte-for-byte reproducible across build machines. An absent cached
archive stops the local desktop builder instead of silently bundling the older
plots. `--local-dashboard-archive` selects a different location for the same
reviewed bytes; `--skip-local-dashboard` explicitly opts into the older published
viewer during development.

The 0.4 source pins Daari Deepa `1486b81e`, including the independent recorded
trajectory viewer and session-bound replay clock. It was built from an isolated
source archive and passed native live/analysis and recorded-control smokes before
pinning. Only this reviewed Mac arm64 payload includes the replay API/assets;
older pinned Intel Mac, Windows and Linux dashboards report the capability limit.
Unity’s internal pose/video replay does not depend on the external viewer.
Published MATREX VR 0.3.1 keeps its earlier `f122518a` payload; updating this lock
does not change that release.

The 0.4 all-platform packaging command must include both
`--local-native-camera-fix` and
`--local-dashboard-archive .integration/local-dashboard/DaariDeepa-macos-arm64.zip`.
The release manifest distinguishes the reviewed local builds from unchanged
upstream packages. Other architectures retain their older pinned strict flight
camera block. The pinned Linux HejjéTrac binary requires glibc 2.39 or newer;
cross-building the Unity player does not establish native Linux execution.
The historical 0.4 release did not include acknowledged per-experiment tracker recording. The current local adapters below add it without changing that published release.


## Experiment recording adapters (local Mac arm64)

The current `rekkefly-macos.patch` combines the existing exact-camera correction with an opt-in native-GUI mailbox and per-take completion receipts. `hejjetrac-recording-macos.patch` adds the same reconnectable endpoint to the audited HejjéTrac desktop worker/finalizer. Their JSON locks pin the exact upstream commits, full patch hashes, changed-file inventory and capabilities. The flight detector and walking numerical core are unchanged. No upstream publication or user checkout mutation is required.

`build_local_desktop.py` installs both adapters on Mac arm64 by default. Each helper extracts the pinned Git source into a marked disposable folder, checks/applies the locked patch, builds with locked Cargo dependencies, preserves attributed native resources, signs the resulting app and seals catalog executable hashes. HejjéTrac resources come from its checksum-pinned v0.3.2 archive. Other-platform catalog entries stay unchanged and never advertise automatic recording capability. Runtime performs no downloads.

An explicit prior unsigned build can be reused only with its matching `MATREX-NATIVE-PROVENANCE.json` (or equivalent saved build receipt) through `--verified-native-binary FILE --native-binary-provenance JSON` for Rekkefly and `--verified-hejjetrac-binary FILE --hejjetrac-binary-provenance JSON` for HejjéTrac. The helpers still verify source and patch integrity before accepting matching executable bytes. New source/patch identities cannot reuse an older binary receipt. Development-only opt-outs are `--skip-native-camera-fix` and `--skip-hejjetrac-recording-control`; automatic recording then reports unavailable for that backend while VR startup continues.

The endpoint uses local files, instance nonces, process IDs, unique request/reply IDs and bounded expiry. A Start queue reply is followed by fresh-directory and first-video-frame verification. Stop includes the expected owned take directory and a stop ID; only the capture worker can stamp that ID into successful completion. Receipts include accepted/written or submitted/encoded/pending counts, container finalization, errors and capture pauses. Never infer closure from inactive status, stable file size or a delay. Manual overrides stay visible and never trigger an automatic restart.

Validation tools: `tools/test_native_recording_control.py` (pure production client or an explicitly supplied real endpoint), `tools/test_build_local_rekkefly.py`, `tools/test_build_local_hejjetrac.py`, and the disposable Unity `NativeRecordingLifecycleValidation`. See [the integration audit](../../docs/recording-control-20261027.md) for executed scope and remaining physical-camera/platform limits.
