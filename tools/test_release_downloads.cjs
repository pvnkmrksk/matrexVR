// Release links must never point to a guessed, incomplete or source-only artifact.
const assert = require('node:assert/strict');
const { availableReleases, platformAsset, formatSize } = require('../docs/site/release-downloads.js');
const asset = (name, state = 'uploaded', size = 4096) => ({ name, state, size,
  browser_download_url: 'https://github.com/pvnkmrksk/matrexVR/releases/download/v0.1.0/' + name });
const baseline = { draft: false, prerelease: false, published_at: '2026-10-25T11:00:00Z', tag_name: 'v0.1.0', assets: [
  asset('MATREX-VR-0.1.0-Windows-x86_64.zip'), asset('MATREX-VR-0.1.0-macOS-universal.zip'), asset('MATREX-VR-0.1.0-Linux-x86_64.zip'), asset('MATREX-source.zip')
] };
for (const [os, filename] of Object.entries({ windows: 'MATREX-VR-0.1.0-Windows-x86_64.zip', macos: 'MATREX-VR-0.1.0-macOS-universal.zip', linux: 'MATREX-VR-0.1.0-Linux-x86_64.zip' }))
  assert.equal(platformAsset(baseline, os).name, filename);
for (const invalid of [asset('Windows.zip', 'starter'), asset('Windows.zip', 'uploaded', 0), asset('Windows.tar.gz'),
  { ...asset('Windows.zip'), browser_download_url: 'https://example.com/fake.zip' }])
  assert.equal(platformAsset({ ...baseline, assets: [invalid] }, 'windows'), null);
assert.equal(platformAsset({ ...baseline, assets: [asset('MATREX-macOS.zip'), asset('MATREX-macOS-universal.zip')] }, 'macos').name, 'MATREX-macOS-universal.zip');
assert.equal(platformAsset(baseline, 'unknown'), null);
assert.equal(platformAsset({ assets: [] }, 'linux'), null);
assert.equal(platformAsset({ assets: 'malformed' }, 'linux'), null);
assert.equal(availableReleases({}).length, 0);
assert.deepEqual(availableReleases([baseline, { ...baseline, tag_name: 'v0.10.0' }, { ...baseline, tag_name: 'v0.2.0' },
  { ...baseline, draft: true }, { ...baseline, prerelease: true }, { ...baseline, published_at: null }]).map(r => r.tag_name), ['v0.10.0', 'v0.2.0', 'v0.1.0']);
assert.equal(formatSize(2 * 1024 * 1024), '2 MB');
assert.equal(formatSize(2 * 1024 * 1024 * 1024), '2.0 GB');
console.log('Release downloads: 15 assertions passed.');
