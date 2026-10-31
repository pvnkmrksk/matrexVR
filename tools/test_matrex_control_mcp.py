#!/usr/bin/env python3
"""Official MCP SDK protocol tests and application-instance-fenced mailbox regressions."""
import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from mcp import Client

SPEC=importlib.util.spec_from_file_location('control',Path(__file__).with_name('matrex_control_mcp.py'))
control=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(control)

class FakeMailbox:
    def __init__(self):self.calls=[]
    def call(self,method,arguments):
        self.calls.append((method,arguments));return {'ok':True,'result':{'method':method,'arguments':arguments}}

class ProtocolTests(unittest.IsolatedAsyncioTestCase):
    async def test_discovery_never_operates_experiment(self):
        mailbox=FakeMailbox()
        async with Client(control.create_server(mailbox)) as client:
            tools=await client.list_tools();resources=await client.list_resources()
            self.assertEqual(len(control.TOOLS),len(tools.tools));self.assertEqual(3,len(resources.resources))
            self.assertEqual([],mailbox.calls)
            self.assertTrue(all(tool.output_schema for tool in tools.tools))
    async def test_only_explicit_tool_calls_send_commands(self):
        mailbox=FakeMailbox()
        async with Client(control.create_server(mailbox)) as client:
            result=await client.call_tool('matrex_start_current',{})
            self.assertFalse(result.is_error);self.assertTrue(result.structured_content['ok'])
            self.assertEqual([('start_current',{})],mailbox.calls)
    async def test_schema_rejections_do_not_enqueue(self):
        mailbox=FakeMailbox()
        async with Client(control.create_server(mailbox)) as client:
            for name,args in [('matrex_replay_seek',{}),('matrex_status',{'extra':True}),('matrex_replay_play',{'playing':'false'}),('matrex_config_history',{'path':'/tmp/a','revision':'abc','action':'destroy'})]:
                result=await client.call_tool(name,args)
                self.assertTrue(result.is_error,(name,args))
            self.assertEqual([],mailbox.calls)
    async def test_resources_use_same_read_only_commands(self):
        mailbox=FakeMailbox()
        async with Client(control.create_server(mailbox)) as client:
            result=await client.read_resource('matrex://status')
            self.assertTrue(json.loads(result.contents[0].text)['ok'])
            self.assertEqual([('status',{})],mailbox.calls)
    async def test_config_json_text_and_revisions_round_trip(self):
        mailbox=FakeMailbox();text='{"unknown":9007199254740993,"gain":1.2300}'
        async with Client(control.create_server(mailbox)) as client:
            result=await client.call_tool('matrex_config_replace',{'path':'/tmp/c.json','revision':'opaque','jsonText':text})
            self.assertFalse(result.is_error)
            self.assertEqual(text,result.structured_content['result']['arguments']['jsonText'])
    async def test_transport_failures_are_structured_tool_errors(self):
        class Broken:
            def call(self,*_):raise TimeoutError('Outcome unknown; inspect status')
        async with Client(control.create_server(Broken())) as client:
            result=await client.call_tool('matrex_stop',{})
            self.assertTrue(result.is_error);self.assertEqual('control_error',result.structured_content['errorCode'])

class MailboxTests(unittest.TestCase):
    def endpoint(self,root):
        (root/'endpoint.json').write_text(json.dumps({'protocolVersion':2,'instance':'a'*32,'methods':['status','stop']}))
    def test_atomic_matching_and_cleanup(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.endpoint(root);observed=[]
            def run():
                deadline=time.monotonic()+3
                while time.monotonic()<deadline:
                    files=list(root.glob('*.request.json'))
                    if files:
                        req=json.loads(files[0].read_text());observed.append(req)
                        staged=root/(req['id']+'.tmp');reply=root/(req['id']+'.response.json')
                        staged.write_text(json.dumps({'id':req['id'],'instance':req['instance'],'ok':True,'result':{'ready':True}}));staged.replace(reply);return
                    time.sleep(.005)
            worker=threading.Thread(target=run);worker.start()
            self.assertTrue(control.Mailbox(root,2).call('status',{})['ok']);worker.join()
            self.assertEqual('a'*32,observed[0]['instance']);self.assertEqual(['endpoint.json'],[p.name for p in root.iterdir()])
    def test_timeout_never_retries_mutation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.endpoint(root)
            with self.assertRaisesRegex(TimeoutError,'outcome is unknown'):control.Mailbox(root,.03).call('stop',{})
            self.assertEqual(['endpoint.json'],[p.name for p in root.iterdir()])
    def test_unknown_capability_never_writes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.endpoint(root)
            with self.assertRaisesRegex(ValueError,'does not expose'):control.Mailbox(root).call('record_start',{})
            self.assertEqual(['endpoint.json'],[p.name for p in root.iterdir()])
    def test_timeout_bounds(self):
        for value in (0,-1,float('nan'),float('inf'),61):
            with self.assertRaises(ValueError):control.Mailbox('/tmp/unused',value)

if __name__=='__main__':unittest.main()
