using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

/// <summary>Native folder dialogs using OS facilities already installed on the experiment computer.</summary>
public static class ReplayFolderPicker
{
    public static Task<string> ChooseAsync()
    {
#if UNITY_EDITOR
        return Task.FromResult(UnityEditor.EditorUtility.OpenFolderPanel("Open saved experiment run",RuntimePaths.RunDataRoot,""));
#elif UNITY_STANDALONE_WIN
        var result=new TaskCompletionSource<string>();
        var thread=new Thread(()=>
        {
            try{result.SetResult(ChooseWindows());}catch(Exception error){result.SetException(error);}
        }){IsBackground=true,Name="Replay run folder picker"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return result.Task;
#elif UNITY_STANDALONE_OSX
        lock(macPickerGate)
        {
            if(macPickerTask!=null&&!macPickerTask.IsCompleted)return macPickerTask;
            return macPickerTask=ChooseOnContext(macMainContext,ChooseMac);
        }
#elif UNITY_STANDALONE_LINUX
        return Task.Run(()=>
        {
            foreach(string path in new[]{"/usr/bin/zenity","/bin/zenity"})
                if(File.Exists(path))return Run(path,"--file-selection --directory --title=\"Open saved experiment run\"");
            foreach(string path in new[]{"/usr/bin/kdialog","/bin/kdialog"})
                if(File.Exists(path))return Run(path,"--getexistingdirectory . --title \"Open saved experiment run\"");
            throw new InvalidOperationException("No system folder picker is installed (zenity or kdialog). Paste the run folder path instead.");
        });
#else
        return Task.FromException<string>(new PlatformNotSupportedException("Paste the saved run folder path on this platform."));
#endif
    }

    // Posting keeps AppKit out of the OnGUI event that requested the dialog.
    // A cancelled native panel is a successful null result, matching other platforms.
    private static Task<string> ChooseOnContext(SynchronizationContext context,Func<string> choose)
    {
        var result=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(context==null)
        {result.SetException(new InvalidOperationException("The Unity main thread is unavailable. Paste the saved run folder path instead."));return result.Task;}
        try
        {
            context.Post(_=>{
                try{result.TrySetResult(choose());}
                catch(OperationCanceledException){result.TrySetResult(null);}
                catch(Exception error){result.TrySetException(error);}
            },null);
        }
        catch(Exception error){result.TrySetException(error);}
        return result.Task;
    }

    private static string Run(string executable,string arguments)
    {
        using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}})
        {
            process.Start();string output=process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();
            if(process.ExitCode!=0)return null;return output.Trim();
        }
    }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private static readonly object macPickerGate=new object();
    private static SynchronizationContext macMainContext;
    private static Task<string> macPickerTask;
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CaptureMacMainContext(){macMainContext=SynchronizationContext.Current;macPickerTask=null;}
    private static string ChooseMac()
    {
        var pool=MacSend(MacSend(MacClass("NSAutoreleasePool"),MacSel("alloc")),MacSel("init"));
        IntPtr owner=IntPtr.Zero,panel=IntPtr.Zero;
        try
        {
            var app=MacSend(MacClass("NSApplication"),MacSel("sharedApplication"));
            owner=MacSend(app,MacSel("mainWindow"));
            if(owner==IntPtr.Zero)owner=MacSend(app,MacSel("keyWindow"));
            if(owner==IntPtr.Zero)throw new InvalidOperationException("The application window is unavailable. Paste the saved run folder path instead.");
            MacBool(app,MacSel("activateIgnoringOtherApps:"),true);
            MacSetObject(owner,MacSel("makeKeyAndOrderFront:"),IntPtr.Zero);
            panel=MacSend(MacClass("NSOpenPanel"),MacSel("openPanel"));
            if(panel==IntPtr.Zero)throw new InvalidOperationException("macOS could not create a folder picker.");
            MacSetObject(panel,MacSel("setTitle:"),MacString("Open saved experiment run"));
            MacSetObject(panel,MacSel("setPrompt:"),MacString("Open run"));
            MacBool(panel,MacSel("setCanChooseDirectories:"),true);
            MacBool(panel,MacSel("setCanChooseFiles:"),false);
            MacBool(panel,MacSel("setAllowsMultipleSelection:"),false);
            MacBool(panel,MacSel("setCanCreateDirectories:"),false);
            if(Directory.Exists(RuntimePaths.RunDataRoot))
                MacSetObject(panel,MacSel("setDirectoryURL:"),MacObject(MacClass("NSURL"),MacSel("fileURLWithPath:"),MacString(RuntimePaths.RunDataRoot)));
            // App ownership and an explicit child relationship keep the dialog on
            // Unity's fullscreen Space rather than behind it in an osascript app.
            const ulong moveToActiveSpace=1UL<<1,fullScreenAuxiliary=1UL<<8;
            MacUnsigned(panel,MacSel("setCollectionBehavior:"),new UIntPtr(moveToActiveSpace|fullScreenAuxiliary));
            MacInteger(panel,MacSel("setLevel:"),Math.Max(8,MacLong(owner,MacSel("level"))+1));
            MacChild(owner,MacSel("addChildWindow:ordered:"),panel,1);
            if(MacLong(panel,MacSel("runModal"))!=1)return null;
            var url=MacSend(panel,MacSel("URL"));
            var path=url!=IntPtr.Zero?MacSend(url,MacSel("path")):IntPtr.Zero;
            return path==IntPtr.Zero?null:Marshal.PtrToStringUTF8(MacSend(path,MacSel("UTF8String")));
        }
        finally
        {
            if(panel!=IntPtr.Zero)
            {
                MacSetObject(panel,MacSel("orderOut:"),IntPtr.Zero);
                if(owner!=IntPtr.Zero)MacSetObject(owner,MacSel("removeChildWindow:"),panel);
            }
            if(owner!=IntPtr.Zero)MacSetObject(owner,MacSel("makeKeyAndOrderFront:"),IntPtr.Zero);
            if(pool!=IntPtr.Zero)MacVoid(pool,MacSel("drain"));
        }
    }
    private static IntPtr MacString(string value)
    {
        var bytes=Encoding.UTF8.GetBytes(value+"\0");var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try{return MacObject(MacClass("NSString"),MacSel("stringWithUTF8String:"),pinned.AddrOfPinnedObject());}
        finally{pinned.Free();}
    }
    private const string ObjC="/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC,EntryPoint="objc_getClass")]private static extern IntPtr MacClass(string name);
    [DllImport(ObjC,EntryPoint="sel_registerName")]private static extern IntPtr MacSel(string name);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern IntPtr MacSend(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacVoid(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern IntPtr MacObject(IntPtr receiver,IntPtr selector,IntPtr value);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacSetObject(IntPtr receiver,IntPtr selector,IntPtr value);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacBool(IntPtr receiver,IntPtr selector,[MarshalAs(UnmanagedType.I1)]bool value);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern long MacLong(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacInteger(IntPtr receiver,IntPtr selector,long value);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacUnsigned(IntPtr receiver,IntPtr selector,UIntPtr value);
    [DllImport(ObjC,EntryPoint="objc_msgSend")]private static extern void MacChild(IntPtr receiver,IntPtr selector,IntPtr child,long order);
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct BrowseInfo
    {
        public IntPtr owner,root;
        public IntPtr displayName;
        [MarshalAs(UnmanagedType.LPWStr)]public string title;
        public uint flags;
        public IntPtr callback,parameter;
        public int image;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SHBrowseForFolderW(ref BrowseInfo info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern bool SHGetPathFromIDListW(IntPtr item,StringBuilder path);
    [DllImport("ole32.dll")]private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")]private static extern void OleUninitialize();
    private static string ChooseWindows()
    {
        int initialized=OleInitialize(IntPtr.Zero);IntPtr display=Marshal.AllocHGlobal(32768*2),item=IntPtr.Zero;
        try
        {
            var info=new BrowseInfo{displayName=display,title="Open saved experiment run",flags=0x0001|0x0040};
            item=SHBrowseForFolderW(ref info);if(item==IntPtr.Zero)return null;
            var path=new StringBuilder(32768);return SHGetPathFromIDListW(item,path)?path.ToString():null;
        }
        finally{if(item!=IntPtr.Zero)Marshal.FreeCoTaskMem(item);Marshal.FreeHGlobal(display);if(initialized>=0)OleUninitialize();}
    }
#endif
}
