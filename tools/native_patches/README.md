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
Acknowledged per-experiment tracker recording Start/Stop and manual override
remain follow-up work; no upstream tracker source update is part of this release.
