#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercise the active nested heading-aligned experiment and legacy flat loading.
/// Disposable project only; real tracking input is disabled.</summary>
[InitializeOnLoad]
public static class SwarmNestedConfigValidation
{
    private const string Key="SwarmNestedConfigValidation.Active";
    private const string Recipe="Examples/Swarm/heading-aligned.example.json";
    private static int checks,stage,nextFrame,sceneHandle;
    private static int[] rigIds;
    private static double deadline;
    private static readonly List<string> errors=new List<string>();
    private static JObject stimulus;
    private static void Check(bool ok,string message) {checks++;if(!ok) throw new Exception(message);}
    private static Dictionary<string,object> Phase(JObject json) => new Dictionary<string,object>{{"__experimentPhase",json}};
    static SwarmNestedConfigValidation()
    {
        EditorApplication.playModeStateChanged+=state=> {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            checks=SessionState.GetInt(Key+".loadingChecks",0);
            stimulus=ExperimentPhase.Parse(JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Recipe))))
                .Single(p=>p.Name=="stimulus").Config;
            deadline=EditorApplication.timeSinceStartup+90;
            Application.logMessageReceived+=OnLog;SceneManager.sceneLoaded+=DisableInputs;EditorApplication.update+=Tick;
        };
    }
    private static void OnLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        if(message=="No backup data file found." || stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message+"\n"+stack);
    }
    private static void DisableInputs(Scene scene,LoadSceneMode mode)
    {
        foreach(var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled=false;
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        try {Loading();} catch(Exception error) {Finish(error.ToString());return;}
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["telemetry"]["enabled"]=false;system["overheadCamera"]["enabled"]=false;system["configs"][0]["displayOrder"]="RBLF";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        // Keep the active sequence and heading recipe unchanged in the test copy.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetInt(Key+".loadingChecks",checks);
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static void Loading()
    {
        var recipe=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Recipe)));
        var phases=ExperimentPhase.Parse(recipe);
        Check(phases.Count==2 && phases[0].Duration==10 && phases[1].Duration==240,"User's phase timing remains 10/240 seconds.");
        var pre=KannadiConfig.LoadSwarm(Phase(phases[0].Config));
        Check(!pre.SwarmEnabled && pre.autopilotSpeed==7 && pre.initialPosition.y==100,"Null swarm disables the pre-stimulus population, preserving shared motion/pose.");
        stimulus=phases[1].Config;
        var config=KannadiConfig.LoadSwarm(Phase(stimulus));
        Check(config.SwarmEnabled && config.agentVisual=="Bogong" && config.bogongVisual.shape=="Moth","Nested stimulus selects the Bogong moth.");
        Check(config.numberOfLocusts==100 && config.dimension=="3D" && config.spawnVolumeSize.ToVector3()==Vector3.one*10,
            "Nested density/count and volume are loaded instead of scene defaults.");
        Check(config.mu==-90 && config.kappa==1000 && config.locustSpeed==7 && config.followAnimalPosition && config.useHeadingReference,
            "Nested direction/order/speed/follow/reference flags remain intact.");
        Check(config.bogongVisual.sizeMode=="Angular" && config.bogongVisual.angularSizeDegrees==2 && config.bogongVisual.color.r==.01f,
            "User's angular size and color are unchanged.");
        Check(config.autopilotSpeed==7 && config.closedLoopPosition==0 && config.closedLoopOrientation==1 &&
            !config.resetPositionOnStart && !config.resetRotationOnStart,"Phase-level controls retain their own values.");
        var original=(JObject)stimulus.DeepClone();
        var mixed=(JObject)stimulus.DeepClone();mixed["numberOfLocusts"]=3;mixed["swarm"]["autopilotSpeed"]=999;
        config=KannadiConfig.LoadSwarm(Phase(mixed));
        Check(config.numberOfLocusts==100 && config.autopilotSpeed==7,"Nested swarm fields override legacy flat copies without overwriting experiment-level motion.");
        Check(JToken.DeepEquals(stimulus,original),"Loading cannot mutate the runner's phase payload.");
        foreach(JToken invalid in new JToken[]{new JValue(false),new JValue("moth"),new JArray()})
        {
            var json=new JObject{{"swarm",invalid}};bool rejected=false;
            try {KannadiConfig.LoadSwarm(Phase(json));} catch(ArgumentException) {rejected=true;}
            Check(rejected,"Malformed nested swarm is rejected rather than spawning a fallback locust.");
        }
        var flat=new JObject{{"numberOfLocusts",2},{"agentVisual","Bogong"},{"bogongVisual",new JObject{{"shape","Dot"}}}};
        config=KannadiConfig.LoadSwarm(Phase(flat));
        Check(config.SwarmEnabled && config.numberOfLocusts==2 && config.bogongVisual.shape=="Dot","Flat phase payloads retain their original visual selection.");
        config=KannadiConfig.LoadSwarm(new Dictionary<string,object>{{"configFile","Examples/Swarm/selwyn-trajectory-playback.example.json"}});
        Check(config.SwarmEnabled && config.agentVisual=="Bogong" && config.bogongVisual.shape=="Moth" && !string.IsNullOrEmpty(config.trajectoryFile),
            "Existing flat CSV playback remains compatible.");
        Check(KannadiConfig.Load(Phase(flat)).numberOfLocusts==2,"Kannadi's original flat loader remains compatible.");
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Nested Swarm stage "+stage);
            if(Time.frameCount<nextFrame) return;
            if(stage==0) {SceneManager.LoadScene("ControlScene");stage=1;nextFrame=Time.frameCount+4;return;}
            var main=MainController.Instance;
            if(SceneManager.GetActiveScene().name!="Swarm" || main==null) return;
            var spawners=Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).OrderBy(p=>p.name).ToArray();
            var rigs=spawners.Select(p=>p.GetComponent<ClosedLoop>()).ToArray();
            if(main.SequenceError!=null) throw new Exception("Active sequence configuration error: "+main.SequenceError);
            if(stage==1)
            {
                Check(main.ExperimentPhase=="preStimulus" && spawners.Length==4,"Current sequence starts its heading pre-stimulus on four rigs.");
                Check(spawners.All(p=>p.Spawned.Count==0),"No locust or moth is instantiated for swarm:null.");
                Check(rigs.All(r=>Math.Abs(r.transform.position.y-100)<.01f),"Shared initial height is respected.");
                Check(rigs.All(r=>r.GetComponent<Keyboard>().AutopilotEnabled && r.GetComponent<Keyboard>().AutopilotSpeed==7),"Current autopilot edits are respected.");
                rigIds=rigs.Select(r=>r.GetInstanceID()).ToArray();sceneHandle=SceneManager.GetActiveScene().handle;
                stage=2;return;
            }
            if(stage==2)
            {
                if(main.ExperimentPhase=="preStimulus") {if(spawners.Any(p=>p.Spawned.Count!=0)) throw new Exception("Agents leaked into observation phase.");return;}
                Check(main.ExperimentPhase=="stimulus","The observed heading phase transitions to the stimulus.");
                Check(sceneHandle==SceneManager.GetActiveScene().handle && rigIds.SequenceEqual(rigs.Select(r=>r.GetInstanceID())),"Scene and rigs survive the transition.");
                foreach(var p in spawners)
                {
                    var rig=p.GetComponent<ClosedLoop>();var reference=rig.GetComponent<StimulusHeadingReference>().Result;
                    Check(reference!=null && reference.sampleCount>0,"A real per-frame heading reference is frozen for "+p.name);
                    Check(Math.Abs(Mathf.DeltaAngle(p.mu,(float)reference.meanHeadingDegrees-90))<.001f,"Swarm uses this animal's heading plus the configured -90 degree offset.");
                    Check(p.Spawned.Count==100 && p.dimension=="3D" && p.locustSpeed==7 && p.kappa==1000,"Current generated swarm count/speed/order are applied.");
                    Check(p.spawnVolumeSize==Vector3.one*10 && p.boundaryManager.followTarget==rig.transform && p.boundaryManager.wrapY,"The configured volume follows this animal.");
                    Check(p.bogongVisual.shape=="Moth" && p.bogongVisual.sizeMode=="Angular" && p.bogongVisual.angularSizeDegrees==2,"This rig has two-degree Bogong moths.");
                    var batches=p.GetComponentsInChildren<BogongSwarmRenderer>();
                    Check(batches.Length==1 && batches[0].GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Bogong/BatchedMoth" &&
                        p.Spawned.All(a=>a.GetComponent<Renderer>()==null && a.GetComponentsInChildren<Animator>().Length==0),"Only the batched moth renderer is used, without legacy locust meshes or Animators.");
                    Check(!rig.PositionTrackingEnabled && rig.OrientationTrackingEnabled && rig.OrientationGain==1 && Math.Abs(rig.transform.position.y-100)<.01f,
                        "Phase tracking flags and retained height remain intact: "+rig.name+" position="+rig.PositionTrackingEnabled+", orientation="+rig.OrientationTrackingEnabled+", y="+rig.transform.position.y);
                }
                var vr1=rigs.Single(r=>r.name.StartsWith("VR1"));
                Check(vr1.GetComponentsInChildren<Camera>(true).Where(c=>c.name.EndsWith("U") || c.name.EndsWith("D")).All(c=>!c.enabled),"System camera omissions are still honored.");
                var controller=Object.FindFirstObjectByType<SwarmController>();
                var noSwarm=(JObject)stimulus.DeepClone();noSwarm["swarm"]=null;
                controller.AdvanceStep(Phase(noSwarm));
                Check(spawners.All(p=>p.Spawned.Count==0),"A later swarm:null clears the old population.");
                controller.AdvanceStep(Phase(stimulus));
                Check(spawners.All(p=>p.Spawned.Count==100),"The nested moth swarm can be re-enabled on the same rigs.");
                stage=3;nextFrame=Time.frameCount+3;return;
            }
            if(stage==3)
            {
                Check(spawners.All(p=>p.GetComponentsInChildren<BogongSwarmRenderer>().Length==1),"Repeated phase changes leave one active batch per rig.");
                Check(errors.Count==0,"No runtime errors: "+string.Join("; ",errors));Finish(null);
            }
        }
        catch(Exception error) {Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;SceneManager.sceneLoaded-=DisableInputs;Application.logMessageReceived-=OnLog;
        SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../swarm-nested-config-validation.json"),JsonConvert.SerializeObject(new{
            checks,failure,errors,unity=Application.unityVersion,recipe=Recipe,
            note="Real current ControlScene sequence, 10-second heading observation, four rigs and nested 100-moth stimulus. Tracker input disabled in an isolated project. Main hardware/sequence/other config edits are not rewritten."
        },Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
