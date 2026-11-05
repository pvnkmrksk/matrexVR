#!/usr/bin/env python3
"""Pinned native recording overlay boundaries; never launches a tracker."""
import json
from pathlib import Path
import plistlib
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import build_local_hejjetrac as native
import build_local_rekkefly as shared
import companion_bundles as bundles


class HejjeOverlayTests(unittest.TestCase):
    def test_exact_source_patch_applies_without_touching_the_clone(self):
        repository = native.ROOT / '.integration/hejjetrak-recording-20261027'
        if not repository.is_dir():
            self.skipTest('Offline source check needs the audited upstream snapshot')
        lock, patch_file = native.load_lock()
        before = subprocess.check_output(['git', '-C', str(repository), 'status', '--porcelain'])
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / 'source'
            shared.prepare_source(repository, source, lock, patch_file)
            self.assertIn('owned-take-stop-v1', (source / 'apps/fictrac-desktop/src/native_mailbox.rs').read_text())
            self.assertIn('control_stop_id', (source / 'apps/fictrac-desktop/src/runtime.rs').read_text())
            shared.prepare_source(repository, source, lock, patch_file)
            (source / 'apps/fictrac-desktop/src/native_mailbox.rs').write_text('modified')
            with self.assertRaises(ValueError):
                shared.prepare_source(repository, source, lock, patch_file)
        self.assertEqual(before, subprocess.check_output(['git', '-C', str(repository), 'status', '--porcelain']))

    def test_overlay_is_pinned_preserves_resources_and_has_no_cross_platform_capability_claim(self):
        lock, patch_file = native.load_lock()
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            archive = root / 'fixture.zip'
            app = 'hejjetrac.app/Contents/'
            with zipfile.ZipFile(archive, 'w') as output:
                output.writestr(app + 'MacOS/hejjetrac', 'upstream')
                output.writestr(app + 'Info.plist', plistlib.dumps({'CFBundleIdentifier': 'native.identity'}))
                for name in ('examples/upstream-config.txt', 'examples/sample.mp4', 'VR1.txt', 'LICENSE'):
                    output.writestr(app + 'Resources/' + name, 'original attributed resource')
            lock['templateArchive'] = dict(lock['templateArchive'], sha256=bundles.digest(archive))
            companions = root / 'Companions'
            companions.mkdir()
            other = dict(backend='hejjetrac', platform='macos-x64', executableSha256='original', releaseTag='v0.1.4')
            entry = dict(backend='hejjetrac', platform='macos-arm64', releaseTag='v0.1.4', archiveSha256='old-archive')
            (companions / 'catalog.json').write_text(json.dumps(dict(catalogId='base', entries=[entry, other])))
            binary = root / 'native'
            binary.write_text('reviewed adapter executable')
            with patch.object(native.subprocess, 'run') as signing:
                staged = native.stage(companions, binary, lock, patch_file, archive)
                self.assertEqual(signing.call_count, 2)
                self.assertEqual(staged, native.stage(companions, binary, lock, patch_file, archive))
            catalog = json.loads((companions / 'catalog.json').read_text())
            self.assertEqual(catalog['entries'][1], other)
            self.assertTrue(staged['supportsRecordingControl'])
            self.assertNotIn('archiveSha256', staged)
            package = companions / staged['packageRoot']
            self.assertEqual((package / 'hejjetrac.app/Contents/Resources/LICENSE').read_text(), 'original attributed resource')
            self.assertEqual((package / staged['executable']).read_bytes(), binary.read_bytes())
            self.assertIn('unsignedBuildExecutableSha256', json.loads((package / 'MATREX-NATIVE-PROVENANCE.json').read_text()))
            binary.write_text('changed')
            with self.assertRaises(ValueError):
                native.stage(companions, binary, lock, patch_file, archive)


if __name__ == '__main__':
    unittest.main()
