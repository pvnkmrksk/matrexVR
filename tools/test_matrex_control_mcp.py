#!/usr/bin/env python3
"""Unit tests for explicit MCP dispatch and the local atomic command mailbox."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest

SPEC = importlib.util.spec_from_file_location("control", Path(__file__).with_name("matrex_control_mcp.py"))
control = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(control)


class FakeMailbox:
    def __init__(self): self.calls = []
    def call(self, method, arguments):
        self.calls.append((method, arguments)); return {"ok": True, "result": {"method": method}}


class ControlTests(unittest.TestCase):
    def test_discovery_never_operates_experiment(self):
        mailbox = FakeMailbox()
        for method in ("initialize", "tools/list", "ping"):
            response = control.dispatch({"id": 1, "method": method}, mailbox)
            self.assertIn("result", response)
        self.assertEqual([], mailbox.calls)
        self.assertIsNone(control.dispatch({"method": "notifications/initialized"}, mailbox))

    def test_only_explicit_tool_call_is_sent(self):
        mailbox = FakeMailbox()
        response = control.dispatch({"id": 2, "method": "tools/call", "params": {"name": "matrex_start_current", "arguments": {}}}, mailbox)
        self.assertFalse(response["result"]["isError"])
        self.assertEqual([("start_current", {})], mailbox.calls)

    def test_unknown_or_malformed_tools_never_enqueue(self):
        mailbox = FakeMailbox()
        for name, arguments in [("unknown", {}), ("matrex_replay_seek", {}), ("matrex_status", {"extra": True}), ("matrex_status", [])]:
            result = control.dispatch({"id": 3, "method": "tools/call", "params": {"name": name, "arguments": arguments}}, mailbox)
            self.assertTrue("error" in result or result["result"]["isError"])
        self.assertEqual([], mailbox.calls)

    def test_atomic_mailbox_matches_response_and_cleans_transport(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); observed = []
            def endpoint():
                deadline = time.monotonic() + 3
                while time.monotonic() < deadline:
                    files = list(root.glob("*.request.json"))
                    if files:
                        request = json.loads(files[0].read_text()); observed.append(request)
                        response = root / (request["id"] + ".response.json")
                        staged = response.with_suffix(".tmp")
                        staged.write_text(json.dumps({"id": request["id"], "ok": True, "result": {"ready": True}})); staged.replace(response); return
                    time.sleep(.005)
            worker = threading.Thread(target=endpoint); worker.start()
            reply = control.Mailbox(root, 2).call("status", {})
            worker.join()
            self.assertTrue(reply["ok"])
            self.assertEqual("status", observed[0]["method"])
            self.assertGreater(datetime.fromisoformat(observed[0]["expiresUtc"]), datetime.now(timezone.utc))
            self.assertEqual([], list(root.iterdir()))

    def test_timeout_removes_unacknowledged_request(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(TimeoutError, "outcome is unknown"):
                control.Mailbox(tmp, .03).call("stop", {})
            self.assertEqual([], list(Path(tmp).iterdir()))

    def test_stdio_protocol_has_no_startup_control_requests(self):
        with tempfile.TemporaryDirectory() as tmp:
            messages = [{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18"}},
                        {"jsonrpc": "2.0", "method": "notifications/initialized"},
                        {"jsonrpc": "2.0", "id": 2, "method": "tools/list"}]
            process = subprocess.run([sys.executable, str(Path(control.__file__)), "--control-dir", tmp],
                                     input="".join(json.dumps(message)+"\n" for message in messages), text=True, capture_output=True, check=True)
            responses = [json.loads(line) for line in process.stdout.splitlines()]
            self.assertEqual(2, len(responses)); self.assertEqual("2025-06-18", responses[0]["result"]["protocolVersion"])
            self.assertEqual(8, len(responses[1]["result"]["tools"])); self.assertEqual([], list(Path(tmp).iterdir()))


if __name__ == "__main__": unittest.main()
