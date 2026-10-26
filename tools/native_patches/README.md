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

The development source now pins Daari Deepa `a5456e33`, adding the independent
recorded trajectory viewer and a session-bound replay clock. It was built from
an isolated source archive and passed native live/analysis and recorded-control
smokes before pinning. Published MATREX VR 0.3.1 keeps its earlier `f122518a`
payload; updating this development lock does not change that release.
