#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Focused native regression probe: repeated in-scene object replacement and both color-swap branches, with no asset unloading or forced GC.</summary>
[InitializeOnLoad]
public static class MaterialOwnershipDiagnostics
{
    private const string Active="MaterialOwnershipDiagnostics.Active",Prefix="DIAG ownership source";
    private static string output;
    private static int cycles,cycle,phase,lastFrame;
    private static double started,deadline;
    private static GameObject source,host,cameraRoot;
    private static Material sourceMaterial;
    private static Camera camera;
    private static RenderTexture target;
    private static DynamicSequenceController controller;
    private static StreamWriter writer;
    private static readonly List<GameObject> rigs=new List<GameObject>();
    private static readonly List<object> samples=new List<object>();
    private static readonly List<string> errors=new List<string>();
    private static bool escaped;
    static MaterialOwnershipDiagnostics()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active,false))return;
            Initialize();
        };
    }
    private static string Argument(string name,string fallback=null)
    {string[] args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return fallback;}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Disposable project only.");
        string directory=Argument("-material-probe-output");if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Supply -material-probe-output.");
        Directory.CreateDirectory(directory);SessionState.SetString(Active+".Output",Path.GetFullPath(directory));SessionState.SetBool(Active,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
    }
    private static void Initialize()
    {
        output=SessionState.GetString(Active+".Output","");cycles=int.Parse(Argument("-material-probe-cycles","40"));
        cycle=phase=0;lastFrame=-1;escaped=false;errors.Clear();samples.Clear();rigs.Clear();
        writer=new StreamWriter(Path.Combine(output,"materials.jsonl")){AutoFlush=true};Application.logMessageReceived+=OnLog;
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Time.captureFramerate=60;
        sourceMaterial=new Material(Shader.Find("Unlit/Color")){name=Prefix};
        source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.name="DiagnosticCube";source.transform.position=new Vector3(10000,10000,10000);source.GetComponent<Renderer>().sharedMaterial=sourceMaterial;
        source.GetComponent<Collider>().enabled=false;
        for(int i=1;i<=4;i++)
        {
            var rig=new GameObject("VR"+i);rig.AddComponent<ClosedLoop>().enabled=false;rig.AddComponent<DataLogger>().enabled=false;rigs.Add(rig);
        }
        host=new GameObject("Material ownership probe controller");host.SetActive(false);controller=host.AddComponent<DynamicSequenceController>();
        controller.prefabs=new[]{source};controller.materials=new[]{sourceMaterial};controller.loopSequence=false;host.SetActive(true);
        cameraRoot=new GameObject("Material probe camera",typeof(Camera));camera=cameraRoot.GetComponent<Camera>();camera.enabled=false;camera.transform.SetPositionAndRotation(new Vector3(0,2,-10),Quaternion.Euler(0,0,0));
        target=new RenderTexture(128,128,16){name="DIAG ownership render target"};target.Create();camera.targetTexture=target;
        deadline=EditorApplication.timeSinceStartup+cycles*2+60;Advance();EditorApplication.update+=Tick;
    }
    private static void Advance()
    {
        var first=new JObject{["type"]="DiagnosticCube",["pos"]=new JArray(-2,0,5),["scale"]=JObject.FromObject(new{x=1,y=1,z=1}),["material"]=Prefix,["color"]=new JArray(.1,.1,.1,1)};
        var second=(JObject)first.DeepClone();second["pos"]=new JArray(2,0,5);second["color"]=new JArray(.8,.8,.8,1);
        if(cycle%2==0){first["swapColor"]=new JArray(.8,.8,.8,1);second["swapColor"]=new JArray(.1,.1,.1,1);}
        var design=new JObject{["seed"]=1,["sync"]=false,["repetitions"]=1,["steps"]=new JArray(new JObject{["name"]="ownership-cycle-"+cycle,["resetPositionOnStart"]=false,["resetRotationOnStart"]=false,["closedLoopPosition"]=false,["closedLoopOrientation"]=false,["swapAfterSeconds"]=.2,["trigger"]=JObject.FromObject(new{type="time",seconds=60}),["objects"]=new JArray(first,second)})};
        controller.AdvanceStep(new Dictionary<string,object>{["design"]="inline-diagnostic",["__experimentPhase"]=design});started=Time.timeAsDouble;phase=0;
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Material ownership probe timeout.");
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;camera.Render();double elapsed=Time.timeAsDouble-started;
            if(phase==0 && elapsed>=.12){Capture("before-swap");phase=1;}
            else if(phase==1 && elapsed>=.35)
            {
                Capture("after-swap");cycle++;
                if(cycle<cycles)Advance();
                else
                {
                    Object.Destroy(host);foreach(var rig in rigs)Object.Destroy(rig);Object.Destroy(source);Object.Destroy(sourceMaterial);
                    started=Time.timeAsDouble;phase=2;
                }
            }
            else if(phase==2 && elapsed>=.15){Capture("after-object-destruction-no-GC-no-UnloadUnusedAssets");Finish(null);}
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Capture(string point)
    {
        Renderer[] renderers=host!=null?host.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>();
        bool allOwned=renderers.All(r=>r.GetComponent<RuntimeMaterialOwner>()!=null && r.sharedMaterial==(Material)typeof(RuntimeMaterialOwner).GetField("owned",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(r.GetComponent<RuntimeMaterialOwner>()));
        Material[] materials=Resources.FindObjectsOfTypeAll<Material>().Where(m=>m!=null && m.name.StartsWith(Prefix,StringComparison.Ordinal)).ToArray();
        bool valid=point.StartsWith("after-object")?materials.Length==0:renderers.Length==8 && allOwned && materials.Length==9;
        escaped|=!valid;
        var row=new{cycle,point,nativeMaterials=materials.Length,rendererCount=renderers.Length,allRenderedMaterialsOwned=allOwned,ownedMaterialCounter=RuntimeMaterialOwner.ActiveMaterialCount,
            names=materials.GroupBy(m=>m.name).Select(g=>new{name=g.Key,count=g.Count()}).ToArray()};samples.Add(row);writer.WriteLine(JsonConvert.SerializeObject(row));
    }
    private static void OnLog(string message,string trace,LogType type)
    {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)if(trace==null || !trace.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(message);}
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Active,false);writer?.Dispose();Time.captureFramerate=0;
        if(camera!=null)camera.targetTexture=null;if(target!=null){target.Release();Object.Destroy(target);}if(cameraRoot!=null)Object.Destroy(cameraRoot);
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonConvert.SerializeObject(new{passed=failure==null && !escaped && errors.Count==0,cyclesCompleted=cycle,failure,errors,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
            sourceSha256=System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Scripts/DynamicSequenceController.cs"))).Select(b=>b.ToString("x2")).Aggregate("",(a,b)=>a+b),
            scope="Focused native API probe, not the experiment reset protocol:40 in-scene rebuilds exercise explicit and implicit color swaps. No GC.Collect or Resources.UnloadUnusedAssets; compare material slots with their actual owners and check destruction. Full scene reload investigation is separate.",samples},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
