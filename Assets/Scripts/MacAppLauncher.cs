using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>Launch a Mac .app through Launch Services with its own privacy identity.</summary>
public static class MacAppLauncher
{
    private const string ObjC="/usr/lib/libobjc.A.dylib",System="/usr/lib/libSystem.B.dylib";
    private const string AppKit="/System/Library/Frameworks/AppKit.framework/AppKit";
    private static readonly Lazy<IntPtr> Framework=new Lazy<IntPtr>(()=>
    {
        var handle=dlopen(AppKit,1);
        if(handle==IntPtr.Zero)throw new InvalidOperationException("Could not load macOS application launching services.");
        return handle;
    });
    public static string BundlePath(string executable)
    {
        if(string.IsNullOrWhiteSpace(executable)||!Path.IsPathFullyQualified(executable))return null;
        var folder=new FileInfo(executable).Directory;
        return folder!=null&&folder.Name=="MacOS"&&folder.Parent?.Name=="Contents"&&
            folder.Parent.Parent!=null&&folder.Parent.Parent.Name.EndsWith(".app",StringComparison.OrdinalIgnoreCase)&&
            File.Exists(Path.Combine(folder.Parent.FullName,"Info.plist"))?folder.Parent.Parent.FullName:null;
    }
    public static Process Launch(string executable,IEnumerable<string> arguments,IDictionary<string,string> environment=null)
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))throw new PlatformNotSupportedException("Native app launching requires macOS.");
        string bundle=BundlePath(executable);
        if(bundle==null||!File.Exists(executable))throw new ArgumentException("Choose the main executable inside a complete Mac .app bundle.");
        string[] args=arguments.ToArray();
        if(args.Any(a=>a==null||a.IndexOfAny(new[]{'\0','\r','\n'})>=0))throw new ArgumentException("Native app arguments contain invalid characters.");
        var framework=Framework.Value;
        var pool=Send(Send(objc_getClass("NSAutoreleasePool"),Sel("alloc")),Sel("init"));
        try
        {
            var nativeBundle=SendObject(objc_getClass("NSBundle"),Sel("bundleWithPath:"),String(bundle));
            string main=Text(Send(nativeBundle,Sel("executablePath")));
            if(string.IsNullOrEmpty(main)||Path.GetFullPath(main)!=Path.GetFullPath(executable))
                throw new ArgumentException("Select the Mac app's main executable, rather than an internal helper.");
            var configuration=Send(objc_getClass("NSMutableDictionary"),Sel("dictionary"));
            Set(configuration,Array(args.Select(String).ToArray()),Constant(framework,"NSWorkspaceLaunchConfigurationArguments"));
            if(environment!=null&&environment.Count>0)
            {
                var variables=Send(objc_getClass("NSMutableDictionary"),Sel("dictionary"));
                foreach(var pair in environment)Set(variables,String(pair.Value),String(pair.Key));
                Set(configuration,variables,Constant(framework,"NSWorkspaceLaunchConfigurationEnvironment"));
            }
            var url=SendObject(objc_getClass("NSURL"),Sel("fileURLWithPath:"),String(bundle));
            var workspace=Send(objc_getClass("NSWorkspace"),Sel("sharedWorkspace"));
            // New instance is intentional per rig; the manager's persisted lock and
            // receipt prevent duplicate launches. Keep the Unity run view focused.
            const uint newInstance=0x00080000,withoutActivation=0x00000200;
            IntPtr error;
            var running=LaunchApplication(workspace,Sel("launchApplicationAtURL:options:configuration:error:"),url,
                new UIntPtr(newInstance|withoutActivation),configuration,out error);
            if(running==IntPtr.Zero)
                throw new IOException("macOS could not open the tracker app: "+(error==IntPtr.Zero?"Unknown Launch Services error":Text(Send(error,Sel("localizedDescription")))));
            int pid=SendInt(running,Sel("processIdentifier"));
            if(pid<=0)throw new IOException("macOS opened the app without returning its process identity.");
            return Process.GetProcessById(pid);
        }
        finally { SendVoid(pool,Sel("drain")); }
    }
    public static bool Activate(int pid)
    {
        if(pid<=0||!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))return false;
        var framework=Framework.Value;
        var pool=Send(Send(objc_getClass("NSAutoreleasePool"),Sel("alloc")),Sel("init"));
        try
        {
            var application=SendPid(objc_getClass("NSRunningApplication"),Sel("runningApplicationWithProcessIdentifier:"),pid);
            // Explicit operator action only; experiment Start retains Unity focus.
            return application!=IntPtr.Zero&&SendActivate(application,Sel("activateWithOptions:"),new UIntPtr(2));
        }
        finally { SendVoid(pool,Sel("drain")); }
    }
    /// <summary>Observe a Launch Services nonchild using Darwin's actual wait status.
    /// Process.ExitCode cannot reliably reap these independent applications.</summary>
    internal static bool TryObserveExit(int pid,long startUtcTicks,Action<int> exited)
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)||pid<=0||startUtcTicks<=0||exited==null)return false;
        if(!MatchesProcess(pid,startUtcTicks))return false;
        int queue=kqueue();if(queue<0)return false;
        bool handedOff=false;
        try
        {
            var change=new ProcessEvent { ident=(ulong)pid,filter=-5,flags=0x15,fflags=0x84000000 }; // EVFILT_PROC; ADD|ENABLE|ONESHOT; EXIT|EXITSTATUS.
            if(RegisterProcessEvent(queue,ref change,1,IntPtr.Zero,0,0,IntPtr.Zero)<0)return false;
            // Reject PID reuse during registration; no old receipt may watch a new app.
            if(!MatchesProcess(pid,startUtcTicks))return false;
            var thread=new Thread(()=>
            {
                int status=-1;
                try
                {
                    ProcessEvent observed;int count;
                    do { count=WaitProcessEvent(queue,IntPtr.Zero,0,out observed,1,0,IntPtr.Zero); }
                    while(count<0&&Marshal.GetLastWin32Error()==4); // EINTR.
                    // A missing exit-status flag, kernel error, nonzero exit or signal is never clean success.
                    if(count==1&&observed.ident==(ulong)pid&&observed.filter==-5&&(observed.flags&0x4000)==0&&
                        (observed.fflags&0x84000000)==0x84000000&&observed.data>=0&&observed.data<=int.MaxValue)
                        status=(int)observed.data;
                }
                catch(Exception) { /* An unverifiable recorder stays pending; never infer success from disappearance. */ }
                finally
                {
                    CloseDescriptor(queue);
                    try { exited(status); } catch(Exception) { /* Observer cleanup must not terminate the player. */ }
                }
            }) { IsBackground=true,Name="Native tracker exit status" };
            thread.Start();handedOff=true;return true;
        }
        finally { if(!handedOff)CloseDescriptor(queue); }
    }
    private static bool MatchesProcess(int pid,long startUtcTicks)
    {
        try { using(var process=Process.GetProcessById(pid))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==startUtcTicks; }
        catch(Exception error) when(error is ArgumentException||error is InvalidOperationException||error is System.ComponentModel.Win32Exception||error is NotSupportedException){return false;}
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessEvent
    {
        public ulong ident;public short filter;public ushort flags;public uint fflags;
        public long data;public ulong udata,extension0,extension1;
    }
    [DllImport(System,SetLastError=true)] private static extern int kqueue();
    [DllImport(System,EntryPoint="close")] private static extern int CloseDescriptor(int descriptor);
    [DllImport(System,EntryPoint="kevent64",SetLastError=true)] private static extern int RegisterProcessEvent(int queue,ref ProcessEvent changes,int changeCount,IntPtr events,int eventCount,uint flags,IntPtr timeout);
    [DllImport(System,EntryPoint="kevent64",SetLastError=true)] private static extern int WaitProcessEvent(int queue,IntPtr changes,int changeCount,out ProcessEvent observed,int eventCount,uint flags,IntPtr timeout);
    private static IntPtr Sel(string name)=>sel_registerName(name);
    private static IntPtr String(string value)
    {
        if(value==null||value.IndexOf('\0')>=0)throw new ArgumentException("A native string contains invalid characters.");
        var bytes=Encoding.UTF8.GetBytes(value+"\0");var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try { return SendObject(objc_getClass("NSString"),Sel("stringWithUTF8String:"),pinned.AddrOfPinnedObject()); }
        finally { pinned.Free(); }
    }
    private static string Text(IntPtr value)=>value==IntPtr.Zero?null:Marshal.PtrToStringUTF8(Send(value,Sel("UTF8String")));
    private static IntPtr Constant(IntPtr framework,string name)
    {
        IntPtr symbol=dlsym(framework,name);
        if(symbol==IntPtr.Zero)throw new InvalidOperationException("Missing macOS application launch key: "+name);
        return Marshal.ReadIntPtr(symbol);
    }
    private static IntPtr Array(IntPtr[] values)
    {
        if(values.Length==0)return Send(objc_getClass("NSArray"),Sel("array"));
        var pinned=GCHandle.Alloc(values,GCHandleType.Pinned);
        try { return SendObjects(objc_getClass("NSArray"),Sel("arrayWithObjects:count:"),pinned.AddrOfPinnedObject(),new UIntPtr((uint)values.Length)); }
        finally { pinned.Free(); }
    }
    private static void Set(IntPtr dictionary,IntPtr value,IntPtr key)=>SetObject(dictionary,Sel("setObject:forKey:"),value,key);
    [DllImport(System)] private static extern IntPtr dlopen(string name,int flags);
    [DllImport(System)] private static extern IntPtr dlsym(IntPtr handle,string symbol);
    [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr Send(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern void SendVoid(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr SendObject(IntPtr receiver,IntPtr selector,IntPtr argument);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr SendObjects(IntPtr receiver,IntPtr selector,IntPtr objects,UIntPtr count);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern void SetObject(IntPtr receiver,IntPtr selector,IntPtr value,IntPtr key);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern int SendInt(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr SendPid(IntPtr receiver,IntPtr selector,int pid);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]
    [return:MarshalAs(UnmanagedType.I1)] private static extern bool SendActivate(IntPtr receiver,IntPtr selector,UIntPtr options);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr LaunchApplication(IntPtr receiver,IntPtr selector,IntPtr url,UIntPtr options,IntPtr configuration,out IntPtr error);
}
