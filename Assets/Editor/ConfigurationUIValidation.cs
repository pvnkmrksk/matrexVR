#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

/// <summary>Uses a disposable copy. Includes real runtime UI, panel rendering and repeated run transitions.</summary>
[InitializeOnLoad]
public static class ConfigurationUIValidation
{
    private const string Key="ConfigurationUIValidation.Active";
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static readonly Dictionary<string,double> timings=new Dictionary<string,double>();
    private static int phase,frame,run;
    private static double deadline;
    private static RenderTexture texture;
    private static void Check(bool ok,string label) { if(!ok) throw new Exception(label); checks.Add(label); }
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Call(object instance,string method,params object[] args) => instance.GetType().GetMethod(method,Flags).Invoke(instance,args);
    private static void Put(object instance,string name,object value) => instance.GetType().GetField(name,Flags).SetValue(instance,value);
    static ConfigurationUIValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            deadline=EditorApplication.timeSinceStartup+120; phase=0; frame=Time.frameCount+8;
            Application.logMessageReceived+=OnLog; EditorApplication.update+=Tick;
            SceneManager.sceneLoaded+=DisableInputs;
        };
    }
    private static void OnLog(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) if(!stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(message); }
    private static void DisableInputs(Scene scene,LoadSceneMode mode)
    { foreach(var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled=false; }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy required.");
        ConfigurationSchemaBuilder.Generate(); UnitChecks();
        var system=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Templates/system_config.template.json")));
        system["telemetry"]["enabled"]=false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),system.ToString());
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"ui-smoke.json"),"{\"objects\":[],\"closedLoopOrientation\":false,\"closedLoopPosition\":false}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":false,\"randomise\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":0.25,\"gain\":1,\"parameters\":{\"configFile\":\"ui-smoke.json\"}}]}");
        SessionState.SetBool(Key,true); EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"); EditorApplication.isPlaying=true;
    }
    public static void RunArchiveRegression()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy required.");
        try { ConfigurationSchemaBuilder.Generate(); UnitChecks(); }
        catch(Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); return; }
        var sequence=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json")));
        sequence["autoStart"]=true; sequence["sequences"][0]["duration"]=30;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),sequence.ToString());
        RunConfigArchiveValidation.Run();
    }
    private static void UnitChecks()
    {
        var schema=ConfigSchema.Load();
        string scratch=Path.Combine(Application.dataPath,"../ui-test-files"); Directory.CreateDirectory(scratch);
        foreach(string file in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath,"Templates"),"*.json").Concat(Directory.GetFiles(Path.Combine(Application.streamingAssetsPath,"Examples"),"*.json",SearchOption.AllDirectories)))
        {
            byte[] bytes=File.ReadAllBytes(file); var original=LosslessJson.Parse(RunConfigArchive.Decode(bytes));
            string id=Infer(file,original); string temp=Path.Combine(scratch,Guid.NewGuid()+".json"); File.WriteAllBytes(temp,bytes);
            var doc=new ConfigDocument(temp,id); doc.Save(); Check(File.ReadAllBytes(temp).SequenceEqual(bytes),"Byte-identical no-op: "+Path.GetFileName(file));
            Check(ConfigSchema.Validate(original,schema.Get(id)).Count==0,"Schema matches: "+Path.GetFileName(file));
            // Change every leaf through the same DOM path used by the form, then undo each change.
            var paths=new List<string[]>(); Leaves(original,new List<string>(),paths);
            foreach(var parts in paths)
            {
                JToken leaf=ConfigDocument.At(doc.Root,parts); JToken replacement=leaf.Type==JTokenType.String?new JValue("ui-test"):leaf.Type==JTokenType.Boolean?new JValue(!(bool)leaf):leaf.Type==JTokenType.Null?new JValue("test"):new JRaw("1.25");
                doc.Edit(root=>ConfigDocument.At(root,parts).Replace(replacement)); doc.Undo();
                Check(LosslessJson.Equivalent(doc.Root,original),"Leaf round trip: "+Path.GetFileName(file)+"/"+string.Join("/",parts));
            }
            doc.ApplyJson(original.ToString(Formatting.Indented)); doc.Save();
            Check(LosslessJson.Equivalent(doc.Root,original),"Raw/form equivalence: "+Path.GetFileName(file));
        }
        string exact="{\"date\":\"2026-10-13\",\"unknown\":{\"large\":1.23456789012345678901234567890123456789,\"exponent\":1e+999,\"integer\":999999999999999999999999999999999999999,\"negativeZero\":-0},\"null\":null,\"false\":false,\"zero\":0,\"array\":[[],{},null]}";
        string precise=Path.Combine(scratch,"precise.json"); File.WriteAllText(precise,exact);
        var precision=new ConfigDocument(precise,"unknown"); precision.Edit(root=>root["date"]="unchanged precision"); precision.Save();
        string saved=File.ReadAllText(precise);
        Check(saved.Contains("1.23456789012345678901234567890123456789") && saved.Contains("1e+999") && saved.Contains("-0"),"Unknown fields and arbitrary numeric precision retained after form save");
        Check(precision.Root["null"].Type==JTokenType.Null && precision.Root["false"].Type==JTokenType.Boolean && precision.Root["zero"].ToString()=="0" && precision.Root["omitted"]==null,"Missing/null/false/zero remain distinct");
        foreach(string invalid in new[]{"{\"x\":1,\"x\":2}","{\"x\":01}","{\"x\":NaN}","{\"x\":1,}","{\"x\":\"\\q\"}","{\"x\":true} garbage"})
        { bool rejected=false; try { LosslessJson.Parse(invalid); } catch { rejected=true; } Check(rejected,"Reject malformed/ambiguous JSON: "+invalid); }
        File.WriteAllText(precise,"{\"external\":true}");
        Check(precision.CheckExternalChange() && precision.Root["external"]!=null,"Clean external JSON edits refresh the DOM");
        precision.Edit(root=>root["draft"]=true); File.WriteAllText(precise,"{\"external\":false}");
        bool conflict=false; try { precision.Save(); } catch(IOException) { conflict=true; }
        Check(conflict && File.ReadAllText(precise)=="{\"external\":false}","Dirty external conflict never overwrites disk");
        var rigSchema=(JObject)schema.Get("system")["properties"]["configs"]["items"];
        var rig=LosslessJson.Parse("{\"closedLoopMode\":\"Kinefly\"}");
        Check(!ConfigSchema.Visible(rig,rigSchema,"sphereDiameter"),"Flight hides walking ball calibration"); rig["closedLoopMode"]="FicTrac";
        Check(ConfigSchema.Visible(rig,rigSchema,"sphereDiameter"),"Walking shows ball calibration");
        var autopilot=LosslessJson.Parse("{\"autopilotEnabled\":false,\"autopilotSpeed\":7}");
        Check(!ConfigSchema.Enabled(autopilot,schema.Get("choice"),"autopilotSpeed"),"Disabled autopilot greys speed without deleting it");
        autopilot["autopilotEnabled"]=true; Check(ConfigSchema.Enabled(autopilot,schema.Get("choice"),"autopilotSpeed"),"Enabling autopilot restores its speed");
        var disabled=LosslessJson.Parse("{\"enabled\":false,\"durationSeconds\":10}"); var phaseSchema=(JObject)schema.Get("choice")["properties"]["stimulus"];
        Check(ConfigSchema.Enabled(disabled,phaseSchema,"enabled") && !ConfigSchema.Enabled(disabled,phaseSchema,"durationSeconds"),"Disabled phases preserve an editable enable toggle");
        Check(!ConfigSchema.Enabled(LosslessJson.Parse("{\"windowSeconds\":10}"),(JObject)phaseSchema["properties"]["headingReference"],"windowSeconds"),"Omitted enable flags use runtime defaults without inserting JSON keys");
        Check(ConfigSchema.Enabled(LosslessJson.Parse("{\"enabled\":42,\"future\":1}"),new JObject(),"future"),"Arbitrary extension flags are preserved without boolean coercion");
        var swarm=(JObject)schema.Get("swarm"); var setting=LosslessJson.Parse("{\"dimension\":\"2D\",\"muElevation\":12}");
        Check(!ConfigSchema.Visible(setting,swarm,"muElevation"),"2D hides elevation"); setting["dimension"]="3D"; Check(ConfigSchema.Visible(setting,swarm,"muElevation"),"3D reveals saved elevation");
        Check(!ConfigSchema.Enabled(LosslessJson.Parse("{\"vSyncCount\":1}"),schema.Get("system"),"targetFrameRate"),"VSync greys software FPS cap");
        Check(!ConfigSchema.Visible(autopilot,schema.Get("choice"),"aglHeight",new JObject{["flight"]=false}) && ConfigSchema.Visible(autopilot,schema.Get("choice"),"aglHeight",new JObject{["flight"]=true}),"System movement mode gates flight terrain settings");
        Check(ConfigDocument.At(LosslessJson.Parse("{\"list\":[]}"),new[]{"list","9"})==null,"External array shrink safely resets stale navigation");
        Check(ConfigSchema.Validate(LosslessJson.Parse("{\"targetFrameRate\":1e999,\"configs\":[]}"),schema.Get("system")).Count>0,"Out-of-range known numbers produce validation errors without losing the JSON");
        var huge=new JObject { ["sequences"]=new JArray(Enumerable.Range(0,10000).Select(i=>(JToken)new JObject { ["sceneName"]="Choice",["duration"]=1,["parameters"]=new JObject{["configFile"]="ui-smoke.json"},["extension"]=i })) };
        var watch=Stopwatch.StartNew(); string stress=huge.ToString(Formatting.None); var parsed=LosslessJson.Parse(stress); watch.Stop(); timings["parse10000StepsMs"]=watch.Elapsed.TotalMilliseconds;
        Check(((JArray)parsed["sequences"]).Count==10000 && parsed["sequences"][9999]["extension"].ToString()=="9999","10,000 sequence rows retain every value");
        watch.Restart(); Check(ConfigSchema.Validate(parsed,schema.Get("sequence")).Count==0,"10,000 rows pass schema validation"); watch.Stop(); timings["validate10000StepsMs"]=watch.Elapsed.TotalMilliseconds;
        File.WriteAllText(Path.Combine(Application.dataPath,"../ui-unit-validation.json"),JsonConvert.SerializeObject(new{checks=checks.Count,timings,passed=checks},Formatting.Indented));
    }
    private static void Leaves(JToken token,List<string> path,List<string[]> leaves)
    {
        if(token is JObject obj) foreach(var p in obj.Properties()) { path.Add(p.Name); Leaves(p.Value,path,leaves); path.RemoveAt(path.Count-1); }
        else if(token is JArray array) for(int i=0;i<array.Count;i++) { path.Add(i.ToString()); Leaves(array[i],path,leaves); path.RemoveAt(path.Count-1); }
        else leaves.Add(path.ToArray());
    }
    private static string Infer(string file,JToken obj)
    {
        if(obj["configs"]!=null) return "system"; if(obj["sequences"]!=null) return "sequence";
        if(obj["steps"]!=null || obj["intertrial"]!=null) return "dynamic-choice";
        JToken payload=obj["stimulus"]??obj;
        if(payload["stimuli"]!=null) return "optomotor";
        if(payload["vrConfigs"]!=null || file.Contains("/Swarm/") || file.Contains("swarm.template") || file.Contains("swarm-playback.template")) return "swarm";
        if(file.Contains("kannadi") || file.Contains("kinematic")) return "kannadi";
        return "choice";
    }
    private static void Tick()
    {
        if(Time.frameCount<frame) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Runtime UI timeout");
            var ui=ConfigWorkbench.Instance;
            if(phase==0)
            {
                Check(ui!=null && ui.Document.rootVisualElement.childCount>0,"Runtime setup panel mounts in ControlScene");
                Check(ExperimentInput.IsEditingText,"Setup captures experiment hotkeys");
                texture=new RenderTexture(1440,900,24); texture.Create(); ui.Document.panelSettings.targetTexture=texture;
                phase=1; frame=Time.frameCount+8;
            }
            else if(phase==1)
            {
                RenderTexture.active=texture; var image=new Texture2D(1440,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply(); RenderTexture.active=null;
                File.WriteAllBytes(Path.Combine(Application.dataPath,"../config-ui-preview.png"),image.EncodeToPNG()); Object.Destroy(image);
                ui.Document.panelSettings.targetTexture=null; Object.Destroy(texture);
                // Actual form callback edits a value; raw view applies it back to the same document.
                var boolField=ui.Document.rootVisualElement.Query<Toggle>().ToList().First(t=>t.parent?.ClassListContains("field-controls")==true);
                boolField.value=!boolField.value;
                Check(ui.Workspace.Sequence.Dirty,"Actual UI toggle updates JSON DOM"); ui.Workspace.Sequence.Undo();
                ui.Workspace.Sequence.Edit(doc=>{ doc["sequences"][0]["sceneName"]="Swarm"; doc["sequences"][0]["parameters"]["dimension"]="2D"; doc["sequences"][0]["parameters"]["muElevation"]=12; });
                var inlinePath=(List<string>)ui.GetType().GetField("path",Flags).GetValue(ui); inlinePath.AddRange(new[]{"sequences","0","parameters"}); Call(ui,"BuildContent");
                var inlineRows=(List<string>)ui.GetType().GetField("rows",Flags).GetValue(ui);
                Check(!inlineRows.Contains("muElevation"),"Actual inline Swarm form hides 3D-only fields in 2D");
                ui.Workspace.Sequence.Root["sequences"][0]["parameters"]["dimension"]="3D"; Call(ui,"BuildContent");
                Check(inlineRows.Contains("muElevation"),"Actual inline Swarm form reveals preserved elevation in 3D");
                ui.Workspace.Sequence.Undo(); inlinePath.Clear();
                Put(ui,"jsonMode",true); Call(ui,"BuildContent");
                var raw=ui.Document.rootVisualElement.Q<TextField>(className:"raw-json"); raw.value=raw.value.Replace("0.25","0.3");
                var apply=ui.Document.rootVisualElement.Query<Button>().ToList().First(b=>b.text=="Apply JSON");
                using(var submit=NavigationSubmitEvent.GetPooled()) { submit.target=apply; apply.SendEvent(submit); }
                Check(ui.Workspace.Sequence.Root["sequences"][0]["duration"].ToString()=="0.3","Actual raw JSON Apply callback updates the document");
                Put(ui,"jsonMode",false); Call(ui,"BuildContent");
                // Mount a huge array and measure actual virtualization rather than rendering all controls.
                var items=(JArray)ui.Workspace.Sequence.Root["sequences"]; JToken original=items[0].DeepClone();
                for(int i=1;i<10000;i++) items.Add(original.DeepClone());
                var nav=(List<string>)ui.GetType().GetField("path",Flags).GetValue(ui); nav.Add("sequences");
                var watch=Stopwatch.StartNew(); Call(ui,"BuildContent"); watch.Stop(); timings["mount10000RowsMs"]=watch.Elapsed.TotalMilliseconds;
                phase=2; frame=Time.frameCount+8;
            }
            else if(phase==2)
            {
                int mounted=ui.Document.rootVisualElement.Query<VisualElement>(className:"field-row").ToList().Count;
                Check(mounted<40,"10,000 row ListView mounts fewer than 40 visual rows (actual "+mounted+")");
                Check(mounted>0,"Large ListView actually renders controls");
                ui.Workspace.Sequence.Reload(); ((List<string>)ui.GetType().GetField("path",Flags).GetValue(ui)).Clear(); Call(ui,"BuildContent");
                string altSequence=Path.Combine(Application.streamingAssetsPath,"ui-alternate.sequence.json");
                string altSystem=Path.Combine(Application.streamingAssetsPath,"ui-alternate.system.json");
                File.WriteAllText(altSequence,ui.Workspace.Sequence.Root.ToString()); File.WriteAllText(altSystem,ui.Workspace.System.Root.ToString());
                ui.Workspace.SelectPrimary("system",altSystem); ui.Workspace.SelectPrimary("sequence",altSequence);
                ui.StartExperiment(); phase=3; frame=Time.frameCount+2;
            }
            else if(phase==3)
            {
                Check(MainController.Instance.SequenceRunning,"Start button reaches actual experiment runner");
                Check(ConfigWorkbench.Instance==null && Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Length==0,"Setup host and UIDocument are destroyed during run");
                Check(!ExperimentInput.IsEditingText,"Gameplay hotkeys restored after teardown");
                Check(MasterDataLogger.Instance.ConfigArchive!=null && File.Exists(MasterDataLogger.Instance.ConfigArchive.ManifestPath),"Run archives selected config files");
                Check(MainController.Instance.SequenceConfigurationFileName.EndsWith("ui-alternate.sequence.json") && File.ReadAllText(MasterDataLogger.Instance.ConfigArchive.ManifestPath).Contains("ui-alternate.sequence.json"),"Selected non-default sequence path reaches the runner and archive");
                phase=4; frame=Time.frameCount+4;
            }
            else if(phase==4 && ui!=null)
            {
                Check(!MainController.Instance.SequenceRunning && SceneManager.GetActiveScene().name=="ControlScene","Completed run returns to setup");
                if(run++==0)
                {
                    Check(File.Exists(Path.Combine(MasterDataLogger.Instance.directoryPath,"FlyMetaData.json")),"Metadata copied into the configured run even when its tab was not opened");
                    ui.StartExperiment(); phase=3; frame=Time.frameCount+2;
                }
                else { Check(errors.Count==0,"No runtime errors"); Finish(null); }
            }
        }
        catch(Exception error) { Finish(error.GetBaseException().ToString()); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog; SceneManager.sceneLoaded-=DisableInputs;
        SessionState.SetBool(Key,false);
        var unit=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"../ui-unit-validation.json")));
        foreach(var t in (JObject)unit["timings"]) timings[t.Key]=(double)t.Value;
        var passed=((JArray)unit["passed"]).Select(p=>p.ToString()).Concat(checks).ToList();
        File.WriteAllText(Path.Combine(Application.dataPath,"../config-ui-validation.json"),JsonConvert.SerializeObject(new { checks=passed.Count,failure,errors,timings,unity=Application.unityVersion,passed },Formatting.Indented));
        EditorApplication.isPlaying=false; EditorApplication.delayCall+=()=>EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
