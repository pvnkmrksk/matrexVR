#!/usr/bin/env python3
"""Pinned builds restore archived files even when Git detects a rename."""
import importlib.util
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("build_release", Path(__file__).with_name("build_release.py"))
builder = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(builder)
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_local_rekkefly


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


class VerifiedNativeReuseTests(unittest.TestCase):
    def fixture(self, root):
        binary = root / "previous unsigned native binary"
        binary.write_bytes(b"locked unsigned native fixture")
        lock, _ = build_local_rekkefly.load_lock()
        receipt = dict(lock, unsignedBuildExecutableSha256=hashlib.sha256(binary.read_bytes()).hexdigest())
        provenance = root / "MATREX-NATIVE-PROVENANCE.json"
        provenance.write_text(json.dumps(receipt))
        return binary, provenance

    def test_invalid_combinations_fail_before_native_verification(self):
        cases = [
            ("macOS", True, True, Path("binary"), None),
            ("macOS", True, True, None, Path("receipt")),
            ("macOS", True, False, Path("binary"), Path("receipt")),
            ("Windows", True, True, Path("binary"), Path("receipt")),
            ("Linux", True, True, Path("binary"), Path("receipt")),
            ("all", False, True, Path("binary"), Path("receipt")),
        ]
        with patch.object(build_local_rekkefly, "verified_binary") as verify:
            for platform, companions, correction, binary, provenance in cases:
                with self.subTest(platform=platform, companions=companions, correction=correction, binary=binary, provenance=provenance):
                    with self.assertRaises(ValueError):
                        builder.validate_local_payload_options(platform, companions, True, correction, None, binary, provenance)
            verify.assert_not_called()

    def test_reuse_checks_existing_provenance_and_actual_binary_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            binary, provenance = self.fixture(Path(folder))
            with patch.object(builder.sys, "platform", "darwin"), patch.object(builder.host_platform, "machine", return_value="arm64"):
                for platform in ("macOS", "all"):
                    builder.validate_local_payload_options(platform, True, True, True, None, binary, provenance)
                binary.write_bytes(b"modified since previous native build")
                with self.assertRaisesRegex(ValueError, "bytes do not match"):
                    builder.validate_local_payload_options("all", True, True, True, None, binary, provenance)
                binary, provenance = self.fixture(Path(folder))
                receipt = json.loads(provenance.read_text())
                receipt["patchSha256"] = "0" * 64
                provenance.write_text(json.dumps(receipt))
                with self.assertRaisesRegex(ValueError, "pinned patchSha256"):
                    builder.validate_local_payload_options("macOS", True, True, True, None, binary, provenance)

    def test_cli_forwards_verified_paths_to_all_target_packaging(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            binary, provenance = self.fixture(root)
            unity = root / "Unity fixture"
            unity.write_text("fixture")
            quickstart = root / "quickstart.txt"
            quickstart.write_text("fixture")
            arguments = ["build_release.py", "--version", "0.0.1", "--platform", "all", "--unity", str(unity),
                         "--workspace", str(root / "release"), "--quickstart", str(quickstart), "--local-native-camera-fix",
                         "--verified-native-binary", str(binary), "--native-binary-provenance", str(provenance)]
            with patch.object(builder.sys, "argv", arguments), patch.object(builder.sys, "platform", "darwin"), \
                 patch.object(builder.host_platform, "machine", return_value="arm64"), \
                 patch.object(builder, "git", return_value=b"1" * 40), patch.object(builder, "prepare"), \
                 patch.object(builder.subprocess, "run") as run, patch.object(builder, "package") as package:
                builder.main()
            self.assertEqual(run.call_count, 3)
            self.assertEqual([call.args[3] for call in package.call_args_list], ["macOS", "Windows", "Linux"])
            for call in package.call_args_list:
                self.assertTrue(call.kwargs["local_native_camera_fix"])
                self.assertEqual(call.kwargs["verified_native_binary"], binary)
                self.assertEqual(call.kwargs["native_binary_provenance"], provenance)


if __name__ == "__main__":
    unittest.main()
