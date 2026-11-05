#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Actual Main execution of the portable GLB example sequence. Disposable project only.</summary>
[InitializeOnLoad]
public static class GlbExamplesStartupValidation
{
    const string Key="GlbExamplesStartupValidation.Active";
    static string output;
    static readonly List<string> checks=new List<string>(),errors=new List<string>();
    static readonly List<object> observations=new List<object>();
    static GlbExamplesStartupValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {output=SessionState.GetString(Key+".Output","");_=ValidateAsync();}};
    }
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Marked disposable project only.");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../glb-examples-startup-validation"));Directory.CreateDirectory(output);
        PlayerSettings.companyName="MATREX Validation";PlayerSettings.productName="GlbExamplesStartup-"+Guid.NewGuid().ToString("N");
        string root=Application.streamingAssetsPath;
        var system=JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));var rig=(JObject)system["configs"][0].DeepClone();
        rig["vrId"]="VR1";rig["closedLoopMode"]="Rekkefly";rig["zmqAddress"]="127.0.0.1";rig["zmqPort"]=39872;rig["desktopPreview"]=true;rig["displayOrder"]="F";rig["targetDisplay"]=0;
        rig["tracker"]=new JObject{["saveVideo"]=false};rig["manualControls"]=new JObject{["enabled"]=true};system["configs"]=new JArray(rig);
        system["targetDisplay"]=0;system["telemetry"]=new JObject{["enabled"]=false};system["statusOverlay"]=new JObject{["visible"]=false};system["overheadCamera"]=new JObject{["enabled"]=false};
        File.WriteAllText(Path.Combine(root,"__glb-system.json"),system.ToString());File.WriteAllText(Path.Combine(root,"system_config.json"),system.ToString());
        var sequence=JObject.Parse(File.ReadAllText(Path.Combine(root,"Examples/Sequences/custom-models.sequence.json")));sequence["returnToSetup"]=true;
        foreach(var row in sequence["sequences"].OfType<JObject>())row["duration"]=1;
        File.WriteAllText(Path.Combine(root,"__glb-sequence.json"),sequence.ToString());File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),sequence.ToString());
        SessionState.SetString(Key+".Output",output);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    static async Task Until(Func<bool> condition,string label,double seconds=40)
    {var clock=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(clock.Elapsed.TotalSeconds>seconds)throw new TimeoutException(label);await Task.Delay(10);}}
    static bool ColorMatches(JToken values,Color c)=>values is JArray a&&a.Count>=4&&Math.Abs((float)a[0]-c.r)<1e-5f&&Math.Abs((float)a[1]-c.g)<1e-5f&&Math.Abs((float)a[2]-c.b)<1e-5f&&Math.Abs((float)a[3]-c.a)<1e-5f;
    static IEnumerable<JObject> Materials(JObject state)=>((JArray)state["nodes"]).OfType<JObject>().SelectMany(n=>(JArray)n["renderers"]).OfType<JObject>().SelectMany(r=>(JArray)r["materials"]).OfType<JObject>();
    static void Log(string message,string stack,LogType type){if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;errors.Add(message+"\n"+stack);}
    static async Task ValidateAsync()
    {
        string failure=null,directory=null;Application.logMessageReceived+=Log;
        try
        {
            await Until(()=>MainController.Instance!=null&&MasterDataLogger.Instance!=null,"Control initialization");var main=MainController.Instance;
            Check(Application.persistentDataPath.Contains("GlbExamplesStartup-"),"Validation uses a fresh isolated native/application profile");
            await MasterDataLogger.Instance.FinalizeSessionAsync("validation-idle");
            var sequence=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"__glb-sequence.json")));
            var rows=sequence["sequences"].OfType<JObject>().ToArray();Check(rows.Length==3,"Supplied custom GLB sequence has all three example rows");
            var examples=rows.Select(row=>JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,(string)row["parameters"]["configFile"])))).ToArray();
            var models=examples.Select(doc=>(string)doc["objects"][0]["modelPath"]).ToArray();
            var hashes=models.ToDictionary(path=>path,path=>RunConfigArchive.Hash(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,path))));
            main.PrepareConfiguredRun("__glb-system.json","__glb-sequence.json");directory=MasterDataLogger.Instance.directoryPath;main.StartSequence();
            await Until(()=>main.SequenceRunning||main.SequenceError!=null,"GLB preloading and actual Main Start");
            Check(main.SequenceRunning&&main.SequenceError==null,"Main preloads all embedded GLBs before starting its scene clock");
            var observed=new HashSet<int>();var timer=System.Diagnostics.Stopwatch.StartNew();
            while(main.SequenceRunning||main.SequencePreparing)
            {
                if(timer.Elapsed.TotalSeconds>30)throw new TimeoutException("Three-row automatic sequence completion");
                int index=main.currentStep;
                if(main.SequenceRunning&&!main.SceneTransitionPending&&index>=0&&index<3&&!observed.Contains(index)&&SceneManager.GetActiveScene().name=="Choice")
                {
                    var target=Object.FindObjectsByType<RecordedWorldStateTarget>(FindObjectsSortMode.None).FirstOrDefault(item=>item.Key=="choice"&&item.transform.childCount==1);
                    if(target!=null)
                    {
                        var instance=target.transform.GetChild(0).gameObject;var renderers=instance.GetComponentsInChildren<Renderer>(true);var animations=instance.GetComponentsInChildren<Animation>(true);var clips=animations.SelectMany(a=>a.Cast<AnimationState>()).ToArray();
                        Check(instance.activeInHierarchy&&renderers.Any(r=>r.enabled),"Row "+index+": actual world GLB is active and rendered");
                        Check(clips.Length>0&&animations.Any(a=>a.isPlaying),"Row "+index+": imported original animation is actually playing");
                        var state=target.Capture(true);Check(!string.IsNullOrEmpty((string)state["animation"]),"Row "+index+": animation evidence is captured from the live object");
                        var appearance=examples[index]["objects"][0]["appearance"].ToObject<ModelAppearanceConfig>();
                        if(appearance.enabled)
                        {
                            var expected=new Color(appearance.color.r,appearance.color.g,appearance.color.b,appearance.color.a);
                            Check(instance.GetComponent<ModelAppearance>()!=null,"Row "+index+": instance appearance override is installed");
                            bool actual=false;foreach(var material in renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null))
                            foreach(string property in new[]{"_Color","_BaseColor","_BaseColorFactor","baseColorFactor"})if(material.HasProperty(property)){var c=material.GetColor(property);actual|=Math.Abs(c.r-expected.r)<1e-5f&&Math.Abs(c.g-expected.g)<1e-5f&&Math.Abs(c.b-expected.b)<1e-5f;}
                            Check(actual,"Row "+index+": actual live material contains the configured color");
                            Check(Materials(state).Any(m=>((JObject)m["properties"]).Properties().Any(p=>ColorMatches(p.Value["c"],expected))),"Row "+index+": realized world evidence contains the actual configured material color");
                        }
                        else Check(instance.GetComponent<ModelAppearance>()==null,"Exported locust keeps its original surface when appearance override is disabled");
                        if(index<2)Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMesh!=null&&r.bones.Length>0),"Row "+index+": original exported/downloaded skin and bones are present");
                        else Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMesh!=null&&r.sharedMesh.blendShapeCount>0),"Morph example retains its actual blend shapes");
                        observed.Add(index);observations.Add(new{index,model=models[index],clips=clips.Select(c=>c.name).ToArray(),state});
                    }
                }
                await Task.Delay(5);
            }
            await main.RecordingCompletion;Check(observed.Count==3,"All three source example GLBs were observed in real sequential Main scene execution");
            Check(main.SequenceError==null&&main.RecordingTransitionError==null,"Three-row sequence and VR finalization have no experiment or recorder error");
            var config=JObject.Parse(File.ReadAllText(Path.Combine(directory,"config_manifest.json")));Check((bool)config["complete"],"Complete configuration archive is retained");
            foreach(string model in models)
            {
                var file=config["files"].OfType<JObject>().Single(f=>(string)f["sourceRelativePath"]==model);
                Check((bool)file["consumed"]&&((JArray)file["roles"]).Values<string>().Contains("model"),model+": actual consumed model is classified as model evidence");
                Check((string)file["sha256"]==hashes[model]&&RunConfigArchive.Hash(File.ReadAllBytes(Path.Combine(directory,(string)file["snapshotPath"])))==hashes[model],model+": archived GLB bytes match the provided original source");
                Check(RunConfigArchive.Hash(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,model)))==hashes[model],model+": running and archiving never modify the external model source");
            }
            foreach(var row in rows)Check(config["files"].Any(f=>(string)f["sourceRelativePath"]==(string)row["parameters"]["configFile"]),"Referenced example JSON is archived: "+row["parameters"]["configFile"]);
            var presentations=File.ReadLines(Path.Combine(directory,"presentations.jsonl")).Select(JObject.Parse).ToArray();Check(presentations.Length==3,"Presentation journal contains exactly three configured model presentations");
            var world=File.ReadLines(Path.Combine(directory,"world_state.jsonl")).Select(JObject.Parse).ToArray();
            foreach(string model in models)
            {
                var presentation=presentations.Single(row=>ExternalModelLibrary.CollectReferences(row["config"]).Contains(model));
                string id=(string)presentation["id"];
                Check(world.Any(frame=>(string)frame["presentationId"]==id&&((JArray)frame["targets"]).Any(target=>(string)target["key"]=="choice")),model+": realized world evidence is bound to its exact configured presentation");
            }
            var manifest=JObject.Parse(File.ReadAllText(Path.Combine(directory,"recording_manifest.json")));Check((string)manifest["lifecycle"]?["status"]=="finalized"&&(bool?)manifest["lifecycle"]?["writersDrained"]==true,"VR recording is finalized with all writers drained");
            Check(File.Exists(directory+".zip"),"Final VR archive ZIP exists");using(var archive=ZipFile.OpenRead(directory+".zip"))Check(archive.Entries.Count(e=>e.FullName.EndsWith(".glb",StringComparison.OrdinalIgnoreCase))>=3,"Final ZIP includes the source GLB bytes");
            await Task.Delay(100);Check(ModelAppearance.ActiveMaterialCount==0,"Scene changes release all instance appearance materials");
            Check(errors.Count==0,"Actual Main custom GLB sequence has no Unity runtime errors: "+string.Join("; ",errors));
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        finally
        {
            Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
            File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,errors,observations,directory,unity=Application.unityVersion,scope="Actual Main execution of provided exported locust/skeletal/morph GLB example sequence at1sec per row, live appearances/animations/world evidence and immutable consumed model bytes with final ZIP. Video disabled; no native camera or input acquisition."},Formatting.Indented));
            EditorApplication.Exit(failure==null?0:1);
        }
    }
}
#endif
