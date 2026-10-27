#!/usr/bin/env python3
"""Local source patch provenance and staging checks; never opens a tracker."""
import copy
from contextlib import redirect_stderr, redirect_stdout
import io
import json
from pathlib import Path
import plistlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import build_local_rekkefly as native
import build_local_desktop as desktop
import companion_bundles


class LocalNativeTests(unittest.TestCase):
    def test_desktop_builder_forwards_only_explicit_verified_pair(self):
        for reuse in (False, True):
            with self.subTest(reuse=reuse), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                argv = ['build_local_desktop.py', '--app', str(root / 'MATREX VR.app'), '--skip-local-dashboard']
                if reuse:
                    argv += ['--verified-native-binary', str(root / 'unsigned'), '--native-binary-provenance', str(root / 'receipt.json')]
                with patch.object(sys, 'argv', argv), patch.object(desktop.platform, 'machine', return_value='arm64'), \
                     patch.object(desktop, 'prepare'), patch.object(desktop, 'run') as launched, redirect_stdout(io.StringIO()):
                    desktop.main()
                commands = [call.args for call in launched.call_args_list]
                native_command = next(command for command in commands if any(str(value).endswith('/build_local_rekkefly.py') for value in command))
                if reuse:
                    self.assertEqual(native_command[-4:], ('--verified-binary', root / 'unsigned', '--binary-provenance', root / 'receipt.json'))
                else:
                    self.assertNotIn('--verified-binary', native_command)
                    self.assertNotIn('--binary-provenance', native_command)

    def test_desktop_builder_preserves_existing_native_and_companion_opt_outs(self):
        for option in ('--skip-companions', '--skip-native-camera-fix'):
            with self.subTest(option=option), tempfile.TemporaryDirectory() as temp:
                argv = ['build_local_desktop.py', '--app', str(Path(temp) / 'MATREX VR.app'), '--skip-local-dashboard', option]
                with patch.object(sys, 'argv', argv), patch.object(desktop.platform, 'machine', return_value='arm64'), \
                     patch.object(desktop, 'prepare'), patch.object(desktop, 'run') as launched, redirect_stdout(io.StringIO()):
                    desktop.main()
                commands = [str(value) for call in launched.call_args_list for value in call.args]
                self.assertFalse(any(value.endswith('/build_local_rekkefly.py') for value in commands))
                if option == '--skip-companions':
                    self.assertFalse(any(value.endswith('/companion_bundles.py') for value in commands))
                self.assertIn('/usr/bin/codesign', commands)

    def test_desktop_reuse_rejects_partial_pair_or_ignored_native_reuse(self):
        cases = [ ['--verified-native-binary', '/unused'], ['--native-binary-provenance', '/unused'],
                  ['--verified-native-binary', '/unused', '--native-binary-provenance', '/unused', '--skip-companions'],
                  ['--verified-native-binary', '/unused', '--native-binary-provenance', '/unused', '--skip-native-camera-fix'] ]
        for options in cases:
            with self.subTest(options=options), patch.object(sys, 'argv', ['build_local_desktop.py', *options]), \
                 patch.object(desktop.platform, 'machine', return_value='arm64'), patch.object(desktop, 'prepare') as prepared, redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as failure:
                    desktop.main()
                self.assertEqual(failure.exception.code, 2)
                prepared.assert_not_called()

    def test_verified_binary_requires_matching_bytes_and_locked_build_identity(self):
        lock, _ = native.load_lock()
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            binary = root / 'unsigned-rekkefly'
            binary.write_bytes(b'previously compiled local binary')
            provenance = dict(lock, unsignedBuildExecutableSha256=companion_bundles.digest(binary))
            receipt = root / 'MATREX-NATIVE-PROVENANCE.json'
            receipt.write_text(json.dumps(provenance))
            self.assertEqual(native.verified_binary(binary, receipt, lock), binary.resolve())
            for field in ('repository', 'sourceRevision', 'patchSha256', 'platform', 'sourceVersion', 'unsignedBuildExecutableSha256'):
                for bad_value in ('unreviewed', None):
                    changed = dict(provenance)
                    changed[field] = bad_value
                    receipt.write_text(json.dumps(changed))
                    with self.subTest(field=field, value=bad_value), self.assertRaises(ValueError):
                        native.verified_binary(binary, receipt, lock)
            receipt.write_text(json.dumps(provenance))
            binary.write_bytes(b'changed after the previous build')
            with self.assertRaises(ValueError):
                native.verified_binary(binary, receipt, lock)
            receipt.write_text('[]')
            with self.assertRaises(ValueError):
                native.verified_binary(binary, receipt, lock)
            with self.assertRaises(ValueError):
                native.verified_binary(root, receipt, lock)

    def test_reuse_cli_requires_explicit_binary_and_receipt_pair(self):
        for option in ('--verified-binary', '--binary-provenance'):
            with self.subTest(option=option), patch.object(sys, 'argv', ['build_local_rekkefly.py', '--companions', '/unused', option, '/unused']), redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as failure:
                    native.main()
                self.assertEqual(failure.exception.code, 2)

    def test_reuse_cli_still_checks_source_and_never_invokes_cargo(self):
        lock, _ = native.load_lock()
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            binary = root / 'rekkefly'
            binary.write_bytes(b'locked prior build')
            receipt = root / 'provenance.json'
            receipt.write_text(json.dumps(dict(lock, unsignedBuildExecutableSha256=companion_bundles.digest(binary))))
            argv = ['build_local_rekkefly.py', '--companions', str(root / 'Companions'), '--cargo', '/absent/cargo',
                    '--verified-binary', str(binary), '--binary-provenance', str(receipt)]
            with patch.object(sys, 'argv', argv), patch.object(native.platform, 'system', return_value='Darwin'), patch.object(native.platform, 'machine', return_value='arm64'), \
                 patch.object(native, 'source_repository', return_value=root / 'repository'), patch.object(native, 'prepare_source') as prepared, \
                 patch.object(native, 'build') as compiled, patch.object(native, 'stage', return_value={}) as staged, redirect_stdout(io.StringIO()):
                native.main()
                prepared.assert_called_once()
                compiled.assert_not_called()
                self.assertEqual(staged.call_args.args[1], binary.resolve())
                prepared.reset_mock()
                staged.reset_mock()
                prepared.side_effect = ValueError('prepared source changed')
                with self.assertRaisesRegex(ValueError, 'prepared source changed'):
                    native.main()
                compiled.assert_not_called()
                staged.assert_not_called()

    def test_normal_cli_keeps_building_instead_of_silently_reusing_cache(self):
        with patch.object(sys, 'argv', ['build_local_rekkefly.py', '--companions', '/unused', '--cargo', '/explicit/cargo']), \
             patch.object(native.platform, 'system', return_value='Darwin'), patch.object(native.platform, 'machine', return_value='arm64'), \
             patch.object(native, 'source_repository'), patch.object(native, 'prepare_source'), patch.object(native, 'build', return_value=Path('/built/rekkefly')) as compiled, \
             patch.object(native, 'verified_binary') as reused, patch.object(native, 'stage', return_value={}), redirect_stdout(io.StringIO()):
            native.main()
            compiled.assert_called_once()
            reused.assert_not_called()

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
