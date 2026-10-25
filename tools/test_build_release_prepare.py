#!/usr/bin/env python3
"""Pinned builds restore archived files even when Git detects a rename."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("build_release", Path(__file__).with_name("build_release.py"))
builder = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(builder)


class PinnedBuildTests(unittest.TestCase):
    def test_restores_pre_archive_filename_and_bytes(self):
        with tempfile.TemporaryDirectory(prefix="matrex-pinned-build-") as folder:
            root = Path(folder) / "repo"
            root.mkdir()

            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args])

            git("init", "-q")
            files = {
                "Assets/Editor/MatrexReleaseBuilder.cs": "// builder fixture\n",
                "Assets/StreamingAssets/Templates/system_config.template.json": '{"configs":[]}\n',
                "Assets/StreamingAssets/legacy-experiment.json": '{"preserve":"archived bytes"}\n',
                "Packages/manifest.json": "{}\n",
                "ProjectSettings/ProjectVersion.txt": "fixture\n",
            }
            for name, content in files.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content)
            git("add", ".")
            git("-c", "user.name=Build Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm", "old source")
            revision = git("rev-parse", "HEAD").decode().strip()
            moved = root / "Assets/StreamingAssets/Archive/Legacy/legacy-experiment.json"
            moved.parent.mkdir(parents=True)
            git("mv", "Assets/StreamingAssets/legacy-experiment.json", str(moved))
            git("-c", "user.name=Build Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm", "archive legacy config")
            original_root = builder.ROOT
            builder.ROOT = root
            try:
                output = Path(folder) / "pinned"
                builder.prepare(revision, output)
            finally:
                builder.ROOT = original_root
            for name, content in files.items():
                self.assertEqual((output / name).read_text(), content, name)
            self.assertFalse((output / "Assets/StreamingAssets/Archive").exists())
            self.assertEqual((output / "SOURCE-REVISION.txt").read_text().strip(), revision)


if __name__ == "__main__":
    unittest.main()
