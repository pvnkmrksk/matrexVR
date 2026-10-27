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
                    (output / 'catalog.json').write_text(json.dumps({'catalogId': 'fixture', 'entries': []}))
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

    def test_release_local_opt_ins_preserve_payloads_before_signing_and_describe_each_platform(self):
        """Exercise the real offline viewer extraction and final ZIP, not just flag plumbing."""
        for platform in ('macOS', 'Windows', 'Linux'):
            with self.subTest(platform=platform), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                official, cache = self.fixture_lock(root)
                local_archive = root / 'reviewed-local.zip'
                local_bytes = b'reviewed newer local native fixture'
                with zipfile.ZipFile(local_archive, 'w') as z:
                    info = zipfile.ZipInfo('DaariDeepa/DaariDeepa')
                    info.create_system = 3
                    info.external_attr = (stat.S_IFREG | 0o755) << 16
                    z.writestr(info, local_bytes)
                local_lock = json.loads(dashboard.LOCAL_LOCK.read_text())
                local_lock['archiveSha256'] = companion_bundles.digest(local_archive)
                local_lock['executableSha256'] = hashlib.sha256(local_bytes).hexdigest()
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
                catalog = {'catalogId': 'official', 'entries': [{'backend': 'rekkefly', 'platform': p,
                            'releaseTag': 'official', 'executableSha256': 'official-fixture'}
                            for p in dashboard.platforms_for_target(platform)]}
                def trackers(platforms, output, ignored_cache):
                    events.append('trackers')
                    output.mkdir(parents=True)
                    (output / 'catalog.json').write_text(json.dumps(catalog))
                real_viewer, real_local, real_run = dashboard.stage, dashboard.stage_local, build_release.subprocess.run
                def viewer(platforms, output, ignored_cache):
                    events.append('dashboard')
                    if platform == 'macOS':
                        self.assertEqual(platforms, ['macos-x64'])
                    return real_viewer(platforms, output, cache, official)
                def local(archive, output):
                    events.append('local-dashboard')
                    return real_local(archive, output, local_lock)
                def run(arguments, **kwargs):
                    if arguments[0] == sys.executable and arguments[1].endswith('build_local_rekkefly.py'):
                        self.assertEqual(platform, 'macOS')
                        self.assertEqual(arguments[4:], ['--verified-binary', str(root / 'unsigned-native'),
                                                        '--binary-provenance', str(root / 'native-provenance.json')])
                        events.append('local-native')
                        destination = Path(arguments[3]) / 'catalog.json'
                        corrected = json.loads(destination.read_text())
                        corrected['catalogId'] = 'corrected'
                        corrected['localNativeCorrection'] = {'sourceRevision': 'pinned-native', 'patchSha256': 'pinned-patch'}
                        corrected['entries'][0].update(localSourceBuild=True, executableSha256='corrected-fixture')
                        destination.write_text(json.dumps(corrected))
                        return
                    if arguments[0] == '/usr/bin/codesign':
                        self.assertEqual(events, ['trackers', 'local-native', 'dashboard', 'local-dashboard'])
                        events.append('sign')
                        return
                    return real_run(arguments, **kwargs)
                with patch.object(companion_bundles, 'stage', side_effect=trackers), \
                     patch.object(companion_bundles, 'urlopen', side_effect=AssertionError('unexpected network')), \
                     patch.object(dashboard, 'stage', side_effect=viewer), \
                     patch.object(dashboard, 'stage_local', side_effect=local), \
                     patch.object(build_release.subprocess, 'run', side_effect=run):
                    build_release.package(build, root, '0.3.1', platform, 'frozen-runtime', root, quickstart,
                                          local_native_camera_fix=True, local_dashboard_archive=local_archive,
                                          verified_native_binary=root / 'unsigned-native',
                                          native_binary_provenance=root / 'native-provenance.json')
                label = 'macOS-universal' if platform == 'macOS' else platform
                stage = root / ('MATREX-VR-0.3.1-' + label)
                manifest = json.loads((stage / 'release-manifest.json').read_text())
                self.assertEqual(manifest['sourceRevision'], 'frozen-runtime')
                self.assertEqual(manifest['quickstartSha256'], hashlib.sha256(b'fixture').hexdigest())
                self.assertEqual(manifest['packagingScriptSha256'], companion_bundles.digest(Path(build_release.__file__)))
                viewers = {e['platform']: e for e in manifest['dashboardPackages']}
                if platform == 'macOS':
                    self.assertEqual(manifest['companionCatalogId'], 'corrected')
                    self.assertEqual(manifest['localNativeCameraCorrection']['patchSha256'], 'pinned-patch')
                    self.assertTrue(manifest['companionPackages'][0]['localSourceBuild'])
                    self.assertFalse(manifest['companionPackages'][1].get('localSourceBuild', False))
                    self.assertEqual(viewers['macos-arm64']['sourceRevision'], local_lock['sourceRevision'])
                    self.assertFalse(viewers['macos-arm64']['published'])
                    self.assertEqual(viewers['macos-x64']['sourceRevision'], official['sourceRevision'])
                else:
                    self.assertEqual(events, ['trackers', 'dashboard'])
                    self.assertIsNone(manifest['localNativeCameraCorrection'])
                    self.assertEqual(manifest['companionCatalogId'], 'official')
                    self.assertEqual(next(iter(viewers.values()))['sourceRevision'], official['sourceRevision'])
                with zipfile.ZipFile(root / (stage.name + '.zip')) as archive:
                    embedded = json.loads(archive.read(stage.name + '/release-manifest.json'))
                    self.assertEqual(embedded, manifest)
                    if platform == 'macOS':
                        self.assertEqual(archive.read(stage.name + '/MATREX VR.app/Contents/Companions/dashboard/macos-arm64/DaariDeepa/DaariDeepa'), local_bytes)

    def test_release_local_opt_ins_reject_invalid_requests_before_unity(self):
        validate = build_release.validate_local_payload_options
        for target in ('Linux', 'Windows'):
            with self.assertRaisesRegex(ValueError, 'only to macOS'):
                validate(target, True, True, True, None)
        with self.assertRaisesRegex(ValueError, 'requires bundled companions'):
            validate('macOS', False, True, True, None)
        with self.assertRaisesRegex(ValueError, 'requires the bundled dashboard'):
            validate('macOS', True, False, False, Path('/unused'))
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / 'tampered.zip'
            archive.write_bytes(b'not the reviewed dashboard')
            with self.assertRaisesRegex(ValueError, 'differs from its reviewed lock'):
                validate('macOS', True, True, False, archive)
        with patch.object(build_release.sys, 'platform', 'linux'):
            with self.assertRaisesRegex(ValueError, 'only on Mac arm64'):
                validate('macOS', True, True, True, None)


if __name__ == '__main__':
    unittest.main()
