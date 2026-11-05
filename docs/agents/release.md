# Release and integration agent

Read [repository instructions](../../AGENTS.md), [release history](../releases.md),
the [packaged quickstart](../release-quickstart.txt), and
[native companion provenance](../../tools/native_patches/README.md). A local
preview, a pushed commit, a Git tag, and a GitHub release with desktop assets are
different deliverables; report each accurately.

## Establish the stable revision

1. Inspect `git status`, `git branch -avv`, remotes, tags, and recent history.
   Fetch the remote before deciding what is missing from `main`.
2. Compare candidate changes against the current remote base and their validation
   evidence. Existing dirty runtime work is not automatically ready to publish.
   Integrate complete dependency sets, including new source files and `.meta`
   files, rather than cherry-picking an isolated call site.
3. Use explicit file staging and review `git diff --cached --check` and
   `git diff --cached`. Preserve unrelated user edits. If a clean checkout is
   needed, use a separate worktree; do not discard or stash the shared workspace.
4. Run the focused subsystem checks and packaging checks on the exact candidate
   revision. For a documentation-only patch, describe that scope plainly; do not
   attach older binaries under a new version or claim a fresh player validation.

Determine the next version from actual published releases and tags. Keep the
Unity application version, citation/release metadata, quickstart, and notes
consistent with the intended release scope. Do not move an existing release tag.
When push/publication is already authorized, carry it through after verification
without another confirmation round. Otherwise finish the concrete reviewable
change and request only the missing publication authorization.

## Packaging checks and build

```sh
python3 tools/test_build_release_prepare.py
python3 tools/test_build_identity.py
python3 tools/test_companion_bundles.py
node tools/test_release_downloads.cjs
```

Desktop archives require an activated Unity 6000.3.16f1 Editor with the selected
macOS, Windows Mono, and Linux Mono build modules. `build_release.py` packages the
pinned `--ref`, ignoring unrelated working-tree source edits; it still uses the
current packaging helper/builder, companion locks and selected quickstart. Keep
those inputs under review and retain their hashes/manifests.

For example, substitute the reviewed immutable commit and chosen version:

```sh
python3 tools/build_release.py --ref COMMIT --version X.Y.Z --platform all \
  --local-native-camera-fix \
  --local-dashboard-archive .integration/local-dashboard/DaariDeepa-macos-arm64.zip
```

The reviewed dashboard archive and Rust/native prerequisites are explained in
the companion guide. Choose a fresh `--workspace` for a rebuild; the helper
refuses to reuse an output directory. It emits ZIPs, release manifests, and
SHA-256 files under `.release-work/release-X.Y.Z/` by default. It never uploads.
`build_local_desktop.py` instead snapshots working-tree source into `Preview/`
and is for local review, not proof of a pinned public build.

Inspect archive inventory, hashes, version/build identity, companion provenance,
and the included `START-HERE.txt`. Smoke-test the actual player. Distinguish a
cross-build from a native walkthrough and retain known platform/hardware limits.

## Publish and verify

Push stable commits to the authorized branch with ordinary fast-forward safety;
do not force-update shared history. Create the version tag on the exact tested
commit and publish notes that state behavior, evidence, and actual asset scope.
For desktop releases, upload the newly built OS archives and SHA-256 files.
For source/documentation releases, state that no new desktop archives are
included. Verify the remote branch SHA, tag target, release page, and uploaded
asset names after publication.

GitHub Pages deploys committed `docs/site` content on matching pushes to `main`.
Regenerate manual sources before pushing and check the deployment when website
changes are part of the release. A release is complete only when the requested
remote state and intended published artifacts are present, or a specific external
blocker has been reported with the completed local work preserved.
