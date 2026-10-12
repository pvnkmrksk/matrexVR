import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from audit_run_config_snapshots import audit_session, read_json


class RunAuditTests(unittest.TestCase):
    def test_hidden_legacy_nested_copy_is_found(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root/"t_Control_sequenceConfig.json").write_text(json.dumps({"sequences": [{"parameters": {"configFile": "Examples/Swarm/example.json"}}]}))
            (root/"t_Control_system_config.json").write_text("{}")
            p = root/"t_Control_Examples/Swarm/example.json"; p.parent.mkdir(parents=True); p.write_text("{}")
            result = audit_session(root)
            self.assertFalse(result["missingSequence"])
            self.assertEqual(result["missingExperimentReferences"], [])
            self.assertEqual(result["nestedExperimentCopies"], ["Examples/Swarm/example.json"])

    def test_comments_paths_and_trailing_comma(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp)/"test.json"
            p.write_text('{/* note */"address":"tcp://localhost:9880", "items":[1,2,], // note\n}')
            self.assertEqual(read_json(p), {"address": "tcp://localhost:9880", "items": [1, 2]})

    def test_new_manifest_integrity_and_missing_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); blob = b'{"saved":true}'; (root/"snapshot.json").write_bytes(blob)
            record = {"roles": ["sequence", "system"], "snapshotPath": "snapshot.json", "sha256": hashlib.sha256(blob).hexdigest()}
            (root/"config_manifest.json").write_text(json.dumps({"complete": True, "files": [record]}))
            self.assertEqual(audit_session(root)["invalidSnapshots"], [])
            (root/"snapshot.json").write_text("changed")
            self.assertEqual(audit_session(root)["invalidSnapshots"], ["snapshot.json"])

    def test_missing_config_is_reported_without_backfill(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root/"sequenceConfig.json").write_text('{"sequences":[{"parameters":{"design":"missing.json"}}]}')
            result = audit_session(root)
            self.assertTrue(result["missingSystem"])
            self.assertEqual(result["missingExperimentReferences"], ["missing.json"])
            self.assertFalse((root/"missing.json").exists())


if __name__ == "__main__": unittest.main()
