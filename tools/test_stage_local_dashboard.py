#!/usr/bin/env python3
"""Dashboard packaging provenance and platform checks; no native app execution."""
import copy
import hashlib
import json
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_release
import companion_bundles
import stage_local_dashboard as dashboard


class DashboardPackagingTests(unittest.TestCase):
    def fixture_lock(self, root):
        cache = root / 'cache'
        cache.mkdir()
        lock = copy.deepcopy(dashboard.load_lock())
        manifest = ''
        for platform, asset in lock['platforms'].items():
            archive = root / asset['asset']
            with zipfile.ZipFile(archive, 'w') as z:
                for name, data in {
                    asset['executable']: ('native fixture ' + platform).encode(),
                    'DaariDeepa/_internal/licenses/LICENSE': b'verbatim upstream license',
                    'DaariDeepa/_internal/runtime.dat': b'whole Python runtime fixture',
                }.items():
                    info = zipfile.ZipInfo(name)
                    info.create_system = 3
                    info.external_attr = (stat.S_IFREG | 0o755) << 16
                    z.writestr(info, data)
            asset['sha256'] = companion_bundles.digest(archive)
            asset['url'] = 'invalid://offline-only/' + asset['asset']
            archive.rename(cache / (asset['sha256'] + '-' + asset['asset']))
            manifest += asset['sha256'] + '  ' + asset['asset'] + '\n'
        lock['checksumManifest'] = manifest
        lock['checksumManifestSha256'] = hashlib.sha256(manifest.encode()).hexdigest()
        return lock, cache

    def test_official_pin_and_target_mapping(self):
        lock = dashboard.load_lock()
        self.assertEqual(lock['sourceRevision'], 'b0f36d231424af6ad19ce264376515fdaf2e78c6')
        self.assertEqual(dashboard.platforms_for_target('macOS'), ['macos-arm64', 'macos-x64'])
        self.assertEqual(dashboard.platforms_for_target('Windows'), ['windows-x64'])
        self.assertEqual(dashboard.platforms_for_target('Linux'), ['linux-x64'])
        self.assertEqual(dashboard.platforms_for_target('all'), list(dashboard.PLATFORMS))
        with self.assertRaises(ValueError):
            dashboard.platforms_for_target('unknown')
        for platform, asset in lock['platforms'].items():
            self.assertEqual(asset['url'], 'https://pvnkmrksk.github.io/trajectory-dashboard/DaariDeepa-' + platform + '.zip')
            self.assertEqual(asset['executable'], 'DaariDeepa/DaariDeepa' + ('.exe' if platform == 'windows-x64' else ''))

    def test_modified_checksums_and_asset_pin_are_rejected(self):
        for field in ('checksumManifest', 'asset'):
            with self.subTest(field=field):
                lock = copy.deepcopy(dashboard.load_lock())
                if field == 'checksumManifest':
                    lock[field] += '\n'
                else:
                    lock['platforms']['linux-x64']['sha256'] = '0' * 64
                with self.assertRaises(ValueError):
                    dashboard.validate_lock(lock)

    def test_all_platforms_stage_offline_preserving_runtime_and_provenance(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            lock, cache = self.fixture_lock(root)
            output = root / 'Companions'
            with patch.object(companion_bundles, 'urlopen', side_effect=AssertionError('unexpected network')):
                entries = dashboard.stage(list(dashboard.PLATFORMS), output, cache, lock)
                for info in entries:
                    directory = output / 'dashboard' / info['platform']
                    executable = directory / info['executable']
                    self.assertEqual(info['executableSha256'], companion_bundles.digest(executable))
                    self.assertEqual(info['sourceRevision'], lock['sourceRevision'])
                    self.assertEqual(info['checksumManifestSha256'], lock['checksumManifestSha256'])
                    self.assertEqual((directory / 'DaariDeepa/_internal/licenses/LICENSE').read_bytes(), b'verbatim upstream license')
                    self.assertTrue((directory / 'DaariDeepa/_internal/runtime.dat').is_file())
                    self.assertEqual(json.loads((directory / 'dashboard-bundle.json').read_text()), info)
                self.assertEqual(dashboard.stage(list(dashboard.PLATFORMS), output, cache, lock), entries)
            self.assertEqual(json.loads((output / 'dashboard/dashboard.lock.json').read_text()), lock)
            self.assertEqual((output / 'dashboard/SHA256SUMS.txt').read_text(), lock['checksumManifest'])
            self.assertFalse(list((output / 'dashboard').glob('*.staging-*')))

    def test_corrupt_output_is_not_reused_or_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            lock, cache = self.fixture_lock(root)
            output = root / 'Companions'
            info = dashboard.stage(['linux-x64'], output, cache, lock)[0]
            executable = output / 'dashboard/linux-x64' / info['executable']
            executable.write_bytes(b'modified')
            with self.assertRaises(FileExistsError):
                dashboard.stage(['linux-x64'], output, cache, lock)
            self.assertEqual(executable.read_bytes(), b'modified')

    def test_reviewed_local_payload_replaces_only_arm64_and_preserves_provenance(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            official, cache = self.fixture_lock(root)
            output = root / 'Companions'
            dashboard.stage(list(dashboard.PLATFORMS), output, cache, official)
            untouched = {p: (output / 'dashboard' / p / 'dashboard-bundle.json').read_bytes()
                         for p in dashboard.PLATFORMS if p != 'macos-arm64'}
            lock = json.loads(dashboard.LOCAL_LOCK.read_text())
            asset = official['platforms']['macos-arm64']
            archive = cache / (asset['sha256'] + '-' + asset['asset'])
            lock['archiveSha256'] = asset['sha256']
            lock['executableSha256'] = companion_bundles.digest(output / 'dashboard/macos-arm64' / lock['executable'])
            info = dashboard.stage_local(archive, output, lock)
            self.assertFalse(info['published'])
            self.assertEqual(info['sourceRevision'], lock['sourceRevision'])
            self.assertEqual(dashboard.stage_local(archive, output, lock), info)
            for platform, before in untouched.items():
                self.assertEqual((output / 'dashboard' / platform / 'dashboard-bundle.json').read_bytes(), before)
            marker = output / 'dashboard/macos-arm64/dashboard-bundle.json'
            before = marker.read_bytes()
            for field in ('archiveSha256', 'executableSha256'):
                bad = dict(lock, **{field: '0' * 64})
                with self.assertRaises(ValueError):
                    dashboard.stage_local(archive, output, bad)
                self.assertEqual(marker.read_bytes(), before)
            self.assertFalse(list((output / 'dashboard').glob('*.local-*')))

    def test_release_defaults_embed_dashboard_before_mac_parent_signing(self):
        for platform in ('macOS', 'Windows', 'Linux'):
            with self.subTest(platform=platform), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                build = root / 'build'
                build.mkdir()
                player = build / build_release.TARGETS[platform][2]
                if platform == 'macOS':
                    (player / 'Contents').mkdir(parents=True)
                else:
                    player.write_bytes(b'Unity fixture')
                (build / 'build-report.json').write_text('{"result":"Succeeded","errors":0}')
                quickstart = root / 'quickstart.txt'
                quickstart.write_text('fixture')
                events = []
                def trackers(platforms, output, cache):
                    events.append(('trackers', platforms, output))
                    output.mkdir(parents=True)
                def viewer(platforms, output, cache):
                    self.assertTrue(output.is_dir())
                    events.append(('dashboard', platforms, output))
                    return [{'sourceRevision': 'fixture', 'platform': p} for p in platforms]
                def run(arguments, **kwargs):
                    if arguments[0] == '/usr/bin/codesign':
                        self.assertEqual([e[0] for e in events], ['trackers', 'dashboard'])
                        events.append(('sign',))
                        return
                    # Let ditto create the archive; only parent signing is mocked.
                    return real_run(arguments, **kwargs)
                real_run = build_release.subprocess.run
                with patch.object(companion_bundles, 'stage', side_effect=trackers), \
                     patch.object(dashboard, 'stage', side_effect=viewer), \
                     patch.object(build_release.subprocess, 'run', side_effect=run):
                    build_release.package(build, root, '0.0.0', platform, 'fixture', root, quickstart)
                name = 'MATREX-VR-0.0.0-' + ('macOS-universal' if platform == 'macOS' else platform)
                stage = root / name
                manifest = json.loads((stage / 'release-manifest.json').read_text())
                self.assertTrue(manifest['dashboardBundled'])
                self.assertTrue(manifest['companionsBundled'])
                self.assertEqual([e['platform'] for e in manifest['dashboardPackages']], dashboard.platforms_for_target(platform))
                expected = stage / 'MATREX VR.app/Contents/Companions' if platform == 'macOS' else stage / 'Companions'
                self.assertEqual(events[0][2], expected)
                self.assertEqual(events[1][2], expected)

    def test_release_opt_out_does_not_fetch_dashboard(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            build = root / 'build'
            build.mkdir()
            (build / 'MATREX VR.x86_64').write_bytes(b'Unity fixture')
            (build / 'build-report.json').write_text('{"result":"Succeeded","errors":0}')
            quickstart = root / 'quickstart.txt'
            quickstart.write_text('fixture')
            with patch.object(dashboard, 'stage', side_effect=AssertionError('dashboard opt-out ignored')):
                build_release.package(build, root, '0.0.0', 'Linux', 'fixture', root, quickstart,
                                      with_companions=False, with_dashboard=False)
            manifest = json.loads((root / 'MATREX-VR-0.0.0-Linux/release-manifest.json').read_text())
            self.assertFalse(manifest['dashboardBundled'])
            self.assertFalse(manifest['companionsBundled'])
            self.assertEqual(manifest['dashboardPackages'], [])


if __name__ == '__main__':
    unittest.main()
