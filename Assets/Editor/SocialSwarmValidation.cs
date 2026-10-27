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
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Actual four-rig generated Swarm -> zero-extra-copy Kannadi; disposable copy, no native acquisition.</summary>
[InitializeOnLoad]
public static class SocialSwarmValidation
{
    const string Key="SocialSwarmValidation.Active";
    static int stage,nextFrame,checks;
    static double deadline;
    static readonly List<string> errors=new List<string>();
    static Kannadi[] social;
    static Vector3 changedPosition;
    static Quaternion changedRotation;
    static SocialSwarmValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
            stage=checks=0;nextFrame=Time.frameCount+5;deadline=EditorApplication.timeSinceStartup+120;errors.Clear();
            Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=DisableInputs;EditorApplication.update+=Tick;
        };
    }
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Log(string text,string stack,LogType type)
    {if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && text!="No backup data file found." && !stack.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(text);}
    static void DisableInputs(Scene scene,LoadSceneMode mode)
    {foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None))listener.enabled=false;}
    static Dictionary<string,object> FileParameters()=>new Dictionary<string,object>{{"configFile","Examples/Kannadi/social-coupling.example.json"}};
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        string root=Application.streamingAssetsPath;
        var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));
        var template=(JObject)system["configs"][0];var rigs=new JArray();
        for(int i=1;i<=4;i++)
        {
            var rig=(JObject)template.DeepClone();rig["vrId"]="VR"+i;rig["closedLoopMode"]="Hejjetrac";rig["zmqPort"]=29940+i;
            rig["displayOrder"]="F";rig["ledPanelWidth"]=64;rig["ledPanelHeight"]=64;rig["startCol"]=i-1;rig["tracker"]=null;rig["desktopPreview"]=false;rigs.Add(rig);
        }
        system["configs"]=rigs;system["telemetry"]=new JObject{{"enabled",false}};system["overheadCamera"]=new JObject{{"enabled",false}};
        File.WriteAllText(Path.Combine(root,"__social-swarm-system.json"),system.ToString());
        var sequence=JObject.Parse(File.ReadAllText(Path.Combine(root,"Examples/Sequences/swarm-entrainment-social.sequence.json")));
        foreach(var row in sequence["sequences"])row["duration"]=30;
        File.WriteAllText(Path.Combine(root,"__social-swarm-sequence.json"),sequence.ToString());
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Social swarm stage "+stage);
            if(Time.frameCount<nextFrame)return;nextFrame=Time.frameCount+3;
            var main=MainController.Instance;if(main==null)return;
            if(main.SequenceError!=null)throw new Exception(main.SequenceError);
            if(stage==0)
            {
                var logger=MasterDataLogger.Instance;
                if(logger.AcceptingWrites){_=logger.FinalizeSessionAsync("validation-setup-rotation");return;}
                if(logger.IsFinalizing)return;
                main.PrepareConfiguredRun("__social-swarm-system.json","__social-swarm-sequence.json");main.StartSequence();stage=1;return;
            }
            if(stage==1)
            {
                if(SceneManager.GetActiveScene().name!="Swarm")return;
                var spawners=Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Where(p=>p.isActiveAndEnabled).ToArray();
                Check(spawners.Length==4,"Four configured observers own distinct swarms");
                foreach(var spawner in spawners)
                {
                    Check(spawner.Spawned.Count==256 && spawner.GetComponent<SwarmTrajectoryPlayback>()==null,"Ordered stimulus uses generated agents only");
                    Check(spawner.boundaryManager.followTarget==spawner.transform && spawner.boundaryManager.preserveOvershoot,"Periodic boundary follows its own observer");
                    Check(Mathf.Abs(Mathf.DeltaAngle(spawner.transform.eulerAngles.y,Mathf.Round(spawner.transform.eulerAngles.y/90)*90))<.01f,"Observer starts at an exact cardinal yaw");
                    Vector3 sum=Vector3.zero;foreach(var agent in spawner.Spawned)sum+=agent.transform.forward;
                    Check(sum.magnitude/spawner.Spawned.Count>.97f,"Generated swarm has high directional order");
                    spawner.transform.position+=new Vector3(12345,0,-23456);
                    spawner.GetComponent<GeneratedSwarmMotion>().Advance(0);
                    foreach(var agent in spawner.Spawned)
                    {
                        Vector3 delta=agent.transform.position-spawner.boundaryManager.Center;
                        Check(Mathf.Abs(delta.x)<=200.01f && Mathf.Abs(delta.z)<=200.01f,"Observer teleport retains agents in its periodic volume");
                    }
                }
                typeof(MainController).GetField("timer",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(main,0f);stage=2;return;
            }
            if(stage==2)
            {
                if(SceneManager.GetActiveScene().name!="Kannadi")return;
                social=Object.FindObjectsByType<Kannadi>(FindObjectsSortMode.None).Where(r=>r.isActiveAndEnabled).OrderBy(r=>r.VRIndex).ToArray();
                Check(social.Length==4 && social.All(r=>r.numberOfRings==0 && r.Clones.Length==1),"Zero extra rings retains exactly one tracked center representation per animal");
                foreach(var rig in social)
                {
                    Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y,Mathf.Round(rig.transform.eulerAngles.y/90)*90))<.01f,"Social presentation independently resets to cardinal yaw");
                    foreach(var camera in rig.GetComponentsInChildren<Camera>(true))
                    {
                        int own=1<<RuntimeRigRegistry.StimulusLayer(rig.VRIndex);
                        Check((camera.cullingMask&own)==0 && (camera.cullingMask&RuntimeRigRegistry.StimulusMask)==(RuntimeRigRegistry.StimulusMask&~own),"Each animal sees every peer and excludes its own representation");
                    }
                }
                changedPosition=social[0].transform.position+new Vector3(3,0,4);changedRotation=Quaternion.Euler(0,37,0);
                social[0].transform.SetPositionAndRotation(changedPosition,changedRotation);stage=3;return;
            }
            if(stage==3)
            {
                Check(Vector2.Distance(new Vector2(social[0].Clones[0].transform.position.x,social[0].Clones[0].transform.position.z),new Vector2(changedPosition.x,changedPosition.z))<.01f,"Peer representation follows tracked translation");
                Quaternion prefab=social[0].tilePrefab.transform.rotation;
                Check(Quaternion.Angle(social[0].Clones[0].transform.rotation,changedRotation*prefab)<.01f,"Peer representation follows tracked heading");
                var parameters=FileParameters();parameters["numberOfRings"]=1;social[0].InitializeScene(parameters);
                Check(social.All(r=>r.Clones.Length==7),"One number switches all animals to one center plus six copies");
                social[0].InitializeScene(FileParameters());Check(social.All(r=>r.Clones.Length==1),"Switching back removes additional copies without duplicating the scene");
                Check(errors.Count==0,"No acquisition/scene runtime errors: "+string.Join(";",errors));Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=DisableInputs;SessionState.SetBool(Key,false);
        Directory.CreateDirectory("docs");File.WriteAllText("docs/social-swarm-validation.json",JsonConvert.SerializeObject(new{passed=failure==null,checks,error=failure,errors,scope="Actual four-rig generated Swarm to Kannadi, cardinal starts, ordered population, moving periodic volume, peer isolation and ring switching. Inputs disabled; no hardware or biological polarization claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);else Debug.Log("SOCIAL SWARM VALIDATION PASSED: "+checks);
        EditorApplication.isPlaying=false;EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
