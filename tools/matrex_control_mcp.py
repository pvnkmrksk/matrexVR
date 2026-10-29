#!/usr/bin/env python3
"""Opt-in MATREX local control MCP server. Uses only stdlib; no network listener."""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import sys
import time
import uuid


def schema(properties=None, required=()):
    return {"type": "object", "properties": properties or {}, "required": list(required), "additionalProperties": False}


PATHS = {"systemFile": {"type": "string", "description": "Absolute saved system JSON path."},
         "sequenceFile": {"type": "string", "description": "Absolute saved sequence JSON path."}}
TOOLS = [
    ("matrex_status", "status", "Read current scene, draft, experiment and replay state.", schema(), True),
    ("matrex_validate_setup", "validate", "Validate saved setup with production schema/parsers without saving or starting.", schema(PATHS, PATHS), True),
    ("matrex_start_current", "start_current", "Only on explicit user request: start the current GUI draft using the normal Start action, including saving the draft and metadata.", schema(), False),
    ("matrex_start_files", "start_files", "Only on explicit user request: select and start saved setup through the normal GUI action. Refuses unsaved GUI changes/dialogs.", schema(PATHS, PATHS), False),
    ("matrex_stop", "stop", "Only on explicit user request: stop the current experiment/preparation and return to setup. Does not quit the app.", schema(), False),
    ("matrex_replay_open", "replay_open", "Only on explicit user request: open a saved run read-only; stops current acquisition and refuses unsaved setup changes.", schema({"directory": {"type": "string", "description": "Absolute saved run directory."}}, ["directory"]), False),
    ("matrex_replay_play", "replay_play", "Only on explicit user request: play or pause the loaded replay.", schema({"playing": {"type": "boolean"}}, ["playing"]), False),
    ("matrex_replay_seek", "replay_seek", "Only on explicit user request: seek the loaded replay to session seconds; never edits recorded data.", schema({"seconds": {"type": "number"}}, ["seconds"]), False),
]


class Mailbox:
    def __init__(self, directory, timeout=15):
        self.directory = Path(directory)
        if not self.directory.is_absolute():
            raise ValueError("--control-dir must be absolute")
        self.timeout = timeout

    def call(self, method, arguments):
        if not self.directory.is_dir():
            raise ValueError("Control directory is unavailable; launch MATREX with the same --control-dir first.")
        request_id = uuid.uuid4().hex
        request = self.directory / (request_id + ".request.json")
        response = self.directory / (request_id + ".response.json")
        temporary = self.directory / (request_id + ".tmp")
        payload = {"id": request_id, "method": method, "arguments": arguments,
                   "expiresUtc": (datetime.now(timezone.utc) + timedelta(seconds=self.timeout)).isoformat()}
        try:
            with open(temporary, "x", encoding="utf-8") as stream:
                json.dump(payload, stream, allow_nan=False)
            os.replace(temporary, request)
            deadline = time.monotonic() + self.timeout
            while time.monotonic() < deadline:
                if response.exists():
                    value = json.loads(response.read_text(encoding="utf-8"))
                    if value.get("id") != request_id:
                        raise ValueError("Response id mismatch")
                    return value
                time.sleep(.025)
            raise TimeoutError("MATREX did not acknowledge the request. Its execution outcome is unknown; inspect status before retrying a state-changing command.")
        finally:
            # Expiry prevents a request that survives a timeout racing with Unity
            # from being executed after a later application launch.
            for path in (temporary, request, response):
                path.unlink(missing_ok=True)


def dispatch(message, mailbox):
    request_id, method = message.get("id"), message.get("method")
    if request_id is None:
        return None
    result = None
    if method == "initialize":
        requested = message.get("params", {}).get("protocolVersion", "2024-11-05")
        result = {"protocolVersion": requested if requested in ("2024-11-05", "2025-03-26", "2025-06-18") else "2024-11-05",
                  "capabilities": {"tools": {}}, "serverInfo": {"name": "matrex-local-control", "version": "1.0.0"}}
    elif method == "ping":
        result = {}
    elif method == "tools/list":
        result = {"tools": [{"name": name, "description": description, "inputSchema": inputs,
                             "annotations": {"readOnlyHint": read_only, "openWorldHint": False}}
                            for name, _, description, inputs, read_only in TOOLS]}
    elif method == "tools/call":
        params = message.get("params", {})
        tool = next((tool for tool in TOOLS if tool[0] == params.get("name")), None)
        if tool is None:
            return {"jsonrpc": "2.0", "id": request_id, "error": {"code": -32602, "message": "Unknown MATREX tool"}}
        try:
            arguments = params.get("arguments", {})
            if not isinstance(arguments, dict):
                raise ValueError("Tool arguments must be an object")
            expected = tool[3]
            if set(arguments) - set(expected["properties"]) or set(expected["required"]) - set(arguments):
                raise ValueError("Tool arguments do not match its declared fields")
            reply = mailbox.call(tool[1], arguments)
            result = {"content": [{"type": "text", "text": json.dumps(reply, allow_nan=False)}], "isError": not reply.get("ok", False)}
        except (OSError, ValueError, TimeoutError) as error:
            result = {"content": [{"type": "text", "text": str(error)}], "isError": True}
    else:
        return {"jsonrpc": "2.0", "id": request_id, "error": {"code": -32601, "message": "Unknown method"}}
    return {"jsonrpc": "2.0", "id": request_id, "result": result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--control-dir", required=True)
    parser.add_argument("--timeout", type=float, default=15)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    mailbox = Mailbox(args.control_dir, args.timeout)
    for line in sys.stdin:
        try:
            message = json.loads(line)
            if not isinstance(message, dict):
                raise ValueError("Request must be an object")
            response = dispatch(message, mailbox)
        except (ValueError, TypeError) as error:
            response = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": str(error)}}
        if response is not None:
            print(json.dumps(response, allow_nan=False), flush=True)


if __name__ == "__main__":
    main()
