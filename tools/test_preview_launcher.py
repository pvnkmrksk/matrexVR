#!/usr/bin/env python3
"""Check visible preview launchers without opening a native application."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('build_local_desktop', Path(__file__).with_name('build_local_desktop.py'))
builder = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(builder)


class PreviewLauncherTests(unittest.TestCase):
    def test_visible_paths_preserve_operator_files(self):
        with tempfile.TemporaryDirectory() as temp:
            previous = builder.ROOT
            try:
                builder.ROOT = Path(temp)
                folder = builder.ROOT / 'Preview'; folder.mkdir()
                app = folder / 'MATREX VR.app'; app.mkdir()
                builder.write_preview_entrypoints(app)
                config = folder / 'Config' / 'sequence.json'; config.write_text('operator draft')
                raw = folder / 'RunData' / 'record.csv'; raw.write_text('version of record')
                builder.write_preview_entrypoints(app)
                self.assertEqual(config.read_text(), 'operator draft')
                self.assertEqual(raw.read_text(), 'version of record')
                launcher = folder / 'Open MATREX VR.command'
                self.assertTrue(launcher.stat().st_mode & 0o111)
                self.assertIn('"$preview_dir/Config"', launcher.read_text())
                self.assertIn('"$preview_dir/RunData"', launcher.read_text())
                self.assertNotIn('.local', launcher.read_text())
            finally:
                builder.ROOT = previous

    def test_custom_output_does_not_create_an_unrelated_preview(self):
        with tempfile.TemporaryDirectory() as temp:
            previous = builder.ROOT
            try:
                builder.ROOT = Path(temp)
                builder.write_preview_entrypoints(builder.ROOT / 'custom' / 'Example.app')
                self.assertFalse((builder.ROOT / 'Preview').exists())
            finally:
                builder.ROOT = previous


if __name__ == '__main__':
    unittest.main()
