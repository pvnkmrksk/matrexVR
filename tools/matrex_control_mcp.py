#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = ["mcp==2.3.0"]
# ///
"""Official-SDK stdio MCP adapter for opt-in MATREX control; no listening network port."""
import argparse
import math
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
STRING={"type":"string"}
REVISION={"path":dict(STRING,description="Absolute path of a configuration in the selected GUI workspace."),"revision":dict(STRING,description="Revision returned by config_get; stale edits are rejected.")}
TOOLS.extend([
    ("matrex_discover","discover","Discover build scenes, treatment/sequence JSON files, object/material names, output displays, configured camera selections and supported control methods.",schema(),True),
    ("matrex_schemas","schemas","Read the exact production GUI schema catalog and field defaults; omitted schemaId returns all schemas.",schema({"schemaId":STRING}),True),
    ("matrex_workspace_get","workspace_get","Read the shared GUI draft, effective defaults, revisions, dirty/conflict state and runtime validation errors.",schema(),True),
    ("matrex_config_get","config_get","Read one selected-workspace configuration with lossless JSON text and effective GUI defaults.",schema({"path":STRING},["path"]),True),
    ("matrex_config_replace","config_replace","Replace a shared GUI JSON draft atomically after production validation; preserves undo and unknown fields supplied in jsonText. Does not save or start.",schema(dict(REVISION,jsonText=STRING,allowInvalidDraft={"type":"boolean","description":"Explicitly allow an invalid GUI draft while repairing a multi-file setup. Start still requires full validation."}),["path","revision","jsonText"]),False),
    ("matrex_config_create","config_create","Create a new JSON configuration within the app config root, without overwriting anything. Uses shared schema validation; cross-file/runtime validation occurs when the file is referenced by the setup.",schema({"relativePath":STRING,"schemaId":STRING,"jsonText":STRING},["relativePath","schemaId","jsonText"]),False),
    ("matrex_config_save","config_save","Save one shared GUI document with revision and external-conflict checks; retains a recoverable .bak file.",schema(REVISION,["path","revision"]),False),
    ("matrex_config_history","config_history","Undo, redo or revert the shared GUI document, guarded by revision.",schema(dict(REVISION,action={"enum":["undo","redo","revert"]}),["path","revision","action"]),False),
    ("matrex_setup_select","setup_select","Select validated saved system/sequence JSON files without starting; refuses unsaved GUI drafts and pending input.",schema(PATHS,PATHS),False),
    ("matrex_tracking_initialize","tracking_initialize","Run the ordinary Initialize tracking/preflight action without starting an experiment or experiment-scoped recording. Poll status for actual input freshness.",schema(),False),
    ("matrex_tracker_control","tracker_control","Launch or explicitly close a configured independent tracker through the same GUI process manager, while idle. Process liveness is not valid acquisition evidence.",schema({"vrId":STRING,"action":{"enum":["launch","stop"]}},["vrId","action"]),False),
    ("matrex_recorder_stop","recorder_stop","Manually stop this experiment-owned native take while VR continues. Ownership-fenced stop waits asynchronously for finalization and latches manual override; poll operation_get.",schema({"vrId":STRING},["vrId"]),False),
    ("matrex_operation_get","operation_get","Read acknowledged completion/error of an asynchronous operation in this app instance.",schema({"operationId":STRING},["operationId"]),True),
    ("matrex_metadata_get","metadata_get","Read the GUI session/animal metadata and optimistic revision.",schema(),True),
    ("matrex_metadata_set","metadata_set","Replace session metadata using its current revision; save follows the ordinary Start workflow.",schema({"jsonText":STRING,"revision":STRING,"save":{"type":"boolean"}},["jsonText","revision"]),False),
    ("matrex_recordings_list","recordings_list","Discover this installation's run data by default, or an explicitly specified recording root. Full replay validation occurs on open.",schema({"directory":STRING}),True),
    ("matrex_replay_options","replay_options","Read or edit playback speed/loop, panel-pixel scaling, trajectory prominence/duration, camera projection, follow target and overview pivot/yaw/pitch/distance/zoom. jsonText is a validated partial JSON object; never modifies the saved recording.",schema({"jsonText":STRING}),False),
    ("matrex_replay_view","replay_view","Show the recorded animal's first-person camera, or the overview when vrId is omitted.",schema({"vrId":STRING}),False),
    ("matrex_runtime_control","runtime_control","Use the ordinary live auto-trim and flight-check controls, retaining current values for omitted keys.",schema({"autoTrim":{"type":"boolean"},"flightCheck":{"type":"boolean"},"targetFrameRate":{"type":"integer"}}),False),
])


