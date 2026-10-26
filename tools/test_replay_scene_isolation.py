#!/usr/bin/env python3
"""Compile exact replay-freeze methods offline against controlled Unity dependencies.

This tests missing-component safety and component state, without a Unity, scene,
camera, network or GPU claim. The native player playthrough covers scene loading.
"""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def declaration(source, signature):
    start = source.index(signature)
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow)+1]
    depth, end = 1, brace+1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


PROGRAM = r'''
using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace UnityEngine {
 public class Component{}public class Behaviour:Component{public bool enabled=true;} public class MonoBehaviour:Behaviour{} public class Camera:Behaviour{}public class AudioListener:Behaviour{}public class Animator:Behaviour{}public class Collider:Behaviour{}public class Rigidbody:Component{public bool isKinematic;}public class Renderer:Behaviour{}
 public class GameObject {public List<Component> components=new List<Component>();public T[] GetComponentsInChildren<T>(bool include) where T:class=>components.Where(c=>c==null||c is T).Select(c=>c as T).ToArray();}
}
public class RuntimeMaterialOwner:MonoBehaviour{}
public class Acquisition:MonoBehaviour{}
static class Regression {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main(){
  var root=new GameObject();var tracking=new Acquisition();var logging=new Acquisition();var owner=new RuntimeMaterialOwner();var camera=new Camera();var audio=new AudioListener();var animator=new Animator();var body=new Rigidbody();var collider=new Collider();var renderer=new Renderer();
  root.components.AddRange(new Component[]{null,tracking,null,logging,owner,camera,audio,animator,body,collider,renderer});
  Actual.SceneRoot(root);Check(!tracking.enabled,"tracking after missing script must freeze");Check(!logging.enabled,"logging after missing script must freeze");Check(!owner.enabled,"scene-owned scripts must all freeze");Check(!camera.enabled,"imported camera must stop");Check(!audio.enabled,"audio listener must stop");Check(!animator.enabled,"animation must stop");Check(body.isKinematic,"physics must stop");Check(!collider.enabled,"colliders must stop");Check(renderer.enabled,"scene geometry must stay renderable");
  var laterRoot=new GameObject();var laterTracking=new Acquisition();laterRoot.components.AddRange(new Component[]{null,laterTracking});Actual.SceneRoot(laterRoot);Check(!laterTracking.enabled,"later rig must freeze after first terrain root with missing scripts");
  var visualRoot=new GameObject();var live=new Acquisition();var cleanup=new RuntimeMaterialOwner();var visualRenderer=new Renderer{enabled=false};var visualCamera=new Camera();visualRoot.components.AddRange(new Component[]{null,live,cleanup,visualRenderer,visualCamera});Actual.FreezeVisual(visualRoot);Check(!live.enabled,"visual must not acquire");Check(cleanup.enabled,"visual cleanup owner must remain enabled");Check(visualRenderer.enabled,"visual renderer must remain visible");Check(!visualCamera.enabled,"visual camera must not render independently");Actual.FreezeVisual(null);Actual.SceneRoot(null);Check(true,"missing visual roots must be tolerated");
  Console.WriteLine("PASS: "+checks+" actual-source hierarchy/visual freeze checks with controlled Unity stubs; no Unity/native process.");
 }
}
'''


def build_and_run(folder, production, program, dotnet, version):
    folder.mkdir(parents=True, exist_ok=True)
    (folder/'Actual.cs').write_text(production)
    (folder/'Program.cs').write_text(program)
    (folder/'Regression.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'+
        f'<TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
    (folder/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    env = dict(os.environ, DOTNET_CLI_HOME=str(folder), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    build = subprocess.run([dotnet, 'build', str(folder/'Regression.csproj'), '--configfile', str(folder/'NuGet.Config'),
        '-p:UseSharedCompilation=false', '-v:q'], env=env, capture_output=True, text=True)
    if build.returncode:
        raise SystemExit(build.stdout+build.stderr)
    subprocess.run([dotnet, str(folder/f'bin/Debug/net{version}.0/Regression.dll')], env=env, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-ref', help='Optional historical ref whose FreezeVisual must reproduce the missing-script failure.')
    args = parser.parse_args()
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required.')
    version = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    source = (ROOT/'Assets/Scripts/Replay/ReplayEnvironmentLoader.cs').read_text()
    methods = declaration(source, 'private static void FreezeHierarchy(')+declaration(source, 'public static void FreezeVisual(')
    actual = 'using System;using UnityEngine;public static class Actual{'+methods+'public static void SceneRoot(GameObject root)=>FreezeHierarchy(root,false);}'
    with tempfile.TemporaryDirectory(prefix='matrex-replay-isolation-') as temporary:
        folder = Path(temporary)
        build_and_run(folder/'current', actual, PROGRAM, dotnet, version)
        if args.baseline_ref:
            old = subprocess.check_output(['git', 'show', args.baseline_ref+':Assets/Scripts/Replay/ReplayEnvironmentLoader.cs'], cwd=ROOT, text=True)
            method = declaration(old, 'public static void FreezeVisual(')
            baseline = 'using System;using UnityEngine;public static class Actual{'+method+'}'
            stubs = PROGRAM.split('static class Regression')[0]
            probe = stubs + 'static class Regression{static void Main(){var root=new GameObject();root.components.Add(null);root.components.Add(new Acquisition());try{Actual.FreezeVisual(root);throw new Exception("Old code unexpectedly accepted missing script");}catch(NullReferenceException){Console.WriteLine("PASS: historical source reproduces missing-script NullReferenceException before later acquisition is disabled.");}}}'
            build_and_run(folder/'baseline', baseline, probe, dotnet, version)


if __name__ == '__main__':
    main()
