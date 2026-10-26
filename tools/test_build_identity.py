#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC=importlib.util.spec_from_file_location('build_identity',Path(__file__).with_name('build_identity.py'))
identity=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(identity)


class BuildIdentityTests(unittest.TestCase):
    def test_prepared_source_hash_and_dirty_provenance(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary)
            (root/'Assets/Scripts').mkdir(parents=True)
            (root/'Assets/Editor').mkdir()
            runtime=root/'Assets/Scripts/Actual.cs';runtime.write_text('class Actual {}')
            editor=root/'Assets/Editor/Only.cs';editor.write_text('class Editor {}')
            first=identity.write(root,'f'*40,'pinned-git-snapshot')
            self.assertEqual(first['runtimeSourceFileCount'],1)
            self.assertFalse(first['sourceTreeDirty'])
            editor.write_text('class DifferentEditor {}')
            self.assertEqual(identity.write(root,'f'*40,'pinned-git-snapshot')['runtimeSourceFingerprintSha256'],first['runtimeSourceFingerprintSha256'])
            runtime.write_text('class ChangedRuntime {}')
            current=identity.write(root,'f'*40,'working-tree-snapshot',[' M Assets/Scripts/Actual.cs'])
            self.assertNotEqual(current['runtimeSourceFingerprintSha256'],first['runtimeSourceFingerprintSha256'])
            self.assertTrue(current['sourceTreeDirty'])
            self.assertEqual(json.loads((root/'Assets/StreamingAssets/player_build_info.json').read_text()),current)
            self.assertIsNone(identity.write(root,'not-a-commit','unknown')['sourceRevision'])


if __name__=='__main__': unittest.main()
