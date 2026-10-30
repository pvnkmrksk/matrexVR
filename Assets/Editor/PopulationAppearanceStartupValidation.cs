#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Actual two-rig mirror/body appearance phases and generated GLB populations.</summary>
[InitializeOnLoad]
public static class PopulationAppearanceStartupValidation
{
    const string Key="PopulationAppearanceStartupValidation.Active";
    const string Model="Models/Examples/SimulatedLocust.glb";
    static string output;
    static readonly List<string> checks=new List<string>(),errors=new List<string>();
    static readonly List<object> observations=new List<object>();
    static readonly Dictionary<string,JObject> liveLenses=new Dictionary<string,JObject>();
    static PopulationAppearanceStartupValidation(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){output=SessionState.GetString(Key+".Output","");_=ValidateAsync();}};}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Marked disposable project only.");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../population-appearance-startup-validation"));Directory.CreateDirectory(output);
        PlayerSettings.companyName="MATREX Validation";PlayerSettings.productName="PopulationAppearance-"+Guid.NewGuid().ToString("N");
        string root=Application.streamingAssetsPath;var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));var source=(JObject)system["configs"][0];var rigs=new JArray();
        for(int i=1;i<=2;i++){var rig=(JObject)source.DeepClone();rig["vrId"]="VR"+i;rig["closedLoopMode"]="Rekkefly";rig["zmqAddress"]="127.0.0.1";rig["zmqPort"]=39872+i;rig["desktopPreview"]=false;rig["displayOrder"]="DRBLFU";rig["targetDisplay"]=0;rig["ledPanelWidth"]=24;rig["ledPanelHeight"]=24;rig["startCol"]=(i-1)*6;rig["startRow"]=0;rig["tracker"]=new JObject{["saveVideo"]=false};rigs.Add(rig);}
        system["configs"]=rigs;system["targetDisplay"]=0;system["telemetry"]=new JObject{["enabled"]=false};system["statusOverlay"]=new JObject{["visible"]=false};system["overheadCamera"]=new JObject{["enabled"]=false};
        File.WriteAllText(Path.Combine(root,"__appearance-system.json"),system.ToString());File.WriteAllText(Path.Combine(root,"system_config.json"),system.ToString());
        var original=JObject.Parse(File.ReadAllText(Path.Combine(root,"Examples/Kannadi/external-glb.example.json")));original["resetPositionOnStart"]=false;original["resetRotationOnStart"]=false;
        var phases=new JObject{["title"]="Same GLB body, appearance-only phases"};int n=0;foreach(string name in new[]{"preStimulus","stimulus","postStimulus"})
        {
            var phase=(JObject)original.DeepClone();phase["enabled"]=true;phase["durationSeconds"]=new[]{.67,.77,.53}[n];
            if(n==1)phase["modelAppearance"]["color"]=new JObject{["r"]=.1,["g"]=.35,["b"]=.9,["a"]=1};if(n==2)phase["modelAppearance"]["enabled"]=false;
            phases[name]=phase;n++;
        }
        File.WriteAllText(Path.Combine(root,"__appearance-mirror.json"),phases.ToString());
        var swarm=new JObject{["populationMode"]="Swarm",["modelPath"]="Models/Examples/RiggedSimple.glb",["numberOfLocusts"]=3,["density"]=JValue.CreateNull(),["agentVisual"]="ScenePrefab",["locustSpeed"]=0,["animateOnMove"]=false,["resetPositionOnStart"]=false,["resetRotationOnStart"]=false,["closedLoopPosition"]=false,["closedLoopOrientation"]=false,["modelAppearance"]=new JObject{["enabled"]=true,["keepTexture"]=false,["color"]=new JObject{["r"]=.2,["g"]=.8,["b"]=.3,["a"]=1}}};
        File.WriteAllText(Path.Combine(root,"__appearance-swarm.json"),swarm.ToString());
        var sequence=new JObject{["title"]="Population model appearance integration",["autoStart"]=false,["loop"]=false,["returnToSetup"]=true,["sequences"]=new JArray(
            new JObject{["sceneName"]="Kannadi",["duration"]=3,["reloadScene"]=true,["parameters"]=new JObject{["configFile"]="__appearance-mirror.json"}},
            new JObject{["sceneName"]="Kannadi",["duration"]=.7,["reloadScene"]=true,["parameters"]=new JObject{["configFile"]="__appearance-swarm.json"}})};
        File.WriteAllText(Path.Combine(root,"__appearance-sequence.json"),sequence.ToString());File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),sequence.ToString());
        SessionState.SetString(Key+".Output",output);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static async Task Until(Func<bool> condition,string label){var clock=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(clock.Elapsed.TotalSeconds>40)throw new TimeoutException(label);await Task.Delay(5);}}
    static Color Surface(GameObject agent)
    {
        foreach(var material in agent.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null))foreach(string property in new[]{"_Color","_BaseColor","_BaseColorFactor","baseColorFactor"})if(material.HasProperty(property))return material.GetColor(property);
        throw new InvalidOperationException("GLB has no observable surface color");
    }
    static bool Near(Color a,Color b)=>Math.Abs(a.r-b.r)<1e-5f&&Math.Abs(a.g-b.g)<1e-5f&&Math.Abs(a.b-b.b)<1e-5f;
    static AnimationState Clip(GameObject agent)=>agent.GetComponentsInChildren<Animation>(true).SelectMany(a=>a.Cast<AnimationState>()).First();
    static void Log(string message,string stack,LogType type){if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;errors.Add(message+"\n"+stack);}
    static void CaptureLenses(IEnumerable<Kannadi> rigs,int step)
    {
        foreach(var rig in rigs)
        {
            var camera=rig.GetComponentsInChildren<Camera>(true).Single(c=>c.name=="Main Camera F");
            Check(Math.Abs(camera.aspect-1)<1e-5f,"Fixture acquisition front camera is1:1 and inside the actual output for VR"+rig.VRIndex);
            liveLenses[step+":VR"+rig.VRIndex]=new JObject{["aspect"]=camera.aspect,["fov"]=camera.fieldOfView,["near"]=camera.nearClipPlane,["far"]=camera.farClipPlane,["orthographic"]=camera.orthographic,["orthographicSize"]=camera.orthographicSize};
        }
    }
    static void SetupReplay(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="ReplayScene")return;
        var replay=Object.FindFirstObjectByType<ReplayController>();replay.loop=false;replay.autoResumeAfterSeek=false;replay.interpolateAnimalPoses=false;replay.showOverviewOnStart=true;
        SceneManager.sceneLoaded-=SetupReplay;
    }
    static bool ValidProjection(Matrix4x4 matrix)
    {
        for(int i=0;i<16;i++)if(float.IsNaN(matrix[i])||float.IsInfinity(matrix[i]))return false;
        return Math.Abs(matrix.determinant)>1e-12f;
    }
    static async Task ValidateReplay(string directory,Color original)
    {
        var sourceHashes=Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(file=>file,file=>RunConfigArchive.Hash(File.ReadAllBytes(file)));
        string zipHash=RunConfigArchive.Hash(File.ReadAllBytes(directory+".zip"));
        var saved=new ReplaySessionArchive(directory);saved.RequireCompatibleBuild();saved.LoadRigSamples();
        var journal=File.ReadLines(Path.Combine(directory,"presentations.jsonl")).Select(JObject.Parse).ToArray();
        var chosen=journal.OrderBy(j=>(int)j["authoredRow"]).ThenBy(j=>(string)j["phase"]=="preStimulus"?0:(string)j["phase"]=="stimulus"?1:2)
            .Select(j=>{var rows=saved.Rigs["VR1"].Where(sample=>sample.presentationId==(string)j["id"]).ToArray();return rows[rows.Length/2];}).ToList();chosen.Add(chosen[0]);
        var streams=ReplaySessionArchive.CsvFiles(directory).Select(file=>ReplayAgentStream.TryOpen(file,Path.Combine(output,"replay-cache"),saved.OriginTicks)).Where(stream=>stream!=null).ToArray();
        try
        {
            SceneManager.sceneLoaded+=SetupReplay;ReplayController.OpenSession(directory);
            await Until(()=>{var candidate=Object.FindFirstObjectByType<ReplayController>();if(candidate?.LoadError!=null)throw new InvalidDataException(candidate.LoadError);return candidate!=null&&candidate.Ready&&!candidate.Loading;},"Actual ReplayScene opens the same-process population recording");
            var replay=Object.FindFirstObjectByType<ReplayController>();replay.SetPlaying(false);Check(replay.AnimalCount==2&&replay.PopulationRecordingCount==4,"Actual ReplayScene opens both rigs and all four recorded GLB population streams");
            int seek=0;foreach(var sample in chosen)
            {
                replay.SeekSeconds(sample.time);await Until(()=>!replay.environmentLoader.Loading,"Replay reconstructs phase "+seek);await Task.Delay(100);
                Check(replay.LoadError==null,"Replay seek "+seek+" has no fidelity/reconstruction error");
                var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(camera=>camera.name.StartsWith("Replay animal view ",StringComparison.Ordinal)).ToArray();
                Check(cameras.Length==2&&cameras.All(camera=>ValidProjection(camera.projectionMatrix)),"Replay seek "+seek+" keeps both original-view camera projections finite and nonsingular");
                int authored=(int)saved.Presentations.For(sample.presentationId)["authoredRow"];
                foreach(var camera in cameras)
                {
                    string rig=camera.name.Substring("Replay animal view ".Length);var lens=liveLenses[authored+":"+rig];
                    Check(Math.Abs(camera.aspect-1)<1e-5f&&Math.Abs(camera.aspect-(float)lens["aspect"])<1e-5f,"Replay seek "+seek+": "+rig+" retains the acquired1:1 LED aspect");
                    Check(Math.Abs(camera.fieldOfView-(float)lens["fov"])<1e-4f&&Math.Abs(camera.nearClipPlane-(float)lens["near"])<1e-4f&&Math.Abs(camera.farClipPlane-(float)lens["far"])<1e-4f&&camera.orthographic==(bool)lens["orthographic"],"Replay seek "+seek+": "+rig+" retains actual acquired FOV/near/far/projection mode");
                }


                var expected=streams.SelectMany(stream=>stream.SampleUnityFrame(sample.unityFrame)).Where(pose=>pose.visible).ToDictionary(pose=>pose.name);
                var visuals=Object.FindObjectsByType<ReplayAnimationDriver>(FindObjectsSortMode.None).Where(driver=>driver.name.StartsWith("Recorded agent ",StringComparison.Ordinal)&&driver.gameObject.activeInHierarchy).ToArray();
                Check(visuals.Length==expected.Count&&expected.Count==(seek==3?6:2),"Replay seek "+seek+" shows every recorded GLB body exactly once");
                Color color=seek==0||seek==4?new Color(.8f,.35f,.08f):seek==1?new Color(.1f,.35f,.9f):seek==2?original:new Color(.2f,.8f,.3f);
                foreach(var driver in visuals)
                {
                    string name=driver.name.Substring("Recorded agent ".Length);Check(expected.TryGetValue(name,out var pose),"Replay seek "+seek+": original recorded body identity "+name);
                    Check(Near(Surface(driver.gameObject),color),"Replay seek "+seek+": actual original GLB material color restored for "+name);
                    Check(!driver.Approximate&&!driver.TransitionApproximation,"Replay seek "+seek+": exact original animation state restored for "+name);
                    var state=JObject.Parse(pose.animationState);var expectedClip=state["legacy"].SelectMany(legacy=>(JArray)legacy["clips"]).First(clip=>(string)clip["name"]==Clip(driver.gameObject).name);
                    Check(Math.Abs(Clip(driver.gameObject).time-(float)expectedClip["time"])<.0001f,"Replay seek "+seek+": actual Legacy clip time matches recorded evidence for "+name);
                    Check(driver.GetComponentsInChildren<Animation>(true).All(animation=>!animation.enabled),"Replay seek "+seek+": paused body animation does not advance on wall time");
                    Check(Vector3.Distance(driver.transform.position,pose.position)<.0001f&&Quaternion.Angle(driver.transform.rotation,Quaternion.Euler(pose.rotation))<.01f,"Replay seek "+seek+": original authoritative body position/yaw restored for "+name);
                }
                Check(ZmqListener.ActiveWorkerCount==0,"Replay seek "+seek+" starts no tracking/acquisition worker");
                observations.Add(new{replaySeek=seek,sample.presentationId,sample.unityFrame,sample.time,bodyCount=visuals.Length,rgba=new[]{color.r,color.g,color.b,color.a}});seek++;
            }
            // Exercise the precise degenerate-viewport failure without recording
            // an invalid apparatus layout or changing any original dataset bytes.
            var source=replay.environmentLoader.RecordedRigs["VR1"].GetComponentsInChildren<Camera>(true).Single(camera=>camera.name=="Main Camera F");
            var targetCamera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(camera=>camera.name=="Replay animal view VR1");
            var priorRect=source.rect;float priorAspect=source.aspect;var priorProjection=source.projectionMatrix;
            try
            {
                source.rect=new Rect(2,0,.1f,.1f);source.ResetAspect();source.ResetProjectionMatrix();
                Check(!ValidProjection(source.projectionMatrix),"Deliberately clipped frozen source camera reproduces the degenerate projection case");
                replay.environmentLoader.ConfigureAnimalCamera("VR1",targetCamera,chosen[0]);
                Check(ValidProjection(targetCamera.projectionMatrix)&&Math.Abs(targetCamera.aspect-1)<1e-5f,"Replay safely reconstructs valid1:1 lens projection from a clipped frozen source camera");
                source.rect=priorRect;source.aspect=priorAspect;var custom=Matrix4x4.Perspective(73,1.25f,.03f,1234);source.projectionMatrix=custom;
                replay.environmentLoader.ConfigureAnimalCamera("VR1",targetCamera,chosen[0]);
                Check(Enumerable.Range(0,16).All(i=>Math.Abs(targetCamera.projectionMatrix[i]-custom[i])<1e-6f),"Replay retains every coefficient of a valid original custom projection");
            }
            finally{source.rect=priorRect;source.aspect=priorAspect;if(ValidProjection(priorProjection))source.projectionMatrix=priorProjection;else source.ResetProjectionMatrix();}
            SceneManager.LoadScene("ControlScene");await Task.Delay(100);
            Check(sourceHashes.Count==Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Count()&&sourceHashes.All(pair=>RunConfigArchive.Hash(File.ReadAllBytes(pair.Key))==pair.Value),"Actual forward/reverse replay never modifies any original recording/model/config/evidence bytes");
            Check(RunConfigArchive.Hash(File.ReadAllBytes(directory+".zip"))==zipHash,"Actual replay never modifies the finalized source ZIP");
            Check(ModelAppearance.ActiveMaterialCount==0,"Closing actual ReplayScene releases every reconstructed body/world appearance material");
        }
        finally{SceneManager.sceneLoaded-=SetupReplay;foreach(var stream in streams)stream.Dispose();}
    }
    static async Task ValidateAsync()
    {
        string failure=null,directory=null;Application.logMessageReceived+=Log;
        try
        {
            await Until(()=>MainController.Instance!=null&&MasterDataLogger.Instance!=null,"Control startup");var main=MainController.Instance;await MasterDataLogger.Instance.FinalizeSessionAsync("validation-idle");
            main.PrepareConfiguredRun("__appearance-system.json","__appearance-sequence.json");directory=MasterDataLogger.Instance.directoryPath;main.StartSequence();await Until(()=>main.SequenceRunning||main.SequenceError!=null,"Population GLB preload");Check(main.SequenceRunning&&main.SequenceError==null,"Actual Main starts two-rig external GLB appearance sequence");
            var seen=new HashSet<string>();int[] bodyIds=null,animationIds=null;Vector3 savedPosition=default;Quaternion savedRotation=default;Color original=Surface(ExternalModelLibrary.GetPrefab(Model));
            var priorTimes=new Dictionary<int,float>();double priorClock=0;var stopwatch=System.Diagnostics.Stopwatch.StartNew();
            while(main.SequenceRunning||main.SequencePreparing)
            {
                if(stopwatch.Elapsed.TotalSeconds>25)throw new TimeoutException("Appearance phase completion");
                if(!main.SceneTransitionPending&&SceneManager.GetActiveScene().name=="Kannadi")
                {
                    var rigs=Object.FindObjectsByType<Kannadi>(FindObjectsSortMode.None).Where(r=>r.enabled).OrderBy(r=>r.VRIndex).ToArray();
                    string key=main.currentStep+":"+main.ExperimentPhase;
                    if(main.currentStep==0&&rigs.Length==2&&rigs.All(r=>r.Clones.Length==1))
                    {
                        var bodies=rigs.Select(r=>r.Clones[0]).ToArray();var times=bodies.Select(b=>Clip(b).time).ToArray();
                        if(!seen.Contains(key))
                        {
                            Check(rigs.Length==2,"Mirror "+main.ExperimentPhase+": both configured rigs remain active");CaptureLenses(rigs,0);
                            if(bodyIds==null){bodyIds=bodies.Select(b=>b.GetInstanceID()).ToArray();animationIds=bodies.Select(b=>b.GetComponentInChildren<Animation>(true).GetInstanceID()).ToArray();main.VRClosedLoops["VR1"].transform.SetPositionAndRotation(new Vector3(3,1,2),Quaternion.Euler(0,17,0));savedPosition=main.VRClosedLoops["VR1"].transform.position;savedRotation=main.VRClosedLoops["VR1"].transform.rotation;}
                            else
                            {
                                Check(bodyIds.SequenceEqual(bodies.Select(b=>b.GetInstanceID())),"Appearance-only "+main.ExperimentPhase+" retains every original body instance");
                                Check(animationIds.SequenceEqual(bodies.Select(b=>b.GetComponentInChildren<Animation>(true).GetInstanceID())),"Appearance-only "+main.ExperimentPhase+" retains every original animation component");
                                Check(Vector3.Distance(savedPosition,main.VRClosedLoops["VR1"].transform.position)<1e-4f&&Quaternion.Angle(savedRotation,main.VRClosedLoops["VR1"].transform.rotation)<1e-3f,"reset=false retains the moved focal pose through "+main.ExperimentPhase);
                                for(int i=0;i<bodies.Length;i++){var clip=Clip(bodies[i]);float expected=priorTimes[bodies[i].GetInstanceID()]+(float)(Time.timeAsDouble-priorClock)*clip.speed;float error=Mathf.Abs(Mathf.DeltaAngle(360*clip.time/clip.length,360*expected/clip.length))*clip.length/360;Check(error<.05f,"Appearance-only "+main.ExperimentPhase+" does not reset live clip phase for VR"+(i+1)+": "+error);}
                            }
                            Color expectedColor=main.ExperimentPhase=="preStimulus"?new Color(.8f,.35f,.08f):main.ExperimentPhase=="stimulus"?new Color(.1f,.35f,.9f):original;
                            foreach(var body in bodies){Check(Near(Surface(body),expectedColor),main.ExperimentPhase+": GLB body actual surface matches enabled/restored appearance");Check(body.GetComponentsInChildren<Animation>(true).Any(a=>a.isPlaying),main.ExperimentPhase+": original external GLB animation keeps playing");}
                            Check(Near(Surface(ExternalModelLibrary.GetPrefab(Model)),original),main.ExperimentPhase+": instance override never changes the loaded shared GLB template");
                            foreach(var rig in rigs)foreach(var camera in rig.GetComponentsInChildren<Camera>(true).Where(c=>c.enabled))Check((camera.cullingMask&(1<<RuntimeRigRegistry.StimulusLayer(rig.VRIndex)))==0&&(camera.cullingMask&(1<<RuntimeRigRegistry.StimulusLayer(3-rig.VRIndex)))!=0,main.ExperimentPhase+": "+camera.name+" retains peer-only mirror visibility for VR"+rig.VRIndex);
                            seen.Add(key);observations.Add(new{key,bodyIds=bodies.Select(b=>b.GetInstanceID()).ToArray(),animationIds=bodies.Select(b=>b.GetComponentInChildren<Animation>(true).GetInstanceID()).ToArray(),times,colors=bodies.Select(body=>{var color=Surface(body);return new[]{color.r,color.g,color.b,color.a};}).ToArray()});
                        }
                        for(int i=0;i<bodies.Length;i++)priorTimes[bodies[i].GetInstanceID()]=times[i];priorClock=Time.timeAsDouble;
                    }
                    else if(main.currentStep==1&&rigs.Length==2&&rigs.All(r=>r.IsSwarm)&&!seen.Contains(key))
                    {
                        var spawners=rigs.Select(r=>r.GetComponent<LocustSpawner>()).ToArray();if(spawners.All(s=>s!=null&&s.Spawned.Count==3))
                        {
                            CaptureLenses(rigs,1);foreach(var spawner in spawners){Check(spawner.Spawned.Count==3,"Generated GLB swarm has exactly three agents per rig");foreach(var agent in spawner.Spawned){Check(Near(Surface(agent),new Color(.2f,.8f,.3f)),"Generated GLB agent inherits configured actual modelAppearance color");Check(agent.GetComponent<ModelAppearance>()!=null&&agent.GetComponentsInChildren<Animation>(true).Any(a=>a.isPlaying),"Generated GLB agent retains original animation and instance-owned surface override");}}
                            seen.Add(key);observations.Add(new{key,count=spawners.Sum(s=>s.Spawned.Count)});
                        }
                    }
                }
                await Task.Delay(5);
            }
            await main.RecordingCompletion;Check(seen.Count==4,"Actual Main observed three retained-body appearance phases plus generated GLB swarm");Check(main.SequenceError==null&&main.RecordingTransitionError==null,"Population appearance run has no experiment/finalization error");
            var manifest=JObject.Parse(File.ReadAllText(Path.Combine(directory,"recording_manifest.json")));Check((string)manifest["lifecycle"]?["status"]=="finalized"&&(bool?)manifest["lifecycle"]?["writersDrained"]==true&&File.Exists(directory+".zip"),"Complete native population/VR writers finalize to a ZIP");
            Check(manifest["writerEvidence"].Count()==4&&manifest["writerEvidence"].All(w=>(long)w["droppedFrames"]==0&&w["failure"].Type==JTokenType.Null),"Both mirror and generated GLB population streams drain without dropped frames or writer failures");
            var presentations=File.ReadLines(Path.Combine(directory,"presentations.jsonl")).Select(JObject.Parse).ToArray();Check(presentations.Length==4&&presentations.All(p=>p["config"]?["modelAppearance"] is JObject),"All phase/population appearance settings are archived in full presentation configurations");
            await Task.Delay(100);Check(ModelAppearance.ActiveMaterialCount==0,"Mirror phase changes, generated populations and world objects release every override material");await ValidateReplay(directory,original);Check(errors.Count==0,"Population appearance integration has no Unity runtime errors: "+string.Join("; ",errors));
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        finally
        {
            Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
            try{File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,errors,observations,liveLenses,directory,unity=Application.unityVersion,scope="Actual Main two-rig external-GLB mirror body color phases with reset=false/instance and clip continuity, source-template isolation, visibility, three generated GLB agents per rig and drained immutable population evidence, then actual ReplayScene forward/reverse colors/poses/Legacy clip times and unchanged source hashes. No native acquisition."},Formatting.Indented));}
            catch(Exception reporting){failure=reporting.ToString();File.WriteAllText(Path.Combine(output,"reporting-failure.txt"),failure);}
            finally{EditorApplication.Exit(failure==null?0:1);}
        }
    }
}
#endif
