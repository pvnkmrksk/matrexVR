#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ChoiceAssetCatalogValidation
{
    private const string Key="ChoiceAssetCatalogValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static string Output=>Path.GetFullPath("choice-catalog-validation");
    private static int stage,nextFrame;private static double deadline;private static RenderTexture target;private static ConfigDocument doc;
    private static string initialJson,initialFile;private static ChoiceAssetCatalog catalog;
    static ChoiceAssetCatalogValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            checks.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+".inventory","[]")));
            deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    public static void Run()
    {
        if(!File.Exists("KANNADI_VALIDATION_COPY"))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);ChoiceAssetCatalogBuilder.Generate();
        var data=JObject.Parse(File.ReadAllText("Assets/Resources/ConfigurationUI/choice-assets.json"));
        foreach(var property in ((JObject)data["scenes"]).Properties())
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+property.Name+".unity");
            try
            {
                var choice=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ChoiceController>(true)).ToArray();
                var dynamic=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<DynamicSequenceController>(true)).ToArray();
                var expectedObjects=choice.SelectMany(c=>c.prefabs??Array.Empty<GameObject>()).Concat(dynamic.SelectMany(c=>c.prefabs??Array.Empty<GameObject>())).Where(p=>p!=null).Select(p=>p.name).Distinct().OrderBy(p=>p,StringComparer.Ordinal);
                var expectedMaterials=choice.SelectMany(c=>c.materials??Array.Empty<Material>()).Concat(dynamic.SelectMany(c=>c.materials??Array.Empty<Material>())).Where(p=>p!=null).Select(p=>p.name).Distinct().OrderBy(p=>p,StringComparer.Ordinal);
                Check(expectedObjects.SequenceEqual(property.Value["objects"].Select(p=>(string)p["name"])),"catalog object names match the actual registered prefabs: "+property.Name);
                Check(expectedMaterials.SequenceEqual(property.Value["materials"].Select(p=>(string)p["name"])),"catalog material names match the actual registered materials: "+property.Name);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"choice-catalog-validation.json"),"{\"objects\":[{\"type\":\"CustomExtension\",\"material\":\"CustomMaterial\",\"unknownExtension\":17}],\"closedLoopOrientation\":false,\"closedLoopPosition\":false}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"sequences\":[{\"sceneName\":\"Choice\",\"duration\":30,\"parameters\":{\"configFile\":\"choice-catalog-validation.json\"}}]}");
        SessionState.SetString(Key+".inventory",JsonConvert.SerializeObject(checks));
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");EditorApplication.isPlaying=true;
    }
    private static void Log(string value,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&value!="No backup data file found."&&!trace.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(value);}
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
    private static void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Output,name+".png"),texture.EncodeToPNG());Object.Destroy(texture);
    }
    private static void Next(int value,int wait=4){stage=value;nextFrame=Time.frameCount+wait;}
    private static void Tick()
    {
        if(Time.frameCount<nextFrame)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Choice catalog UI timeout.");
            var ui=ConfigWorkbench.Instance;if(ui?.Document==null)return;var root=ui.Document.rootVisualElement;
            switch(stage)
            {
                case 0:
                    target=new RenderTexture(1920,1080,24);target.Create();ui.Document.panelSettings.scale=1;ui.Document.panelSettings.targetTexture=target;
                    doc=ui.Workspace.Open("choice-catalog-validation.json","choice");initialJson=doc.Root.ToString(Formatting.None);initialFile=File.ReadAllText(doc.Path);
                    Call(ui,"Select",doc);catalog=ChoiceAssetCatalog.Load();Next(1,40);break;
                case 1:
                    Check(catalog.Entries("Choice","type").Count>10,"compiled catalog exposes a useful set of choice prefabs without scene loading");
                    Check(catalog.Entries("Choice","material").Count>1,"compiled catalog exposes registered material alternatives");
                    Check(catalog.Entries("CustomScene","type").Count==0,"unknown scenes retain ordinary custom JSON fields");
                    Check(!ChoiceAssetCatalog.IsObjectField(new[]{"unrelated","type"})&&ChoiceAssetCatalog.IsObjectField(new[]{"stimulus","objects","0","type"}),"picker targets object fields, including phased objects");
                    Check(root.Q<Button>("show-choice-assets")!=null,"choice settings expose the available-assets action");
                    Call(ui,"ShowChoiceCatalog");Next(2);break;
                case 2:
                    var list=root.Q<ScrollView>("choice-asset-catalog");Check(list!=null,"available-assets dialog is scrollable");
                    var headings=list.Query<Label>().ToList().Where(label=>label.text.StartsWith("Objects ·")||label.text.StartsWith("Materials ·")).ToArray();
                    Check(headings.Length==2&&Mathf.Abs(headings[0].worldBound.y-headings[1].worldBound.y)<1,"object and material columns align at the top");
                    foreach(var entry in catalog.Entries("Choice","type"))Check(list.Query<Label>().ToList().Any(label=>label.text==entry.Label),"available list includes exact registered object "+entry.Name);
                    Check(doc.Root.ToString(Formatting.None)==initialJson&&!doc.Dirty,"browsing the catalog does not change JSON or materialize defaults");Capture("available-objects-full-hd");Call(ui,"CloseModal");
                    int assignments=0;var args=new object[]{new[]{"objects","0","type"},doc.Root["objects"][0]["type"],(Action<JToken>)(value=>{assignments++;doc.Edit(r=>r["objects"][0]["type"]=value);}),null};
                    Check((bool)Call(ui,"TryChoiceCatalogControl",args),"object type has a catalog-backed dropdown");
                    var control=(VisualElement)args[3];root.Add(control);var dropdown=control.Q<DropdownField>();
                    Check((string)doc.Root["objects"][0]["type"]=="CustomExtension"&&assignments==0,"opening a picker retains an unknown existing value without assigning it");
                    Check(control.Q<TextField>().value=="CustomExtension","existing custom name stays visible and editable");
                    var chosen=catalog.Entries("Choice","type").First();dropdown.value=chosen.Label;
                    Check((string)doc.Root["objects"][0]["type"]==chosen.Name&&assignments==1,"choosing a displayed object writes its exact runtime key to the original JSON");
                    Check((int)doc.Root["objects"][0]["unknownExtension"]==17,"selection preserves unrelated object extensions");
                    control.RemoveFromHierarchy();doc.Undo();Check(doc.Root.ToString(Formatting.None)==initialJson,"ordinary Undo restores the exact custom JSON value");
                    Check(File.ReadAllText(doc.Path)==initialFile,"catalog inspection and draft selection do not overwrite the source file");
                    Call(ui,"Select",doc);Call(ui,"ShowEditorTools");Next(3);break;
                case 3:
                    Call(ui,"CloseModal");Call(ui,"Select",doc);
                    var typeSchema=ConfigSchema.Child(ConfigSchema.Child(ConfigSchema.Child(ui.Workspace.Schemas.Get("choice"),"objects"),"",true),"type");
                    var args2=new object[]{doc,new[]{"objects","0","type"},typeSchema,(JObject)doc.Root["objects"][0],new JObject()};
                    var setting=(VisualElement)Call(ui,"Setting",args2);root.Add(setting);
                    Check(setting.Q<DropdownField>()!=null,"normal settings use the same authoritative picker as advanced JSON fields");
                    setting.RemoveFromHierarchy();Check(!MainController.Instance.SequenceRunning,"catalog validation never starts an experiment or tracker");Finish(null);break;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,failure,errors,passed=checks,scope="Actual registered prefab/material inventory and production UI Toolkit selection with lossless JSON and undo; isolated no-experiment profile"},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
