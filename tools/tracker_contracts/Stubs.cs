using System.Collections.Generic;
using System;
public sealed class ManualControlConfig { }
// Recorder process boundary is tested here; actual archive/import semantics have their own suite.
public sealed class MediaRecordingSource {public string RigId,Backend,DirectoryPath,SourceRevision,ExecutableSha256,TrackerSessionId,SourceClock,ChildDirectoryPrefix,State;public long NotBeforeUtcTicks;}
public static class CompanionMediaArchive {public static void ConfigureReceiptRoot(string root){}public static void RegisterLaunchedSource(MediaRecordingSource source){}public static void ClearLaunchedSource(string rig){}public static void AcknowledgeStopped(string rig,string session=null){}}
public static class RuntimePaths { public static string ConfigRoot=System.IO.Path.GetTempPath(); }
namespace UnityEngine
{
    public static class Application { public static string persistentDataPath=System.IO.Path.GetTempPath();public static string dataPath=System.IO.Path.GetTempPath(); }
    public class MonoBehaviour { protected static void DontDestroyOnLoad(object value){} }
    public class GameObject { public GameObject(string name){} public T AddComponent<T>() where T:new()=>new T(); }
    public static class Debug { public static void LogWarning(string value) => Console.Error.WriteLine(value); }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Quaternion { public Vector3 eulerAngles; public static Quaternion Euler(float x,float y,float z)=>new Quaternion{eulerAngles=new Vector3(x,y,z)}; }
    public static class Mathf { public const float Rad2Deg=180/(float)Math.PI; }
}

public sealed class MainController
{
    public static MainController Instance { get; set; }
    public static event Action SystemConfigurationChanged;
    public IReadOnlyDictionary<string,SystemConfig> SystemConfigs { get; set; } = new Dictionary<string,SystemConfig>();
    public static void RaiseConfigurationChanged() => SystemConfigurationChanged?.Invoke();
}
