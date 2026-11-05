#!/usr/bin/env python3
"""Read-only MATREX connection check; run with Python 3.11+ and mcp==2.3.0."""

import argparse
import asyncio
import json
from pathlib import Path
import sys

from mcp import Client
from mcp.client.stdio import StdioServerParameters


async def inspect_connection(adapter, control_directory):
    server = StdioServerParameters(
        command=sys.executable,
        args=[str(adapter), "--control-dir", str(control_directory)],
    )
    async with Client(server) as client:
        tools = await client.list_tools()
        resources = await client.list_resources()
        print(json.dumps({"tools": len(tools.tools), "resources": len(resources.resources)}))
        for name in ("matrex_discover", "matrex_status", "matrex_workspace_get"):
            response = await client.call_tool(name, {})
            envelope = response.structured_content
            print(json.dumps({"tool": name, "reply": envelope}, indent=2, ensure_ascii=False))
            if response.is_error or not envelope or not envelope.get("ok"):
                raise RuntimeError(f"{name} failed; inspect the reply above")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adapter", required=True, type=Path, help="Absolute path to matrex_control_mcp.py")
    parser.add_argument("--control-dir", required=True, type=Path, help="Same absolute directory used by the running player")
    args = parser.parse_args()
    if not args.adapter.is_absolute() or not args.adapter.is_file():
        parser.error("--adapter must be an existing absolute script path")
    if not args.control_dir.is_absolute():
        parser.error("--control-dir must be absolute")
    try:
        asyncio.run(inspect_connection(args.adapter, args.control_dir))
    except Exception as error:
        print(f"Connection check failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
