using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

/// <summary>Native file and folder dialogs using OS facilities already installed on the experiment computer.</summary>
public static class ReplayFolderPicker
{
    public static Task<string> ChooseAsync() => ChoosePathAsync("Open saved experiment run", "Open run", RuntimePaths.RunDataRoot, null);
    public static Task<string> ChooseMediaAsync() => ChoosePathAsync("Import tracker recording", "Choose recording", RuntimePaths.RunDataRoot, null);
    public static Task<string> ChooseFileAsync(string title, string initialDirectory, string extension) =>
        ChoosePathAsync(title, "Open file", initialDirectory, extension ?? "");

    private static Task<string> ChoosePathAsync(string title, string prompt, string initialDirectory, string extension)
    {
#if UNITY_EDITOR
        return Task.FromResult(extension == null
            ? UnityEditor.EditorUtility.OpenFolderPanel(title, initialDirectory, "")
            : UnityEditor.EditorUtility.OpenFilePanel(title, initialDirectory, extension));
#elif UNITY_STANDALONE_WIN
        var owner = Process.GetCurrentProcess().MainWindowHandle;
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { result.SetResult(extension == null ? ChooseWindowsFolder(title, owner) : ChooseWindowsFile(title, initialDirectory, extension, owner)); }
            catch (Exception error) { result.SetException(error); }
        }) { IsBackground = true, Name = "Native path picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
#elif UNITY_STANDALONE_OSX
        lock (macPickerGate)
        {
            if (macPickerTask != null && !macPickerTask.IsCompleted) return macPickerTask;
            return macPickerTask = ChooseOnContext(macMainContext, () => ChooseMac(title, prompt, initialDirectory, extension));
        }
#elif UNITY_STANDALONE_LINUX
        return Task.Run(() =>
        {
            if (File.Exists("/usr/bin/zenity"))
            {
                var arguments = new System.Collections.Generic.List<string> { "--file-selection", "--title=" + title };
                if (Directory.Exists(initialDirectory)) arguments.Add("--filename=" + initialDirectory.TrimEnd('/') + "/");
                if(extension==null) arguments.Add("--directory");
                else if(extension.Length>0) arguments.Add("--file-filter=Files | *." + extension + " *." + extension.ToUpperInvariant());
                return Run("/usr/bin/zenity", arguments.ToArray());
            }
            if (File.Exists("/usr/bin/kdialog"))
            {
                string directory = Directory.Exists(initialDirectory) ? initialDirectory : ".";
                return extension == null ? Run("/usr/bin/kdialog", "--getexistingdirectory", directory, "--title", title)
                    : Run("/usr/bin/kdialog", "--getopenfilename", directory, extension.Length==0?"*":"*." + extension, "--title", title);
            }
            throw new InvalidOperationException("No system file picker is installed (zenity or kdialog).");
        });
#else
        return Task.FromException<string>(new PlatformNotSupportedException("A native file picker is unavailable on this platform."));
#endif
    }

    // Posting keeps AppKit out of the OnGUI event that requested the dialog.
    // A cancelled native panel is a successful null result, matching other platforms.
    private static Task<string> ChooseOnContext(SynchronizationContext context,Func<string> choose)
    {
        var result=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(context==null)
        {result.SetException(new InvalidOperationException("The Unity main thread is unavailable for the native file picker."));return result.Task;}
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

    private static string Run(string executable, params string[] arguments)
    {
        // ProcessStartInfo uses argv parsing and never invokes a shell.
        string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        var startInfo = new ProcessStartInfo(executable, string.Join(" ", Array.ConvertAll(arguments, Quote)))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            var error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 1) return null;
            if (process.ExitCode != 0) throw new InvalidOperationException("The system file picker could not open. " + error.GetAwaiter().GetResult().Trim());
            return output.TrimEnd('\r', '\n');
        }
    }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private static readonly object macPickerGate=new object();
    private static SynchronizationContext macMainContext;
    private static Task<string> macPickerTask;
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CaptureMacMainContext(){macMainContext=SynchronizationContext.Current;macPickerTask=null;}
    private static string ChooseMac(string title,string prompt,string initialDirectory,string extension)
    {
        var pool=MacSend(MacSend(MacClass("NSAutoreleasePool"),MacSel("alloc")),MacSel("init"));
        IntPtr owner=IntPtr.Zero,panel=IntPtr.Zero;
        try
        {
            var app=MacSend(MacClass("NSApplication"),MacSel("sharedApplication"));
            owner=MacSend(app,MacSel("mainWindow"));
            if(owner==IntPtr.Zero)owner=MacSend(app,MacSel("keyWindow"));
            if(owner==IntPtr.Zero)throw new InvalidOperationException("The application window is unavailable for the native file picker.");
            MacBool(app,MacSel("activateIgnoringOtherApps:"),true);
            MacSetObject(owner,MacSel("makeKeyAndOrderFront:"),IntPtr.Zero);
            panel=MacSend(MacClass("NSOpenPanel"),MacSel("openPanel"));
            if(panel==IntPtr.Zero)throw new InvalidOperationException("macOS could not create a file picker.");
            MacSetObject(panel,MacSel("setTitle:"),MacString(title));
            MacSetObject(panel,MacSel("setPrompt:"),MacString(prompt));
            MacBool(panel,MacSel("setCanChooseDirectories:"),extension==null);
            MacBool(panel,MacSel("setCanChooseFiles:"),extension!=null);
            if(!string.IsNullOrEmpty(extension))
                MacSetObject(panel,MacSel("setAllowedFileTypes:"),MacObject(MacClass("NSArray"),MacSel("arrayWithObject:"),MacString(extension)));
            MacBool(panel,MacSel("setAllowsMultipleSelection:"),false);
            MacBool(panel,MacSel("setCanCreateDirectories:"),false);
            if(Directory.Exists(initialDirectory))
                MacSetObject(panel,MacSel("setDirectoryURL:"),MacObject(MacClass("NSURL"),MacSel("fileURLWithPath:"),MacString(initialDirectory)));
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
    private static string ChooseWindowsFolder(string title, IntPtr owner)
    {
        int initialized=OleInitialize(IntPtr.Zero);IntPtr display=Marshal.AllocHGlobal(32768*2),item=IntPtr.Zero;
        try
        {
            var info=new BrowseInfo{owner=owner,displayName=display,title=title,flags=0x0001|0x0040};
            item=SHBrowseForFolderW(ref info);if(item==IntPtr.Zero)return null;
            var path=new StringBuilder(32768);return SHGetPathFromIDListW(item,path)?path.ToString():null;
        }
        finally{if(item!=IntPtr.Zero)Marshal.FreeCoTaskMem(item);Marshal.FreeHGlobal(display);if(initialized>=0)OleUninitialize();}
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int size;
        public IntPtr owner, instance, filter, customFilter;
        public int maxCustomFilter, filterIndex;
        public IntPtr file;
        public int maxFile;
        public IntPtr fileTitle;
        public int maxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string initialDirectory, title;
        public int flags;
        public short fileOffset, extensionOffset;
        public IntPtr defaultExtension, customData, hook, templateName, reserved;
        public int reservedFlags, flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OpenFileName info);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();

    private static string ChooseWindowsFile(string title, string initialDirectory, string extension, IntPtr owner)
    {
        const int pathCapacity = 32768;
        var path = Marshal.AllocHGlobal(pathCapacity * sizeof(char));
        var filter = Marshal.StringToHGlobalUni(extension.Length==0?"All files (*.*)\0*.*\0\0":
            extension.ToUpperInvariant() + " files (*." + extension + ")\0*." + extension + "\0\0");
        try
        {
            Marshal.WriteInt16(path, 0);
            const int fileMustExist = 0x1000, pathMustExist = 0x0800, noChangeDirectory = 0x0008, explorer = 0x80000;
            var info = new OpenFileName
            {
                size = Marshal.SizeOf<OpenFileName>(), owner = owner, filter = filter, filterIndex = 1,
                file = path, maxFile = pathCapacity, title = title, initialDirectory = initialDirectory,
                flags = fileMustExist | pathMustExist | noChangeDirectory | explorer
            };
            if (GetOpenFileNameW(ref info)) return Marshal.PtrToStringUni(path);
            uint error = CommDlgExtendedError();
            if (error != 0) throw new InvalidOperationException("Windows file picker error 0x" + error.ToString("X") + ".");
            return null;
        }
        finally { Marshal.FreeHGlobal(filter); Marshal.FreeHGlobal(path); }
    }
#endif
}
