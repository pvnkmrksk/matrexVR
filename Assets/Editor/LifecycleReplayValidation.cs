#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Accelerated native-resource teardown and real saved-run viewer regression. Disposable project copy only.</summary>
[InitializeOnLoad]
public static class LifecycleReplayValidation
{
    private const string Key="LifecycleReplayValidation.Active";
    private static int stage,cycle,checks,nextFrame,materialBaseline,gratingBaseline,workerBaseline;
    private static double deadline;
    private static string fixture;
    private static GameObject trial,grating,listener;
    private static Material sourceMaterial;
    private static readonly List<string> errors=new List<string>();
    private static int sceneReloads;
    private static readonly List<object> soakSamples=new List<object>();
    static LifecycleReplayValidation()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
            stage=cycle=checks=sceneReloads=0;nextFrame=Time.frameCount+3;deadline=EditorApplication.timeSinceStartup+600;
            materialBaseline=RuntimeMaterialOwner.ActiveMaterialCount;gratingBaseline=SinusoidalGrating.ActiveResourceSets;workerBaseline=ZmqListener.ActiveWorkerCount;
            fixture=SessionState.GetString(Key+".Fixture","");
            Application.logMessageReceived+=OnLog;EditorApplication.update+=Tick;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Run this on a marked disposable project copy.");
        fixture=Path.Combine(Path.GetTempPath(),"matrex-replay-unity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
        CreateFixture(fixture);
        SessionState.SetString(Key+".Fixture",fixture);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    private static void Check(bool value,string description){checks++;if(!value)throw new Exception(description);}
    private static void OnLog(string message,string trace,LogType type)
    {
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)
        {
            if(trace.Contains("UnityEditor.Search.SearchDatabase"))return;
            errors.Add(message);
        }
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Lifecycle/replay stage "+stage+" cycle "+cycle);
            if(Time.frameCount<nextFrame)return;nextFrame=Time.frameCount+1;
            if(stage==0)
            {
                trial=GameObject.CreatePrimitive(PrimitiveType.Cube);
                sourceMaterial=new Material(Shader.Find("Unlit/Color"));RuntimeMaterialOwner.Assign(trial.GetComponent<Renderer>(),sourceMaterial);
                trial.AddComponent<ColorDrift>().logDataToFile=false;
                grating=new GameObject("Grating lifecycle probe");grating.AddComponent<SinusoidalGrating>().enabled=false;
                listener=new GameObject("Tracking lifecycle probe");var input=listener.AddComponent<ZmqListener>();input.address="127.0.0.1";input.port=59871;
                stage=1;nextFrame+=2;return;
            }
            if(stage==1)
            {
                Check(RuntimeMaterialOwner.ActiveMaterialCount==materialBaseline+1,"one owned trial material during cycle "+cycle);
                Check(SinusoidalGrating.ActiveResourceSets==gratingBaseline+1,"one native grating set during cycle "+cycle);
                Check(trial.GetComponent<Renderer>().sharedMaterial!=sourceMaterial,"ColorDrift reuses owned instance during cycle "+cycle);
                Object.Destroy(trial);Object.Destroy(grating);Object.Destroy(listener);Object.Destroy(sourceMaterial);
                stage=2;nextFrame+=2;return;
            }
            if(stage==2)
            {
                Check(RuntimeMaterialOwner.ActiveMaterialCount==materialBaseline,"trial material released after reset "+cycle);
                Check(SinusoidalGrating.ActiveResourceSets==gratingBaseline,"grating mesh/material/texture released after reset "+cycle);
                Check(ZmqListener.ActiveWorkerCount==workerBaseline,"tracking worker stopped after reset "+cycle);
                cycle++;
                if(cycle<150){stage=0;return;}
                Check(errors.Count==0,"native teardown errors: "+string.Join("; ",errors));
                BeginSceneSoak();stage=29;nextFrame+=3;return;
            }
            if(stage==29)
            {
                var main=MainController.Instance;
                main.PrepareConfiguredRun("__lifecycle-system.json","__lifecycle-sequence.json");
                SceneManager.sceneLoaded+=ObserveSoak;main.StartSequence();stage=30;return;
            }
            if(stage==30)
            {
                var main=MainController.Instance;if(main==null)return;
                Check(main.SequenceError==null,"scene reload soak sequence remains valid");
                Check(ZmqListener.ActiveWorkerCount<=workerBaseline+4,"reloading does not accumulate tracking workers");
                if(main.SequenceRunning)return;
                Check(sceneReloads==150,"150 actual MainController full scene loads completed");
                Check(RuntimeMaterialOwner.ActiveMaterialCount==materialBaseline,"no owned native material survives full scene soak");
                if(AsyncSwarmWriter.ActiveWriterCount>0)return;
                Check(errors.Count==0,"actual reload soak errors: "+string.Join("; ",errors));
                SceneManager.sceneLoaded-=ObserveSoak;
                SceneManager.sceneLoaded+=FreezeAsynchronousInputs;
                SceneManager.LoadScene("Choice_desync");stage=20;nextFrame+=3;return;
            }
            if(stage==20)
            {
                var scene=SceneManager.GetActiveScene();
                var configs=Enumerable.Range(1,6).ToDictionary(i=>"VR"+i,i=>new SystemConfig{vrId="VR"+i,displayOrder="",targetDisplay=0,zmqAddress="127.0.0.1",zmqPort=59870+i});
                string sixFile=Path.Combine(RuntimePaths.ConfigRoot,"__lifecycle-async-system.json");
                File.WriteAllText(sixFile,new JObject{["targetDisplay"]=0,["configs"]=JArray.FromObject(configs.Values),["telemetry"]=JObject.FromObject(new{enabled=false}),["overheadCamera"]=JObject.FromObject(new{enabled=false}),["statusOverlay"]=JObject.FromObject(new{visible=false})}.ToString());
                MainController.Instance.SetSystemConfigFile("__lifecycle-async-system.json");
                RuntimeRigRegistry.Configure(scene,configs);
                foreach(var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None))input.enabled=false;
                var dynamic=Object.FindFirstObjectByType<DynamicSequenceController>();
                Check(dynamic!=null,"asynchronous scene controller loads");
                GameObject prefab=dynamic.prefabs.First(p=>p!=null && p.GetComponent<Renderer>()!=null);
                if(prefab.GetComponent<ScaleWithDistance>()==null)prefab=dynamic.prefabs.FirstOrDefault(p=>p!=null && p.GetComponent<ScaleWithDistance>()!=null)??prefab;
                var design=new JObject{["seed"]=1,["repetitions"]=1,["shuffle"]=false,["steps"]=new JArray(new JObject{
                    ["name"]="six-animal-async",["resetPositionOnStart"]=false,["resetRotationOnStart"]=false,
                    ["closedLoopPosition"]=false,["closedLoopOrientation"]=false,
                    ["objects"]=new JArray(new JObject{["type"]=prefab.name,["pos"]=new JArray(3,2,4),["visualAngleDegrees"]=12,["scale"]=JObject.FromObject(new{x=1,y=1,z=1})}),
                    ["trigger"]=JObject.FromObject(new{type="time",seconds=60})})};
                dynamic.InitializeScene(new Dictionary<string,object>{["design"]="fixture",["__experimentPhase"]=design});
                stage=21;nextFrame+=4;return;
            }
            if(stage==21)
            {
                var rigs=RuntimeRigRegistry.Rigs(SceneManager.GetActiveScene());Check(rigs.Length==6,"asynchronous scene supports six configured animals");
                foreach(var rig in rigs)
                {
                    int index=RuntimeRigRegistry.ParseIndex(rig.name);var root=GameObject.Find("Rig_VR"+index+"_Objects");
                    Check(root!=null && root.transform.childCount==1,"asynchronous object spawned for animal "+index);
                    var stimulus=root.transform.GetChild(0);int expected=index<=4?LayerMask.NameToLayer("ChoiceVR"+index):RuntimeRigRegistry.StimulusLayer(index);
                    Check(stimulus.gameObject.layer==expected,"asynchronous object has isolated layer for animal "+index);
                    if(index>4)Check(stimulus.CompareTag("Untagged"),"additional asynchronous animals need no custom tags");
                    var scaled=stimulus.GetComponent<ScaleWithDistance>();
                    if(scaled!=null)Check(scaled.playerCamera==rig.transform,"visual angle follows its own animal "+index);
                    if(index>4)
                    {
                        int isolated=RuntimeRigRegistry.StimulusMask|LayerMask.GetMask("ChoiceVR1","ChoiceVR2","ChoiceVR3","ChoiceVR4");
                        foreach(var camera in rig.GetComponentsInChildren<Camera>(true))Check((camera.cullingMask&isolated)==(1<<expected),"asynchronous camera excludes other animals "+index);
                    }
                }
                Check(errors.Count==0,"six-animal asynchronous errors: "+string.Join("; ",errors));
                SceneManager.sceneLoaded-=FreezeAsynchronousInputs;
                SceneManager.sceneLoaded+=ConfigureReplay;
                ReplayController.OpenSession(fixture);stage=3;nextFrame+=5;return;
            }
            if(stage==3)
            {
                var replay=Object.FindFirstObjectByType<ReplayController>();
                if(replay==null || replay.Loading || !replay.Ready)return;
                Check(replay.LoadError==null,"session loads without an exception");
                var environment=Object.FindFirstObjectByType<ReplayEnvironmentLoader>();if(environment.Loading)return;
                replay.SetPlaying(false);replay.SetScrubNormalized(.25f);
                Check(replay.AnimalCount==2,"fallback markers include both recorded animals");
                Check(replay.OverviewVisible,"God's-eye view is enabled by default");
                Check(Math.Abs(replay.Duration-4)<.001,"common timeline duration");
                Check(replay.PopulationRecordingCount==1,"recorded population gzip loaded");
                var marker=GameObject.Find("Recorded animal VR1");
                Check(marker!=null && Mathf.Abs(marker.transform.position.x-1)<.01f,"timeline interpolates recorded position");
                Check(Mathf.Abs(Mathf.DeltaAngle(marker.transform.eulerAngles.y,0))<.05f,"359 to 1 uses shortest quaternion interpolation");
                replay.SetPlaying(true);replay.SetScrubNormalized(.5f);Check(replay.IsPlaying,"seek retains prior playing state");
                replay.SetPlaying(false);replay.SetScrubNormalized(0);Check(!replay.IsPlaying,"seek retains prior paused state");
                var source=SceneManager.GetSceneByName("Choice");Check(source.IsValid() && source.isLoaded,"original built scene geometry restored additively");
                Check(environment.RecordedRigs.ContainsKey("VR5"),"saved five-index rig layout restores instead of installed layout");
                int vr5Layer=RuntimeRigRegistry.StimulusLayer(5),vr1Layer=RuntimeRigRegistry.StimulusLayer(1);
                int vr5View=ReplayController.AnimalCameraMask("VR5");
                Check(vr5Layer>=0 && (vr5View&(1<<vr5Layer))!=0,"replay animal5 sees its allocated stimulus layer");
                Check((vr5View&(1<<vr1Layer))==0,"replay animal5 excludes animal1 stimulus layer");
                Check(ReplayEnvironmentLoader.ObjectLayerForRig("VR5")==vr5Layer,"replayed objects use allocated animal5 layer");
                var cartesian=ReplayEnvironmentLoader.ResolveRecordedPosition(JObject.Parse("{\"position\":{\"x\":7,\"y\":2,\"z\":-3,\"radius\":100,\"angle\":90}}"));
                Check(Vector3.Distance(cartesian,new Vector3(7,2,-3))<.00001f,"complete Choice cartesian coordinates take precedence over polar fields");
                var polar=ReplayEnvironmentLoader.ResolveRecordedPosition(JObject.Parse("{\"position\":{\"x\":7,\"radius\":10,\"angle\":90,\"height\":2}}"));
                Check(Vector3.Distance(polar,new Vector3(10,2,0))<.00001f,"partial Choice cartesian coordinates retain polar fallback");
                foreach(var root in source.GetRootGameObjects())
                {
                    Check(root.GetComponentsInChildren<ZmqListener>(true).All(z=>!z.enabled),"imported tracking stays off");
                    Check(root.GetComponentsInChildren<DataLogger>(true).All(z=>!z.enabled),"imported recording stays off");
                    Check(root.GetComponentsInChildren<Camera>(true).All(z=>!z.enabled),"imported stimulus cameras stay off");
                }
                Check(RenderSettings.skybox!=null && RenderSettings.skybox.name=="Recorded replay sky material","archived sky pixels applied");
                Check(replay.Session.Warnings.Any(w=>w.Contains("no orientation")),"legacy population fidelity limit is visible");
                stage=4;nextFrame+=5;return;
            }
            if(stage==4)
            {
                Check(ZmqListener.ActiveWorkerCount==workerBaseline,"replay did not start acquisition workers");
                Check(errors.Count==0,"saved-run replay errors: "+string.Join("; ",errors));
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"../docs/replay-validation-preview.png"));
                stage=5;nextFrame+=3;return;
            }
            if(stage==5)Finish(null);
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void ConfigureReplay(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="ReplayScene")return;
        var replay=Object.FindFirstObjectByType<ReplayController>();if(replay!=null){replay.rigMarkerPrefab=null;replay.showOverviewOnStart=true;replay.loop=false;}
        SceneManager.sceneLoaded-=ConfigureReplay;
    }
    private static void FreezeAsynchronousInputs(Scene scene,LoadSceneMode mode)
    {
        foreach(var root in scene.GetRootGameObjects())foreach(var input in root.GetComponentsInChildren<ZmqListener>(true))input.enabled=false;
    }
    private static void BeginSceneSoak()
    {
        string root=RuntimePaths.ConfigRoot;
        var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));
        system["targetDisplay"]=0;system["telemetry"]=JObject.FromObject(new{enabled=false});system["overheadCamera"]=JObject.FromObject(new{enabled=false});system["statusOverlay"]=JObject.FromObject(new{visible=false});
        foreach(var config in system["configs"]){config["zmqAddress"]="127.0.0.1";config["zmqPort"]=59871;config["displayOrder"]="";}
        var configs=system["configs"] as JArray;
        while(configs.Count<4){var clone=(JObject)configs[0].DeepClone();clone["vrId"]="VR"+(configs.Count+1);configs.Add(clone);}
        while(configs.Count>4)configs.RemoveAt(configs.Count-1);
        File.WriteAllText(Path.Combine(root,"__lifecycle-system.json"),system.ToString());
        File.WriteAllText(Path.Combine(root,"__lifecycle-choice.json"),"{\"objects\":[],\"closedLoopOrientation\":false,\"closedLoopPosition\":false,\"autopilotEnabled\":false,\"resetPositionOnStart\":false,\"resetRotationOnStart\":false}");
        File.WriteAllText(Path.Combine(root,"__lifecycle-swarm.json"),"{\"numberOfLocusts\":2,\"agentVisual\":\"Bogong\",\"closedLoopPosition\":0,\"closedLoopOrientation\":0,\"locustSpeed\":0,\"autopilotEnabled\":false,\"resetPositionOnStart\":false,\"resetRotationOnStart\":false}");
        var rows=new JArray();for(int i=0;i<150;i++)rows.Add(new JObject{["sceneName"]=i%2==0?"Choice":"Swarm",["duration"]=.10,["reloadScene"]=true,["parameters"]=new JObject{["configFile"]=i%2==0?"__lifecycle-choice.json":"__lifecycle-swarm.json"}});
        File.WriteAllText(Path.Combine(root,"__lifecycle-sequence.json"),new JObject{["autoStart"]=false,["loop"]=false,["randomise"]=false,["sequences"]=rows}.ToString());
        if(MainController.Instance==null)
        {
            var host=new GameObject("Lifecycle soak controller");host.SetActive(false);var main=host.AddComponent<MainController>();
            typeof(MainController).GetField("systemConfigFileName",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(main,"__lifecycle-system.json");
            host.SetActive(true);
        }
    }
    private static void ObserveSoak(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Choice" && scene.name!="Swarm")return;
        sceneReloads++;
        soakSamples.Add(new{scene=scene.name,load=sceneReloads,trackingWorkers=ZmqListener.ActiveWorkerCount,asyncWriters=AsyncSwarmWriter.ActiveWriterCount,ownedMaterials=RuntimeMaterialOwner.ActiveMaterialCount});
    }
    private static void CreateFixture(string folder)
    {
        var files=new JArray();
        void Save(string name,string role,JObject json)
        {
            byte[] bytes=System.Text.Encoding.UTF8.GetBytes(json.ToString());string hash=RunConfigArchive.Hash(bytes);
            string relative="Configs/Snapshots/"+hash+".json",path=Path.Combine(folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);
            files.Add(new JObject{["sourceRelativePath"]=name,["sourcePath"]="/original/computer/"+name,["sha256"]=hash,["snapshotPath"]=relative,["references"]=new JArray(name),["roles"]=new JArray(role)});
        }
        Save("system_config.json","system",JObject.Parse("{\"configs\":[{\"vrId\":\"VR1\",\"displayOrder\":\"\"},{\"vrId\":\"VR5\",\"displayOrder\":\"\"}]}"));
        Save("sequenceConfig.json","sequence",JObject.Parse("{\"sequences\":[{\"sceneName\":\"Choice\",\"parameters\":{\"configFile\":\"choice.json\"}}]}"));
        Save("choice.json","experiment",JObject.Parse("{\"objects\":[],\"backgroundColor\":{\"r\":0.1,\"g\":0.1,\"b\":0.1,\"a\":1}}"));
        File.WriteAllText(Path.Combine(folder,"config_manifest.json"),new JObject{["complete"]=true,["files"]=files}.ToString());
        string header="Current Time,VR,Scene,CurrentStep,ConfigFile,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ,stepIndex,stepName,experimentPhase,skyboxId\n";
        File.WriteAllText(Path.Combine(folder,"animals.csv"),header+
            "2026-01-01 10:00:00.000,VR1,Choice,0,choice.json,0,1,0,0,359,0,0,choice,stimulus,fixture\n"+
            "2026-01-01 10:00:02.000,VR1,Choice,0,choice.json,2,1,0,0,1,0,0,choice,stimulus,fixture\n"+
            "2026-01-01 10:00:04.000,VR1,Choice,0,choice.json,4,1,0,0,3,0,0,choice,stimulus,fixture\n"+
            "2026-01-01 10:00:00.000,VR5,Choice,0,choice.json,0,1,2,0,30,0,0,choice,stimulus,fixture\n"+
            "2026-01-01 10:00:04.000,VR5,Choice,0,choice.json,4,1,2,0,30,0,0,choice,stimulus,fixture\n");
        using(var writer=new StreamWriter(new GZipStream(File.Create(Path.Combine(folder,"agents.csv.gz")),System.IO.Compression.CompressionLevel.Fastest)))
            writer.Write("Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase\n2026-01-01 10:00:00.000,recorded,SimulatedLocustsVR1,1,1,1,fixture,,stimulus\n2026-01-01 10:00:04.000,recorded,SimulatedLocustsVR1,5,1,1,fixture,,stimulus\n");
        string sky=Path.Combine(folder,"Skyboxes");Directory.CreateDirectory(sky);
        var texture=new Texture2D(8,4,TextureFormat.RGBA32,false);texture.SetPixels(Enumerable.Repeat(new Color(.03f,.04f,.1f),32).ToArray());texture.Apply();
        byte[] image=texture.EncodeToPNG();Object.DestroyImmediate(texture);File.WriteAllBytes(Path.Combine(sky,"fixture.png"),image);
        File.WriteAllText(Path.Combine(sky,"fixture.json"),new JObject{["imageSha256"]=RunConfigArchive.Hash(image)}.ToString());
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SceneManager.sceneLoaded-=ConfigureReplay;SceneManager.sceneLoaded-=ObserveSoak;SceneManager.sceneLoaded-=FreezeAsynchronousInputs;SessionState.SetBool(Key,false);
        Directory.CreateDirectory(Path.Combine(Application.dataPath,"../docs"));
        File.WriteAllText(Path.Combine(Application.dataPath,"../docs/lifecycle-replay-validation-results.json"),JsonConvert.SerializeObject(new{
            passed=failure==null,checks,nativeResourceCycles=cycle,fullSceneLoads=sceneReloads,soakSamples,unity=Application.unityVersion,failure,errors,
            scope="150 accelerated native material/grating/ColorDrift/socket teardown cycles;150 actual MainController Choice/Swarm full scene loads at0.1s configured duration with4local SUB listeners and generated2-agent populations; six-animal asynchronous object/layer/camera/reference checks; actual ReplayScene saved-folder/geometry/sky/population/scrub/overview checks. Does not reproduce30s hardware acquisition or prove its historical crash eliminated."
        },Formatting.Indented));
        foreach(string name in new[]{"__lifecycle-system.json","__lifecycle-async-system.json","__lifecycle-choice.json","__lifecycle-swarm.json","__lifecycle-sequence.json"})
        {
            string path=Path.Combine(RuntimePaths.ConfigRoot,name);if(File.Exists(path))File.Delete(path);if(File.Exists(path+".meta"))File.Delete(path+".meta");
        }
        if(failure!=null)Debug.LogError(failure);EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
