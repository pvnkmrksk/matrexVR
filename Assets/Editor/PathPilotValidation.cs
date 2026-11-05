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
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PathPilotValidation
{
    const string Key="PathPilotValidation.Active";
    const string Folder="Experiments/PathAnglePilot/";
    static string output, eventPath;
    static int stage, test, contacts;
    static double deadline, next;
    static bool moved, paused;
    static readonly List<string> checks=new();
    static readonly List<string> errors=new();
    static readonly List<string> editorDiagnostics=new();
    static readonly List<object> sessions=new();
    static JObject source;
    static MainController main;
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);checks.Add(message);}
    static PathPilotValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            output=SessionState.GetString(Key+"Output","");source=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Folder+"design.json")));
            checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+"Checks","[]")));
            stage=0;test=0;deadline=EditorApplication.timeSinceStartup+340;next=EditorApplication.timeSinceStartup+1;
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        };
    }
    static void Log(string message,string stack,LogType type)
    {
        if(stack.Contains("UnityEditor.Search.SearchDatabase")) { editorDiagnostics.Add(message+"\n"+stack); return; }
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../ANT_PILOT_VALIDATION_COPY")))throw new Exception("Isolated pilot project required.");
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-pilot-output");
        output=Path.GetFullPath(index>=0?args[index+1]:Path.Combine(Application.dataPath,"../validation-output/runtime"));
        Directory.CreateDirectory(output);
        PathPilotPreview.ExportImages(Path.Combine(output,"previews"));
        // Geometry checked independently against fixed known endpoints and reflex-angle expectations.
        Check(Math.Abs(PathGround.ForkZ(40))<.0001,"40 degrees splits at the origin");
        Check(Math.Abs(PathGround.ForkZ(180)-56.38156)<.001,"180 degrees is a T at goal depth");
        Check(Math.Abs(PathGround.ForkZ(210)-61.8801)<.01,"210 degrees forks beyond the goals");
        foreach(int angle in PathPilotPreview.Angles) {
            double z=PathGround.ForkZ(angle);double measured=2*Math.Atan2(20.5212086,56.3815572-z)*180/Math.PI;
            Check(Math.Abs(measured-angle)<.001,"Measured branch opening "+angle);
        }
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Folder+"system.preview.json")));
        system["configs"][0]["manualControls"]["enabled"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,Folder+"validation-system.json"),system.ToString());
        SessionState.SetString(Key+"Checks",JsonConvert.SerializeObject(checks));
        SessionState.SetString(Key+"Output",output);SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static JObject Fixture()
    {
        var design=(JObject)source.DeepClone();design["seed"]=2041;design["sessionSeconds"]=300;
        if(test==0) {
            design["intertrial"]["trigger"]["seconds"]=.05;
            foreach(JObject step in design["steps"])step["trigger"]=new JObject{["type"]="time",["seconds"]=.1};
        } else {
            design["steps"]=new JArray(source["steps"][test==2?3:9].DeepClone());
            if(test>=3) {
                design["sessionSeconds"]=3;
                if(test!=4) design["intertrial"]["trigger"]["seconds"]=.1;
            }
        }
        return design;
    }
    static List<JObject> Events() => string.IsNullOrEmpty(eventPath)||!File.Exists(eventPath)?new():File.ReadAllLines(eventPath).Select(JObject.Parse).ToList();
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Pilot validation stage "+stage+" case "+test);
            if(EditorApplication.timeSinceStartup<next)return;
            if(errors.Count>0)throw new Exception("Unity runtime error: "+errors[0]);
            if(stage==0) {
                main=MainController.Instance;var logger=MasterDataLogger.Instance;if(main==null||logger==null)return;
                if(logger.AcceptingWrites){_=logger.FinalizeSessionAsync("validation-setup");return;}
                if(logger.IsFinalizing)return;
                if(test>=6){Finish(null);return;}
                var fixture=Fixture();string fixtureName="validation-"+test+".json";
                File.WriteAllText(Path.Combine(Application.streamingAssetsPath,Folder+fixtureName),fixture.ToString());
                File.WriteAllText(Path.Combine(output,fixtureName),fixture.ToString());
                var sequence=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,Folder+"sequence.json")));
                sequence["sequences"][0]["parameters"]["design"]=Folder+fixtureName;
                File.WriteAllText(Path.Combine(Application.streamingAssetsPath,Folder+"validation-sequence.json"),sequence.ToString());
                main.PrepareConfiguredRun(Folder+"validation-system.json",Folder+"validation-sequence.json");
                eventPath=Path.Combine(logger.directoryPath,"path_pilot_events.jsonl");
                sessions.Add(new {test,directory=logger.directoryPath});
                main.StartSequence();contacts=0;moved=false;paused=false;stage=1;next=EditorApplication.timeSinceStartup+.2;return;
            }
            var controller=Object.FindFirstObjectByType<DynamicSequenceController>();
            var events=Events();var starts=events.Where(e=>(string)e["kind"]=="trial_start").ToArray();var ends=events.Where(e=>(string)e["kind"]=="trial_end").ToArray();
            if(test==0) {
                var blocks=events.Where(e=>(string)e["kind"]=="block_end"&&(bool?)e["data"]["incomplete"]==false).ToArray();
                if(blocks.Length<2)return;
                var orders=events.Where(e=>(string)e["kind"]=="block_start").Take(2).Select(e=>(JArray)e["data"]["order"]).ToArray();
                foreach(var order in orders)Check(order.Count==10&&order.Select(v=>v.ToString()).Distinct().Count()==10,"Complete randomized block contains each of ten conditions exactly once");
                Check(orders[0].ToString()!=orders[1].ToString(),"Successive blocks are reshuffled with a continuing recorded RNG seed");
                CompleteCase();return;
            }
            if(test==1) {
                if(starts.Length>0&&!moved) {
                    var ground=Object.FindFirstObjectByType<PathGround>();Check(ground!=null,"No-bars trial retains painted ground");
                    Check(ground.GetComponentsInChildren<Collider>().Length==0,"Ground has no colliders");
                    Check(Object.FindObjectsByType<TriggerRelay>(FindObjectsSortMode.None).Length==0,"No-bars condition has no goal triggers");
                    MoveGoal(-1);moved=true;
                    Capture("runtime-no-bars.png");
                }
                if(ends.Length==0)return;
                Check((string)ends[0]["data"]["outcome"]=="duration_complete","Crossing the absent goal does not terminate the no-bars trial");
                Check((double)ends[0]["data"]["durationSeconds"]>=20&&(double)ends[0]["data"]["durationSeconds"]<21,"No-bars trial lasts the configured 20 seconds");
                Check((double)starts[0]["elapsedSeconds"]-(double)events.First(e=>(string)e["kind"]=="intertrial_start")["elapsedSeconds"]>=5,"Initial intertrial lasts the configured 5 seconds");
                CompleteCase();return;
            }
            if(test==2) {
                if(starts.Length>contacts&&contacts<2&&main.SessionElapsedSeconds-(double)starts.Last()["elapsedSeconds"]>.3) {
                    var ground=Object.FindFirstObjectByType<PathGround>();Check(ground!=null,"Bar trial includes ground");
                    var rig=GameObject.Find("VR1");var camera=rig.GetComponentsInChildren<Camera>().First(c=>c.enabled);
                    Check((camera.cullingMask&(1<<ground.gameObject.layer))!=0,"Animal camera sees its ground layer");
                    if(contacts==0)Capture("runtime-bars.png");
                    var trigger=Object.FindObjectsByType<TriggerRelay>(FindObjectsSortMode.None).First().GetComponent<CapsuleCollider>();
                    Check(Math.Abs(trigger.radius-3.5f)<.001&&Math.Abs(trigger.height-100)<.001,"Historical capsule contact geometry retained");
                    MoveGoal(contacts==0?-1:1);contacts++;
                }
                if(ends.Length<3)return;
                Check((string)ends[0]["data"]["goalSide"]=="left"&&(string)ends[1]["data"]["goalSide"]=="right","Real physics contacts record left and right goals");
                Check((string)ends[2]["data"]["outcome"]=="no_choice_timeout","Stationary bar trial records no choice on timeout");
                Check((double)ends[2]["data"]["durationSeconds"]>=120&&(double)ends[2]["data"]["durationSeconds"]<121,"Bar timeout lasts the configured 120 seconds");
                CompleteCase();return;
            }
            if(test>=3) {
                bool ready=test==4?events.Any(e=>(string)e["kind"]=="intertrial_start"):starts.Length>0;
                if(ready&&!paused) {paused=true;if(test==3)Time.timeScale=0;else EditorApplication.isPaused=true;}
                if(!events.Any(e=>(string)e["kind"]=="session_end"))return;
                Check(!main.SequenceRunning&&!controller.PilotActive,"Deadline stops session, case "+test);
                Check(!Object.FindObjectsByType<PathGround>(FindObjectsSortMode.None).Any(),"Deadline hides all ground and bar containers, case "+test);
                string kind=test==4?"intertrial_interrupted":"trial_interrupted";
                Check(events.Any(e=>(string)e["kind"]==kind),"Deadline explicitly records "+kind+", case "+test);
                Check(events.Any(e=>(string)e["kind"]=="block_end"&&(bool?)e["data"]["incomplete"]==true),"Deadline records incomplete final block, case "+test);
                double elapsed=(double)events.Last(e=>(string)e["kind"]=="session_end")["elapsedSeconds"];
                Check(elapsed>=3&&elapsed<3.5,"Monotonic deadline survives "+(test==3?"timeScale=0":"editor pause")+", case "+test);
                CompleteCase();
            }
        }catch(Exception e){Finish(e.ToString());}
    }
    static void MoveGoal(int side) {var rig=GameObject.Find("VR1").GetComponent<ClosedLoop>();rig.ApplyStartPose(new Vector3(side*PathGround.RightGoal.x,0,PathGround.RightGoal.y),Quaternion.identity,true,true);Physics.SyncTransforms();}
    static void Capture(string name) {
        var cam=GameObject.Find("VR1").GetComponentsInChildren<Camera>().First(c=>c.enabled);
        var old=cam.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(512,512,24);cam.targetTexture=target;cam.Render();RenderTexture.active=target;
        var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
        cam.targetTexture=old;RenderTexture.active=active;Object.Destroy(pixels);target.Release();Object.Destroy(target);
    }
    static void CompleteCase() {
        EditorApplication.isPaused=false;Time.timeScale=1;
        if(main.SequenceRunning)main.StopSequence("validation-case-complete");
        File.Copy(eventPath,Path.Combine(output,"events-"+test+".jsonl"),true);
        test++;stage=0;next=EditorApplication.timeSinceStartup+.2;
    }
    static void Finish(string failure) {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
        EditorApplication.isPaused=false;Time.timeScale=1;
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new {passed=failure==null,failure,checks,errors,editorDiagnostics,sessions,unity=Application.unityVersion,scope="Actual Choice_desync runtime: synthetic pose contacts, real 5/20/120 second timings, accelerated blocks and deadlines; no physical apparatus or three-hour soak"},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
