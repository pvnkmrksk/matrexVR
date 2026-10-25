#!/usr/bin/env python3
"""Local source patch provenance and staging checks; never opens a tracker."""
import copy
import json
from pathlib import Path
import plistlib
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import build_local_rekkefly as native
import companion_bundles


class LocalNativeTests(unittest.TestCase):
    def test_pinned_patch_applies_to_exact_upstream_and_detects_modified_source(self):
        lock, patch_file = native.load_lock()
        repository = native.ROOT.parent / 'rekkefly'
        if not repository.is_dir():
            self.skipTest('This offline source check needs the existing upstream clone')
        before = subprocess.check_output(['git', '-C', str(repository), 'status', '--porcelain'])
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / 'source'
            native.prepare_source(repository, output, lock, patch_file)
            self.assertIn('no_camera_fallback', (output / 'src/main.rs').read_text())
            self.assertIn('supported_formats()', (output / 'src/camera.rs').read_text())
            self.assertEqual(native.prepare_source(repository, output, lock, patch_file), output)
            with (output / 'src/camera.rs').open('a') as stream:
                stream.write('// unreviewed change\n')
            with self.assertRaises(ValueError):
                native.prepare_source(repository, output, lock, patch_file)
        self.assertEqual(subprocess.check_output(['git', '-C', str(repository), 'status', '--porcelain']), before)

    def test_local_overlay_preserves_other_platforms_and_does_not_claim_official_archive(self):
        lock, patch_file = native.load_lock()
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            companions = root / 'Companions'
            package = companions / 'rekkefly/v0.6.1/macos-arm64/upstream'
            resources = package / 'Rekkefly.app/Contents/Resources'
            (resources / 'configs').mkdir(parents=True)
            (resources / 'configs/demo.toml').write_text('kind="demo"')
            (resources / 'configs/camera-a.toml').write_text('kind="camera"')
            (resources / 'LICENSE').write_text('upstream license')
            executable = package / 'Rekkefly.app/Contents/MacOS/rekkefly'
            executable.parent.mkdir()
            executable.write_bytes(b'upstream executable')
            (package / 'rekkefly').write_bytes(b'upstream executable')
            with (package / 'Rekkefly.app/Contents/Info.plist').open('wb') as stream:
                plistlib.dump({'CFBundleIdentifier': 'unchanged.identity', 'CFBundleVersion': '0.6.0'}, stream)
            details = companion_bundles.describe(package, 'rekkefly', 'macos-arm64')
            entry = dict(backend='rekkefly', platform='macos-arm64', releaseTag='v0.6.1', archiveSha256='a' * 64,
                         releaseUrl='https://example.invalid/official', packageRoot=package.relative_to(companions).as_posix(), **details)
            unrelated = dict(backend='rekkefly', platform='macos-x64', executableSha256='b' * 64, releaseTag='v0.6.1')
            catalog = {'schemaVersion': 1, 'catalogId': 'original', 'entries': [entry, unrelated]}
            (companions / 'catalog.json').write_text(json.dumps(catalog))
            binary = root / 'locally-built-rekkefly'
            binary.write_bytes(b'local binary from locked patch')
            with patch.object(native.subprocess, 'run') as signing:
                staged = native.stage(companions, binary, lock, patch_file)
                self.assertEqual(signing.call_count, 2)
                self.assertNotIn('--deep', signing.call_args_list[0].args[0])
                self.assertEqual(native.stage(companions, binary, lock, patch_file), staged)
            actual = json.loads((companions / 'catalog.json').read_text())
            self.assertEqual(actual['entries'][1], unrelated)
            self.assertEqual(staged['sourceRevision'], lock['sourceRevision'])
            self.assertEqual(staged['patchSha256'], lock['patchSha256'])
            self.assertTrue(staged['supportsNoCameraFallback'])
            self.assertTrue(staged['supportsSourceProvenance'])
            self.assertNotIn('archiveSha256', staged)
            self.assertEqual(staged['upstreamArchiveSha256'], 'a' * 64)
            self.assertTrue(staged['releaseTag'].startswith('local-'))
            self.assertNotEqual(actual['catalogId'], 'original')
            local = companions / staged['packageRoot']
            self.assertEqual((local / staged['executable']).read_bytes(), binary.read_bytes())
            self.assertEqual((local / 'Rekkefly.app/Contents/Resources/LICENSE').read_text(), 'upstream license')
            with (local / 'Rekkefly.app/Contents/Info.plist').open('rb') as stream:
                info = plistlib.load(stream)
            self.assertEqual(info['CFBundleIdentifier'], 'unchanged.identity')
            self.assertEqual(info['CFBundleVersion'], lock['sourceVersion'])
            binary.write_bytes(b'different source build')
            with self.assertRaises(ValueError):
                native.stage(companions, binary, lock, patch_file)


if __name__ == '__main__':
    unittest.main()
