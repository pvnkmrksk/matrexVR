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

/// <summary>Records real tracked scenes, then validates those saved rows through the actual ReplayScene.</summary>
[InitializeOnLoad]
public static class ReplayRoundtripValidation
{
    const string Key="ReplayRoundtripValidation.Active";
    static int stage,nextFrame,checks,seekIndex;
    static double deadline;
    static string output,runFolder;
    static ReplayController replay;
    static readonly List<string> errors=new List<string>();
    static readonly List<Observation> observations=new List<Observation>();
    static readonly List<ReplaySessionArchive.Sample> seeks=new List<ReplaySessionArchive.Sample>();
    static readonly Dictionary<string,int> sceneObservations=new Dictionary<string,int>();
    static readonly List<object> comparisons=new List<object>();
    static Texture2D sourceImage;
    sealed class Observation { public int frame; public long ticks; public string scene; public float[] position,rotation; public float[][] colors; public string image; }
    static ReplayRoundtripValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
            output=SessionState.GetString(Key+".Output","");stage=checks=seekIndex=0;nextFrame=Time.frameCount+5;deadline=EditorApplication.timeSinceStartup+240;
            observations.Clear();errors.Clear();sceneObservations.Clear();seeks.Clear();comparisons.Clear();
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        };
    }
    static string Arg(string key,string fallback=null){var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]==key)return args[i+1];return fallback;}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Disposable project only.");
        output=Path.GetFullPath(Arg("-replay-output",Path.Combine(Application.dataPath,"../replay-roundtrip")));Directory.CreateDirectory(output);
        SessionState.SetString(Key+".Output",output);SessionState.SetBool(Key,true);
        string root=Application.streamingAssetsPath;
        foreach(string name in new[]{"system_config.json","sequenceConfig.json"})if(File.Exists(Path.Combine(root,name)))File.Copy(Path.Combine(root,name),Path.Combine(output,"original-"+name),true);
        var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));
        var rig=(JObject)((JArray)system["configs"])[0].DeepClone();rig["vrId"]="VR1";rig["closedLoopMode"]=Arg("-replay-backend","Rekkefly");rig["zmqAddress"]="127.0.0.1";rig["zmqPort"]=int.Parse(Arg("-replay-port","29921"));rig["displayOrder"]="F";rig["targetDisplay"]=0;rig["ledPanelWidth"]=128;rig["ledPanelHeight"]=128;rig["tracker"]=null;
        system["configs"]=new JArray(rig);system["targetDisplay"]=0;system["telemetry"]=new JObject{["enabled"]=false};system["statusOverlay"]=new JObject{["visible"]=false};system["overheadCamera"]=new JObject{["enabled"]=false};
        Write(root,"__roundtrip-system.json",system);
        var choice=new JObject{["closedLoopPosition"]=false,["closedLoopOrientation"]=true,["autopilotEnabled"]=false,["initialPosition"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["initialRotation"]=new JObject{["x"]=0,["y"]=0,["z"]=0},["uniformSkyColor"]=new JObject{["r"]=.12,["g"]=.24,["b"]=.36,["a"]=1},["objects"]=new JArray(new JObject{["type"]="Cube",["material"]="Black",["position"]=new JObject{["x"]=-4,["y"]=1,["z"]=12},["scale"]=new JObject{["x"]=2,["y"]=8,["z"]=2}}),["autoTrimSettings"]=new JObject{["flightCheckEnabled"]=false}};
        Write(root,"__roundtrip-choice.json",choice);
        var dynamic=new JObject{["repetitions"]=1,["shuffle"]=false,["loop"]=false,["autoTrimSettings"]=new JObject{["flightCheckEnabled"]=false},["steps"]=new JArray(new JObject{["name"]="Recorded, color swap",["closedLoopPosition"]=false,["closedLoopOrientation"]=true,["swapAfterSeconds"]=1.5,["initialPosition"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["objects"]=new JArray(Bar(-4,new JArray(0,0,0,1),new JArray(.8,.3,.1,1)),Bar(4,new JArray(.7,.7,.7,1),new JArray(.1,.6,.8,1))),["camera"]=new JArray(new JObject{["vrId"]="VR1",["clearFlags"]="SolidColor",["bgColor"]=new JArray(.3,.3,.3,1)}),["trigger"]=new JObject{["type"]="time",["seconds"]=60}})};
        Write(root,"__roundtrip-dynamic.json",dynamic);
        Write(root,"__roundtrip-sequence.json",new JObject{["autoStart"]=false,["loop"]=false,["randomise"]=false,["returnToSetup"]=true,["sequences"]=new JArray(Row("Choice","configFile","__roundtrip-choice.json"),Row("Choice_desync","design","__roundtrip-dynamic.json"))});
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static JObject Bar(float x,JArray color,JArray swap)=>new JObject{["type"]="Cube",["material"]="SetColor",["pos"]=new JArray(x,1,12),["scale"]=new JObject{["x"]=2,["y"]=8,["z"]=2},["color"]=color,["swapColor"]=swap};
    static JObject Row(string scene,string key,string config)=>new JObject{["sceneName"]=scene,["duration"]=4,["gain"]=10,["reloadScene"]=true,["parameters"]=new JObject{[key]=config}};
    static void Write(string root,string name,JObject data)=>File.WriteAllText(Path.Combine(root,name),data.ToString());
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){if(trace.Contains("UnityEditor.Search.SearchDatabase"))return;errors.Add(message+"\n"+trace);}}
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Replay roundtrip stage "+stage);
            if(Time.frameCount<nextFrame)return;
            nextFrame=Time.frameCount+1;
            if(stage==0)
            {
                Check(MainController.Instance!=null&&MasterDataLogger.Instance!=null,"Control controller/logger available");
                MainController.Instance.PrepareConfiguredRun("__roundtrip-system.json","__roundtrip-sequence.json");
                runFolder=MasterDataLogger.Instance.directoryPath;
                MainController.Instance.StartSequence();stage=1;return;
            }
            if(stage==1)
            {
                var main=MainController.Instance;Check(main.SequenceError==null,"acquisition sequence valid");
                string scene=SceneManager.GetActiveScene().name;
                if(main.SequenceRunning&&(scene=="Choice"||scene=="Choice_desync"))
                {
                    var rig=RuntimeRigRegistry.Rigs(SceneManager.GetActiveScene()).FirstOrDefault();
                    if(rig==null)return;
                    var input=rig.GetComponent<ZmqListener>();
                    var sample=input.ReadRawSample();
                    if(!sample.hasPose||TimeSpan.FromTicks(DateTime.UtcNow.Ticks-sample.receivedUtcTicks).TotalSeconds>1)return;
                    float elapsed=4-main.RemainingStepSeconds;
                    int point=elapsed>2.5?2:elapsed>.5?1:0;
                    sceneObservations.TryGetValue(scene,out int previous);
                    if(point>previous)
                    {
                        var camera=rig.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Main Camera F");
                        var objects=Stimuli(scene);
                        string image=scene+"-"+point+".png";
                        sourceImage=Render(camera);File.WriteAllBytes(Path.Combine(output,image),sourceImage.EncodeToPNG());Object.Destroy(sourceImage);sourceImage=null;
                        observations.Add(new Observation{frame=Time.frameCount,ticks=DateTime.Now.Ticks,scene=scene,position=V(rig.transform.position),rotation=V(rig.transform.eulerAngles),colors=objects.Select(r=>C(r.sharedMaterial.color)).ToArray(),image=image});
                        sceneObservations[scene]=point;
                    }
                    return;
                }
                if(main.SequenceRunning)return;
                MasterDataLogger.Instance.ZipDataFolder();
                Check(observations.Count==4,"two live observations in each actual scene");
                var saved=new ReplaySessionArchive(runFolder);saved.LoadRigSamples();
                Check(saved.Rigs.Count==1,"actual CSV produces one canonical animal track");
                Check(saved.Rigs["VR1"].Select(s=>s.scene).Distinct().Contains("Choice_desync"),"both recorded scenes load");
                if(Arg("-replay-require-motion","true")=="true")Check(saved.Rigs["VR1"].Where(s=>s.scene=="Choice").Select(s=>s.rotation.y).Distinct().Count()>10,"external live input moved the tracked view");
                foreach(var observation in observations)
                {
                    var exact=saved.Rigs["VR1"].Where(s=>s.unityFrame==observation.frame&&s.scene==observation.scene).LastOrDefault();
                    Check(exact!=null,"rendered frame has a recorded CSV row: "+observation.image);
                    Check(Vector3.Distance(exact.position,Vec(observation.position))<.00001f&&Quaternion.Angle(Quaternion.Euler(exact.rotation),Quaternion.Euler(Vec(observation.rotation)))<.005f,"recording captures the rendered pose after movement: "+observation.image);
                    seeks.Add(exact);
                }
                seeks.Add(seeks[2]);seeks.Add(seeks[0]); // Reverse through swap and across scene boundary.
                File.WriteAllText(Path.Combine(output,"live-observations.json"),JsonConvert.SerializeObject(observations,Formatting.Indented));
                SceneManager.sceneLoaded+=SetupReplay;ReplayController.OpenSession(runFolder);stage=2;nextFrame=Time.frameCount+5;return;
            }
            if(stage==2)
            {
                replay=Object.FindFirstObjectByType<ReplayController>();if(replay==null||!replay.Ready||replay.Loading)return;
                Check(replay.LoadError==null,"actual ReplayScene opens the new recording");
                Check(replay.OverviewVisible,"God's-eye view is the replay default");
                replay.SetPlaying(false);stage=3;return;
            }
            if(stage==3)
            {
                replay.SeekSeconds(seeks[seekIndex].time);stage=4;nextFrame=Time.frameCount+4;return;
            }
            if(stage==4)
            {
                if(replay.environmentLoader.Loading)return;
                var expected=seeks[seekIndex];
                var marker=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="Recorded animal VR1");
                // The normalized slider is a float: compare against its actual sample/interpolation time.
                Check(Vector3.Distance(marker.position,expected.position)<.001f,"replayed recorded position: "+seekIndex);
                Check(Quaternion.Angle(marker.rotation,Quaternion.Euler(expected.rotation))<.02f,"replayed recorded yaw: "+seekIndex);
                Check(replay.environmentLoader.RecordedRigs.ContainsKey("VR1"),"built scene's recorded rig restored");
                var visuals=replay.environmentLoader.environmentRoot.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.name.StartsWith("Replay Cube ",StringComparison.Ordinal)).ToArray();
                var observation=observations.First(o=>o.frame==expected.unityFrame&&o.scene==expected.scene);
                Check(visuals.Length==observation.colors.Length,"recorded stimulus count restored: "+seekIndex);
                for(int i=0;i<visuals.Length;i++)Check(ColorDistance(visuals[i].sharedMaterial.color,observation.colors[i])<.00001f,"recorded object color including reverse swap: "+seekIndex+"/"+i);
                var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.name=="Replay animal view VR1");
                using(var pixels=new ImagePair(camera,Path.Combine(output,observation.image)))
                {
                    comparisons.Add(new{seek=seekIndex,scene=expected.scene,expected.unityFrame,colorsSwapped=expected.colorsSwapped,pixelMeanAbsoluteError=pixels.error});
                    File.WriteAllBytes(Path.Combine(output,"replay-"+seekIndex+".png"),pixels.replay.EncodeToPNG());
                    Check(pixels.error<.02,"recorded animal camera matches rendered pixels within2%: "+seekIndex+" error="+pixels.error);
                }
                Check(ZmqListener.ActiveWorkerCount==0,"replay does not start acquisition listeners");
                seekIndex++;
                if(seekIndex>=seeks.Count){Finish(null);return;}
                stage=3;return;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void SetupReplay(Scene scene,LoadSceneMode mode){if(scene.name!="ReplayScene")return;var controller=Object.FindFirstObjectByType<ReplayController>();controller.loop=false;controller.interpolateAnimalPoses=false;controller.showOverviewOnStart=true;SceneManager.sceneLoaded-=SetupReplay;}
    static Renderer[] Stimuli(string scene)
    {
        var root=GameObject.Find(scene=="Choice"?"ChoiceSpawnedObjects":"Rig_VR1_Objects");
        return root==null?Array.Empty<Renderer>():root.GetComponentsInChildren<Renderer>();
    }
    static Texture2D Render(Camera camera)
    {
        var oldTarget=camera.targetTexture;var oldRect=camera.rect;float oldAspect=camera.aspect;var active=RenderTexture.active;
        var texture=new RenderTexture(256,256,24);texture.Create();camera.targetTexture=texture;camera.rect=new Rect(0,0,1,1);camera.aspect=1;camera.Render();RenderTexture.active=texture;
        var image=new Texture2D(256,256,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
        camera.targetTexture=oldTarget;camera.rect=oldRect;camera.aspect=oldAspect;RenderTexture.active=active;texture.Release();Object.Destroy(texture);return image;
    }
    sealed class ImagePair:IDisposable
    {
        public Texture2D replay;Texture2D original;public double error;
        public ImagePair(Camera camera,string path){replay=Render(camera);original=new Texture2D(2,2,TextureFormat.RGB24,false);original.LoadImage(File.ReadAllBytes(path));var a=original.GetPixels32();var b=replay.GetPixels32();long difference=0;for(int i=0;i<a.Length;i++)difference+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);error=difference/(a.Length*3.0*255);}
        public void Dispose(){Object.Destroy(replay);Object.Destroy(original);}
    }
    static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};static Vector3 Vec(float[] p)=>new Vector3(p[0],p[1],p[2]);static float[] C(Color c)=>new[]{c.r,c.g,c.b,c.a};
    static float ColorDistance(Color c,float[] b)=>Mathf.Max(Mathf.Abs(c.r-b[0]),Mathf.Max(Mathf.Abs(c.g-b[1]),Mathf.Abs(c.b-b[2])));
    static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=SetupReplay;SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{passed=failure==null,checks,failure,errors,runFolder,comparisons,unity=Application.unityVersion,scope="Real two-scene MainController recording and actual ReplayScene; external native-format PUB input; no physical camera/LED claim."},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
