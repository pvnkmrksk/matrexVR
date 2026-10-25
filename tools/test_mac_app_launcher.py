#!/usr/bin/env python3
"""Exercise Launch Services with a windowless fixture app; no cameras or Unity."""
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    if sys.platform != 'darwin':
        print('Mac Launch Services fixture skipped on this platform')
        return
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise SystemExit('A local .NET SDK is required')
    sdk = max(map(int, re.findall(r'^(\d+)\.', subprocess.check_output([dotnet, '--list-sdks'], text=True), re.M)))
    pid = None
    with tempfile.TemporaryDirectory(prefix='matrex-launch-services-') as temp:
        root = Path(temp)
        app = root / 'Native β quoted.app'
        executable = app / 'Contents/MacOS/launch-fixture'
        executable.parent.mkdir(parents=True)
        with (app / 'Contents/Info.plist').open('wb') as stream:
            plistlib.dump({'CFBundleName': 'MATREX Launch Fixture', 'CFBundleIdentifier': 'invalid.matrex.fixture.' + uuid.uuid4().hex,
                          'CFBundleExecutable': executable.name, 'CFBundlePackageType': 'APPL', 'LSUIElement': True,
                          'NSHighResolutionCapable': True}, stream)
        source = root / 'fixture.m'
        source.write_text(r'''#import <AppKit/AppKit.h>
#include <unistd.h>
@interface ProbeDelegate : NSObject <NSApplicationDelegate> @end
@implementation ProbeDelegate
- (void)applicationDidFinishLaunching:(NSNotification *)notification {
    NSArray *arguments = NSProcessInfo.processInfo.arguments;
    NSDictionary *proof = @{ @"pid": @(getpid()), @"parentPid": @(getppid()),
        @"arguments": [arguments subarrayWithRange:NSMakeRange(1, arguments.count - 1)],
        @"environment": NSProcessInfo.processInfo.environment[@"MATREX_LAUNCH_FIXTURE"] ?: @"missing" };
    NSData *json = [NSJSONSerialization dataWithJSONObject:proof options:0 error:nil];
    [json writeToFile:arguments[1] atomically:YES];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 60 * NSEC_PER_SEC), dispatch_get_main_queue(), ^{ [NSApp terminate:nil]; });
}
@end
int main(void) { @autoreleasepool {
    [NSApplication sharedApplication];
    [NSApp setActivationPolicy:NSApplicationActivationPolicyProhibited];
    ProbeDelegate *delegate = [ProbeDelegate new]; NSApp.delegate = delegate;
    [NSApp run]; return 0;
} }
''')
        subprocess.run(['/usr/bin/clang', '-fobjc-arc', '-framework', 'AppKit', str(source), '-o', str(executable)], check=True)
        subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', '--timestamp=none', str(app)], check=True)
        project = root / 'harness'
        project.mkdir()
        (root / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (project / 'Harness.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net' + str(sdk) + '.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="' + str(ROOT / 'Assets/Scripts/MacAppLauncher.cs') + '" /></ItemGroup></Project>')
        (project / 'Program.cs').write_text(r'''using System; using System.Collections.Generic; using System.Diagnostics; using System.IO; using System.Linq; using System.Text.Json; using System.Threading;
class Harness { static void Main(string[] args) {
    var literal = new[]{args[1], "Camera β \"2\"", "$(literal); `not-a-shell` 'single' \\path"};
    using(var process = MacAppLauncher.Launch(args[0], literal, new Dictionary<string,string>{{"MATREX_LAUNCH_FIXTURE","environment β \"literal\""}})) {
        for(int wait=0; wait<100 && !File.Exists(args[1]); wait++) Thread.Sleep(50);
        if(!File.Exists(args[1])) throw new Exception("Native fixture did not finish launching");
        var proof=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        if(proof.GetProperty("pid").GetInt32()!=process.Id)throw new Exception("Returned PID is not native app PID");
        if(!proof.GetProperty("arguments").EnumerateArray().Select(v=>v.GetString()).SequenceEqual(literal))throw new Exception("Literal argv changed");
        if(proof.GetProperty("environment").GetString()!="environment β \"literal\"")throw new Exception("Literal environment changed");
        if(proof.GetProperty("parentPid").GetInt32()==Process.GetCurrentProcess().Id)throw new Exception("App is still a child of launcher");
        Console.WriteLine(JsonSerializer.Serialize(new {pid=process.Id,actualNativePidVerified=true,literalArgumentsVerified=true,literalEnvironmentVerified=true,independentParentVerified=true}));
    }
} }
''')
        environment = dict(os.environ, DOTNET_CLI_HOME=str(root), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        subprocess.run([dotnet, 'build', str(project / 'Harness.csproj'), '--configfile', str(root / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-v:q'], env=environment, check=True, stdout=subprocess.DEVNULL)
        proof_file = root / 'launch-proof.json'
        try:
            result = subprocess.run([dotnet, str(project / ('bin/Debug/net' + str(sdk) + '.0/Harness.dll')), str(executable), str(proof_file)],
                                    env=environment, check=True, capture_output=True, text=True, timeout=30)
            report = json.loads(result.stdout)
            pid = report['pid']
            os.kill(pid, 0)
            report['survivesLauncherExit'] = True
            report['fixtureHasNoWindowsOrCamera'] = True
            print(json.dumps(report, indent=2))
        finally:
            if pid is None and proof_file.exists():
                pid = json.loads(proof_file.read_text())['pid']
            if pid is not None:
                actual = subprocess.check_output(['/bin/ps', '-p', str(pid), '-o', 'comm='], text=True).strip()
                if actual == str(executable):
                    os.kill(pid, signal.SIGTERM)


if __name__ == '__main__':
    main()