class Mailbox:
    def __init__(self, directory, timeout=15):
        self.directory = Path(directory)
        if not self.directory.is_absolute():
            raise ValueError("--control-dir must be absolute")
        if not math.isfinite(timeout) or not 0 < timeout <= 60:
            raise ValueError("--timeout must be finite and in (0,60] seconds")
        self.timeout = timeout

    def call(self, method, arguments):
        if not self.directory.is_dir():
            raise ValueError("Control directory is unavailable; launch MATREX with the same --control-dir first.")
        descriptor_path=self.directory / "endpoint.json"
        descriptor=json.loads(descriptor_path.read_text(encoding="utf-8"))
        instance=descriptor.get("instance")
        if descriptor.get("protocolVersion") != 2 or not isinstance(instance,str) or len(instance)!=32:
            raise ValueError("Unsupported endpoint; use a MATREX build with control protocol 2")
        if method not in descriptor.get("methods",[]):
            raise ValueError("This MATREX build does not expose: "+method)
        request_id = uuid.uuid4().hex
        request = self.directory / (request_id + ".request.json")
        response = self.directory / (request_id + ".response.json")
        temporary = self.directory / (request_id + ".tmp")
        payload = {"id": request_id, "instance":instance,"method": method, "arguments": arguments,
                   "expiresUtc": (datetime.now(timezone.utc) + timedelta(seconds=self.timeout)).isoformat()}
        try:
            with open(temporary, "x", encoding="utf-8") as stream:
                json.dump(payload, stream, allow_nan=False)
            os.replace(temporary, request)
            deadline = time.monotonic() + self.timeout
            while time.monotonic() < deadline:
                if response.exists():
                    value = json.loads(response.read_text(encoding="utf-8"))
                    if value.get("id") != request_id or value.get("instance")!=instance:
                        raise ValueError("Response/application instance mismatch; inspect status before retrying")
                    return value
                time.sleep(.025)
            raise TimeoutError("MATREX did not acknowledge the request. Its execution outcome is unknown; inspect status before retrying a state-changing command.")
        finally:
            # Expiry prevents a request that survives a timeout racing with Unity
            # from being executed after a later application launch.
            for path in (temporary, request, response):
                path.unlink(missing_ok=True)



def create_server(mailbox):
    import anyio
    import jsonschema
    from mcp import types
    from mcp.server.lowlevel import Server

    tools={entry[0]:entry for entry in TOOLS}
    output_schema={"type":"object","properties":{"ok":{"type":"boolean"}},"required":["ok"]}
    async def list_tools(ctx,params):
        return types.ListToolsResult(tools=[types.Tool(name=name,description=description,input_schema=inputs,
            output_schema=output_schema,annotations=types.ToolAnnotations(read_only_hint=read_only,open_world_hint=False))
            for name,_,description,inputs,read_only in TOOLS])
    async def call_tool(ctx,params):
        try:
            if params.name not in tools:raise ValueError("Unknown MATREX tool")
            _,method,_,inputs,_=tools[params.name]
            arguments=params.arguments or {}
            jsonschema.validate(arguments,inputs)
            reply=await anyio.to_thread.run_sync(lambda:mailbox.call(method,arguments))
        except (OSError,ValueError,TimeoutError,jsonschema.ValidationError) as error:
            reply={"ok":False,"errorCode":"control_error","error":str(error)}
        return types.CallToolResult(content=[types.TextContent(type="text",text=json.dumps(reply,allow_nan=False))],
            structured_content=reply,is_error=not reply.get("ok",False))
    resources={"matrex://status":("Current experiment status","status"),"matrex://catalog":("Build and configuration catalog","discover"),"matrex://schemas":("Production configuration schemas","schemas")}
    async def list_resources(ctx,params):
        return types.ListResourcesResult(resources=[types.Resource(name=name,uri=uri,mime_type="application/json") for uri,(name,_) in resources.items()])
    async def read_resource(ctx,params):
        uri=str(params.uri)
        if uri not in resources:raise ValueError("Unknown MATREX resource")
        reply=await anyio.to_thread.run_sync(lambda:mailbox.call(resources[uri][1],{}))
        return types.ReadResourceResult(contents=[types.TextResourceContents(uri=uri,mime_type="application/json",text=json.dumps(reply,allow_nan=False))])
    return Server("matrex-local-control",version="2.0.0",on_list_tools=list_tools,on_call_tool=call_tool,
        get_tool_input_schema=lambda name:tools[name][3] if name in tools else None,
        on_list_resources=list_resources,on_read_resource=read_resource)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--control-dir",required=True)
    parser.add_argument("--timeout",type=float,default=15)
    args=parser.parse_args()
    try:
        import anyio
        from mcp.server.stdio import stdio_server
        server=create_server(Mailbox(args.control_dir,args.timeout))
    except ImportError as error:
        parser.exit(2,"Use Python 3.11+ with mcp==2.3.0, or uv run --script tools/matrex_control_mcp.py. "+str(error)+"\n")
    async def serve():
        async with stdio_server() as (reader,writer):
            await server.run(reader,writer,server.create_initialization_options())
    anyio.run(serve)


if __name__=="__main__":main()
