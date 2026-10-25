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
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CapabilityGallery
{
    const string Key="CapabilityGallery.Active";
    static int index,stage,frame;static double deadline;static string output;
    static readonly List<object> records=new List<object>();static readonly List<string> errors=new List<string>();
    static readonly string[] names={"forest-choice","milky-way-terrain","panorama-terrain","choice-bars","optomotor","stripe-fixation","choice-objects","locust-swarm","kannadi"};
    static readonly string[] scenes={"Choice_Forrest","Choice_Selwyn","Choice_Selwyn","Choice","Optomotor","Choice","Choice","Swarm","Kannadi"};
    static CapabilityGallery(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=Loaded;deadline=EditorApplication.timeSinceStartup+240;}};}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Disposable project only");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/site/assets/gallery"));Directory.CreateDirectory(output);
        SessionState.SetString(Key+".Output",output);
        foreach(string scene in scenes.Distinct())if(!EditorBuildSettings.scenes.Any(s=>s.path=="Assets/Scenes/"+scene+".unity"&&s.enabled))
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene("Assets/Scenes/"+scene+".unity",true)}).ToArray();
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["targetDisplay"]=0;system["telemetry"]=JObject.FromObject(new{enabled=false});system["statusOverlay"]=JObject.FromObject(new{visible=false});system["overheadCamera"]=JObject.FromObject(new{enabled=false});
        foreach(var rig in system["configs"]){rig["displayOrder"]="F";rig["targetDisplay"]=0;}
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":false,\"sequences\":[]}");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static void Log(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(text+"\n"+trace);}
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        foreach(var c in Object.FindObjectsByType<ZmqListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
        foreach(var c in Object.FindObjectsByType<DataLogger>(FindObjectsSortMode.None))c.enabled=false;
        foreach(var c in Object.FindObjectsByType<Keyboard>(FindObjectsSortMode.None))c.enabled=false;
    }
    static JObject Config()
    {
        if(index==4)return JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/optomotor.template.json")));
        if(index==7){var j=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/swarm.template.json")));j["numberOfLocusts"]=100;j["spawnAreaSize"]=60;j["kappa"]=15;j["locustSpeed"]=3;return j;}
        if(index==8){var j=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/kannadi.template.json")));j["numberOfRings"]=3;j["hexRadius"]=8;return j;}
        var config=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/choice.template.json")));
        config["objects"]=new JArray();config["closedLoopPosition"]=false;config["closedLoopOrientation"]=false;config["autopilotEnabled"]=false;config["backgroundColor"]=null;
        if(index==1||index==2){config["aglHeight"]=100;config["initialPosition"]=JObject.FromObject(new{x=0,y=100,z=0});}
        if(index==1){var sky=(JObject)config["nightSky"];sky["enabled"]=true;sky["date"]="2026-10-07";sky["localTime"]="22:00:00";sky["utcOffsetHours"]=11;sky["timeZoneId"]=null;sky["advanceWithRealTime"]=false;sky["imageWidth"]=2048;sky["exposure"]=.9;sky["faintDetailCutoff"]=.03;}
        if(index==2)config["skyboxPath"]="Photosphere/adaminaby.png";
        if(index==3||index==5){config["uniformSkyColor"]=JObject.FromObject(new{r=.85,g=.85,b=.85,a=1});}
        return config;
    }
    static void Tick()
    {
        if(Time.frameCount<frame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Gallery timed out at"+index+" stage"+stage);
            if(stage==0){MainController.Instance?.StopSequence();SceneManager.LoadScene(scenes[index]);stage=1;frame=Time.frameCount+5;return;}
            if(stage==1)
            {
                var config=Config();var choice=Object.FindFirstObjectByType<ChoiceController>();
                if(choice!=null)
                {
                    if(index==0||index==6){var prefs=choice.prefabs.Where(p=>p!=null&&!p.name.ToLowerInvariant().Contains("band")).ToArray();string tree=prefs.FirstOrDefault(p=>p.name.StartsWith("tree",StringComparison.OrdinalIgnoreCase))?.name??prefs[0].name;
                        config["objects"]=new JArray(JObject.FromObject(new{type=tree,position=new{x=-9,y=-1,z=25},scale=new{x=2,y=2,z=2}}),JObject.FromObject(new{type=tree,position=new{x=9,y=-1,z=25},scale=new{x=2,y=2,z=2}}));}
                    if(index==3||index==5){string bar=choice.prefabs.First(p=>p!=null&&p.name=="Cube").name;string black=choice.materials.FirstOrDefault(m=>m!=null&&m.name.ToLowerInvariant().Contains("black"))?.name??"";
                        var objects=new JArray();foreach(float x in index==5?new[]{0f}:new[]{-6f,6f})objects.Add(JObject.FromObject(new{type=bar,material=black,position=new{x=x,y=6,z=20},visualAngleDegrees=6,scale=new{x=2,y=16,z=1}}));config["objects"]=objects;}
                }
                string file="__gallery-"+names[index]+".json";File.WriteAllText(Path.Combine(Application.streamingAssetsPath,file),config.ToString());
                var controller=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISceneController>().FirstOrDefault();
                if(controller==null)throw new Exception("No controller in"+scenes[index]);
                controller.InitializeScene(new Dictionary<string,object>{{"configFile",file}});
                if(index==0){var landscape=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Landscape.prefab");if(landscape!=null)Object.Instantiate(landscape);}
                File.WriteAllText(Path.Combine(SessionState.GetString(Key+".Output","") ,names[index]+".config.json"),config.ToString());
                foreach(var c in Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))c.enabled=false;
                foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.enabled=false;
                stage=2;frame=Time.frameCount+24;return;
            }
            if(stage==2)
            {
                var rigs=Object.FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None);var rig=rigs.First(r=>RuntimeRigRegistry.ParseIndex(r.name)==1);
                var camera=rig.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Main Camera F");
                camera.fieldOfView=80;camera.enabled=true;camera.farClipPlane=3000;
                if(index==0){var terrain=Terrain.activeTerrains.FirstOrDefault(t=>t.terrainData!=null);if(terrain!=null){var center=terrain.transform.position+terrain.terrainData.size*.5f;center.y=terrain.SampleHeight(center)+terrain.transform.position.y+2;camera.transform.position=center;camera.transform.rotation=Quaternion.Euler(0,35,0);}}
                if(index==1||index==2){camera.transform.rotation=Quaternion.Euler(index==1?-8:2,0,0);}
                if(index==7||index==8){camera.transform.position=new Vector3(40,55,-60);camera.transform.LookAt(new Vector3(0,0,0));camera.cullingMask=~LayerMask.GetMask("UI","OverheadCameraMarkers");camera.farClipPlane=1000;}
                var target=new RenderTexture(1440,900,24);target.Create();var oldTarget=camera.targetTexture;var oldRect=camera.rect;var active=RenderTexture.active;
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
                output=SessionState.GetString(Key+".Output","");File.WriteAllBytes(Path.Combine(output,names[index]+".jpg"),image.EncodeToJPG(90));
                records.Add(new{name=names[index],scene=scenes[index],image=names[index]+".jpg",configuration=names[index]+".config.json",additionalPrefab=index==0?"Assets/Prefabs/Landscape.prefab":null,width=1440,height=900,cameraPosition=new{x=camera.transform.position.x,y=camera.transform.position.y,z=camera.transform.position.z},cameraEuler=new{x=camera.transform.eulerAngles.x,y=camera.transform.eulerAngles.y,z=camera.transform.eulerAngles.z},fov=camera.fieldOfView,skyboxId=NightSkyController.CurrentId,skyboxSampleUtc=NightSkyController.CurrentSampleUtc,unity=Application.unityVersion,source="Actual Unity scene/controller render; synthetic demonstration, no measured animal behavior"});
                RenderTexture.active=active;camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Object.Destroy(target);Object.Destroy(image);
                index++;stage=0;if(index>=names.Length){Finish(null);return;}
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=Loaded;SessionState.SetBool(Key,false);
        output=SessionState.GetString(Key+".Output","");File.WriteAllText(Path.Combine(output,"provenance.json"),JsonConvert.SerializeObject(new{records,failure,errors},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
