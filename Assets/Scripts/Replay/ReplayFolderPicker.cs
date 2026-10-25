using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
        return Task.Run(()=>Run("/usr/bin/osascript","-e \"POSIX path of (choose folder with prompt \\\"Open saved experiment run\\\")\""));
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

    private static string Run(string executable,string arguments)
    {
        using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}})
        {
            process.Start();string output=process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();
            if(process.ExitCode!=0)return null;return output.Trim();
        }
    }
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
